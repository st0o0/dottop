using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Models;
using dtop.Core.Platform;
using dtop.Services;
using Servus;
using Servus.Diagnostics;

namespace dtop.Actors;

/// <summary>Pure sampler: Tick in → measure → publish to the metric sink.</summary>
public sealed class CpuMonitorActor : ReceiveActor
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Cpu");

    public static Props Props(ICpuMetrics cpuMetrics, IMetricSink sink) =>
        Akka.Actor.Props.Create(() => new CpuMonitorActor(cpuMetrics, sink));

    public CpuMonitorActor(ICpuMetrics cpuMetrics, IMetricSink sink)
    {
        Receive<Tick>(_ =>
        {
            var measurement = cpuMetrics.Measure();
            sink.Publish(new CpuSnapshot(cpuMetrics.ProcessorName, measurement.TotalPercent, measurement.CorePercents));
        });
    }

    protected override void PostStop()
    {
        Trace.Debug(this, "Stopped");
        base.PostStop();
    }
}
