using System.Net.NetworkInformation;
using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;

namespace dottop.Actors;

public sealed class NetworkMonitorActor : ReceiveActor
{
    private readonly TimeSpan _interval;
    private Channel<List<NetworkSnapshot>>? _channel;
    private ICancelable? _tickSchedule;
    private CancellationTokenSource? _streamCts;
    private Dictionary<string, (long Rx, long Tx)>? _prevBytes;

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

            var currentBytes = new Dictionary<string, (long Rx, long Tx)>();
            var nets = new List<NetworkSnapshot>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.Speed == 0) continue;
                    var stats = ni.GetIPv4Statistics();
                    var name = ni.Name.Length > 20 ? ni.Name[..20] + "..." : ni.Name;
                    currentBytes[name] = (stats.BytesReceived, stats.BytesSent);
                    ulong rxPerSec = 0, txPerSec = 0;
                    if (_prevBytes is not null && _prevBytes.TryGetValue(name, out var prev))
                    {
                        rxPerSec = (ulong)Math.Max(0, stats.BytesReceived - prev.Rx);
                        txPerSec = (ulong)Math.Max(0, stats.BytesSent - prev.Tx);
                    }
                    nets.Add(new NetworkSnapshot(name, rxPerSec, txPerSec));
                }
            }
            catch { }
            _prevBytes = currentBytes;
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
