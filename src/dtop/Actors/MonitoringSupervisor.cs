using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Platform;
using dtop.Services;
using Servus;
using Servus.Diagnostics;

namespace dtop.Actors;

public sealed class MonitoringSupervisor : TickRouter
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Monitoring");

    private readonly IActorRef _cpu;
    private readonly IActorRef _memory;
    private readonly IActorRef _disk;
    private readonly IActorRef _network;
    private readonly IActorRef _gpu;
    private readonly IActorRef _processSupervisor;
    private readonly IActorRef _connections;

    public static Props Props(
        ICpuMetrics cpuMetrics,
        IMemoryMetrics memoryMetrics,
        IDiskMetrics diskMetrics,
        INetworkMetrics networkMetrics,
        IGpuMetrics gpuMetrics,
        IProcessClassifier processClassifier,
        IProcessTreeProvider processTreeProvider,
        IServiceManager serviceManager,
        IConnectionProvider connectionProvider,
        IMetricSink sink) =>
        Akka.Actor.Props.Create(() => new MonitoringSupervisor(
            cpuMetrics, memoryMetrics, diskMetrics, networkMetrics, gpuMetrics,
            processClassifier, processTreeProvider, serviceManager, connectionProvider, sink));

    public MonitoringSupervisor(
        ICpuMetrics cpuMetrics,
        IMemoryMetrics memoryMetrics,
        IDiskMetrics diskMetrics,
        INetworkMetrics networkMetrics,
        IGpuMetrics gpuMetrics,
        IProcessClassifier processClassifier,
        IProcessTreeProvider processTreeProvider,
        IServiceManager serviceManager,
        IConnectionProvider connectionProvider,
        IMetricSink sink)
    {
        _cpu = Context.ActorOf(CpuMonitorActor.Props(cpuMetrics, sink), "cpu-monitor");
        _memory = Context.ActorOf(MemoryMonitorActor.Props(memoryMetrics, sink), "memory-monitor");
        _disk = Context.ActorOf(DiskMonitorActor.Props(diskMetrics, sink), "disk-monitor");
        _network = Context.ActorOf(NetworkMonitorActor.Props(networkMetrics, sink), "network-monitor");
        _gpu = Context.ActorOf(GpuMonitorActor.Props(gpuMetrics, sink), "gpu-monitor");

        _processSupervisor = Context.ActorOf(
            ProcessSupervisor.Props(processClassifier, processTreeProvider, serviceManager, sink),
            "process-supervisor");

        _connections = Context.ActorOf(
            NetworkConnectionsActor.Props(connectionProvider, sink),
            "network-connections");

        // Self-register monitors with the TickRouter
        Self.Tell(new RegisterMonitor(MetricKind.Cpu, _cpu, true, null));
        Self.Tell(new RegisterMonitor(MetricKind.Memory, _memory, true, null));
        Self.Tell(new RegisterMonitor(MetricKind.Disk, _disk, false, null));
        Self.Tell(new RegisterMonitor(MetricKind.Network, _network, false, null));
        Self.Tell(new RegisterMonitor(MetricKind.Gpu, _gpu, false, null));
        Self.Tell(new RegisterMonitor(MetricKind.Process, _processSupervisor, false, null));
        Self.Tell(new RegisterMonitor(MetricKind.NetworkConnections, _connections, false, null));

        // Process action commands
        Receive<KillProcess>(msg => _processSupervisor.Forward(msg));
        Receive<SetProcessPriority>(msg => _processSupervisor.Forward(msg));
        Receive<SetProcessAffinity>(msg => _processSupervisor.Forward(msg));
        Receive<GetProcessTree>(msg => _processSupervisor.Forward(msg));
        Receive<GetProcessEnvironment>(msg => _processSupervisor.Forward(msg));
        Receive<GetProcessHandles>(msg => _processSupervisor.Forward(msg));

        // Service commands
        Receive<GetServices>(msg => _processSupervisor.Forward(msg));
        Receive<StartService>(msg => _processSupervisor.Forward(msg));
        Receive<StopService>(msg => _processSupervisor.Forward(msg));
        Receive<RestartService>(msg => _processSupervisor.Forward(msg));

        Trace.Info(this, "Supervisor started");
    }

    protected override SupervisorStrategy SupervisorStrategy() =>
        new OneForOneStrategy(
            maxNrOfRetries: 10,
            withinTimeRange: TimeSpan.FromSeconds(30),
            localOnlyDecider: ex =>
            {
                var directive = ex switch
                {
                    IOException => Directive.Resume,
                    UnauthorizedAccessException => Directive.Resume,
                    _ => Directive.Restart
                };
                Trace.Warning(this, "Supervision decision: {0} for {1}", directive, ex.GetType().Name);
                return directive;
            });

    protected override void PostStop()
    {
        Trace.Debug(this, "Stopped");
        base.PostStop();
    }
}
