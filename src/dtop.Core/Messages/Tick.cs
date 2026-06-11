namespace dtop.Core.Messages;

/// <summary>One beat of the global refresh clock. BaseInterval is the rate at emission time.</summary>
public sealed record Tick(long Seq, TimeSpan BaseInterval);
