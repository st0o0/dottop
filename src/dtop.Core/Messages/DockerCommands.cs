namespace dtop.Core.Messages;

public sealed record StartDockerMonitoring;
public sealed record StartContainer(string Id);
public sealed record StopContainer(string Id);
public sealed record RestartContainer(string Id);
public sealed record GetContainerLogs(string Id, int TailLines = 20);
public sealed record ContainerLogsResult(IReadOnlyList<string> Lines);
