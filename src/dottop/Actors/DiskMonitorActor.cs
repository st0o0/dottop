using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;
using dottop.Platform;
using Hardware.Info;

namespace dottop.Actors;

public sealed class DiskMonitorActor : ReceiveActor
{
    private readonly HardwareInfo _hw = new(TimeSpan.FromSeconds(2));
    private readonly IDiskMetricsProvider _diskMetrics;
    private readonly TimeSpan _interval;
    private Channel<List<DiskSnapshot>>? _channel;
    private ICancelable? _tickSchedule;
    private CancellationTokenSource? _streamCts;

    public static Props Props(IDiskMetricsProvider diskMetrics, TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new DiskMonitorActor(diskMetrics, interval));

    public DiskMonitorActor(IDiskMetricsProvider diskMetrics, TimeSpan interval)
    {
        _diskMetrics = diskMetrics;
        _interval = interval;

        Receive<StartMonitoring>(_ =>
        {
            CleanupPreviousStream();

            _streamCts = new CancellationTokenSource();
            _channel = Channel.CreateBounded<List<DiskSnapshot>>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.Zero, _interval, Self, new Tick(), Self);

            var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, _streamCts.Token);
            Sender.Tell(new MonitoringStream<List<DiskSnapshot>>(stream, _streamCts));
        });

        Receive<Tick>(_ =>
        {
            if (_channel is null) return;

            try { _hw.RefreshDriveList(); }
            catch { return; }
            var disks = _hw.DriveList
                .Where(d => d.PartitionList.Count > 0)
                .SelectMany(d => d.PartitionList
                    .Where(p => p.VolumeList.Count > 0)
                    .SelectMany(p => p.VolumeList)
                    .Select(v =>
                    {
                        var name = ExtractDriveLetter(v.Name, d.Name);
                        var (read, write, active) = _diskMetrics.GetMetrics(name);
                        return new DiskSnapshot(name, v.Size, v.FreeSpace, read, write, active);
                    }))
                .Where(d => d.TotalBytes > 0)
                .OrderBy(d => d.Name)
                .ToList();
            _channel.Writer.TryWrite(disks);
        });
    }

    private static string ExtractDriveLetter(string volumeName, string driveName)
    {
        foreach (var s in new[] { volumeName, driveName })
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                continue;
            }

            var trimmed = s.TrimEnd('\\', '/');
            if (trimmed is [_, ':', ..])
            {
                return trimmed[..2];
            }
        }
        return !string.IsNullOrWhiteSpace(volumeName) ? volumeName : driveName;
    }

    private void CleanupPreviousStream()
    {
        _tickSchedule?.Cancel();
        _streamCts?.Cancel();
        _streamCts?.Dispose();
        _channel?.Writer.TryComplete();
        _tickSchedule = null;
        _streamCts = null;
        _channel = null;
    }

    protected override void PostStop()
    {
        CleanupPreviousStream();
        base.PostStop();
    }
}
