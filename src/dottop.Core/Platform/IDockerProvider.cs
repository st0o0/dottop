using dottop.Core.Models;

namespace dottop.Core.Platform;

public interface IDockerProvider
{
    bool IsAvailable { get; }
    Task<IReadOnlyList<ContainerSnapshot>> GetContainersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetLogsAsync(string containerId, int tailLines = 20, CancellationToken ct = default);
    Task StartAsync(string containerId, CancellationToken ct = default);
    Task StopAsync(string containerId, CancellationToken ct = default);
    Task RestartAsync(string containerId, CancellationToken ct = default);
}
