using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;
using dottop.Platform;

namespace dottop.Actors;

public sealed class GpuMonitorActor : ReceiveActor
{
    private readonly IGpuMetricsProvider _gpuMetrics;
    private Channel<GpuSnapshot>? _channel;
    private ICancelable? _tickSchedule;

    public static Props Props(IGpuMetricsProvider gpuMetrics) =>
        Akka.Actor.Props.Create(() => new GpuMonitorActor(gpuMetrics));

    public GpuMonitorActor(IGpuMetricsProvider gpuMetrics)
    {
        _gpuMetrics = gpuMetrics;

        Receive<StartMonitoring>(_ =>
        {
            CleanupPreviousStream();

            var cts = new CancellationTokenSource();
            _channel = Channel.CreateBounded<GpuSnapshot>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.Zero, TimeSpan.FromSeconds(1), Self, new Tick(), Self);

            var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, cts.Token);
            Sender.Tell(new MonitoringStream<GpuSnapshot>(stream, cts));
        });

        Receive<Tick>(_ =>
        {
            if (_channel is null || !_gpuMetrics.IsAvailable) return;
            var snapshot = _gpuMetrics.GetSnapshot();
            _channel.Writer.TryWrite(snapshot);
        });
    }

    private void CleanupPreviousStream()
    {
        _tickSchedule?.Cancel();
        _channel?.Writer.TryComplete();
        _tickSchedule = null;
        _channel = null;
    }

    protected override void PostStop()
    {
        CleanupPreviousStream();
        base.PostStop();
    }
}
