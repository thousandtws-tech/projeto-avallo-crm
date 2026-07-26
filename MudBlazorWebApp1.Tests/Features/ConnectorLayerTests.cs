using System.Runtime.CompilerServices;
using BraSeller.Connectors.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MudBlazorWebApp1.Domain;
using MudBlazorWebApp1.Features.Connectors;
using MudBlazorWebApp1.Features.Accounting;
using MudBlazorWebApp1.Features.Inventory;
using MudBlazorWebApp1.Features.Fiscal;
using MudBlazorWebApp1.Infrastructure;
using Xunit;

namespace MudBlazorWebApp1.Tests.Features;

public sealed class ConnectorLayerTests
{
    [Fact]
    public async Task Authentication_encrypts_tokens_at_rest()
    {
        var setup = await CreateSetupAsync();

        var connection = await setup.Gateway.AuthenticateAsync(
            "test-marketplace", new Dictionary<string, string> { ["code"] = "authorization-code" }, null,
            TestContext.Current.CancellationToken);

        Assert.NotEqual("access-token", connection.EncryptedAccessToken);
        Assert.NotEqual("refresh-token", connection.EncryptedRefreshToken);
        Assert.Equal(MarketplaceConnectionStates.Active, connection.Status);
    }

    [Fact]
    public async Task SyncAll_is_idempotent_and_populates_the_normalized_financial_model()
    {
        var setup = await CreateSetupAsync();
        var connection = await setup.Gateway.AuthenticateAsync(
            "test-marketplace", new Dictionary<string, string> { ["code"] = "authorization-code" }, null,
            TestContext.Current.CancellationToken);
        var accounting = new AccountingEngine(setup.Db, setup.TenantContext, TimeProvider.System);
        var inventory = new InventoryCostService(setup.Db, setup.TenantContext, new NfeXmlParser(), TimeProvider.System);
        var tax = new TaxEngine(setup.Db, setup.TenantContext, TimeProvider.System);
        var sync = new ConnectorSyncService(setup.Db, setup.Gateway, accounting, inventory, tax, setup.TenantContext, TimeProvider.System);

        await sync.SyncAllAsync(connection.Id, DateTimeOffset.UtcNow.AddDays(-30), TestContext.Current.CancellationToken);
        await sync.SyncAllAsync(connection.Id, DateTimeOffset.UtcNow.AddDays(-30), TestContext.Current.CancellationToken);

        Assert.Equal(1, await setup.Db.MarketplaceOrders.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await setup.Db.FinancialEntries.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await setup.Db.MarketplaceOrderItems.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await setup.Db.MarketplacePayments.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await setup.Db.MarketplaceFees.CountAsync(TestContext.Current.CancellationToken));
        var entry = await setup.Db.FinancialEntries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(100, entry.GrossAmount);
        Assert.Equal(10, entry.FeeAmount);
        Assert.Equal(90, entry.ReceivedAmount);
        Assert.Equal(1, await setup.Db.AccountingEntries.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(6, await setup.Db.AccountingPostings.CountAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<Setup> CreateSetupAsync()
    {
        var tenantId = Guid.NewGuid();
        var tenantContext = new StubTenantContext(tenantId);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(options, tenantContext);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test tenant" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var registry = new ConnectorRegistry([new TestConnector()]);
        var gateway = new ConnectorGateway(
            db, registry, tenantContext, new EphemeralDataProtectionProvider(), TimeProvider.System);
        return new Setup(db, gateway, tenantContext);
    }

    private sealed record Setup(AppDbContext Db, ConnectorGateway Gateway, StubTenantContext TenantContext);
    private sealed record StubTenantContext(Guid? TenantId) : ITenantContext;

    private sealed class TestConnector : IMarketplaceConnector
    {
        public ConnectorDescriptor Descriptor => new("test-marketplace", "Test Marketplace", "1.0.0");

        public Task<ConnectorAuthentication> AuthenticateAsync(AuthenticationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectorAuthentication("access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1), "account-1", "Test account"));

        public Task<ConnectorAuthentication> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectorAuthentication("renewed-token", "renewed-refresh", DateTimeOffset.UtcNow.AddHours(1), "account-1"));

        public Task<ConnectorPage<StandardOrder>> GetOrdersAsync(ConnectorContext context, OrderFilter filter, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectorPage<StandardOrder>([Order()], null, false));

        public Task<StandardOrder> GetOrderDetailAsync(ConnectorContext context, string orderId, CancellationToken cancellationToken = default) => Task.FromResult(Order());
        public Task<IReadOnlyCollection<StandardPayment>> GetPaymentsAsync(ConnectorContext context, string orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<StandardPayment>>([new StandardPayment(
                "PAY-1", orderId, 100, 90, "Pix", StandardPaymentStatus.Paid,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)]);
        public Task<IReadOnlyCollection<StandardFee>> GetFeesAsync(ConnectorContext context, string orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<StandardFee>>([
                new StandardFee("commission", "Commission", 7, Category: StandardFeeCategory.MarketplaceCommission, ExternalId: "commission"),
                new StandardFee("shipping", "Shipping", 3, Category: StandardFeeCategory.SellerShipping, ExternalId: "shipping")
            ]);

        public async IAsyncEnumerable<StandardOrder> SyncAllAsync(
            ConnectorContext context,
            DateTimeOffset since,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return Order();
        }

        public Task<ConnectorStatus> GetStatusAsync(ConnectorContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectorStatus(ConnectorConnectionState.Active));

        private static StandardOrder Order() => new(
            "ORDER-1", "test-marketplace", DateTimeOffset.UtcNow.AddDays(-1),
            100, 10, 90, "Pix", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            StandardOrderStatus.Paid, "Buyer", [new StandardOrderItem("SKU-1", "Product", 1, 100)], "NF-1",
            StandardFulfillmentStatus.Delivered, DateTimeOffset.UtcNow);
    }
}
