using Microsoft.EntityFrameworkCore;
using MudBlazorWebApp1.Domain;
using MudBlazorWebApp1.Infrastructure;

namespace MudBlazorWebApp1.Features.Notifications;

public sealed class NotificationDispatchService(AppDbContext db, ITenantContext tenantContext)
{
    public async Task QueueAsync(
        Guid userId,
        string recipient,
        string type,
        string eventKey,
        string title,
        string message,
        string htmlBody,
        bool sendEmail,
        string? link = "/notifications",
        ExportedAttachment? attachment = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantContext.TenantId ?? throw new UnauthorizedAccessException("Tenant is required.");
        if (await db.Notifications.AnyAsync(x => x.UserId == userId && x.EventKey == eventKey, cancellationToken))
            return;

        db.Notifications.Add(new UserNotification
        {
            TenantId = tenantId,
            UserId = userId,
            Type = type,
            EventKey = eventKey,
            Title = title,
            Message = message,
            Link = link
        });
        if (sendEmail)
        {
            db.EmailOutbox.Add(new EmailOutbox
            {
                TenantId = tenantId,
                UserId = userId,
                EventKey = eventKey,
                Recipient = recipient,
                Subject = title,
                HtmlBody = htmlBody,
                AttachmentName = attachment?.Name,
                AttachmentContentType = attachment?.ContentType,
                AttachmentContent = attachment?.Content
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record ExportedAttachment(string Name, string ContentType, byte[] Content);
