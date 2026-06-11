using dtop.Core.Models;
using dtop.Nodes;
using R3;

namespace dtop.Services;

public sealed class MetricStore : IMetricSink
{
    private readonly int _keyedHistoryLimit;
    private readonly Dictionary<string, MetricHistory> _keyed = new();
    private readonly LinkedList<string> _lru = new();
    private readonly Lock _gate = new();

    public ReactiveProperty<CpuSnapshot?> Cpu { get; } = new(null);
    public ReactiveProperty<MemorySnapshot?> Memory { get; } = new(null);
    public ReactiveProperty<GpuSnapshot?> Gpu { get; } = new(null);
    public ReactiveProperty<IReadOnlyList<DiskSnapshot>> Disks { get; } = new([]);
    public ReactiveProperty<IReadOnlyList<NetworkSnapshot>> Networks { get; } = new([]);
    public ReactiveProperty<IReadOnlyList<ProcessSnapshot>> Processes { get; } = new([]);
    public ReactiveProperty<IReadOnlyList<ConnectionSnapshot>> Connections { get; } = new([]);
    public ReactiveProperty<List<ContainerSnapshot>?> Docker { get; } = new(null);

    public MetricHistory CpuHistory { get; } = new();
    public MetricHistory MemHistory { get; } = new();
    public MetricHistory GpuHistory { get; } = new();

    public MetricStore(int keyedHistoryLimit = 256)
    {
        _keyedHistoryLimit = keyedHistoryLimit;
    }

    public void Publish(CpuSnapshot snapshot)
    {
        CpuHistory.Push(snapshot.TotalPercent);
        Cpu.Value = snapshot;
    }

    public void Publish(MemorySnapshot snapshot)
    {
        MemHistory.Push(snapshot.TotalBytes > 0 ? (double)snapshot.UsedBytes / snapshot.TotalBytes * 100 : 0);
        Memory.Value = snapshot;
    }

    public void Publish(GpuSnapshot snapshot)
    {
        GpuHistory.Push(snapshot.UsagePercent);
        Gpu.Value = snapshot;
    }

    public void Publish(List<DiskSnapshot> snapshots)
    {
        foreach (var d in snapshots)
        {
            History($"disk:{d.Name}:active").Push(d.ActiveTimePercent);
            History($"disk:{d.Name}:transfer").Push(d.TransferBytesPerSec);
        }

        Disks.Value = snapshots;
    }

    public void Publish(List<NetworkSnapshot> snapshots) => Networks.Value = snapshots;

    public void Publish(List<ProcessSnapshot> snapshots)
    {
        foreach (var p in snapshots)
        {
            History($"pid:{p.Pid}").Push(p.CpuPercent);
        }

        Processes.Value = snapshots;
    }

    public void Publish(List<ConnectionSnapshot> snapshots) => Connections.Value = snapshots;

    public void Publish(List<ContainerSnapshot> snapshots)
    {
        foreach (var c in snapshots)
        {
            History($"docker:{c.Id}:cpu").Push(c.CpuPercent);
        }

        Docker.Value = snapshots;
    }

    public MetricHistory History(string key)
    {
        lock (_gate)
        {
            if (_keyed.TryGetValue(key, out var existing))
            {
                _lru.Remove(key);
                _lru.AddLast(key);
                return existing;
            }

            if (_keyed.Count >= _keyedHistoryLimit && _lru.First is { } oldest)
            {
                _keyed.Remove(oldest.Value);
                _lru.RemoveFirst();
            }

            var history = new MetricHistory();
            _keyed[key] = history;
            _lru.AddLast(key);
            return history;
        }
    }
}
