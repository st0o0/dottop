using dottop.Plugin.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace dottop.App;

public sealed class PluginBuilder : IPluginBuilder
{
    public IServiceCollection Services { get; }
    public ITickSource TickSource { get; }
    public PluginTabInfo? Tab { get; private set; }
    public Delegate? ActorSetup { get; private set; }
    public Delegate? RouteSetup { get; private set; }

    public PluginBuilder(IServiceCollection services, ITickSource tickSource)
    {
        Services = services;
        TickSource = tickSource;
    }

    public IPluginBuilder WithTab(string label, string route, ConsoleKey? hotKey = null)
    {
        Tab = new PluginTabInfo(label, route, hotKey);
        return this;
    }

    public IPluginBuilder ConfigureActors(Delegate actorSetup)
    {
        ActorSetup = actorSetup;
        return this;
    }

    public IPluginBuilder ConfigureRoutes(Delegate routeSetup)
    {
        RouteSetup = routeSetup;
        return this;
    }

    public IPluginBuilder WithService<TInterface, TImplementation>()
        where TInterface : class
        where TImplementation : class, TInterface
    {
        Services.AddSingleton<TInterface, TImplementation>();
        return this;
    }

    public IPluginBuilder WithSingleton<T>(T instance) where T : class
    {
        Services.AddSingleton(instance);
        return this;
    }
}
