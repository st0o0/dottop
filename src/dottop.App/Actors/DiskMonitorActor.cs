using System.Threading.Channels;
using Akka.Actor;
using dottop.Core.Messages;
using dottop.Core.Models;
using dottop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dottop.Actors;

public sealed class DiskMonitorActor : ReceiveActor
{
    private static readonly TraceChannel _trace = Senf.Tracing.For("Disk");

    private sealed record Tick;

    private readonly IDiskMetrics _diskMetrics;
    private readonly TimeSpan _interval;
    private Channel<List<DiskSnapshot>>? _channel;
    private ICancelable? _tickSchedule;
    private CancellationTokenSource? _streamCts;

    public static Props Props(IDiskMetrics diskMetrics, TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new DiskMonitorActor(diskMetrics, interval));

    public DiskMonitorActor(IDiskMetrics diskMetrics, TimeSpan interval)
    {
        _diskMetrics = diskMetrics;
        _interval = interval;

        Receive<StartDiskMonitoring>(_ => HandleStartMonitoring());
        Receive<StartMonitoring>(_ => HandleStartMonitoring());

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

    private void HandleStartMonitoring()
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
        _trace.Info(this, "Monitoring started, interval={0}ms", _interval.TotalMilliseconds);
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
        _trace.Debug(this, "Stopped");
        base.PostStop();
    }
}
