using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.App.Actors;

public sealed class MonitoringSupervisor : ReceiveActor
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Monitoring");

    private readonly IActorRef _cpu;
    private readonly IActorRef _memory;
    private readonly IActorRef _disk;
    private readonly IActorRef _network;
    private readonly IActorRef _gpu;
    private readonly IActorRef _processSupervisor;

    public static Props Props(
        ICpuMetrics cpuMetrics,
        IMemoryMetrics memoryMetrics,
        IDiskMetrics diskMetrics,
        INetworkMetrics networkMetrics,
        IGpuMetrics gpuMetrics,
        IProcessClassifier processClassifier,
        IProcessTreeProvider processTreeProvider,
        IServiceManager serviceManager,
        TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new MonitoringSupervisor(
            cpuMetrics, memoryMetrics, diskMetrics, networkMetrics, gpuMetrics,
            processClassifier, processTreeProvider, serviceManager, interval));

    public MonitoringSupervisor(
        ICpuMetrics cpuMetrics,
        IMemoryMetrics memoryMetrics,
        IDiskMetrics diskMetrics,
        INetworkMetrics networkMetrics,
        IGpuMetrics gpuMetrics,
        IProcessClassifier processClassifier,
        IProcessTreeProvider processTreeProvider,
        IServiceManager serviceManager,
        TimeSpan interval)
    {
        _cpu = Context.ActorOf(CpuMonitorActor.Props(cpuMetrics, interval), "cpu-monitor");
        _memory = Context.ActorOf(MemoryMonitorActor.Props(memoryMetrics, interval), "memory-monitor");
        _disk = Context.ActorOf(DiskMonitorActor.Props(diskMetrics, interval), "disk-monitor");
        _network = Context.ActorOf(NetworkMonitorActor.Props(networkMetrics, interval), "network-monitor");
        _gpu = Context.ActorOf(GpuMonitorActor.Props(gpuMetrics, interval), "gpu-monitor");

        _processSupervisor = Context.ActorOf(
            ProcessSupervisor.Props(processClassifier, processTreeProvider, serviceManager, interval),
            "process-supervisor");

        // Typed monitoring start commands — forward to the right child
        Receive<StartCpuMonitoring>(msg => _cpu.Forward(msg));
        Receive<StartMemoryMonitoring>(msg => _memory.Forward(msg));
        Receive<StartDiskMonitoring>(msg => _disk.Forward(msg));
        Receive<StartNetworkMonitoring>(msg => _network.Forward(msg));
        Receive<StartGpuMonitoring>(msg => _gpu.Forward(msg));
        Receive<StartProcessMonitoring>(msg => _processSupervisor.Forward(msg));

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

        Trace.Info(this, "Supervisor started with interval={0}ms", interval.TotalMilliseconds);
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
