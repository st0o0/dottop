using Akka.Actor;
using Akka.Hosting;
using R3;
using dottop.Actors;
using dottop.Models;
using Termina.Input;
using Termina.Reactive;

namespace dottop.Pages;

public enum PerfDetailSection { Cpu, Ram, Disk, Network }

public class PerformanceViewModel : ReactiveViewModel
{
    private readonly IRequiredActor<CpuMonitorActor> _cpuRef;
    private readonly IRequiredActor<MemoryMonitorActor> _memRef;
    private readonly IRequiredActor<DiskMonitorActor> _diskRef;
    private readonly IRequiredActor<NetworkMonitorActor> _netRef;
    private CancellationTokenSource? _cts;

    public ReactiveProperty<double> CpuTotal { get; } = new(0);
    public ReactiveProperty<IReadOnlyList<double>> CpuCores { get; } = new([]);
    public ReactiveProperty<string> CpuName { get; } = new("Loading...");
    public ReactiveProperty<ulong> RamTotal { get; } = new(0);
    public ReactiveProperty<ulong> RamUsed { get; } = new(0);
    public ReactiveProperty<IReadOnlyList<DiskSnapshot>> Disks { get; } = new([]);
    public ReactiveProperty<IReadOnlyList<NetworkSnapshot>> Networks { get; } = new([]);

    public ReactiveProperty<bool> IsDetailOpen { get; } = new(false);
    public ReactiveProperty<PerfDetailSection> DetailSection { get; } = new(PerfDetailSection.Cpu);
    public ReactiveProperty<int> DiskDetailIndex { get; } = new(0);

    private readonly Subject<Unit> _detailContentChanged = new();
    public Observable<Unit> DetailContentChanged => _detailContentChanged.AsObservable();

    public PerformanceViewModel(
        IRequiredActor<CpuMonitorActor> cpuRef,
        IRequiredActor<MemoryMonitorActor> memRef,
        IRequiredActor<DiskMonitorActor> diskRef,
        IRequiredActor<NetworkMonitorActor> netRef)
    {
        _cpuRef = cpuRef;
        _memRef = memRef;
        _diskRef = diskRef;
        _netRef = netRef;
    }

    public override void OnActivated()
    {
        _cts = new CancellationTokenSource();
        _ = InitializeAsync();

        Input.OfType<IInputEvent, KeyPressed>()
            .Subscribe(HandleKey)
            .DisposeWith(Subscriptions);
    }

    private async Task InitializeAsync()
    {
        var ct = _cts!.Token;

        var cpuActor = await _cpuRef.GetAsync(ct);
        var memActor = await _memRef.GetAsync(ct);
        var diskActor = await _diskRef.GetAsync(ct);
        var netActor = await _netRef.GetAsync(ct);

        var cpuStream = await cpuActor.Ask<MonitoringStream<CpuSnapshot>>(new StartMonitoring(), TimeSpan.FromSeconds(5));
        var memStream = await memActor.Ask<MonitoringStream<MemorySnapshot>>(new StartMonitoring(), TimeSpan.FromSeconds(5));
        var diskStream = await diskActor.Ask<MonitoringStream<List<DiskSnapshot>>>(new StartMonitoring(), TimeSpan.FromSeconds(5));
        var netStream = await netActor.Ask<MonitoringStream<List<NetworkSnapshot>>>(new StartMonitoring(), TimeSpan.FromSeconds(5));

        _ = ConsumeAsync(cpuStream.Data, ct, snapshot =>
        {
            CpuName.Value = snapshot.Name;
            CpuTotal.Value = snapshot.TotalPercent;
            CpuCores.Value = snapshot.CorePercents;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Cpu)
                _detailContentChanged.OnNext(Unit.Default);
        });

        _ = ConsumeAsync(memStream.Data, ct, snapshot =>
        {
            RamTotal.Value = snapshot.TotalBytes;
            RamUsed.Value = snapshot.UsedBytes;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Ram)
                _detailContentChanged.OnNext(Unit.Default);
        });

        _ = ConsumeAsync(diskStream.Data, ct, disks =>
        {
            Disks.Value = disks;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Disk)
                _detailContentChanged.OnNext(Unit.Default);
        });

        _ = ConsumeAsync(netStream.Data, ct, nets =>
        {
            Networks.Value = nets;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Network)
                _detailContentChanged.OnNext(Unit.Default);
        });
    }

    private static async Task ConsumeAsync<T>(IAsyncEnumerable<T> stream, CancellationToken ct, Action<T> handler)
    {
        try
        {
            await foreach (var item in stream.WithCancellation(ct))
                handler(item);
        }
        catch (OperationCanceledException) { }
    }

    private void HandleKey(KeyPressed key)
    {
        if (IsDetailOpen.Value)
        {
            HandleDetailKey(key);
            return;
        }

        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.Enter or ConsoleKey.Tab:
                DetailSection.Value = PerfDetailSection.Cpu;
                IsDetailOpen.Value = true;
                _detailContentChanged.OnNext(Unit.Default);
                break;
            case ConsoleKey.D1: Navigate("/"); break;
            case ConsoleKey.D3: Navigate("/services"); break;
            case ConsoleKey.D4: Navigate("/network"); break;

            case ConsoleKey.Q or ConsoleKey.Escape: Shutdown(); break;
        }
    }

    private void HandleDetailKey(KeyPressed key)
    {
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.Escape:
                IsDetailOpen.Value = false;
                break;
            case ConsoleKey.Tab or ConsoleKey.RightArrow:
                DetailSection.Value = DetailSection.Value switch
                {
                    PerfDetailSection.Cpu => PerfDetailSection.Ram,
                    PerfDetailSection.Ram => PerfDetailSection.Disk,
                    PerfDetailSection.Disk => PerfDetailSection.Network,
                    _ => PerfDetailSection.Cpu,
                };
                DiskDetailIndex.Value = 0;
                _detailContentChanged.OnNext(Unit.Default);
                break;
            case ConsoleKey.LeftArrow:
                DetailSection.Value = DetailSection.Value switch
                {
                    PerfDetailSection.Ram => PerfDetailSection.Cpu,
                    PerfDetailSection.Disk => PerfDetailSection.Ram,
                    PerfDetailSection.Network => PerfDetailSection.Disk,
                    _ => PerfDetailSection.Network,
                };
                DiskDetailIndex.Value = 0;
                _detailContentChanged.OnNext(Unit.Default);
                break;
            case ConsoleKey.UpArrow:
                if (DetailSection.Value == PerfDetailSection.Disk && DiskDetailIndex.Value > 0)
                {
                    DiskDetailIndex.Value--;
                    _detailContentChanged.OnNext(Unit.Default);
                }
                break;
            case ConsoleKey.DownArrow:
                if (DetailSection.Value == PerfDetailSection.Disk &&
                    DiskDetailIndex.Value < Disks.Value.Count - 1)
                {
                    DiskDetailIndex.Value++;
                    _detailContentChanged.OnNext(Unit.Default);
                }
                break;
        }
    }

    public override void OnDeactivating()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        base.OnDeactivating();
    }

    public override void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        CpuTotal.Dispose();
        CpuCores.Dispose();
        CpuName.Dispose();
        RamTotal.Dispose();
        RamUsed.Dispose();
        Disks.Dispose();
        Networks.Dispose();
        IsDetailOpen.Dispose();
        DetailSection.Dispose();
        DiskDetailIndex.Dispose();
        _detailContentChanged.Dispose();
        base.Dispose();
    }
}
