using System.Runtime.Versioning;
using System.ServiceProcess;
using dottop.Models;

namespace dottop.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsServiceManager : IServiceManager
{
    public List<WindowsServiceInfo> GetServices()
    {
        return ServiceController.GetServices()
            .Select(s => new WindowsServiceInfo(
                s.ServiceName, s.DisplayName,
                MapStatus(s.Status), ServiceStartType.Manual, null))
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
        if (sc.Status == ServiceControllerStatus.Running) sc.Stop();
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
