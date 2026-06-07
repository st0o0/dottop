using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using dottop.Core.Platform;

namespace dottop.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsMemoryMetrics : IMemoryMetrics
{
    public (ulong TotalBytes, ulong UsedBytes) Measure()
    {
        var status = new MEMORYSTATUSEX { dwLength = 64 };
        if (GlobalMemoryStatusEx(ref status))
            return (status.ullTotalPhys, status.ullTotalPhys - status.ullAvailPhys);
        return (0, 0);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}
