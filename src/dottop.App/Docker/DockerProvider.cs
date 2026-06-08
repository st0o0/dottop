using Docker.DotNet;
using Docker.DotNet.Models;
using dottop.Core.Models;
using dottop.Core.Platform;

namespace dottop.App.Docker;

public sealed class DockerProvider : IDockerProvider
{
    private readonly DockerClient _client;
    private bool? _isAvailable;

    public DockerProvider()
    {
        _client = new DockerClientBuilder()
            .WithTimeout(TimeSpan.FromSeconds(5))
            .Build();
    }

    public bool IsAvailable
    {
        get
        {
            if (_isAvailable.HasValue) return _isAvailable.Value;
            try
            {
                _client.System.PingAsync().GetAwaiter().GetResult();
                _isAvailable = true;
            }
            catch
            {
                _isAvailable = false;
            }
            return _isAvailable.Value;
        }
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
            catch { }

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
        catch
        {
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

            if (stats is null) return default;

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
        catch
        {
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
        if (systemDelta <= 0 || cpuDelta <= 0) return 0;

        var cpuCount = (double)(stats.CPUStats.OnlineCPUs ?? 0);
        if (cpuCount == 0) cpuCount = stats.CPUStats.CPUUsage.PercpuUsage?.Count ?? 1;

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
        catch
        {
            return ["Error reading logs"];
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
}
