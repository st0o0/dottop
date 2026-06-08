using Microsoft.Extensions.DependencyInjection;

namespace dtop.Plugin;

public interface IPluginBuilder
{
    IServiceCollection Services { get; }
    ITickSource TickSource { get; }

    IPluginBuilder WithTab(string label, string route, ConsoleKey? hotKey = null);
    IPluginBuilder WithSingleton<T>(T instance) where T : class;
    IPluginBuilder ConfigureActors(Action<IPluginActorContext> configure);
    IPluginBuilder ConfigureRoutes(Action<IRouteContext> configure);
}

public interface IPluginActorContext
{
    IServiceProvider Services { get; }
    ITickSource TickSource { get; }
    void Register<TActor>(string name, object props) where TActor : class;
}

public interface IRouteContext
{
    void RegisterRoute<TPage, TViewModel>(string route)
        where TPage : class
        where TViewModel : class;
}
