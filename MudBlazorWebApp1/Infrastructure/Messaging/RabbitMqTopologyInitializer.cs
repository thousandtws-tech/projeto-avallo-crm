using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace MudBlazorWebApp1.Infrastructure.Messaging;

public sealed class RabbitMqTopologyInitializer(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqTopologyInitializer> logger) : IHostedService
{
    private readonly RabbitMqOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("RabbitMQ integration is disabled in configuration.");
            return;
        }

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

            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            // 1. Declare Main Topic Exchange
            await channel.ExchangeDeclareAsync(
                exchange: _options.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            // 2. Declare Dead Letter Exchange
            await channel.ExchangeDeclareAsync(
                exchange: _options.DeadLetterExchangeName,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            // 3. Declare Dead Letter Queue
            await channel.QueueDeclareAsync(
                queue: _options.DeadLetterQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: _options.DeadLetterQueue,
                exchange: _options.DeadLetterExchangeName,
                routingKey: "dead-letter",
                cancellationToken: cancellationToken);

            // Queue arguments for Dead Letter routing
            var queueArgs = new Dictionary<string, object?>
            {
                { "x-dead-letter-exchange", _options.DeadLetterExchangeName },
                { "x-dead-letter-routing-key", "dead-letter" }
            };

            // 4. Declare Orders Queue & Binding
            await channel.QueueDeclareAsync(
                queue: _options.OrdersQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: _options.OrdersQueue,
                exchange: _options.ExchangeName,
                routingKey: "order.#",
                cancellationToken: cancellationToken);

            // 5. Declare Emails Queue & Binding
            await channel.QueueDeclareAsync(
                queue: _options.EmailsQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: _options.EmailsQueue,
                exchange: _options.ExchangeName,
                routingKey: "email.#",
                cancellationToken: cancellationToken);

            // 6. Declare Tasks Queue & Binding
            await channel.QueueDeclareAsync(
                queue: _options.TasksQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: queueArgs,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                queue: _options.TasksQueue,
                exchange: _options.ExchangeName,
                routingKey: "task.#",
                cancellationToken: cancellationToken);

            logger.LogInformation("RabbitMQ topology initialized successfully (Exchanges: {Exchange}, Queues: {Orders}, {Emails}, {Tasks})",
                _options.ExchangeName, _options.OrdersQueue, _options.EmailsQueue, _options.TasksQueue);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not initialize RabbitMQ topology. Will retry when connection is established.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
