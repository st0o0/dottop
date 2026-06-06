using System.Diagnostics;
using System.Runtime.Versioning;

namespace dottop.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsDiskMetrics : IDiskMetricsProvider
{
    private volatile Dictionary<string, DiskPerfCounters>? _counters;
    private volatile bool _ready;

    public void Initialize()
    {
        var counters = InitPerfCounters();

        foreach (var c in counters.Values)
        {
            try
            {
                c.Read.NextValue();
                c.Write.NextValue();
                c.Active.NextValue();
            }
            catch
            {
            }
        }

        _counters = counters;
        _ready = true;
    }

    public (ulong ReadBytesPerSec, ulong WriteBytesPerSec, double ActivePercent) GetMetrics(string diskName)
    {
        if (!_ready || _counters is null || !_counters.TryGetValue(diskName, out var c))
            return (0, 0, 0);

        try
        {
            var read = (ulong)Math.Max(0, c.Read.NextValue());
            var write = (ulong)Math.Max(0, c.Write.NextValue());
            var active = Math.Clamp(c.Active.NextValue(), 0, 100);
            return (read, write, active);
        }
        catch
        {
            return (0, 0, 0);
        }
    }

    public void Dispose()
    {
        if (_counters is not null)
            foreach (var c in _counters.Values)
                c.Dispose();
    }

    private static Dictionary<string, DiskPerfCounters> InitPerfCounters()
    {
        var counters = new Dictionary<string, DiskPerfCounters>();
        try
        {
            var category = new PerformanceCounterCategory("LogicalDisk");
            foreach (var instance in category.GetInstanceNames())
            {
                if (instance == "_Total" || instance.Length < 2 || instance[1] != ':')
                    continue;
                try
                {
                    counters[instance[..2]] = new DiskPerfCounters(
                        new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", instance, true),
                        new PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", instance, true),
                        new PerformanceCounter("LogicalDisk", "% Disk Time", instance, true));
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return counters;
    }

    private sealed record DiskPerfCounters(
        PerformanceCounter Read,
        PerformanceCounter Write,
        PerformanceCounter Active) : IDisposable
    {
        public void Dispose()
        {
            Read.Dispose();
            Write.Dispose();
            Active.Dispose();
        }
    }
}