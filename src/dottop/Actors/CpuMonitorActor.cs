using Akka.Actor;
using dottop.Models;
using Hardware.Info;

namespace dottop.Actors;

public sealed class CpuMonitorActor : ReceiveActor
{
    private readonly HardwareInfo _hw = new(TimeSpan.FromSeconds(2));
    public static Props Props() => Akka.Actor.Props.Create<CpuMonitorActor>();

    public CpuMonitorActor()
    {
        _hw.RefreshCPUList(includePercentProcessorTime: false, 250, false);
        Receive<Tick>(_ =>
        {
            _hw.RefreshCPUList(includePercentProcessorTime: true, 250, false);
            var totalPercent = _hw.CpuList.Count > 0
                ? _hw.CpuList.Average(c => (double)c.PercentProcessorTime) : 0;
            var cores = _hw.CpuList.SelectMany(c => c.CpuCoreList)
                .Select(c => (double)c.PercentProcessorTime).ToList();
            var name = _hw.CpuList.FirstOrDefault()?.Name ?? "Unknown";
            Context.System.EventStream.Publish(new CpuSnapshot(name, totalPercent, cores));
        });
    }
}
