using System.Diagnostics;
using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Models;
using dtop.Core.Platform;
using dtop.Services;
using Servus;
using Servus.Diagnostics;

namespace dtop.Actors;

public sealed class ProcessMonitorActor : ReceiveActor
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Process");

    private readonly IProcessClassifier _classifier;
    private readonly IMetricSink _sink;
    private Dictionary<int, (TimeSpan CpuTime, DateTime Timestamp)> _previousCpu = new();

    public static Props Props(IProcessClassifier classifier, IProcessTreeProvider treeProvider, IMetricSink sink) =>
        Akka.Actor.Props.Create(() => new ProcessMonitorActor(classifier, treeProvider, sink));

    public ProcessMonitorActor(IProcessClassifier classifier, IProcessTreeProvider treeProvider, IMetricSink sink)
    {
        _classifier = classifier;
        _sink = sink;

        Receive<Tick>(_ =>
        {
            var now = DateTime.UtcNow;
            var coreCount = Environment.ProcessorCount;
            var currentCpu = new Dictionary<int, (TimeSpan CpuTime, DateTime Timestamp)>();

            var processes = Process.GetProcesses();
            try
            {
                var snapshots = processes
                    .Select(p =>
                    {
                        try
                        {
                            var pid = p.Id;
                            var cpuTime = p.TotalProcessorTime;
                            currentCpu[pid] = (cpuTime, now);

                            double cpuPercent = 0;
                            if (_previousCpu.TryGetValue(pid, out var prev))
                            {
                                var elapsed = (now - prev.Timestamp).TotalMilliseconds;
                                if (elapsed > 0)
                                {
                                    var cpuDelta = (cpuTime - prev.CpuTime).TotalMilliseconds;
                                    cpuPercent = cpuDelta / elapsed / coreCount * 100;
                                    cpuPercent = Math.Clamp(cpuPercent, 0, 100);
                                }
                            }

                            return new ProcessSnapshot(
                                Pid: pid, Name: p.ProcessName, Group: _classifier.Classify(p),
                                CpuPercent: Math.Round(cpuPercent, 1),
                                WorkingSetBytes: p.WorkingSet64,
                                DiskBytesPerSec: 0, NetworkBytesPerSec: 0,
                                ThreadCount: p.Threads.Count, HandleCount: p.HandleCount,
                                UserName: "", ParentPid: 0);
                        }
                        catch
                        {
                            return null;
                        }
                    })
                    .Where(p => p is not null)
                    .OrderByDescending(p => p!.WorkingSetBytes)
                    .ToList();

                _previousCpu = currentCpu;
                _sink.Publish(snapshots!);
            }
            finally
            {
                foreach (var p in processes)
                {
                    p.Dispose();
                }
            }
        });
    }

    protected override void PostStop()
    {
        Trace.Debug(this, "Stopped");
        base.PostStop();
    }
}
