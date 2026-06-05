using System.Diagnostics;
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
    private Dictionary<string, DiskPerfCounters>? _counters;

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

            InitPerfCounters();

            _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.Zero, TimeSpan.FromSeconds(1), Self, new Tick(), Self);

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
                        var name = ExtractDriveLetter(v.Name, d.Name);
                        var (read, write, active) = ReadPerfCounters(name);
                        return new DiskSnapshot(name, v.Size, v.FreeSpace, read, write, active);
                    }))
                .Where(d => d.TotalBytes > 0)
                .OrderBy(d => d.Name)
                .ToList();
            _channel.Writer.TryWrite(disks);
        });
    }

    private void InitPerfCounters()
    {
        _counters = new Dictionary<string, DiskPerfCounters>();
        try
        {
            var category = new PerformanceCounterCategory("LogicalDisk");
            foreach (var instance in category.GetInstanceNames())
            {
                if (instance == "_Total" || instance.Length < 2 || instance[1] != ':') continue;
                try
                {
                    _counters[instance[..2]] = new DiskPerfCounters(
                        new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", instance, true),
                        new PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", instance, true),
                        new PerformanceCounter("LogicalDisk", "% Disk Time", instance, true));
                }
                catch { }
            }
        }
        catch { }
    }

    private (ulong Read, ulong Write, double Active) ReadPerfCounters(string driveLetter)
    {
        if (_counters is null || !_counters.TryGetValue(driveLetter, out var c))
            return (0, 0, 0);
        try
        {
            var read = (ulong)Math.Max(0, c.Read.NextValue());
            var write = (ulong)Math.Max(0, c.Write.NextValue());
            var active = Math.Clamp(c.Active.NextValue(), 0, 100);
            return (read, write, active);
        }
        catch { return (0, 0, 0); }
    }

    private static string ExtractDriveLetter(string volumeName, string driveName)
    {
        foreach (var s in new[] { volumeName, driveName })
        {
            if (string.IsNullOrWhiteSpace(s)) continue;
            var trimmed = s.TrimEnd('\\', '/');
            if (trimmed.Length >= 2 && trimmed[1] == ':')
                return trimmed[..2];
        }
        return !string.IsNullOrWhiteSpace(volumeName) ? volumeName : driveName;
    }

    protected override void PostStop()
    {
        _tickSchedule?.Cancel();
        _channel?.Writer.TryComplete();
        if (_counters is not null)
            foreach (var c in _counters.Values)
                c.Dispose();
        base.PostStop();
    }

    private sealed record DiskPerfCounters(
        PerformanceCounter Read,
        PerformanceCounter Write,
        PerformanceCounter Active) : IDisposable
    {
        public void Dispose()
        {
            Read.Dispose();
            Write.Dispose();
            Active.Dispose();
        }
    }
}
