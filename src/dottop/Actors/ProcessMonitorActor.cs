using System.Diagnostics;
using Akka.Actor;
using dottop.Models;

namespace dottop.Actors;

public sealed class ProcessMonitorActor : ReceiveActor
{
    public static Props Props() => Akka.Actor.Props.Create<ProcessMonitorActor>();

    public ProcessMonitorActor()
    {
        Receive<Tick>(_ =>
        {
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
            Context.System.EventStream.Publish(snapshots!);
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
}
