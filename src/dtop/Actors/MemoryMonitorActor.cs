using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Models;
using dtop.Core.Platform;
using dtop.Services;
using Servus;
using Servus.Diagnostics;

namespace dtop.Actors;

/// <summary>Pure sampler: Tick in → measure → publish to the metric sink.</summary>
public sealed class MemoryMonitorActor : ReceiveActor
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Memory");

    public static Props Props(IMemoryMetrics memoryMetrics, IMetricSink sink) =>
        Akka.Actor.Props.Create(() => new MemoryMonitorActor(memoryMetrics, sink));

    public MemoryMonitorActor(IMemoryMetrics memoryMetrics, IMetricSink sink)
    {
        Receive<Tick>(_ =>
        {
            var (total, used) = memoryMetrics.Measure();
            sink.Publish(new MemorySnapshot(total, used));
        });
    }

    protected override void PostStop()
    {
        Trace.Debug(this, "Stopped");
        base.PostStop();
    }
}
