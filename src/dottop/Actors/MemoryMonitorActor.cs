using Akka.Actor;
using dottop.Models;
using Hardware.Info;

namespace dottop.Actors;

public sealed class MemoryMonitorActor : ReceiveActor
{
    private readonly HardwareInfo _hw = new(TimeSpan.FromSeconds(2));
    public static Props Props() => Akka.Actor.Props.Create<MemoryMonitorActor>();

    public MemoryMonitorActor()
    {
        _hw.RefreshMemoryList();
        var total = _hw.MemoryList.Aggregate(0UL, (sum, m) => sum + m.Capacity);
        Receive<Tick>(_ =>
        {
            _hw.RefreshMemoryStatus();
            var status = _hw.MemoryStatus;
            var used = status.TotalPhysical - status.AvailablePhysical;
            Context.System.EventStream.Publish(new MemorySnapshot(total, used));
        });
    }
}
