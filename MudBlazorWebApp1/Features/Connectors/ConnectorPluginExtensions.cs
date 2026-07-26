using System.Reflection;
using System.Runtime.Loader;
using BraSeller.Connectors.Abstractions;

namespace MudBlazorWebApp1.Features.Connectors;

public static class ConnectorPluginExtensions
{
    public static IServiceCollection AddConnectorLayer(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredPath = configuration["Connectors:PluginPath"] ?? "connectors";
        var pluginPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));
        if (configuration.GetValue("Connectors:LoadPlugins", false) && Directory.Exists(pluginPath))
        {
            foreach (var assemblyPath in Directory.EnumerateFiles(pluginPath, "BraSeller.Connector.*.dll", SearchOption.TopDirectoryOnly))
            {
                var context = new PluginLoadContext(assemblyPath);
                var assembly = context.LoadFromAssemblyPath(assemblyPath);
                foreach (var moduleType in assembly.GetTypes().Where(x =>
                             typeof(IConnectorModule).IsAssignableFrom(x) && x is { IsAbstract: false, IsInterface: false }))
                {
                    var module = (IConnectorModule)Activator.CreateInstance(moduleType)!;
                    module.Register(services, configuration);
                }
            }
        }

        services.AddScoped<ConnectorRegistry>();
        services.AddScoped<ConnectorGateway>();
        services.AddScoped<ConnectorSyncService>();
        services.AddScoped<ConnectorOAuthService>();
        return services;
    }

    private sealed class PluginLoadContext(string pluginPath) : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == typeof(IMarketplaceConnector).Assembly.GetName().Name)
                return null;
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }
}
