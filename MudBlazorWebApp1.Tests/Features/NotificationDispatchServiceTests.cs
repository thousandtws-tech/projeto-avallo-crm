using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using MudBlazorWebApp1.Domain;
using MudBlazorWebApp1.Features.Notifications;
using MudBlazorWebApp1.Infrastructure;
using Xunit;

namespace MudBlazorWebApp1.Tests.Features;

public sealed class NotificationDispatchServiceTests
{
    [Fact]
    public async Task Queue_is_tenant_scoped_and_idempotent()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new StubTenantContext(tenantId);
        await using var context = new AppDbContext(options, tenantContext);
        var service = new NotificationDispatchService(context, tenantContext);

        await service.QueueAsync(userId, "user@example.com", NotificationTypes.NewSale,
            "sale:1", "New sale", "Sale received", "<p>Sale received</p>", true,
            cancellationToken: TestContext.Current.CancellationToken);
        await service.QueueAsync(userId, "user@example.com", NotificationTypes.NewSale,
            "sale:1", "New sale", "Sale received", "<p>Sale received</p>", true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, await context.Notifications.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await context.EmailOutbox.CountAsync(TestContext.Current.CancellationToken));
        Assert.All(await context.Notifications.ToListAsync(TestContext.Current.CancellationToken),
            notification => Assert.Equal(tenantId, notification.TenantId));
    }

    [Fact]
    public async Task Saving_a_sale_notifies_only_users_who_enabled_the_option()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new StubTenantContext(tenantId);
        await using var context = new AppDbContext(options, tenantContext);
        context.Tenants.Add(new Tenant { Id = tenantId, Name = "Test company" });
        context.Users.Add(new ApplicationUser
        {
            Id = userId, TenantId = tenantId, UserName = "seller@example.com",
            Email = "seller@example.com", DisplayName = "Seller"
        });
        context.Roles.Add(new IdentityRole<Guid> { Id = roleId, Name = Roles.Seller, NormalizedName = Roles.Seller.ToUpperInvariant() });
        context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = userId, RoleId = roleId });
        context.NotificationPreferences.Add(new NotificationPreference
        {
            TenantId = tenantId, UserId = userId, NewSaleNotification = true
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.FinancialEntries.Add(new FinancialEntry
        {
            TenantId = tenantId, ExternalId = "SALE-1", Description = "New order",
            Marketplace = "Marketplace", PaymentMethod = "Pix", Status = "Approved",
            OccurredAt = DateTimeOffset.UtcNow, GrossAmount = 150
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var notification = Assert.Single(await context.Notifications.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(NotificationTypes.NewSale, notification.Type);
        Assert.Single(await context.EmailOutbox.ToListAsync(TestContext.Current.CancellationToken));
    }

    private sealed record StubTenantContext(Guid? TenantId) : ITenantContext;
}
