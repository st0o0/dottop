using Akka.Actor;
using Akka.Hosting;
using dottop.Core.Platform;
using dottop.Plugin.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace dottop.Plugin.Docker;

public sealed class DockerPlugin : IDottopPlugin
{
    public string Name => "Docker";
    public int Order => 5;
    public bool IsAvailable { get; private set; }
    public PluginTabInfo? TabInfo => IsAvailable
        ? new PluginTabInfo("5:Docker", "/docker", ConsoleKey.D5)
        : null;

    private DockerProvider? _provider;

    public void ConfigureServices(IServiceCollection services)
    {
        _provider = new DockerProvider();
        IsAvailable = _provider.IsAvailable;
        if (!IsAvailable) return;
        services.AddSingleton<IDockerProvider>(_provider);
    }

    public void ConfigureActors(ActorSystem system, IActorRegistry registry,
        IServiceProvider services, TimeSpan refreshInterval)
    {
        if (!IsAvailable || _provider is null) return;
        var dockerInterval = TimeSpan.FromSeconds(Math.Max(3, refreshInterval.TotalSeconds));
        var actor = system.ActorOf(
            DockerMonitorActor.Props(_provider, dockerInterval), "docker-monitor");
        registry.Register<DockerMonitorActor>(actor);
    }
}
