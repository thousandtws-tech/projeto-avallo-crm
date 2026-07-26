using Microsoft.AspNetCore.Identity;

namespace MudBlazorWebApp1.Domain;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }
    public required string DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
    public bool RequiresPasswordChange { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
