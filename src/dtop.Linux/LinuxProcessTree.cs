using System.Diagnostics;
using dtop.Core.Messages;
using dtop.Core.Platform;

namespace dtop.Linux;

public sealed class LinuxProcessTree : IProcessTreeProvider
{
    public ProcessTreeResult BuildTree(int rootPid)
    {
        var parentMap = new Dictionary<int, int>();
        var nameMap = new Dictionary<int, string>();

        try
        {
            foreach (var dir in Directory.GetDirectories("/proc"))
            {
                var dirName = Path.GetFileName(dir);
                if (!int.TryParse(dirName, out var pid))
                {
                    continue;
                }

                try
                {
                    var statLine = File.ReadAllText(Path.Combine(dir, "stat"));
                    // Format: pid (comm) state ppid ...
                    // comm can contain spaces/parens, so find last ')' to parse reliably
                    var closeParenIdx = statLine.LastIndexOf(')');
                    if (closeParenIdx < 0)
                    {
                        continue;
                    }

                    var afterComm = statLine[(closeParenIdx + 2)..].Split(' ');
                    if (afterComm.Length >= 2 && int.TryParse(afterComm[1], out var ppid))
                    {
                        parentMap[pid] = ppid;
                    }

                    // Extract comm from between parens
                    var openParenIdx = statLine.IndexOf('(');
                    if (openParenIdx >= 0 && closeParenIdx > openParenIdx)
                    {
                        nameMap[pid] = statLine[(openParenIdx + 1)..closeParenIdx];
                    }
                }
                catch { }
            }
        }
        catch
        {
            // Fallback: use Process API
            foreach (var p in Process.GetProcesses())
            {
                try { nameMap[p.Id] = p.ProcessName; } catch { }
            }
        }

        var childrenMap = new Dictionary<int, List<int>>();
        foreach (var (pid, ppid) in parentMap)
        {
            if (!childrenMap.ContainsKey(ppid))
            {
                childrenMap[ppid] = [];
            }

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
}
