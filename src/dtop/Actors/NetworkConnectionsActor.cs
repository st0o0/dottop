using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Platform;
using dtop.Services;

namespace dtop.Actors;

public sealed class NetworkConnectionsActor : ReceiveActor
{
    public static Props Props(IConnectionProvider provider, IMetricSink sink) =>
        Akka.Actor.Props.Create(() => new NetworkConnectionsActor(provider, sink));

    public NetworkConnectionsActor(IConnectionProvider provider, IMetricSink sink)
    {
        Receive<Tick>(_ => sink.Publish(provider.GetConnections()));
    }
}
