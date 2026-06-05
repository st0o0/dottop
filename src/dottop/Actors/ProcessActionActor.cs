using System.Diagnostics;
using Akka.Actor;
using dottop.Platform;

namespace dottop.Actors;

public sealed class ProcessActionActor : ReceiveActor
{
    public static Props Props(IProcessTreeProvider treeProvider) =>
        Akka.Actor.Props.Create(() => new ProcessActionActor(treeProvider));

    public ProcessActionActor(IProcessTreeProvider treeProvider)
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
                var tree = treeProvider.BuildTree(msg.Pid);
                Sender.Tell(tree);
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        Receive<GetProcessEnvironment>(msg =>
        {
            try
            {
                IReadOnlyDictionary<string, string> env = Environment.GetEnvironmentVariables()
                    .Cast<System.Collections.DictionaryEntry>()
                    .ToDictionary(e => e.Key.ToString()!, e => e.Value?.ToString() ?? "");
                Sender.Tell(new ProcessEnvironmentResult(env));
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        Receive<GetProcessHandles>(msg =>
        {
            try
            {
                var modules = GetProcessModules(msg.Pid);
                Sender.Tell(new ProcessHandlesResult(modules));
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });
    }

    private static IReadOnlyList<string> GetProcessModules(int pid)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            var modules = new List<string>();
            foreach (ProcessModule module in proc.Modules)
            {
                try
                {
                    var size = module.ModuleMemorySize / 1024;
                    modules.Add($"{module.ModuleName,-30} {size,8} KB  {module.FileName}");
                }
                catch
                {
                    // noop
                }
            }
            return modules.OrderBy(m => m).ToList();
        }
        catch
        {
            return ["Unable to read modules (access denied or process exited)"];
        }
    }
}
