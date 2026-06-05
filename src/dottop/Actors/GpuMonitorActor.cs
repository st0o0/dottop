using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;
using dottop.Platform;

namespace dottop.Actors;

public sealed class GpuMonitorActor : ReceiveActor
{
    private readonly TimeSpan _interval;
    private Channel<GpuSnapshot>? _channel;
    private ICancelable? _tickSchedule;
    private CancellationTokenSource? _streamCts;

    public static Props Props(IGpuMetricsProvider gpuMetrics, TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new GpuMonitorActor(gpuMetrics, interval));

    public GpuMonitorActor(IGpuMetricsProvider gpuMetrics, TimeSpan interval)
    {
        _interval = interval;
        var gpuMetrics1 = gpuMetrics;

        Receive<StartMonitoring>(_ =>
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
        });

        Receive<Tick>(_ =>
        {
            if (_channel is null || !gpuMetrics1.IsAvailable)
            {
                return;
            }

            var snapshot = gpuMetrics1.GetSnapshot();
            _channel.Writer.TryWrite(snapshot);
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