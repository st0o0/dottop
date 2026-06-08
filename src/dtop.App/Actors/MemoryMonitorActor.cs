using System.Threading.Channels;
using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Models;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.App.Actors;

public sealed class MemoryMonitorActor : ReceiveActor
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Memory");

    private sealed record Tick;

    private readonly IMemoryMetrics _memoryMetrics;
    private readonly TimeSpan _interval;
    private Channel<MemorySnapshot>? _channel;
    private ICancelable? _tickSchedule;
    private CancellationTokenSource? _streamCts;

    public static Props Props(IMemoryMetrics memoryMetrics, TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new MemoryMonitorActor(memoryMetrics, interval));

    public MemoryMonitorActor(IMemoryMetrics memoryMetrics, TimeSpan interval)
    {
        _memoryMetrics = memoryMetrics;
        _interval = interval;

        Receive<StartMemoryMonitoring>(_ => HandleStartMonitoring());
        Receive<StartMonitoring>(_ => HandleStartMonitoring());

        Receive<Tick>(_ =>
        {
            if (_channel is null)
            {
                return;
            }

            var (total, used) = _memoryMetrics.Measure();
            _channel.Writer.TryWrite(new MemorySnapshot(total, used));
        });
    }

    private void HandleStartMonitoring()
    {
        CleanupPreviousStream();
        _streamCts = new CancellationTokenSource();
        _channel = Channel.CreateBounded<MemorySnapshot>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
            TimeSpan.Zero, _interval, Self, new Tick(), Self);
        var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, _streamCts.Token);
        Sender.Tell(new MonitoringStream<MemorySnapshot>(stream, _streamCts));
        Trace.Info(this, "Monitoring started, interval={0}ms", _interval.TotalMilliseconds);
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
        Trace.Debug(this, "Stopped");
        base.PostStop();
    }
}
