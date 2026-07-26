using System.ComponentModel.DataAnnotations;

namespace MudBlazorWebApp1.Features.Notifications;

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public bool Enabled { get; init; }
    public string Host { get; init; } = string.Empty;
    [Range(1, 65535)] public int Port { get; init; } = 587;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromEmail { get; init; } = string.Empty;
    public string FromName { get; init; } = "Nucleo";
    public string Security { get; init; } = "StartTls";
}

public sealed class NotificationWorkerOptions
{
    public const string SectionName = "Notifications:Worker";
    [Range(1, 1440)] public int IntervalMinutes { get; init; } = 10;
    [Range(1, 30)] public int BatchSize { get; init; } = 10;
}
