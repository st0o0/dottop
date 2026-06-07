using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using dottop.Core.Messages;
using dottop.Core.Platform;

namespace dottop.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsProcessTree : IProcessTreeProvider
{
    public ProcessTreeResult BuildTree(int rootPid)
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
            if (!childrenMap.TryGetValue(ppid, out var value))
            {
                value = [];
                childrenMap[ppid] = value;
            }

            value.Add(pid);
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
}
