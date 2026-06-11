using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Models;
using dtop.Core.Platform;
using dtop.Services;
using Servus;
using Servus.Diagnostics;

namespace dtop.Actors;

/// <summary>Pure sampler: Tick in → measure → publish to the metric sink.</summary>
public sealed class NetworkMonitorActor : ReceiveActor
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Network");

    public static Props Props(INetworkMetrics networkMetrics, IMetricSink sink) =>
        Akka.Actor.Props.Create(() => new NetworkMonitorActor(networkMetrics, sink));

    public NetworkMonitorActor(INetworkMetrics networkMetrics, IMetricSink sink)
    {
        Receive<Tick>(_ =>
        {
            var snapshots = networkMetrics.Measure().ToList();
            sink.Publish(snapshots);
        });
    }

    protected override void PostStop()
    {
        Trace.Debug(this, "Stopped");
        base.PostStop();
    }
}
