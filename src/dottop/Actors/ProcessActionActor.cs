using System.Diagnostics;
using System.Management;
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
                var tree = BuildProcessTree(msg.Pid);
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

    private static ProcessTreeResult BuildProcessTree(int rootPid)
    {
        var parentMap = new Dictionary<int, int>();
        var nameMap = new Dictionary<int, string>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, ParentProcessId, Name FROM Win32_Process");
            foreach (var obj in searcher.Get())
            {
                var pid = Convert.ToInt32(obj["ProcessId"]);
                var ppid = Convert.ToInt32(obj["ParentProcessId"]);
                var name = obj["Name"]?.ToString() ?? "";
                parentMap[pid] = ppid;
                nameMap[pid] = name;
            }
        }
        catch
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    nameMap[p.Id] = p.ProcessName;
                }
                catch
                {
                    // noop
                }
            }
        }

        var childrenMap = new Dictionary<int, List<int>>();
        foreach (var (pid, ppid) in parentMap)
        {
            if (!childrenMap.ContainsKey(ppid))
                childrenMap[ppid] = [];
            childrenMap[ppid].Add(pid);
        }

        return BuildNode(rootPid, nameMap, childrenMap, depth: 0);
    }

    private static ProcessTreeResult BuildNode(int pid, Dictionary<int, string> names,
        Dictionary<int, List<int>> childrenMap, int depth)
    {
        var name = names.GetValueOrDefault(pid, $"PID {pid}");
        var children = new List<ProcessTreeResult>();

        if (depth < 5 && childrenMap.TryGetValue(pid, out var childPids))
        {
            foreach (var childPid in childPids.OrderBy(p => p))
            {
                children.Add(BuildNode(childPid, names, childrenMap, depth + 1));
            }
        }

        return new ProcessTreeResult(pid, name, children);
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
