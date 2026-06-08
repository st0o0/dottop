namespace dtop.Linux;

using dtop.Core.Platform;

public sealed class LinuxCpuMetrics : ICpuMetrics
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
            var lines = File.ReadAllLines("/proc/stat");
            var totalPct = ParseProcStatLine(lines[0], ref _prevIdle, ref _prevTotal);

            var coreCount = Environment.ProcessorCount;
            _prevCoreIdle ??= new long[coreCount];
            _prevCoreTotal ??= new long[coreCount];

            var cores = new List<double>(coreCount);
            for (var i = 0; i < coreCount; i++)
            {
                var lineIdx = i + 1;
                if (lineIdx < lines.Length && lines[lineIdx].StartsWith("cpu"))
                {
                    cores.Add(ParseProcStatLine(lines[lineIdx], ref _prevCoreIdle[i], ref _prevCoreTotal[i]));
                }
                else
                {
                    cores.Add(totalPct);
                }
            }

            return new CpuMeasurement(totalPct, cores);
        }
        catch
        {
            return new CpuMeasurement(0, []);
        }
    }

    private static double ParseProcStatLine(string line, ref long prevIdle, ref long prevTotal)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
        {
            return 0;
        }

        long user = long.Parse(parts[1]), nice = long.Parse(parts[2]);
        long system = long.Parse(parts[3]), idle = long.Parse(parts[4]);
        var total = user + nice + system + idle;
        for (var j = 5; j < Math.Min(parts.Length, 8); j++)
            if (long.TryParse(parts[j], out var v))
            {
                total += v;
            }

        var idleDelta = idle - prevIdle;
        var totalDelta = total - prevTotal;
        prevIdle = idle;
        prevTotal = total;

        var pct = totalDelta > 0 ? (1.0 - (double)idleDelta / totalDelta) * 100 : 0;
        return Math.Clamp(pct, 0, 100);
    }

    private static string ReadCpuName()
    {
        try
        {
            var cpuinfo = File.ReadAllLines("/proc/cpuinfo");
            var nameLine = cpuinfo.FirstOrDefault(l => l.StartsWith("model name"));
            return nameLine?.Split(':').LastOrDefault()?.Trim() ?? "CPU";
        }
        catch
        {
            return "CPU";
        }
    }
}
