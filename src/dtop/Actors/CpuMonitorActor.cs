using System.Threading.Channels;
using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Models;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.Actors;

public sealed class CpuMonitorActor : ReceiveActor
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Cpu");

    private sealed record Tick;

    private readonly ICpuMetrics _cpuMetrics;
    private readonly TimeSpan _interval;
    private Channel<CpuSnapshot>? _channel;
    private ICancelable? _tickSchedule;
    private CancellationTokenSource? _streamCts;

    public static Props Props(ICpuMetrics cpuMetrics, TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new CpuMonitorActor(cpuMetrics, interval));

    public CpuMonitorActor(ICpuMetrics cpuMetrics, TimeSpan interval)
    {
        _cpuMetrics = cpuMetrics;
        _interval = interval;

        Receive<StartCpuMonitoring>(_ => HandleStartMonitoring());
        Receive<StartMonitoring>(_ => HandleStartMonitoring());

        Receive<Tick>(_ =>
        {
            if (_channel is null)
            {
                return;
            }

            var measurement = _cpuMetrics.Measure();
            _channel.Writer.TryWrite(new CpuSnapshot(
                _cpuMetrics.ProcessorName, measurement.TotalPercent, measurement.CorePercents));
        });
    }

    private void HandleStartMonitoring()
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
