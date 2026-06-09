using dtop.Pages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Servus.Application.Startup;
using Termina.Hosting;
using Termina.Pages;

namespace dtop.Setup;

public sealed class TerminaSetup : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTermina("/", termina =>
        {
            // Core routes
            termina.ConfigureRuntime(x =>
            {
                x.PreferRawInput = true;
                x.ScrollInputMode = ScrollInputMode.AlternateScroll;
                x.CtrlCHandlingMode = CtrlCHandlingMode.DoublePressWhenRawInput;
            });
            termina.RegisterRoute<ProcessesPage, ProcessesViewModel>("/", NavigationBehavior.PreserveState);
            termina.RegisterRoute<PerformancePage, PerformanceViewModel>("/performance",
                NavigationBehavior.PreserveState);
            termina.RegisterRoute<ServicesPage, ServicesViewModel>("/services", NavigationBehavior.PreserveState);
            termina.RegisterRoute<NetworkPage, NetworkViewModel>("/network", NavigationBehavior.PreserveState);

            // Docker route (conditional — only if IDockerProvider was registered)
            var docker = GetRegisteredSingleton<DockerAvailability>(services);
            if (docker is { IsAvailable: true })
            {
                termina.RegisterRoute<DockerPage, DockerViewModel>("/docker", NavigationBehavior.PreserveState);
            }

            // Plugin routes
            var pluginRegistry = GetRegisteredSingleton<PluginRegistry>(services);
            if (pluginRegistry is not null)
            {
                var routeCtx = new PluginRouteContext(termina);
                foreach (var plugin in pluginRegistry.LoadedPlugins)
                {
                    plugin.RouteSetup?.Invoke(routeCtx);
                }
            }
        });
    }

    private static T? GetRegisteredSingleton<T>(IServiceCollection services) where T : class
    {
        var descriptor = services.LastOrDefault(d =>
            d.ServiceType == typeof(T) && d.Lifetime == ServiceLifetime.Singleton);

        return descriptor?.ImplementationInstance as T;
    }
}