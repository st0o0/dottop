using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Models;
using dtop.Core.Platform;
using dtop.Services;
using Servus;
using Servus.Diagnostics;

namespace dtop.Actors;

/// <summary>Pure sampler: Tick in → measure → publish to the metric sink.</summary>
public sealed class GpuMonitorActor : ReceiveActor
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Gpu");

    public static Props Props(IGpuMetrics gpuMetrics, IMetricSink sink) =>
        Akka.Actor.Props.Create(() => new GpuMonitorActor(gpuMetrics, sink));

    public GpuMonitorActor(IGpuMetrics gpuMetrics, IMetricSink sink)
    {
        Receive<Tick>(_ =>
        {
            if (!gpuMetrics.IsAvailable)
            {
                return;
            }

            var snapshot = gpuMetrics.GetSnapshot();
            sink.Publish(snapshot);
        });
    }

    protected override void PostStop()
    {
        Trace.Debug(this, "Stopped");
        base.PostStop();
    }
}
