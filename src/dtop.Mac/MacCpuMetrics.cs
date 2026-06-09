using System.Runtime.InteropServices;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.Mac;

public sealed class MacCpuMetrics : ICpuMetrics
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Mac.CpuMetrics");

    private const int PROCESSOR_CPU_LOAD_INFO = 2;
    private const int CPU_STATE_USER = 0;
    private const int CPU_STATE_SYSTEM = 1;
    private const int CPU_STATE_IDLE = 2;
    private const int CPU_STATE_NICE = 3;
    private const int CPU_STATE_MAX = 4;

    private long _prevIdle;
    private long _prevTotal;
    private long[]? _prevCoreIdle;
    private long[]? _prevCoreTotal;

    public string ProcessorName => field ??= ReadCpuName();
    public int CoreCount => Environment.ProcessorCount;

    public CpuMeasurement Measure()
    {
        try
        {
            var host = mach_host_self();
            if (host_processor_info(host, PROCESSOR_CPU_LOAD_INFO, out var cpuCount,
                out var cpuInfo, out var cpuInfoCount) != 0)
            {
                return new CpuMeasurement(0, []);
            }

            var coreCount = (int)cpuCount;
            _prevCoreIdle ??= new long[coreCount];
            _prevCoreTotal ??= new long[coreCount];

            long totalUser = 0, totalSystem = 0, totalIdle = 0, totalNice = 0;
            var cores = new List<double>(coreCount);

            unsafe
            {
                var info = (int*)cpuInfo;
                for (var i = 0; i < coreCount; i++)
                {
                    var offset = i * CPU_STATE_MAX;
                    long user = info[offset + CPU_STATE_USER];
                    long system = info[offset + CPU_STATE_SYSTEM];
                    long idle = info[offset + CPU_STATE_IDLE];
                    long nice = info[offset + CPU_STATE_NICE];

                    totalUser += user; totalSystem += system;
                    totalIdle += idle; totalNice += nice;

                    var coreTotal = user + system + idle + nice;
                    var coreIdleDelta = idle - _prevCoreIdle[i];
                    var coreTotalDelta = coreTotal - _prevCoreTotal[i];
                    _prevCoreIdle[i] = idle;
                    _prevCoreTotal[i] = coreTotal;

                    var pct = coreTotalDelta > 0 ? (1.0 - (double)coreIdleDelta / coreTotalDelta) * 100 : 0;
                    cores.Add(Math.Clamp(pct, 0, 100));
                }
            }

            // Deallocate the info array
            vm_deallocate(mach_task_self(), cpuInfo, (nint)(cpuInfoCount * sizeof(int)));

            var total = totalUser + totalSystem + totalIdle + totalNice;
            var idleDelta = totalIdle - _prevIdle;
            var totalDelta = total - _prevTotal;
            _prevIdle = totalIdle;
            _prevTotal = total;

            var totalPct = totalDelta > 0 ? (1.0 - (double)idleDelta / totalDelta) * 100 : 0;
            return new CpuMeasurement(Math.Clamp(totalPct, 0, 100), cores);
        }
        catch (Exception ex)
        {
            Trace.Warning(this, "CPU measurement failed: {0}", ex.Message);
            return new CpuMeasurement(0, []);
        }
    }

    private static string ReadCpuName()
    {
        try
        {
            // Use sysctl to get CPU brand string
            return RunSysctl("machdep.cpu.brand_string") ?? "CPU";
        }
        catch
        {
            return "CPU";
        }
    }

    internal static string? RunSysctl(string key)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("sysctl", $"-n {key}")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd().Trim();
            proc?.WaitForExit(3000);
            return string.IsNullOrEmpty(output) ? null : output;
        }
        catch { return null; }
    }

    [DllImport("libSystem.dylib")]
    private static extern uint mach_host_self();

    [DllImport("libSystem.dylib")]
    private static extern uint mach_task_self();

    [DllImport("libSystem.dylib")]
    private static extern int host_processor_info(uint host, int flavor,
        out uint processorCount, out nint processorInfo, out uint processorInfoCount);

    [DllImport("libSystem.dylib")]
    private static extern int vm_deallocate(uint task, nint address, nint size);
}
