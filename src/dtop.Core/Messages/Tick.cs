namespace dtop.Core.Messages;

/// <summary>One beat of the global refresh clock. BaseInterval is the rate at emission time.</summary>
public sealed record Tick(long Seq, TimeSpan BaseInterval);

/// <summary>Identifies a monitor for demand tracking.</summary>
public enum MetricKind
{
    Cpu,
    Memory,
    Gpu,
    Disk,
    Network,
    Process,
    Docker,
    NetworkConnections,
}
