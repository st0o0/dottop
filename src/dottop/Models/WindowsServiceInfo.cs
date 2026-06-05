namespace dottop.Models;

public enum ServiceStatus { Running, Stopped, StartPending, StopPending, Paused }
public enum ServiceStartType { Automatic, Manual, Disabled }

public record WindowsServiceInfo(
    string Name,
    string DisplayName,
    ServiceStatus Status,
    ServiceStartType StartType,
    int? Pid);
