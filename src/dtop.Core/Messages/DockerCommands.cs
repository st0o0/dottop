using dtop.Core.Models;

namespace dtop.Core.Messages;

public sealed record StartDockerMonitoring;
public sealed record StartContainer(string Id);
public sealed record StopContainer(string Id);
public sealed record RestartContainer(string Id);
public sealed record GetContainerLogs(string Id, int TailLines = 20);
public sealed record ContainerLogsResult(IReadOnlyList<string> Lines);

public sealed record GetNetworks;
public sealed record GetVolumes;
public sealed record GetImages;
public sealed record NetworksResult(IReadOnlyList<NetworkInfo> Networks);
public sealed record VolumesResult(IReadOnlyList<VolumeInfo> Volumes);
public sealed record ImagesResult(IReadOnlyList<ImageInfo> Images);
public sealed record CreateNetwork(string Name, string Driver = "bridge");
public sealed record CreateVolume(string Name, string Driver = "local");
public sealed record PullImage(string Image);
public sealed record DeleteNetwork(string Id);
public sealed record DeleteVolume(string Name);
public sealed record DeleteImage(string Id);
public sealed record PruneVolumes;
public sealed record PruneImages;
