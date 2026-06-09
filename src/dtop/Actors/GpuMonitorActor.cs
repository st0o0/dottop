using System.Threading.Channels;
using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Models;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.Actors;

public sealed class GpuMonitorActor : ReceiveActor
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Gpu");

    private sealed record Tick;

    private readonly IGpuMetrics _gpuMetrics;
    private readonly TimeSpan _interval;
    private Channel<GpuSnapshot>? _channel;
    private ICancelable? _tickSchedule;
    private CancellationTokenSource? _streamCts;

    public static Props Props(IGpuMetrics gpuMetrics, TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new GpuMonitorActor(gpuMetrics, interval));

    public GpuMonitorActor(IGpuMetrics gpuMetrics, TimeSpan interval)
    {
        _gpuMetrics = gpuMetrics;
        _interval = interval;

        Receive<StartGpuMonitoring>(_ => HandleStartMonitoring());
        Receive<StartMonitoring>(_ => HandleStartMonitoring());

        Receive<Tick>(_ =>
        {
            if (_channel is null || !_gpuMetrics.IsAvailable)
            {
                return;
            }

            var snapshot = _gpuMetrics.GetSnapshot();
            _channel.Writer.TryWrite(snapshot);
        });
    }

    private void HandleStartMonitoring()
    {
        CleanupPreviousStream();

        _streamCts = new CancellationTokenSource();
        _channel = Channel.CreateBounded<GpuSnapshot>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
            TimeSpan.Zero, _interval, Self, new Tick(), Self);

        var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, _streamCts.Token);
        Sender.Tell(new MonitoringStream<GpuSnapshot>(stream, _streamCts));
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
