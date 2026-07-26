namespace MudBlazorWebApp1.Infrastructure.Messaging;

public interface IRabbitMqPublisher
{
    ValueTask PublishAsync<T>(string routingKey, T message, CancellationToken cancellationToken = default);
}
