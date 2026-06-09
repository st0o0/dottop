using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using dtop.Core.Messages;
using dtop.Core.Platform;

namespace dtop.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsProcessTree : IProcessTreeProvider
{
    public ProcessTreeResult BuildTree(int rootPid)
    {
        var (parentMap, nameMap) = ReadProcessMaps();
        return ProcessTreeBuilder.Build(rootPid, parentMap, nameMap);
    }

    private static (Dictionary<int, int> ParentMap, Dictionary<int, string> NameMap) ReadProcessMaps()
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
                try { nameMap[p.Id] = p.ProcessName; } catch { }
            }
        }

        return (parentMap, nameMap);
    }
}
