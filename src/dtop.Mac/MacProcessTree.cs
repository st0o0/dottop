using System.Diagnostics;
using dtop.Core.Messages;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.Mac;

public sealed class MacProcessTree : IProcessTreeProvider
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Mac.ProcessTree");

    public ProcessTreeResult BuildTree(int rootPid)
    {
        try
        {
            var psi = new ProcessStartInfo("ps", "-eo pid,ppid,comm")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd() ?? "";
            proc?.WaitForExit(3000);

            var parentMap = new Dictionary<int, int>();
            var nameMap = new Dictionary<int, string>();

            foreach (var line in output.Split('\n').Skip(1)) // skip header
            {
                var parts = line.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && int.TryParse(parts[0], out var pid) && int.TryParse(parts[1], out var ppid))
                {
                    parentMap[pid] = ppid;
                    nameMap[pid] = Path.GetFileName(parts[2].Trim());
                }
            }

            var childrenMap = new Dictionary<int, List<int>>();
            foreach (var (pid, ppid) in parentMap)
            {
                if (!childrenMap.TryGetValue(ppid, out var children))
                {
                    children = [];
                    childrenMap[ppid] = children;
                }
                children.Add(pid);
            }

            return BuildNode(rootPid, nameMap, childrenMap, 0);
        }
        catch (Exception ex)
        {
            Trace.Warning(this, "Process tree failed: {0}", ex.Message);
            return new ProcessTreeResult(rootPid, $"PID {rootPid}", []);
        }
    }

    private static ProcessTreeResult BuildNode(int pid, Dictionary<int, string> names,
        Dictionary<int, List<int>> childrenMap, int depth)
    {
        var name = names.GetValueOrDefault(pid, $"PID {pid}");
        var children = new List<ProcessTreeResult>();
        if (depth < 5 && childrenMap.TryGetValue(pid, out var childPids))
        {
            foreach (var childPid in childPids.OrderBy(p => p))
                children.Add(BuildNode(childPid, names, childrenMap, depth + 1));
        }
        return new ProcessTreeResult(pid, name, children);
    }
}
