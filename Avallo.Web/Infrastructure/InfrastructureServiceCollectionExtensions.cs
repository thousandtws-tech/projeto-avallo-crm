using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Avallo.Web.Features.Expenses;
using Avallo.Web.Features.Fiscal;
using Avallo.Web.Features.Inventory;
using Avallo.Web.Features.Reconciliation;

namespace Avallo.Web.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        // 1. Data Access & Persistence
        // AppDbContext depends on the scoped tenant context, so it cannot use the pooled factory.
        // O interceptor publica app.tenant_id a cada abertura de conexao; e o que as policies
        // de Row Level Security leem no PostgreSQL.
        services.AddScoped<TenantRlsConnectionInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetRequiredService<TenantRlsConnectionInterceptor>()));

        // 2. Multi-Tenancy Infrastructure
        services.AddHttpContextAccessor();
        services.AddScoped<HttpTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<HttpTenantContext>());
        services.AddScoped<ITenantScope>(sp => sp.GetRequiredService<HttpTenantContext>());

        // 3. Storage & Cloud Infrastructure
        services.Configure<ObjectStorageOptions>(configuration.GetSection("ObjectStorage"));
        services.AddScoped<AzureBlobExpenseStorage>();
        services.AddScoped<IExpenseStorage>(sp => sp.GetRequiredService<AzureBlobExpenseStorage>());

        // 4. External Clients & Services
        services.AddHttpClient<BrasilApiCnpjClient>(client =>
            client.BaseAddress = new Uri("https://brasilapi.com.br/"));

        // 5. Parsers & Converters
        services.AddSingleton<INfeXmlParser, NfeXmlParser>();
        services.AddSingleton<IStatementParser, StatementParser>();

        // 6. Security, Caching & Time Provider
        services.AddMemoryCache();
        services.AddDataProtection().SetApplicationName("Avallo");
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
