using System.ComponentModel.DataAnnotations;

namespace MudBlazorWebApp1.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public bool Enabled { get; set; } = true;

    [Required]
    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    [Required]
    public string UserName { get; set; } = "braseller";

    [Required]
    public string Password { get; set; } = "braseller_secret";

    public string VirtualHost { get; set; } = "/";

    public string ExchangeName { get; set; } = "braseller.events";

    public string DeadLetterExchangeName { get; set; } = "braseller.dlx";

    public string OrdersQueue { get; set; } = "orders.queue";

    public string EmailsQueue { get; set; } = "emails.queue";

    public string TasksQueue { get; set; } = "tasks.queue";

    public string DeadLetterQueue { get; set; } = "dead-letter.queue";
}
