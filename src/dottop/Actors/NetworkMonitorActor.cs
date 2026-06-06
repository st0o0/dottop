using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;
using Hardware.Info;

namespace dottop.Actors;

public sealed class NetworkMonitorActor : ReceiveActor
{
    private readonly HardwareInfo _hw = new(TimeSpan.FromSeconds(2));
    private readonly TimeSpan _interval;
    private Channel<List<NetworkSnapshot>>? _channel;
    private ICancelable? _tickSchedule;
    private CancellationTokenSource? _streamCts;

    public static Props Props(TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new NetworkMonitorActor(interval));

    public NetworkMonitorActor(TimeSpan interval)
    {
        _interval = interval;

        Receive<StartMonitoring>(_ =>
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
        });

        Receive<Tick>(_ =>
        {
            if (_channel is null) return;

            try { _hw.RefreshNetworkAdapterList(); }
            catch { }
            var nets = _hw.NetworkAdapterList
                .Where(n => n.Speed > 0)
                .Select(n => new NetworkSnapshot(
                    n.Name.Length > 20 ? n.Name[..20] + "..." : n.Name,
                    n.BytesReceivedPersec, n.BytesSentPersec))
                .ToList();
            _channel.Writer.TryWrite(nets);
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
