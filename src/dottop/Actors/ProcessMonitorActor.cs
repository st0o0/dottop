using System.Diagnostics;
using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;
using dottop.Platform;

namespace dottop.Actors;

public sealed class ProcessMonitorActor : ReceiveActor
{
    private readonly TimeSpan _interval;
    private Channel<List<ProcessSnapshot>>? _channel;
    private ICancelable? _tickSchedule;
    private CancellationTokenSource? _streamCts;
    private Dictionary<int, (TimeSpan CpuTime, DateTime Timestamp)> _previousCpu = new();

    public static Props Props(IProcessClassifier classifier, TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new ProcessMonitorActor(classifier, interval));

    public ProcessMonitorActor(IProcessClassifier classifier, TimeSpan interval)
    {
        _interval = interval;

        Receive<StartMonitoring>(_ =>
        {
            CleanupPreviousStream();

            _streamCts = new CancellationTokenSource();
            _channel = Channel.CreateBounded<List<ProcessSnapshot>>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.Zero, _interval, Self, new Tick(), Self);

            var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, _streamCts.Token);
            Sender.Tell(new MonitoringStream<List<ProcessSnapshot>>(stream, _streamCts));
        });

        Receive<Tick>(_ =>
        {
            if (_channel is null)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var coreCount = Environment.ProcessorCount;
            var currentCpu = new Dictionary<int, (TimeSpan CpuTime, DateTime Timestamp)>();

            var snapshots = Process.GetProcesses()
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
                            Pid: pid, Name: p.ProcessName, Group: classifier.Classify(p),
                            CpuPercent: Math.Round(cpuPercent, 1),
                            WorkingSetBytes: p.WorkingSet64,
                            DiskBytesPerSec: 0, NetworkBytesPerSec: 0,
                            ThreadCount: p.Threads.Count, HandleCount: p.HandleCount,
                            UserName: "", ParentPid: 0);
                    }
                    catch { return null; }
                })
                .Where(p => p is not null)
                .OrderByDescending(p => p!.WorkingSetBytes)
                .ToList();

            _previousCpu = currentCpu;
            _channel.Writer.TryWrite(snapshots!);
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
