using BraSeller.Connectors.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BraSeller.Connector.MercadoLivre;

public sealed class MercadoLivreModule : IConnectorModule
{
    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MercadoLivreOptions>()
            .BindConfiguration(MercadoLivreOptions.SectionName);
        services.AddSingleton<MercadoLivreRateLimiter>();
        services.AddHttpClient<MercadoLivreConnector>(client =>
        {
            client.BaseAddress = new Uri("https://api.mercadolibre.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("BraSeller-MercadoLivre/1.0");
        });
        services.AddTransient<IMarketplaceConnector>(services => services.GetRequiredService<MercadoLivreConnector>());
    }
}
