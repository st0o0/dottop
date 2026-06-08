using dtop.Core.Models;

namespace dtop.Core.Platform;

public interface IGpuMetrics
{
    bool IsAvailable { get; }
    GpuSnapshot GetSnapshot();
}
