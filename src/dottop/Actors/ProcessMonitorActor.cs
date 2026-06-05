using System.Diagnostics;
using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;

namespace dottop.Actors;

public sealed class ProcessMonitorActor : ReceiveActor
{
    private Channel<List<ProcessSnapshot>>? _channel;
    private ICancelable? _tickSchedule;
    private Dictionary<int, (TimeSpan CpuTime, DateTime Timestamp)> _previousCpu = new();

    public static Props Props() => Akka.Actor.Props.Create<ProcessMonitorActor>();

    public ProcessMonitorActor()
    {
        Receive<StartMonitoring>(_ =>
        {
            CleanupPreviousStream();

            var cts = new CancellationTokenSource();
            _channel = Channel.CreateBounded<List<ProcessSnapshot>>(new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            _tickSchedule = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                TimeSpan.Zero, TimeSpan.FromSeconds(1), Self, new Tick(), Self);

            var stream = ChannelHelper.ReadFromChannelAsync(_channel.Reader, cts.Token);
            Sender.Tell(new MonitoringStream<List<ProcessSnapshot>>(stream, cts));
        });

        Receive<Tick>(_ =>
        {
            if (_channel is null) return;

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
                            Pid: pid, Name: p.ProcessName, Group: ClassifyProcess(p),
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

    private static ProcessGroup ClassifyProcess(Process p)
    {
        try
        {
            if (p.MainWindowHandle != nint.Zero) return ProcessGroup.Apps;
            if (p.SessionId == 0) return ProcessGroup.Windows;
            return ProcessGroup.Background;
        }
        catch { return ProcessGroup.Background; }
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
