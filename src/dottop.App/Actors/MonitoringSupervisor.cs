using Akka.Actor;
using dottop.Core.Platform;

namespace dottop.Actors;

public sealed class MonitoringSupervisor : ReceiveActor
{
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
        Context.ActorOf(CpuMonitorActor.Props(cpuMetrics, interval), "cpu-monitor");
        Context.ActorOf(MemoryMonitorActor.Props(memoryMetrics, interval), "memory-monitor");
        Context.ActorOf(DiskMonitorActor.Props(diskMetrics, interval), "disk-monitor");
        Context.ActorOf(NetworkMonitorActor.Props(networkMetrics, interval), "network-monitor");
        Context.ActorOf(GpuMonitorActor.Props(gpuMetrics, interval), "gpu-monitor");

        Context.ActorOf(
            ProcessSupervisor.Props(processClassifier, processTreeProvider, serviceManager, interval),
            "process-supervisor");
    }

    protected override SupervisorStrategy SupervisorStrategy() =>
        new OneForOneStrategy(
            maxNrOfRetries: 10,
            withinTimeRange: TimeSpan.FromSeconds(30),
            localOnlyDecider: ex => ex switch
            {
                IOException => Directive.Resume,
                UnauthorizedAccessException => Directive.Resume,
                _ => Directive.Restart
            });
}
