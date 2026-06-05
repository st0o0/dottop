using System.Diagnostics;
using Akka.Actor;

namespace dottop.Actors;

public sealed class ProcessActionActor : ReceiveActor
{
    public static Props Props() => Akka.Actor.Props.Create<ProcessActionActor>();

    public ProcessActionActor()
    {
        Receive<KillProcess>(msg =>
        {
            try
            {
                var proc = Process.GetProcessById(msg.Pid);
                proc.Kill();
                Sender.Tell(new ActionSuccess($"Killed process {msg.Pid}"));
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        Receive<SetProcessPriority>(msg =>
        {
            try
            {
                var proc = Process.GetProcessById(msg.Pid);
                proc.PriorityClass = msg.Priority;
                Sender.Tell(new ActionSuccess($"Priority set for {msg.Pid}"));
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        Receive<SetProcessAffinity>(msg =>
        {
            try
            {
                var proc = Process.GetProcessById(msg.Pid);
                proc.ProcessorAffinity = msg.AffinityMask;
                Sender.Tell(new ActionSuccess($"Affinity set for {msg.Pid}"));
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        Receive<GetProcessTree>(msg =>
        {
            try
            {
                var proc = Process.GetProcessById(msg.Pid);
                var tree = BuildProcessTree(msg.Pid, proc.ProcessName);
                Sender.Tell(tree);
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        Receive<GetProcessEnvironment>(msg =>
        {
            try
            {
                var proc = Process.GetProcessById(msg.Pid);
                var env = proc.StartInfo.Environment
                    .ToDictionary(kv => kv.Key, kv => kv.Value ?? "")
                    as IReadOnlyDictionary<string, string>;
                Sender.Tell(new ProcessEnvironmentResult(env));
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        Receive<GetProcessHandles>(msg =>
        {
            try
            {
                Sender.Tell(new ProcessHandlesResult([]));
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });
    }

    private static ProcessTreeResult BuildProcessTree(int pid, string name)
    {
        var children = new List<ProcessTreeResult>();
        return new ProcessTreeResult(pid, name, children);
    }
}
