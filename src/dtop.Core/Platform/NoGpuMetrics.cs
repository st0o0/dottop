using dtop.Core.Models;

namespace dtop.Core.Platform;

public sealed class NoGpuMetrics : IGpuMetrics
{
    public static readonly NoGpuMetrics Instance = new();
    public bool IsAvailable => false;
    public GpuSnapshot GetSnapshot() => new("N/A", 0, 0, 0, 0);
}
