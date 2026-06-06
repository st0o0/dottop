using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;
using dottop.Platform;

namespace dottop.Actors;

public sealed class CpuMonitorActor : ReceiveActor
{
    private readonly ICpuMetricsProvider _cpuMetrics;
    private readonly TimeSpan _interval;
    private Channel<CpuSnapshot>? _channel;
    private ICancelable? _tickSchedule;
    private CancellationTokenSource? _streamCts;

    public static Props Props(ICpuMetricsProvider cpuMetrics, TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new CpuMonitorActor(cpuMetrics, interval));

    public CpuMonitorActor(ICpuMetricsProvider cpuMetrics, TimeSpan interval)
    {
        _cpuMetrics = cpuMetrics;
        _interval = interval;

        Receive<StartMonitoring>(_ =>
        {
            CleanupPreviousStream();

            _streamCts = new CancellationTokenSource();
            _channel = Channel.CreateBounded<CpuSnapshot>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.Zero, _interval, Self, new Tick(), Self);

            var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, _streamCts.Token);
            Sender.Tell(new MonitoringStream<CpuSnapshot>(stream, _streamCts));
        });

        Receive<Tick>(_ =>
        {
            if (_channel is null) return;

            var (name, total, cores) = _cpuMetrics.GetSnapshot();
            _channel.Writer.TryWrite(new CpuSnapshot(name, total, cores));
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
