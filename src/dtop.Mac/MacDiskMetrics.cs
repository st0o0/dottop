using dtop.Core.Platform;

namespace dtop.Mac;

public sealed class MacDiskMetrics : IDiskMetrics
{
    public void Initialize() { }

    public (ulong ReadBytesPerSec, ulong WriteBytesPerSec, double ActivePercent) GetMetrics(string diskName)
        => (0, 0, 0);

    public void Dispose() { }
}
