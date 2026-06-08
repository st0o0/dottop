using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace dottop.Plugin.Abstractions;

public interface IDottopPlugin
{
    string Name { get; }
    int Order { get; }
    bool IsAvailable { get; }
    PluginTabInfo? TabInfo { get; }

    void ConfigureServices(IServiceCollection services);
    void ConfigureActors(ActorSystem system, IActorRegistry registry, IServiceProvider services, TimeSpan refreshInterval);
}
