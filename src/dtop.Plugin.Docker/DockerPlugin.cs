using dtop.Core.Platform;
using dtop.Plugin;

namespace dtop.Plugin.Docker;

public sealed class DockerPlugin : IDtopPlugin
{
    public string Name => "Docker";

    public void Configure(IPluginBuilder builder)
    {
        var provider = new DockerProvider();
        if (!provider.IsAvailable)
        {
            return;
        }

        builder
            .WithSingleton<IDockerProvider>(provider)
            .WithTab("5:Docker", "/docker", ConsoleKey.D5)
            .ConfigureActors(ctx =>
            {
                var interval = TimeSpan.FromSeconds(Math.Max(3, ctx.TickSource.CurrentInterval.TotalSeconds));
                ctx.Register<DockerMonitorActor>("docker-monitor", DockerMonitorActor.Props(provider, interval));
            })
            .ConfigureRoutes(ctx =>
            {
                ctx.RegisterRoute<DockerPage, DockerViewModel>("/docker");
            });
    }
}
