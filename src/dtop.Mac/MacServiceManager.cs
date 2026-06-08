using System.Diagnostics;
using dtop.Core.Models;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.Mac;

public sealed class MacServiceManager : IServiceManager
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Mac.ServiceManager");

    public List<ServiceInfo> GetServices()
    {
        try
        {
            var psi = new ProcessStartInfo("launchctl", "list")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd() ?? "";
            proc?.WaitForExit(3000);

            var services = new List<ServiceInfo>();
            foreach (var line in output.Split('\n').Skip(1)) // skip header
            {
                var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    var pidStr = parts[0].Trim();
                    var label = parts[2].Trim();
                    var hasPid = int.TryParse(pidStr, out var pid) && pid > 0;
                    var status = hasPid ? ServiceStatus.Running : ServiceStatus.Stopped;
                    services.Add(new ServiceInfo(label, label, status, ServiceStartType.Automatic, hasPid ? pid : null));
                }
            }
            return services;
        }
        catch (Exception ex)
        {
            Trace.Warning(this, "launchctl list failed: {0}", ex.Message);
            return [];
        }
    }

    public string Start(string name) => RunLaunchctl("start", name);
    public string Stop(string name) => RunLaunchctl("stop", name);
    public string Restart(string name) { Stop(name); return Start(name); }

    private static string RunLaunchctl(string action, string label)
    {
        try
        {
            var psi = new ProcessStartInfo("launchctl", $"{action} {label}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            return proc?.ExitCode == 0 ? $"Service {action}: {label}" : $"Failed to {action} {label}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
