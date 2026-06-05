using dottop.Models;

namespace dottop.Platform;

public interface IGpuMetricsProvider
{
    bool IsAvailable { get; }
    GpuSnapshot GetSnapshot();
}
