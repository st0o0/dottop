using dtop.Nodes;
using dtop.Plugin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Servus.Application.Startup;

namespace dtop.Setup;

public sealed class PluginSetup : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        // Resolve ITickSource from the service collection (registered by ServicesSetup)
        var tickSource = GetRegisteredSingleton<ITickSource>(services)
            ?? throw new InvalidOperationException("ITickSource must be registered before PluginSetup. Ensure ServicesSetup runs first.");

        var pluginRegistry = PluginLoader.DiscoverAndConfigure(services, tickSource);

        // Register Docker as a built-in tab when available
        var docker = GetRegisteredSingleton<DockerAvailability>(services);
        if (docker is { IsAvailable: true })
        {
            pluginRegistry.AddBuiltInTab(new PluginTabInfo("5:Docker", "/docker", ConsoleKey.D5));
        }

        services.AddSingleton(pluginRegistry);
        TabBarNode.RegisterPluginTabs(pluginRegistry);
    }

    private static T? GetRegisteredSingleton<T>(IServiceCollection services) where T : class
    {
        var descriptor = services.LastOrDefault(d =>
            d.ServiceType == typeof(T) && d.Lifetime == ServiceLifetime.Singleton);

        return descriptor?.ImplementationInstance as T;
    }
}
