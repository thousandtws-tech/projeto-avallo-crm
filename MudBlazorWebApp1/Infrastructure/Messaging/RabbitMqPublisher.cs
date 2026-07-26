using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace MudBlazorWebApp1.Infrastructure.Messaging;

public sealed class RabbitMqPublisher(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqPublisher> logger) : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private async ValueTask EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null && _connection is not null && _connection.IsOpen && _channel.IsOpen)
            return;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_channel is not null && _connection is not null && _connection.IsOpen && _channel.IsOpen)
                return;

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}", _options.HostName, _options.Port);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to RabbitMQ broker at {Host}:{Port}", _options.HostName, _options.Port);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async ValueTask PublishAsync<T>(string routingKey, T message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            logger.LogDebug("RabbitMQ is disabled. Skipping publish for routing key {RoutingKey}", routingKey);
            return;
        }

        try
        {
            await EnsureConnectedAsync(cancellationToken);

            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await _channel!.BasicPublishAsync(
                exchange: _options.ExchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: cancellationToken);

            logger.LogDebug("Published message to exchange '{Exchange}' with routing key '{RoutingKey}'", _options.ExchangeName, routingKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error publishing message to RabbitMQ routing key '{RoutingKey}'", routingKey);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }
        if (_connection is not null)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }
        _semaphore.Dispose();
    }
}
