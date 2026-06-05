using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;
using Hardware.Info;

namespace dottop.Actors;

public sealed class DiskMonitorActor : ReceiveActor
{
    private readonly HardwareInfo _hw = new(TimeSpan.FromSeconds(2));
    private Channel<List<DiskSnapshot>>? _channel;
    private ICancelable? _tickSchedule;

    public static Props Props() => Akka.Actor.Props.Create<DiskMonitorActor>();

    public DiskMonitorActor()
    {
        Receive<StartMonitoring>(_ =>
        {
            var cts = new CancellationTokenSource();
            _channel = Channel.CreateBounded<List<DiskSnapshot>>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.Zero, TimeSpan.FromSeconds(2), Self, new Tick(), Self);

            var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, cts.Token);
            Sender.Tell(new MonitoringStream<List<DiskSnapshot>>(stream, cts));
        });

        Receive<Tick>(_ =>
        {
            if (_channel is null) return;
            _hw.RefreshDriveList();
            var disks = _hw.DriveList
                .Where(d => d.PartitionList.Count > 0)
                .SelectMany(d => d.PartitionList
                    .Where(p => p.VolumeList.Count > 0)
                    .SelectMany(p => p.VolumeList)
                    .Select(v =>
                    {
                        var name = !string.IsNullOrWhiteSpace(v.VolumeName)
                            ? v.VolumeName
                            : v.Name;
                        if (string.IsNullOrWhiteSpace(name))
                            name = d.Name;
                        return new DiskSnapshot(name, v.Size, v.FreeSpace, 0, 0);
                    }))
                .Where(d => d.TotalBytes > 0)
                .ToList();
            _channel.Writer.TryWrite(disks);
        });
    }

    protected override void PostStop()
    {
        _tickSchedule?.Cancel();
        _channel?.Writer.TryComplete();
        base.PostStop();
    }
}
