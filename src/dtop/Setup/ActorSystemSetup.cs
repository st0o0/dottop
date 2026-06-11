using Akka.Actor;
using Akka.Hosting;
using Akka.Logger.Serilog;
using dtop.Actors;
using dtop.Core.Messages;
using dtop.Core.Platform;
using dtop.Plugin;
using dtop.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using R3;
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
                        resolver.GetService<IConnectionProvider>(),
                        resolver.GetService<IMetricSink>()),
                    "monitoring-supervisor");
                registry.Register<MonitoringSupervisor>(supervisor);

                // Subscribe RefreshService ticks to drive the supervisor
                var refresh = resolver.GetService<IRefreshService>();
                refresh.Ticks.Subscribe(t => supervisor.Tell(t));

                // Docker actor (conditional)
                var docker = resolver.GetService<IDockerProvider>();
                if (docker is not null)
                {
                    var dockerActor = system.ActorOf(
                        DockerMonitorActor.Props(docker, resolver.GetService<IMetricSink>()),
                        "docker-monitor");
                    registry.Register<DockerMonitorActor>(dockerActor);
                    supervisor.Tell(new RegisterMonitor(MetricKind.Docker, dockerActor, AlwaysOn: false, TimeSpan.FromSeconds(3)));
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