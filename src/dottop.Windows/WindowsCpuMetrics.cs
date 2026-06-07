using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using dottop.Core.Platform;
using Microsoft.Win32;

namespace dottop.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsCpuMetrics : ICpuMetrics
{
    private long _prevIdle;
    private long _prevTotal;
    private long[]? _prevCoreIdle;
    private long[]? _prevCoreTotal;
    private string? _cpuName;

    public string ProcessorName => _cpuName ??= ReadCpuName();
    public int CoreCount => Environment.ProcessorCount;

    public CpuMeasurement Measure()
    {
        try
        {
            GetSystemTimes(out var idleTime, out var kernelTime, out var userTime);
            var idle = idleTime.ToLong();
            var total = kernelTime.ToLong() + userTime.ToLong();

            var idleDelta = idle - _prevIdle;
            var totalDelta = total - _prevTotal;
            _prevIdle = idle;
            _prevTotal = total;

            var totalPercent = totalDelta > 0 ? (1.0 - (double)idleDelta / totalDelta) * 100 : 0;
            totalPercent = Math.Clamp(totalPercent, 0, 100);

            var coreCount = Environment.ProcessorCount;
            var cores = GetPerCoreCpu(coreCount);

            return new CpuMeasurement(totalPercent, cores);
        }
        catch
        {
            return new CpuMeasurement(0, []);
        }
    }

    private List<double> GetPerCoreCpu(int coreCount)
    {
        try
        {
            var size = Marshal.SizeOf<SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>() * coreCount;
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                var status = NtQuerySystemInformation(8, buffer, size, out _);
                if (status != 0) return Enumerable.Repeat(0.0, coreCount).ToList();

                _prevCoreIdle ??= new long[coreCount];
                _prevCoreTotal ??= new long[coreCount];

                var cores = new List<double>(coreCount);
                for (var i = 0; i < coreCount; i++)
                {
                    var ptr = buffer + i * Marshal.SizeOf<SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>();
                    var info = Marshal.PtrToStructure<SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION>(ptr);
                    var coreIdle = info.IdleTime;
                    var coreTotal = info.KernelTime + info.UserTime;

                    var idleDelta = coreIdle - _prevCoreIdle[i];
                    var totalDelta = coreTotal - _prevCoreTotal[i];
                    _prevCoreIdle[i] = coreIdle;
                    _prevCoreTotal[i] = coreTotal;

                    var pct = totalDelta > 0 ? (1.0 - (double)idleDelta / totalDelta) * 100 : 0;
                    cores.Add(Math.Clamp(pct, 0, 100));
                }
                return cores;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        catch { return Enumerable.Repeat(0.0, coreCount).ToList(); }
    }

    private static string ReadCpuName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "CPU";
        }
        catch { return "CPU"; }
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(int infoClass, IntPtr buffer, int size, out int returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION
    {
        public long IdleTime;
        public long KernelTime;
        public long UserTime;
        public long DpcTime;
        public long InterruptTime;
        public int InterruptCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint Low;
        public uint High;
        public long ToLong() => ((long)High << 32) | Low;
    }
}
