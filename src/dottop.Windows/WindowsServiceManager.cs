using System.Management;
using System.Runtime.Versioning;
using System.ServiceProcess;
using dottop.Core.Models;
using dottop.Core.Platform;

namespace dottop.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsServiceManager : IServiceManager
{
    public List<ServiceInfo> GetServices()
    {
        var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, Description FROM Win32_Service");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "";
                var desc = obj["Description"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(name))
                {
                    descriptions[name] = desc;
                }
            }
        }
        catch { }

        return ServiceController.GetServices()
            .Select(s => new ServiceInfo(
                s.ServiceName, s.DisplayName,
                MapStatus(s.Status), ServiceStartType.Manual, null,
                descriptions.GetValueOrDefault(s.ServiceName, "")))
            .OrderBy(s => s.DisplayName)
            .ToList();
    }

    public string Start(string name)
    {
        var sc = new ServiceController(name);
        sc.Start();
        return $"Service {name} gestartet";
    }

    public string Stop(string name)
    {
        var sc = new ServiceController(name);
        sc.Stop();
        return $"Service {name} gestoppt";
    }

    public string Restart(string name)
    {
        var sc = new ServiceController(name);
        if (sc.Status == ServiceControllerStatus.Running)
        {
            sc.Stop();
        }

        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
        sc.Start();
        return $"Service {name} neugestartet";
    }

    private static ServiceStatus MapStatus(ServiceControllerStatus status) => status switch
    {
        ServiceControllerStatus.Running => ServiceStatus.Running,
        ServiceControllerStatus.Stopped => ServiceStatus.Stopped,
        ServiceControllerStatus.StartPending => ServiceStatus.StartPending,
        ServiceControllerStatus.StopPending => ServiceStatus.StopPending,
        ServiceControllerStatus.Paused => ServiceStatus.Paused,
        _ => ServiceStatus.Stopped,
    };
}
