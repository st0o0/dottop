using Akka.Actor;
using Akka.Hosting;
using dottop.Core.Platform;
using dottop.Plugin.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Termina.Hosting;
using Termina.Pages;

namespace dottop.Plugin.Docker;

public sealed class DockerPlugin : IDottopPlugin
{
    public string Name => "Docker";

    public void Configure(IPluginBuilder builder)
    {
        var provider = new DockerProvider();
        if (!provider.IsAvailable) return;

        builder
            .WithSingleton<IDockerProvider>(provider)
            .WithTab("5:Docker", "/docker", ConsoleKey.D5)
            .ConfigureActors((Action<ActorSystem, IActorRegistry, IServiceProvider, ITickSource>)(
                (system, registry, sp, tick) =>
                {
                    var interval = TimeSpan.FromSeconds(Math.Max(3, tick.CurrentInterval.TotalSeconds));
                    var actor = system.ActorOf(DockerMonitorActor.Props(provider, interval), "docker-monitor");
                    registry.Register<DockerMonitorActor>(actor);
                }))
            .ConfigureRoutes((Action<TerminaBuilder>)(termina =>
            {
                termina.RegisterRoute<DockerPage, DockerViewModel>("/docker", NavigationBehavior.PreserveState);
            }));
    }
}
