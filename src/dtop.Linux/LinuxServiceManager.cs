using System.Diagnostics;
using dtop.Core.Models;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.Linux;

public sealed class LinuxServiceManager : IServiceManager
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Linux.ServiceManager");
    public List<ServiceInfo> GetServices()
    {
        var services = new List<ServiceInfo>();
        try
        {
            var output = RunSystemctl("list-units --type=service --all --no-pager --no-legend");
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4)
                {
                    continue;
                }

                var name = parts[0].Replace(".service", "");
                var active = parts[2]; // active/inactive
                var status = active switch
                {
                    "active" => ServiceStatus.Running,
                    "inactive" => ServiceStatus.Stopped,
                    "activating" => ServiceStatus.StartPending,
                    "deactivating" => ServiceStatus.StopPending,
                    _ => ServiceStatus.Stopped,
                };

                var displayName = parts.Length > 4 ? string.Join(' ', parts[4..]) : name;
                var description = GetServiceDescription(name);
                services.Add(new ServiceInfo(name, displayName, status, ServiceStartType.Manual, null, description));
            }
        }
        catch (Exception ex) { Trace.Warning("LinuxServiceManager", "Failed to list systemd services: {0}", ex.Message); }

        return services.OrderBy(s => s.DisplayName).ToList();
    }

    public string Start(string name)
    {
        RunSystemctl($"start {name}");
        return $"Service {name} gestartet";
    }

    public string Stop(string name)
    {
        RunSystemctl($"stop {name}");
        return $"Service {name} gestoppt";
    }

    public string Restart(string name)
    {
        RunSystemctl($"restart {name}");
        return $"Service {name} neugestartet";
    }

    private static string GetServiceDescription(string name)
    {
        try
        {
            var output = RunSystemctl($"show -p Description --value {name}");
            return output.Trim();
        }
        catch (Exception ex) { Trace.Warning("LinuxServiceManager", "Failed to get description for service {0}: {1}", name, ex.Message); return ""; }
    }

    private static string RunSystemctl(string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "systemctl",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(TimeSpan.FromSeconds(10));
        return output;
    }
}
