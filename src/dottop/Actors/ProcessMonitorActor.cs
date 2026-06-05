using System.Diagnostics;
using System.Threading.Channels;
using Akka.Actor;
using dottop.Models;

namespace dottop.Actors;

public sealed class ProcessMonitorActor : ReceiveActor
{
    private Channel<List<ProcessSnapshot>>? _channel;
    private ICancelable? _tickSchedule;

    public static Props Props() => Akka.Actor.Props.Create<ProcessMonitorActor>();

    public ProcessMonitorActor()
    {
        Receive<StartMonitoring>(_ =>
        {
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
            var snapshots = Process.GetProcesses()
                .Select(p =>
                {
                    try
                    {
                        return new ProcessSnapshot(
                            Pid: p.Id, Name: p.ProcessName, Group: ClassifyProcess(p),
                            CpuPercent: 0, WorkingSetBytes: p.WorkingSet64,
                            DiskBytesPerSec: 0, NetworkBytesPerSec: 0,
                            ThreadCount: p.Threads.Count, HandleCount: p.HandleCount,
                            UserName: "", ParentPid: 0);
                    }
                    catch { return null; }
                })
                .Where(p => p is not null)
                .OrderByDescending(p => p!.WorkingSetBytes)
                .ToList();
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

    protected override void PostStop()
    {
        _tickSchedule?.Cancel();
        _channel?.Writer.TryComplete();
        base.PostStop();
    }
}
