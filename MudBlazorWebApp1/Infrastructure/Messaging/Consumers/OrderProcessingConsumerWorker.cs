using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MudBlazorWebApp1.Domain;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MudBlazorWebApp1.Infrastructure.Messaging.Consumers;

public sealed record OrderProcessingMessagePayload(
    Guid TenantId,
    string OrderId,
    string Marketplace,
    decimal GrossValue,
    decimal PlatformFee,
    decimal NetValue,
    string BuyerName,
    DateTimeOffset CreatedAt);

public sealed class OrderProcessingConsumerWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<OrderProcessingConsumerWorker> logger) : BackgroundService
{
    private readonly RabbitMqOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("OrderProcessingConsumerWorker is disabled.");
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

                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 20, global: false, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    var body = ea.Body.ToArray();
                    var messageText = Encoding.UTF8.GetString(body);

                    try
                    {
                        var payload = JsonSerializer.Deserialize<OrderProcessingMessagePayload>(messageText);
                        if (payload is not null)
                        {
                            await ProcessOrderPayloadAsync(payload, stoppingToken);
                        }

                        // Explicit Ack - removes message from queue after successful consumption
                        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                        logger.LogInformation("Order message {OrderId} processed & acknowledged.", payload?.OrderId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to process order message {DeliveryTag}. Sending to Dead Letter Queue.", ea.DeliveryTag);
                        await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: _options.OrdersQueue,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                logger.LogInformation("OrderProcessingConsumerWorker listening on queue '{Queue}'", _options.OrdersQueue);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "OrderProcessingConsumerWorker encountered error. Retrying in 10s...");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private async Task ProcessOrderPayloadAsync(OrderProcessingMessagePayload payload, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantScope>();
        using var _ = tenantScope.BeginScope(payload.TenantId);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Check idempotency - ensure order isn't already created in FinancialEntries
        var exists = db.FinancialEntries.Any(x => x.ExternalId == payload.OrderId && x.Marketplace == payload.Marketplace);
        if (!exists)
        {
            var entry = new FinancialEntry
            {
                TenantId = payload.TenantId,
                ExternalId = payload.OrderId,
                Marketplace = payload.Marketplace,
                Description = $"Venda {payload.Marketplace} #{payload.OrderId} - Comprador: {payload.BuyerName}",
                GrossAmount = payload.GrossValue,
                ReceivedAmount = payload.NetValue,
                FeeAmount = payload.PlatformFee,
                PaymentMethod = "MarketplacePayout",
                Status = "Imported",
                OccurredAt = payload.CreatedAt
            };
            db.FinancialEntries.Add(entry);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Imported order {OrderId} for Tenant {TenantId} into FinancialEntries.", payload.OrderId, payload.TenantId);
        }
    }
}
