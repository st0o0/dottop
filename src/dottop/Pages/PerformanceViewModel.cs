using Akka.Actor;
using Akka.Hosting;
using R3;
using dottop.Actors;
using dottop.Models;
using dottop.Platform;
using dottop.Services;
using Termina.Input;
using Termina.Reactive;
using Termina.Rendering;

namespace dottop.Pages;

public enum PerfDetailSection { Cpu, Ram, Disk, Network, Gpu }

public class PerformanceViewModel : ReactiveViewModel
{
    private readonly IRequiredActor<CpuMonitorActor> _cpuRef;
    private readonly IRequiredActor<MemoryMonitorActor> _memRef;
    private readonly IRequiredActor<DiskMonitorActor> _diskRef;
    private readonly IRequiredActor<NetworkMonitorActor> _netRef;
    private readonly IRequiredActor<GpuMonitorActor> _gpuRef;
    private readonly IGpuMetricsProvider _gpuMetrics;
    private CancellationTokenSource? _cts;

    public GraphStyle GraphStyleSetting { get; }

    public ReactiveProperty<double> CpuTotal { get; } = new(0);
    public ReactiveProperty<IReadOnlyList<double>> CpuCores { get; } = new([]);
    public ReactiveProperty<string> CpuName { get; } = new("Loading...");
    public ReactiveProperty<ulong> RamTotal { get; } = new(0);
    public ReactiveProperty<ulong> RamUsed { get; } = new(0);
    public ReactiveProperty<IReadOnlyList<DiskSnapshot>> Disks { get; } = new([]);
    public ReactiveProperty<IReadOnlyList<NetworkSnapshot>> Networks { get; } = new([]);
    public ReactiveProperty<GpuSnapshot?> Gpu { get; } = new(null);
    public bool GpuAvailable => _gpuMetrics.IsAvailable;

    public ReactiveProperty<bool> IsDetailOpen { get; } = new(false);
    public ReactiveProperty<PerfDetailSection> DetailSection { get; } = new(PerfDetailSection.Cpu);
    public ReactiveProperty<int> DiskDetailIndex { get; } = new(0);

    private readonly Subject<Unit> _detailContentChanged = new();
    public Observable<Unit> DetailContentChanged => _detailContentChanged.AsObservable();

    public PerformanceViewModel(
        IRequiredActor<CpuMonitorActor> cpuRef,
        IRequiredActor<MemoryMonitorActor> memRef,
        IRequiredActor<DiskMonitorActor> diskRef,
        IRequiredActor<NetworkMonitorActor> netRef,
        IRequiredActor<GpuMonitorActor> gpuRef,
        IGpuMetricsProvider gpuMetrics,
        SettingsService settingsService)
    {
        _cpuRef = cpuRef;
        _memRef = memRef;
        _diskRef = diskRef;
        _netRef = netRef;
        _gpuRef = gpuRef;
        _gpuMetrics = gpuMetrics;

        GraphStyleSetting = settingsService.Settings.GraphStyle switch
        {
            "braille" => GraphStyle.Braille,
            "outline" => GraphStyle.Outline,
            "ascii" => GraphStyle.Ascii,
            _ => GraphStyle.Blocks,
        };
    }

    public override void OnActivated()
    {
        _cts = new CancellationTokenSource();
        _ = InitializeAsync();

        Input.OfType<IInputEvent, KeyPressed>()
            .Subscribe(HandleKey)
            .DisposeWith(Subscriptions);
    }

    private Task InitializeAsync()
    {
        var ct = _cts!.Token;

        _ = ConnectStream(_cpuRef, ct, (CpuSnapshot snapshot) =>
        {
            CpuName.Value = snapshot.Name;
            CpuTotal.Value = snapshot.TotalPercent;
            CpuCores.Value = snapshot.CorePercents;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Cpu)
                _detailContentChanged.OnNext(Unit.Default);
        });

        _ = ConnectStream(_memRef, ct, (MemorySnapshot snapshot) =>
        {
            RamTotal.Value = snapshot.TotalBytes;
            RamUsed.Value = snapshot.UsedBytes;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Ram)
                _detailContentChanged.OnNext(Unit.Default);
        });

        _ = ConnectStream(_diskRef, ct, (List<DiskSnapshot> disks) =>
        {
            Disks.Value = disks;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Disk)
                _detailContentChanged.OnNext(Unit.Default);
        });

        _ = ConnectStream(_netRef, ct, (List<NetworkSnapshot> nets) =>
        {
            Networks.Value = nets;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Network)
                _detailContentChanged.OnNext(Unit.Default);
        });

        if (_gpuMetrics.IsAvailable)
        {
            _ = ConnectStream(_gpuRef, ct, (GpuSnapshot snapshot) =>
            {
                Gpu.Value = snapshot;
                if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Gpu)
                    _detailContentChanged.OnNext(Unit.Default);
            });
        }

        return Task.CompletedTask;
    }

    private async Task ConnectStream<TActor, TData>(
        IRequiredActor<TActor> actorRef, CancellationToken ct, Action<TData> handler)
        where TActor : ActorBase
    {
        for (var attempt = 0; attempt < 3 && !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                var actor = await actorRef.GetAsync(ct);
                var stream = await actor.Ask<MonitoringStream<TData>>(new StartMonitoring(), TimeSpan.FromSeconds(30));
                await foreach (var item in stream.Data.WithCancellation(ct))
                    handler(item);
                return;
            }
            catch (OperationCanceledException) { return; }
            catch
            {
                await Task.Delay(1000, ct);
            }
        }
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
            case ConsoleKey.D5: Navigate("/settings"); break;

            case ConsoleKey.Q or ConsoleKey.Escape: Shutdown(); break;
        }
    }

    private PerfDetailSection NextSection(PerfDetailSection current)
    {
        return current switch
        {
            PerfDetailSection.Cpu => PerfDetailSection.Ram,
            PerfDetailSection.Ram => PerfDetailSection.Disk,
            PerfDetailSection.Disk => PerfDetailSection.Network,
            PerfDetailSection.Network => GpuAvailable ? PerfDetailSection.Gpu : PerfDetailSection.Cpu,
            PerfDetailSection.Gpu => PerfDetailSection.Cpu,
            _ => PerfDetailSection.Cpu,
        };
    }

    private PerfDetailSection PrevSection(PerfDetailSection current)
    {
        return current switch
        {
            PerfDetailSection.Ram => PerfDetailSection.Cpu,
            PerfDetailSection.Disk => PerfDetailSection.Ram,
            PerfDetailSection.Network => PerfDetailSection.Disk,
            PerfDetailSection.Gpu => PerfDetailSection.Network,
            PerfDetailSection.Cpu => GpuAvailable ? PerfDetailSection.Gpu : PerfDetailSection.Network,
            _ => PerfDetailSection.Network,
        };
    }

    private void HandleDetailKey(KeyPressed key)
    {
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.Escape:
                IsDetailOpen.Value = false;
                break;
            case ConsoleKey.Tab or ConsoleKey.RightArrow:
                DetailSection.Value = NextSection(DetailSection.Value);
                DiskDetailIndex.Value = 0;
                _detailContentChanged.OnNext(Unit.Default);
                break;
            case ConsoleKey.LeftArrow:
                DetailSection.Value = PrevSection(DetailSection.Value);
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
        Gpu.Dispose();
        IsDetailOpen.Dispose();
        DetailSection.Dispose();
        DiskDetailIndex.Dispose();
        _detailContentChanged.Dispose();
        base.Dispose();
    }
}
