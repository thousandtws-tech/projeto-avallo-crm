using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MudBlazorWebApp1.Domain;
using MudBlazorWebApp1.Features.Notifications;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MudBlazorWebApp1.Infrastructure.Messaging.Consumers;

public sealed record EmailMessagePayload(
    Guid TenantId,
    Guid EmailId,
    string ToEmail,
    string Subject,
    string Body);

public sealed class EmailConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<EmailConsumerWorker> logger) : BackgroundService
{
    private readonly RabbitMqOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("EmailConsumerWorker is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _options.HostName,
                    Port = _options.Port,
                    UserName = _options.UserName,
                    Password = _options.Password,
                    VirtualHost = _options.VirtualHost
                };

                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var messageText = Encoding.UTF8.GetString(body);

                    try
                    {
                        var payload = JsonSerializer.Deserialize<EmailMessagePayload>(messageText);
                        if (payload is not null)
                        {
                            await ProcessEmailPayloadAsync(payload, stoppingToken);
                        }

                        // Explicit Ack - removes message from queue after successful consumption
                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                        logger.LogInformation("Email message {DeliveryTag} processed & acknowledged.", ea.DeliveryTag);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to process email message {DeliveryTag}. Sending to Dead Letter Queue.", ea.DeliveryTag);
                        // Reject and route to Dead Letter Exchange (requeue: false)
                        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: _options.EmailsQueue,
                    autoAck: false, // Manual Acknowledgment
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                logger.LogInformation("EmailConsumerWorker listening on queue '{Queue}'", _options.EmailsQueue);

                // Keep running until cancellation requested
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "EmailConsumerWorker encountered error. Retrying in 10s...");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task ProcessEmailPayloadAsync(EmailMessagePayload payload, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>();
        using var _ = tenantScope.BeginScope(payload.TenantId);

        var sender = scope.ServiceProvider.GetRequiredService<SmtpEmailSender>();
        if (!sender.IsEnabled)
            return;

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var outboxItem = await db.EmailOutbox.FindAsync([payload.EmailId], cancellationToken);
        if (outboxItem is not null && outboxItem.SentAt == null)
        {
            await sender.SendAsync(outboxItem, cancellationToken);
            outboxItem.SentAt = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
