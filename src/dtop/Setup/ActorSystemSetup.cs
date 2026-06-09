using Akka.Hosting;
using Akka.Logger.Serilog;
using dtop.Actors;
using dtop.Core.Platform;
using dtop.Plugin;
using dtop.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Servus;
using Servus.Application.Startup;
using Servus.Diagnostics;

namespace dtop.Setup;

public sealed class ActorSystemSetup : IServiceSetupContainer
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Setup.ActorSystem");

    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAkka("dtop", builder =>
        {
            builder.ConfigureLoggers(logging =>
            {
                logging.LogLevel = Akka.Event.LogLevel.WarningLevel;
                logging.AddSerilogLogging();
                logging.DeadLetterOptions = new DeadLetterOptions
                {
                    ShouldLog = TriStateValue.None,
                    LogDuringShutdown = false
                };
            });

            builder.AddHocon("akka.stdout-loglevel = Off", HoconAddMode.Prepend);

            builder.WithActors((system, registry, resolver) =>
            {
                var interval = resolver.GetService<AppTickSource>().CurrentInterval;

                // Initialize disk metrics
                var diskMetrics = resolver.GetService<IDiskMetrics>();
                try
                {
                    diskMetrics.Initialize();
                }
                catch (Exception ex)
                {
                    Trace.Warning("ActorSystemSetup", "Failed to initialize disk metrics: {0}", ex.Message);
                }

                // Core monitoring supervisor
                var supervisor = system.ActorOf(
                    MonitoringSupervisor.Props(
                        resolver.GetService<ICpuMetrics>(),
                        resolver.GetService<IMemoryMetrics>(),
                        diskMetrics,
                        resolver.GetService<INetworkMetrics>(),
                        resolver.GetService<IGpuMetrics>(),
                        resolver.GetService<IProcessClassifier>(),
                        resolver.GetService<IProcessTreeProvider>(),
                        resolver.GetService<IServiceManager>(),
                        interval),
                    "monitoring-supervisor");
                registry.Register<MonitoringSupervisor>(supervisor);

                // Docker actor (conditional)
                var docker = resolver.GetService<IDockerProvider>();
                if (docker is not null)
                {
                    var dockerInterval = TimeSpan.FromSeconds(Math.Max(3, interval.TotalSeconds));
                    var dockerActor = system.ActorOf(
                        DockerMonitorActor.Props(docker, dockerInterval),
                        "docker-monitor");
                    registry.Register<DockerMonitorActor>(dockerActor);
                }

                // Plugin actors
                var pluginRegistry = resolver.GetService<PluginRegistry>();
                var tickSource = resolver.GetService<ITickSource>();
                var sp = resolver.GetService<IServiceProvider>();
                var actorCtx = new PluginActorContextImpl(system, registry, sp, tickSource);
                foreach (var plugin in pluginRegistry.LoadedPlugins)
                {
                    plugin.ActorSetup?.Invoke(actorCtx);
                }
            });
        });
    }
}