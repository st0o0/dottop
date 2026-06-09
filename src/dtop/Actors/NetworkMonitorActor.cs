using System.Threading.Channels;
using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Models;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.Actors;

public sealed class NetworkMonitorActor : ReceiveActor
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Network");

    private sealed record Tick;

    private readonly INetworkMetrics _networkMetrics;
    private readonly TimeSpan _interval;
    private Channel<List<NetworkSnapshot>>? _channel;
    private ICancelable? _tickSchedule;
    private CancellationTokenSource? _streamCts;

    public static Props Props(INetworkMetrics networkMetrics, TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new NetworkMonitorActor(networkMetrics, interval));

    public NetworkMonitorActor(INetworkMetrics networkMetrics, TimeSpan interval)
    {
        _networkMetrics = networkMetrics;
        _interval = interval;

        Receive<StartNetworkMonitoring>(_ => HandleStartMonitoring());
        Receive<StartMonitoring>(_ => HandleStartMonitoring());

        Receive<Tick>(_ =>
        {
            if (_channel is null)
            {
                return;
            }

            var snapshots = _networkMetrics.Measure().ToList();
            _channel.Writer.TryWrite(snapshots);
        });
    }

    private void HandleStartMonitoring()
    {
        CleanupPreviousStream();
        _streamCts = new CancellationTokenSource();
        _channel = Channel.CreateBounded<List<NetworkSnapshot>>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
            TimeSpan.Zero, _interval, Self, new Tick(), Self);
        var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, _streamCts.Token);
        Sender.Tell(new MonitoringStream<List<NetworkSnapshot>>(stream, _streamCts));
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
