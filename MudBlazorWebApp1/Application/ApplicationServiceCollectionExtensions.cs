using MudBlazorWebApp1.Features.Accounting;
using MudBlazorWebApp1.Features.Auth;
using MudBlazorWebApp1.Features.Fiscal;
using MudBlazorWebApp1.Features.Inventory;
using MudBlazorWebApp1.Features.PeriodClosing;
using MudBlazorWebApp1.Features.Reconciliation;
using MudBlazorWebApp1.Features.Reports;

namespace MudBlazorWebApp1.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<TokenService>();
        services.AddScoped<UserAccessService>();
        services.AddScoped<ReportExportService>();
        services.AddScoped<AccountingEngine>();
        services.AddScoped<InventoryCostService>();
        services.AddScoped<TaxEngine>();
        services.AddScoped<PeriodClosingService>();
        services.AddScoped<ReconciliationService>();

        return services;
    }
}
