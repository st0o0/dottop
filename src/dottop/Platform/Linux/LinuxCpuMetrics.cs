namespace dottop.Platform.Linux;

public sealed class LinuxCpuMetrics : ICpuMetricsProvider
{
    private readonly string _cpuName;
    private ulong[]? _previousTotal;
    private ulong[]? _previousIdle;

    public LinuxCpuMetrics()
    {
        _cpuName = ReadCpuName();
        // Prime initial reading so deltas can be computed
        ReadRawStats(out _previousTotal, out _previousIdle);
    }

    public (string Name, double TotalPercent, IReadOnlyList<double> CorePercents) GetSnapshot()
    {
        ReadRawStats(out var currentTotal, out var currentIdle);

        if (_previousTotal is null || _previousIdle is null ||
            currentTotal.Length != _previousTotal.Length)
        {
            _previousTotal = currentTotal;
            _previousIdle = currentIdle;
            return (_cpuName, 0, Array.Empty<double>());
        }

        // Index 0 is the aggregate "cpu" line, indices 1..N are per-core
        var cores = new double[currentTotal.Length - 1];
        double totalPercent = 0;

        for (var i = 0; i < currentTotal.Length; i++)
        {
            var totalDelta = currentTotal[i] - _previousTotal[i];
            var idleDelta = currentIdle[i] - _previousIdle[i];
            var percent = totalDelta > 0
                ? Math.Clamp((double)(totalDelta - idleDelta) / totalDelta * 100, 0, 100)
                : 0;

            if (i == 0)
            {
                totalPercent = percent;
            }
            else
            {
                cores[i - 1] = percent;
            }
        }

        _previousTotal = currentTotal;
        _previousIdle = currentIdle;

        return (_cpuName, totalPercent, cores);
    }

    public void Dispose()
    {
        // No unmanaged resources
    }

    /// <summary>
    /// Reads /proc/stat and returns parallel arrays of (totalTicks, idleTicks)
    /// for the aggregate cpu line (index 0) followed by each cpuN line.
    /// </summary>
    private static void ReadRawStats(out ulong[] totals, out ulong[] idles)
    {
        var totalList = new List<ulong>();
        var idleList = new List<ulong>();

        try
        {
            foreach (var line in File.ReadAllLines("/proc/stat"))
            {
                if (!line.StartsWith("cpu"))
                {
                    continue;
                }

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5)
                {
                    continue;
                }

                // cpu/cpuN user nice system idle [iowait irq softirq steal ...]
                ulong sum = 0;
                ulong idle = 0;

                for (var i = 1; i < parts.Length; i++)
                {
                    if (ulong.TryParse(parts[i], out var val))
                    {
                        sum += val;
                        if (i == 4) // idle field
                        {
                            idle = val;
                        }

                        if (i == 5) // iowait counts as idle
                        {
                            idle += val;
                        }
                    }
                }

                totalList.Add(sum);
                idleList.Add(idle);
            }
        }
        catch
        {
            // If /proc/stat is unreadable, return empty
        }

        totals = totalList.ToArray();
        idles = idleList.ToArray();
    }

    private static string ReadCpuName()
    {
        try
        {
            foreach (var line in File.ReadAllLines("/proc/cpuinfo"))
            {
                if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
                {
                    var colon = line.IndexOf(':');
                    if (colon >= 0)
                    {
                        return line[(colon + 1)..].Trim();
                    }
                }
            }
        }
        catch { }

        return "Unknown CPU";
    }
}
