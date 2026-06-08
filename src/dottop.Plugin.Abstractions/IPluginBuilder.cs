using Microsoft.Extensions.DependencyInjection;

namespace dottop.Plugin.Abstractions;

public interface IPluginBuilder
{
    IServiceCollection Services { get; }
    ITickSource TickSource { get; }

    IPluginBuilder WithTab(string label, string route, ConsoleKey? hotKey = null);
    IPluginBuilder WithSingleton<T>(T instance) where T : class;
    IPluginBuilder ConfigureActors(Delegate actorSetup);
    IPluginBuilder ConfigureRoutes(Delegate routeSetup);
}
