using MudBlazorWebApp1.Client.Services;

namespace MudBlazorWebApp1.Client;

public static class ClientServiceCollectionExtensions
{
    public static IServiceCollection AddClientServices(this IServiceCollection services)
    {
        // Core Infrastructure Services
        services.AddScoped<AuthService>();
        services.AddScoped<GeoLocationService>();
        services.AddScoped<WebPushNotificationService>();
        services.AddSingleton<AppLocalizer>();

        // Domain API Client Services
        services.AddScoped<UserAccessClient>();
        services.AddScoped<ReportService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<ConnectorService>();
        services.AddScoped<AccountingService>();
        services.AddScoped<ExpenseService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<FiscalService>();
        services.AddScoped<PeriodClosingClient>();
        services.AddScoped<ReconciliationClient>();

        return services;
    }
}
