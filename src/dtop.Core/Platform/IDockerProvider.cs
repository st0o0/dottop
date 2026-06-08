using dtop.Core.Models;

namespace dtop.Core.Platform;

public interface IDockerProvider
{
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ContainerSnapshot>> GetContainersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetLogsAsync(string containerId, int tailLines = 20, CancellationToken ct = default);
    Task StartAsync(string containerId, CancellationToken ct = default);
    Task StopAsync(string containerId, CancellationToken ct = default);
    Task RestartAsync(string containerId, CancellationToken ct = default);
    Task<IReadOnlyList<NetworkInfo>> GetNetworksAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VolumeInfo>> GetVolumesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ImageInfo>> GetImagesAsync(CancellationToken ct = default);
    Task CreateNetworkAsync(string name, string driver = "bridge", CancellationToken ct = default);
    Task CreateVolumeAsync(string name, string driver = "local", CancellationToken ct = default);
    Task PullImageAsync(string image, CancellationToken ct = default);
    Task DeleteNetworkAsync(string id, CancellationToken ct = default);
    Task DeleteVolumeAsync(string name, CancellationToken ct = default);
    Task DeleteImageAsync(string id, CancellationToken ct = default);
}
