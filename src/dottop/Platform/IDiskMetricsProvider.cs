namespace dottop.Platform;

public interface IDiskMetricsProvider : IDisposable
{
    void Initialize();
    (ulong ReadBytesPerSec, ulong WriteBytesPerSec, double ActivePercent) GetMetrics(string diskName);
}
