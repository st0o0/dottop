using System.Diagnostics;
using dtop.Core.Messages;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.Linux;

public sealed class LinuxProcessTree : IProcessTreeProvider
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Linux.ProcessTree");

    public ProcessTreeResult BuildTree(int rootPid)
    {
        var (parentMap, nameMap) = ReadProcessMaps();
        return ProcessTreeBuilder.Build(rootPid, parentMap, nameMap);
    }

    private (Dictionary<int, int> ParentMap, Dictionary<int, string> NameMap) ReadProcessMaps()
    {
        var parentMap = new Dictionary<int, int>();
        var nameMap = new Dictionary<int, string>();

        try
        {
            foreach (var dir in Directory.GetDirectories("/proc"))
            {
                var dirName = Path.GetFileName(dir);
                if (!int.TryParse(dirName, out var pid))
                    continue;

                try
                {
                    var statLine = File.ReadAllText(Path.Combine(dir, "stat"));
                    var closeParenIdx = statLine.LastIndexOf(')');
                    if (closeParenIdx < 0)
                        continue;

                    var afterComm = statLine[(closeParenIdx + 2)..].Split(' ');
                    if (afterComm.Length >= 2 && int.TryParse(afterComm[1], out var ppid))
                        parentMap[pid] = ppid;

                    var openParenIdx = statLine.IndexOf('(');
                    if (openParenIdx >= 0 && closeParenIdx > openParenIdx)
                        nameMap[pid] = statLine[(openParenIdx + 1)..closeParenIdx];
                }
                catch (Exception ex)
                {
                    Trace.Warning("LinuxProcessTree", "Failed to read /proc/{0}/stat: {1}", dirName, ex.Message);
                }
            }
        }
        catch
        {
            foreach (var p in Process.GetProcesses())
            {
                try { nameMap[p.Id] = p.ProcessName; }
                catch (Exception ex) { Trace.Warning("LinuxProcessTree", "Failed to read process name for PID {0}: {1}", p.Id, ex.Message); }
            }
        }

        return (parentMap, nameMap);
    }
}
