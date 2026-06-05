using Akka.Actor;
using dottop.Models;
using Hardware.Info;

namespace dottop.Actors;

public sealed class DiskMonitorActor : ReceiveActor
{
    private readonly HardwareInfo _hw = new(TimeSpan.FromSeconds(2));
    public static Props Props() => Akka.Actor.Props.Create<DiskMonitorActor>();

    public DiskMonitorActor()
    {
        Receive<Tick>(_ =>
        {
            _hw.RefreshDriveList();
            var disks = _hw.DriveList
                .Where(d => d.PartitionList.Count > 0)
                .Where(d => d.PartitionList.Any(p => p.VolumeList.Count > 0))
                .SelectMany(d => d.PartitionList.SelectMany(p => p.VolumeList)
                    .Select(v => new DiskSnapshot(v.VolumeName, v.Size, v.FreeSpace, 0, 0)))
                .ToList();
            Context.System.EventStream.Publish(disks);
        });
    }
}
