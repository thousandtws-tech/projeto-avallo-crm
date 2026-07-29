using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MudBlazorWebApp1.Features.Expenses;
using MudBlazorWebApp1.Features.Fiscal;
using MudBlazorWebApp1.Features.Inventory;
using MudBlazorWebApp1.Features.Reconciliation;

namespace MudBlazorWebApp1.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        // 1. Data Access & Persistence
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        // 2. Multi-Tenancy Infrastructure
        services.AddHttpContextAccessor();
        services.AddScoped<HttpTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<HttpTenantContext>());
        services.AddScoped<ITenantScope>(sp => sp.GetRequiredService<HttpTenantContext>());

        // 3. Storage & Cloud Infrastructure
        services.Configure<ObjectStorageOptions>(configuration.GetSection("ObjectStorage"));
        services.AddScoped<IExpenseStorage, BlobExpenseStorage>();

        // 4. External Clients & Services
        services.AddHttpClient<BrasilApiCnpjClient>(client =>
            client.BaseAddress = new Uri("https://brasilapi.com.br/"));

        // 5. Parsers & Converters
        services.AddSingleton<INfeXmlParser, NfeXmlParser>();
        services.AddSingleton<IStatementParser, StatementParser>();

        // 6. Security, Caching & Time Provider
        services.AddMemoryCache();
        services.AddDataProtection().SetApplicationName("BraSeller");
        services.AddSingleton(TimeProvider.System);

        // 7. RabbitMQ Messaging Infrastructure
        services.AddOptions<Messaging.RabbitMqOptions>()
            .BindConfiguration(Messaging.RabbitMqOptions.SectionName)
            .PostConfigure(options =>
            {
                var railwayUrl =
                    configuration["RABBITMQ_URL"] ??
                    configuration["RABBITMQ_PRIVATE_URL"] ??
                    configuration["RABBITMQ_PUBLIC_URL"];
                if (string.IsNullOrWhiteSpace(options.ConnectionString) &&
                    !string.IsNullOrWhiteSpace(railwayUrl))
                {
                    options.ConnectionString = railwayUrl;
                    if (string.IsNullOrWhiteSpace(configuration["RabbitMQ__Enabled"]))
                        options.Enabled = true;
                }
            })
            .ValidateDataAnnotations()
            .Validate(options => !options.Enabled ||
                !string.IsNullOrWhiteSpace(options.ConnectionString) ||
                (!string.IsNullOrWhiteSpace(options.HostName) &&
                 !string.IsNullOrWhiteSpace(options.UserName) &&
                 !string.IsNullOrWhiteSpace(options.Password)),
                "RabbitMQ requires RABBITMQ_URL (recommended on Railway) or host credentials.")
            .ValidateOnStart();
        services.AddSingleton<Messaging.IRabbitMqPublisher, Messaging.RabbitMqPublisher>();
        services.AddHostedService<Messaging.Consumers.EmailConsumerWorker>();
        services.AddHostedService<Messaging.Consumers.OrderProcessingConsumerWorker>();
        services.AddHostedService<Messaging.Consumers.AsyncTaskConsumerWorker>();

        return services;
    }
}
