using dottop.Models;

namespace dottop.Platform;

public sealed class NoGpuMetrics : IGpuMetricsProvider
{
    public bool IsAvailable => false;

    public GpuSnapshot GetSnapshot() =>
        new("N/A", 0, 0, 0, 0);
}
