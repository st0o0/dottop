namespace dottop.Core.Models;

public record ContainerSnapshot(
    string Id,
    string Name,
    string Image,
    string Status,
    string State,
    DateTimeOffset Created,
    double CpuPercent,
    ulong MemoryUsageBytes,
    ulong MemoryLimitBytes,
    ulong NetworkRxBytes,
    ulong NetworkTxBytes,
    IReadOnlyList<string> Ports);
