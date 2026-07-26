using System.ComponentModel.DataAnnotations;

namespace MudBlazorWebApp1.Features.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string? Link,
    bool IsRead,
    DateTimeOffset CreatedAt);

public sealed record NotificationListResponse(
    IReadOnlyCollection<NotificationResponse> Items,
    int UnreadCount);

public sealed record NotificationPreferenceResponse(
    bool MonthlyCloseEmail,
    bool MercadoLivreReleaseAlert,
    bool NewSaleNotification,
    bool WeeklyAccountantReport,
    int MercadoLivreAlertDays);

public sealed record UpdateNotificationPreferenceRequest(
    bool MonthlyCloseEmail,
    bool MercadoLivreReleaseAlert,
    bool NewSaleNotification,
    bool WeeklyAccountantReport,
    [property: Range(1, 7)] int MercadoLivreAlertDays);
