using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;
using Hardware.Info;

namespace dottop.Actors;

public sealed class NetworkMonitorActor : ReceiveActor
{
    private readonly HardwareInfo _hw = new(TimeSpan.FromSeconds(2));
    private Channel<List<NetworkSnapshot>>? _channel;
    private ICancelable? _tickSchedule;

    public static Props Props() => Akka.Actor.Props.Create<NetworkMonitorActor>();

    public NetworkMonitorActor()
    {
        Receive<StartMonitoring>(_ =>
        {
            CleanupPreviousStream();

            var cts = new CancellationTokenSource();
            _channel = Channel.CreateBounded<List<NetworkSnapshot>>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.Zero, TimeSpan.FromSeconds(1), Self, new Tick(), Self);

            var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, cts.Token);
            Sender.Tell(new MonitoringStream<List<NetworkSnapshot>>(stream, cts));
        });

        Receive<Tick>(_ =>
        {
            if (_channel is null) return;
            _hw.RefreshNetworkAdapterList();
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
