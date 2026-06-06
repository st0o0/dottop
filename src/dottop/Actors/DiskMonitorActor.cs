using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;
using dottop.Platform;

namespace dottop.Actors;

public sealed class DiskMonitorActor : ReceiveActor
{
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

            var disks = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.TotalSize > 0)
                .Select(d =>
                {
                    var name = d.Name.TrimEnd('\\', '/');
                    if (name.Length >= 2 && name[1] == ':') name = name[..2];
                    var (read, write, active) = _diskMetrics.GetMetrics(name);
                    return new DiskSnapshot(name, (ulong)d.TotalSize, (ulong)d.AvailableFreeSpace, read, write, active);
                })
                .OrderBy(d => d.Name)
                .ToList();
            _channel.Writer.TryWrite(disks);
        });
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
