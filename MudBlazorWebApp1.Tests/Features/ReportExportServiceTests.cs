using System.Text;
using Microsoft.EntityFrameworkCore;
using MudBlazorWebApp1.Domain;
using MudBlazorWebApp1.Features.Reports;
using MudBlazorWebApp1.Infrastructure;
using QuestPDF.Infrastructure;
using Xunit;

namespace MudBlazorWebApp1.Tests.Features;

public sealed class ReportExportServiceTests
{
    [Theory]
    [InlineData("pdf", "application/pdf", "%PDF")]
    [InlineData("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "PK")]
    [InlineData("csv", "text/csv; charset=utf-8", "Data;")]
    public async Task Export_creates_the_requested_file(
        string format,
        string contentType,
        string expectedSignature)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenantContext = new StubTenantContext(tenantId);
        await using var context = new AppDbContext(options, tenantContext);
        context.Tenants.Add(new Tenant { Id = tenantId, Name = "Test company" });
        context.FinancialEntries.Add(new FinancialEntry
        {
            TenantId = tenantId,
            ExternalId = "ORDER-1",
            Description = "Test sale",
            Marketplace = "Test marketplace",
            PaymentMethod = "Credit card",
            Status = "Received",
            OccurredAt = DateTimeOffset.UtcNow,
            GrossAmount = 100,
            ReceivedAmount = 90,
            FeeAmount = 10
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ReportExportService(context, tenantContext, TimeProvider.System);

        var report = await service.ExportAsync(
            new ExportReportRequest { Format = format },
            TestContext.Current.CancellationToken);

        Assert.Equal(contentType, report.ContentType);
        Assert.EndsWith($".{format}", report.FileName);
        if (format == "csv")
            Assert.Contains(expectedSignature, Encoding.UTF8.GetString(report.Content));
        else
            Assert.Equal(expectedSignature, Encoding.ASCII.GetString(report.Content, 0, expectedSignature.Length));
    }

    private sealed record StubTenantContext(Guid? TenantId) : ITenantContext;
}
