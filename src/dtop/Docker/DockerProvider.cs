using Docker.DotNet;
using Docker.DotNet.Models;
using dtop.Core.Models;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;
using CoreVolumeInfo = dtop.Core.Models.VolumeInfo;

namespace dtop.Docker;

public sealed class DockerProvider : IDockerProvider
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Docker.Provider");
    private readonly DockerClient _client;
    private bool? _isAvailable;

    public DockerProvider()
    {
        _client = new DockerClientBuilder()
            .WithTimeout(TimeSpan.FromSeconds(5))
            .Build();
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (_isAvailable.HasValue)
        {
            return _isAvailable.Value;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            await _client.System.PingAsync(cts.Token);
            _isAvailable = true;
        }
        catch (Exception ex)
        {
            Trace.Warning("DockerProvider", "Docker ping failed: {0}", ex.Message);
            _isAvailable = false;
        }

        return _isAvailable.Value;
    }

    public async Task<IReadOnlyList<ContainerSnapshot>> GetContainersAsync(CancellationToken ct = default)
    {
        try
        {
            var containers = await _client.Containers.ListContainersAsync(
                new ContainersListParameters { All = true }, ct);

            var running = containers.Where(c => c.State == "running").ToList();

            // Fetch stats for all running containers in parallel
            var statsTasks = running.ToDictionary(
                c => c.ID,
                c => FetchStatsAsync(c.ID, ct));
            try { await Task.WhenAll(statsTasks.Values).WaitAsync(TimeSpan.FromSeconds(3), ct); }
            catch (Exception ex) { Trace.Warning("DockerProvider", "Timeout fetching container stats: {0}", ex.Message); }

            return containers.Select(c =>
            {
                double cpu = 0;
                ulong memUsage = 0, memLimit = 0, netRx = 0, netTx = 0;

                if (statsTasks.TryGetValue(c.ID, out var task) && task.IsCompletedSuccessfully)
                {
                    (cpu, memUsage, memLimit, netRx, netTx) = task.Result;
                }

                return new ContainerSnapshot(
                    Id: c.ID[..12],
                    Name: c.Names.FirstOrDefault()?.TrimStart('/') ?? c.ID[..12],
                    Image: c.Image,
                    Status: c.State,
                    State: c.Status,
                    Created: new DateTimeOffset(c.Created),
                    CpuPercent: Math.Round(cpu, 1),
                    MemoryUsageBytes: memUsage,
                    MemoryLimitBytes: memLimit,
                    NetworkRxBytes: netRx,
                    NetworkTxBytes: netTx,
                    Ports: c.Ports?.Select(p => p.PublicPort > 0
                            ? $"{p.PublicPort}:{p.PrivatePort}"
                            : $"-:{p.PrivatePort}")
                        .ToList() ?? [],
                    ComposeProject: c.Labels?.TryGetValue("com.docker.compose.project", out var project) == true ? project : null
                );
            }).ToList();
        }
        catch (Exception ex)
        {
            Trace.Warning("DockerProvider", "Failed to list containers: {0}", ex.Message);
            _isAvailable = false;
            return [];
        }
    }

    private async Task<(double Cpu, ulong MemUsage, ulong MemLimit, ulong NetRx, ulong NetTx)> FetchStatsAsync(
        string id, CancellationToken ct)
    {
        try
        {
            ContainerStatsResponse? stats = null;
            var tcs = new TaskCompletionSource<bool>();

            var progress = new Progress<ContainerStatsResponse>(s =>
            {
                stats = s;
                tcs.TrySetResult(true);
            });

            _ = _client.Containers.GetContainerStatsAsync(id,
                new ContainerStatsParameters { Stream = false }, progress, ct)
                .ContinueWith(_ => tcs.TrySetResult(true), TaskScheduler.Default);

            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2), ct);

            if (stats is null)
            {
                return default;
            }

            var cpu = CalculateCpuPercent(stats);
            var memUsage = stats.MemoryStats.Usage ?? 0;
            var memLimit = stats.MemoryStats.Limit ?? 0;
            ulong netRx = 0, netTx = 0;
            if (stats.Networks is not null)
            {
                foreach (var net in stats.Networks.Values)
                {
                    netRx += net.RxBytes;
                    netTx += net.TxBytes;
                }
            }
            return (cpu, memUsage, memLimit, netRx, netTx);
        }
        catch (Exception ex)
        {
            Trace.Warning("DockerProvider", "Failed to fetch stats for container {0}: {1}", id, ex.Message);
            return default;
        }
    }

    private static double CalculateCpuPercent(ContainerStatsResponse stats)
    {
        var cpuUsage = stats.CPUStats.CPUUsage.TotalUsage;
        var preCpuUsage = stats.PreCPUStats.CPUUsage.TotalUsage;
        var systemUsage = stats.CPUStats.SystemUsage;
        var preSystemUsage = stats.PreCPUStats.SystemUsage;

        var cpuDelta = (double)(cpuUsage - preCpuUsage);
        var systemDelta = (double)(systemUsage - preSystemUsage);
        if (systemDelta <= 0 || cpuDelta <= 0)
        {
            return 0;
        }

        var cpuCount = (double)(stats.CPUStats.OnlineCPUs ?? 0);
        if (cpuCount == 0)
        {
            cpuCount = stats.CPUStats.CPUUsage.PercpuUsage?.Count ?? 1;
        }

        return cpuDelta / systemDelta * cpuCount * 100.0;
    }

    public async Task<IReadOnlyList<string>> GetLogsAsync(string containerId, int tailLines = 20, CancellationToken ct = default)
    {
        try
        {
            var lines = new List<string>();
            var logParams = new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Tail = tailLines.ToString()
            };

            var progress = new Progress<string>(line => lines.Add(line));
            await _client.Containers.GetContainerLogsAsync(containerId, logParams, progress, ct)
                .WaitAsync(TimeSpan.FromSeconds(3), ct);

            return lines.TakeLast(tailLines).ToList();
        }
        catch (Exception ex)
        {
            Trace.Warning("DockerProvider", "Failed to read logs for container {0}: {1}", containerId, ex.Message);
            return [$"Error reading logs: {ex.Message}"];
        }
    }

    public async Task StartAsync(string containerId, CancellationToken ct = default)
    {
        await _client.Containers.StartContainerAsync(containerId, new ContainerStartParameters(), ct);
    }

    public async Task StopAsync(string containerId, CancellationToken ct = default)
    {
        await _client.Containers.StopContainerAsync(containerId, new ContainerStopParameters { WaitBeforeKillSeconds = 10 }, ct);
    }

    public async Task RestartAsync(string containerId, CancellationToken ct = default)
    {
        await _client.Containers.RestartContainerAsync(containerId, new ContainerRestartParameters { WaitBeforeKillSeconds = 10 }, ct);
    }

    public async Task<IReadOnlyList<NetworkInfo>> GetNetworksAsync(CancellationToken ct = default)
    {
        try
        {
            var networks = await _client.Networks.ListNetworksAsync(new NetworksListParameters(), ct);
            return networks.Select(n =>
            {
                var subnet = n.IPAM?.Config?.FirstOrDefault()?.Subnet ?? "";
                var containers = n.Containers?.Select(kvp =>
                    new NetworkContainer(kvp.Key[..Math.Min(12, kvp.Key.Length)], kvp.Value.Name, kvp.Value.IPv4Address))
                    .ToList() as IReadOnlyList<NetworkContainer> ?? [];
                return new NetworkInfo(
                    Id: n.ID,
                    Name: n.Name,
                    Driver: n.Driver,
                    Scope: n.Scope,
                    Internal: n.Internal,
                    IPv6: n.EnableIPv6,
                    Subnet: subnet,
                    Containers: containers);
            }).ToList();
        }
        catch (Exception ex)
        {
            Trace.Warning("DockerProvider", "Failed to list networks: {0}", ex.Message);
            return [];
        }
    }

    public async Task<IReadOnlyList<CoreVolumeInfo>> GetVolumesAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _client.Volumes.ListAsync(ct);
            return (response.Volumes ?? []).Select(v => new CoreVolumeInfo(
                Name: v.Name,
                Driver: v.Driver,
                Mountpoint: v.Mountpoint,
                Created: DateTimeOffset.TryParse(v.CreatedAt, out var created) ? created : DateTimeOffset.MinValue,
                SizeBytes: v.UsageData?.Size ?? 0,
                MountCount: (int)(v.UsageData?.RefCount ?? 0),
                Labels: v.Labels as IReadOnlyDictionary<string, string>
                    ?? (v.Labels?.ToDictionary(kv => kv.Key, kv => kv.Value)
                        as IReadOnlyDictionary<string, string> ?? new Dictionary<string, string>())
            )).ToList();
        }
        catch (Exception ex)
        {
            Trace.Warning("DockerProvider", "Failed to list volumes: {0}", ex.Message);
            return [];
        }
    }

    public async Task<IReadOnlyList<ImageInfo>> GetImagesAsync(CancellationToken ct = default)
    {
        try
        {
            var images = await _client.Images.ListImagesAsync(new ImagesListParameters { All = false }, ct);
            return images.Select(img =>
            {
                var repoTag = img.RepoTags?.FirstOrDefault() ?? "<none>:<none>";
                var parts = repoTag.Split(':', 2);
                return new ImageInfo(
                    Id: img.ID.Replace("sha256:", "")[..12],
                    Repository: parts[0],
                    Tag: parts.Length > 1 ? parts[1] : "<none>",
                    SizeBytes: img.Size,
                    Created: new DateTimeOffset(img.Created),
                    OsArch: "",
                    ContainerCount: (int)img.Containers);
            }).ToList();
        }
        catch (Exception ex)
        {
            Trace.Warning("DockerProvider", "Failed to list images: {0}", ex.Message);
            return [];
        }
    }

    public async Task CreateNetworkAsync(string name, string driver = "bridge", CancellationToken ct = default)
    {
        await _client.Networks.CreateNetworkAsync(new NetworksCreateParameters { Name = name, Driver = driver }, ct);
    }

    public async Task CreateVolumeAsync(string name, string driver = "local", CancellationToken ct = default)
    {
        await _client.Volumes.CreateAsync(new VolumesCreateParameters { Name = name, Driver = driver }, ct);
    }

    public async Task PullImageAsync(string image, CancellationToken ct = default)
    {
        var parts = image.Split(':', 2);
        var repo = parts[0];
        var tag = parts.Length > 1 ? parts[1] : "latest";
        await _client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = repo, Tag = tag },
            null, new Progress<JSONMessage>(), ct);
    }

    public async Task DeleteNetworkAsync(string id, CancellationToken ct = default)
    {
        await _client.Networks.DeleteNetworkAsync(id, ct);
    }

    public async Task DeleteVolumeAsync(string name, CancellationToken ct = default)
    {
        await _client.Volumes.RemoveAsync(name, null, ct);
    }

    public async Task DeleteImageAsync(string id, CancellationToken ct = default)
    {
        await _client.Images.DeleteImageAsync(id, new ImageDeleteParameters(), ct);
    }
}
