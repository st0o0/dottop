namespace dtop.Linux;

using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

public sealed class LinuxMemoryMetrics : IMemoryMetrics
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Linux.MemoryMetrics");
    public (ulong TotalBytes, ulong UsedBytes) Measure()
    {
        try
        {
            ulong total = 0, available = 0;
            foreach (var line in File.ReadAllLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:"))
                {
                    total = ParseKb(line) * 1024;
                }
                else if (line.StartsWith("MemAvailable:"))
                {
                    available = ParseKb(line) * 1024;
                }
            }

            if (total > 0)
            {
                return (total, total - available);
            }
        }
        catch (Exception ex) { Trace.Warning("LinuxMemoryMetrics", "Failed to read /proc/meminfo: {0}", ex.Message); }

        return (0, 0);
    }

    private static ulong ParseKb(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && ulong.TryParse(parts[1], out var val) ? val : 0;
    }
}
