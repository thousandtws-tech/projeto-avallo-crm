using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MudBlazorWebApp1.Domain;
using MudBlazorWebApp1.Features.PeriodClosing;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MudBlazorWebApp1.Infrastructure.Messaging.Consumers;

public sealed record AsyncTaskMessagePayload(
    Guid TenantId,
    string TaskType,
    int? Year,
    int? Month,
    Dictionary<string, string>? Parameters);

public sealed class AsyncTaskConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<AsyncTaskConsumerWorker> logger) : BackgroundService
{
    private readonly RabbitMqOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("AsyncTaskConsumerWorker is disabled.");
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

                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 5, global: false, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var messageText = Encoding.UTF8.GetString(body);

                    try
                    {
                        var payload = JsonSerializer.Deserialize<AsyncTaskMessagePayload>(messageText);
                        if (payload is not null)
                        {
                            await ProcessAsyncTaskPayloadAsync(payload, stoppingToken);
                        }

                        // Explicit Ack - removes message from queue after successful consumption
                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                        logger.LogInformation("Async task message {TaskType} processed & acknowledged.", payload?.TaskType);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to process async task message {DeliveryTag}. Sending to Dead Letter Queue.", ea.DeliveryTag);
                        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: _options.TasksQueue,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                logger.LogInformation("AsyncTaskConsumerWorker listening on queue '{Queue}'", _options.TasksQueue);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "AsyncTaskConsumerWorker encountered error. Retrying in 10s...");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task ProcessAsyncTaskPayloadAsync(AsyncTaskMessagePayload payload, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>();
        using var _ = tenantScope.BeginScope(payload.TenantId);

        logger.LogInformation("Orchestrating async task '{TaskType}' for Tenant {TenantId}", payload.TaskType, payload.TenantId);

        switch (payload.TaskType?.ToLowerInvariant())
        {
            case "period_closing_audit":
                if (payload.Year.HasValue && payload.Month.HasValue)
                {
                    var service = scope.ServiceProvider.GetRequiredService<PeriodClosingService>();
                    await service.ValidateAsync(payload.Year.Value, payload.Month.Value, Guid.Empty, cancellationToken);
                }
                break;

            case "reconciliation_sync":
                // Perform background financial reconciliation sync
                break;

            default:
                logger.LogWarning("Unknown task type '{TaskType}' received.", payload.TaskType);
                break;
        }
    }
}
