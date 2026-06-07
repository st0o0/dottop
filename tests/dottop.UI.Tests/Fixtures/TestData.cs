using dottop.Core.Models;

namespace dottop.UI.Tests.Fixtures;

public static class TestData
{
    public static CpuSnapshot Cpu => new(
        "Test CPU i7-13700K",
        42.5,
        [35.0, 50.0, 40.0, 45.0]);

    public static MemorySnapshot Memory => new(
        TotalBytes: 16UL * 1024 * 1024 * 1024,   // 16 GB
        UsedBytes: 8UL * 1024 * 1024 * 1024);     // 8 GB

    public static GpuSnapshot Gpu => new(
        "N/A", 0, 0, 0, 0);

    public static List<DiskSnapshot> Disks =>
    [
        new("C:", 500UL * 1024 * 1024 * 1024, 200UL * 1024 * 1024 * 1024, 1024, 2048, 15.0),
        new("D:", 1000UL * 1024 * 1024 * 1024, 700UL * 1024 * 1024 * 1024, 512, 1024, 5.0),
    ];

    public static List<NetworkSnapshot> Networks =>
    [
        new("Ethernet", 1024 * 100, 1024 * 50),
        new("Wi-Fi", 1024 * 30, 1024 * 10),
    ];

    public static List<ProcessSnapshot> Processes =>
    [
        new(1001, "chrome",   ProcessGroup.Apps,       12.5, 500_000_000, 1024, 2048, 30, 150, "User", 0),
        new(1002, "code",     ProcessGroup.Apps,        8.3, 400_000_000,  512, 1024, 25, 120, "User", 0),
        new(1003, "svchost",  ProcessGroup.Windows,     2.1, 50_000_000,  256,  128,  5,  40, "SYSTEM", 0),
        new(1004, "explorer", ProcessGroup.Windows,     1.0, 80_000_000,  128,   64, 10,  60, "User", 0),
        new(1005, "spotify",  ProcessGroup.Apps,        5.0, 200_000_000,  768, 4096, 15,  80, "User", 0),
    ];

    public static List<ServiceInfo> Services =>
    [
        new("wuauserv",  "Windows Update",  ServiceStatus.Running, ServiceStartType.Automatic, 1234, "Manages Windows Updates"),
        new("Spooler",   "Print Spooler",   ServiceStatus.Stopped, ServiceStartType.Manual,    null, "Manages print jobs"),
        new("W32Time",   "Windows Time",    ServiceStatus.Running, ServiceStartType.Automatic, 5678, "Synchronizes date and time"),
    ];

    public static List<ConnectionSnapshot> Connections =>
    [
        new("chrome",  1001, "192.168.1.10:54321", "142.250.80.46:443",   "Established", "TCP"),
        new("svchost", 1003, "0.0.0.0:135",        "0.0.0.0:0",           "Listen",      "TCP"),
        new("spotify", 1005, "192.168.1.10:50000",  "35.186.224.45:4070",  "TimeWait",    "TCP"),
    ];
}
