namespace dtop.Core.Platform;

public interface IMemoryMetrics
{
    (ulong TotalBytes, ulong UsedBytes) Measure();
}
