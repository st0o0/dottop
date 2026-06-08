using dtop.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace dtop.App;

public sealed class PluginBuilder(IServiceCollection services, ITickSource tickSource) : IPluginBuilder
{
    public IServiceCollection Services { get; } = services;
    public ITickSource TickSource { get; } = tickSource;
    public PluginTabInfo? Tab { get; private set; }
    public Action<IPluginActorContext>? ActorSetup { get; private set; }
    public Action<IRouteContext>? RouteSetup { get; private set; }

    public IPluginBuilder WithTab(string label, string route, ConsoleKey? hotKey = null)
    {
        Tab = new PluginTabInfo(label, route, hotKey);
        return this;
    }

    public IPluginBuilder WithSingleton<T>(T instance) where T : class
    {
        Services.AddSingleton(instance);
        return this;
    }

    public IPluginBuilder ConfigureActors(Action<IPluginActorContext> configure)
    {
        ActorSetup = configure;
        return this;
    }

    public IPluginBuilder ConfigureRoutes(Action<IRouteContext> configure)
    {
        RouteSetup = configure;
        return this;
    }
}
