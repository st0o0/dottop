using Akka.Actor;
using Akka.Hosting;
using dtop.Plugin;
using Termina.Hosting;
using Termina.Pages;

namespace dtop.App;

public sealed class PluginActorContextImpl(
    ActorSystem system,
    IActorRegistry registry,
    IServiceProvider services,
    ITickSource tickSource) : IPluginActorContext
{
    public IServiceProvider Services => services;
    public ITickSource TickSource => tickSource;

    public void Register<TActor>(string name, object props) where TActor : class
    {
        var actor = system.ActorOf((Props)props, name);
        registry.Register<TActor>(actor);
    }
}

public sealed class PluginRouteContext(TerminaBuilder termina) : IRouteContext
{
    public void RegisterRoute<TPage, TViewModel>(string route)
        where TPage : class
        where TViewModel : class
    {
        var method = typeof(TerminaBuilder).GetMethods()
            .First(m => m.Name == "RegisterRoute" && m.GetGenericArguments().Length == 2)
            .MakeGenericMethod(typeof(TPage), typeof(TViewModel));
        method.Invoke(termina, [route, NavigationBehavior.PreserveState]);
    }
}
