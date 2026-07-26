using MudBlazorWebApp1.Features.Accounting;
using MudBlazorWebApp1.Features.Auth;
using MudBlazorWebApp1.Features.Connectors;
using MudBlazorWebApp1.Features.Expenses;
using MudBlazorWebApp1.Features.Fiscal;
using MudBlazorWebApp1.Features.Inventory;
using MudBlazorWebApp1.Features.Notifications;
using MudBlazorWebApp1.Features.PeriodClosing;
using MudBlazorWebApp1.Features.Reconciliation;
using MudBlazorWebApp1.Features.Reports;

namespace MudBlazorWebApp1.Hosting;

/// <summary>
/// Keeps the host as a composition root. Each business module remains responsible
/// for its own routes instead of exposing endpoint details to Program.cs.
/// </summary>
public static class FeatureEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapBusinessModules(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAuthEndpoints();
        endpoints.MapReportEndpoints();
        endpoints.MapNotificationEndpoints();
        endpoints.MapConnectorEndpoints();
        endpoints.MapAccountingEndpoints();
        endpoints.MapExpenseEndpoints();
        endpoints.MapInventoryEndpoints();
        endpoints.MapFiscalEndpoints();
        endpoints.MapPeriodClosingEndpoints();
        endpoints.MapReconciliationEndpoints();

        return endpoints;
    }
}
