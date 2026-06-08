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
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var containers = await _client.Containers.ListContainersAsync(
                new ContainersListParameters { All = true }, cts.Token);

            return containers.Select(c => new ContainerSnapshot(
                Id: c.ID[..12],
                Name: c.Names.FirstOrDefault()?.TrimStart('/') ?? c.ID[..12],
                Image: c.Image,
                Status: c.State,
                State: c.Status,
                Created: new DateTimeOffset(c.Created),
                CpuPercent: 0,
                MemoryUsageBytes: 0,
                MemoryLimitBytes: 0,
                NetworkRxBytes: 0,
                NetworkTxBytes: 0,
                Ports: c.Ports?.Select(p => p.PublicPort > 0
                        ? $"{p.PublicPort}:{p.PrivatePort}"
                        : $"{p.PrivatePort}")
                    .ToList() ?? []
            )).ToList();
        }
        catch
        {
            _isAvailable = false;
            return [];
        }
    }

    public async Task<IReadOnlyList<string>> GetLogsAsync(string containerId, int tailLines = 20, CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var lines = new List<string>();
            var logParams = new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Tail = tailLines.ToString()
            };

            var progress = new Progress<string>(line => lines.Add(line));
            await _client.Containers.GetContainerLogsAsync(containerId, logParams, progress, cts.Token);

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
