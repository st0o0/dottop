using dottop.Core.Models;

namespace dottop.Core.Platform;

public interface IGpuMetrics
{
    bool IsAvailable { get; }
    GpuSnapshot GetSnapshot();
}
