using Akka.Actor;
using dottop.Models;
using Hardware.Info;

namespace dottop.Actors;

public sealed class NetworkMonitorActor : ReceiveActor
{
    private readonly HardwareInfo _hw = new(TimeSpan.FromSeconds(2));
    public static Props Props() => Akka.Actor.Props.Create<NetworkMonitorActor>();

    public NetworkMonitorActor()
    {
        Receive<Tick>(_ =>
        {
            _hw.RefreshNetworkAdapterList();
            var nets = _hw.NetworkAdapterList
                .Where(n => n.Speed > 0)
                .Select(n => new NetworkSnapshot(
                    n.Name.Length > 20 ? n.Name[..20] + "..." : n.Name,
                    n.BytesReceivedPersec, n.BytesSentPersec))
                .ToList();
            Context.System.EventStream.Publish(nets);
        });
    }
}
