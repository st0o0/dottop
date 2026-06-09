using Akka.Actor;
using Akka.Hosting;
using dtop.Actors;
using dtop.Core.Messages;
using dtop.Core.Models;
using dtop.Core.Platform;
using dtop.Nodes;
using dtop.Resources;
using dtop.Services;
using R3;
using Termina.Input;
using Termina.Notifications;
using Termina.Reactive;
using Termina.Terminal;

namespace dtop.Pages;

public enum PerfDetailSection { Cpu, Ram, Disk, Network, Gpu }

public class PerformanceViewModel : ReactiveViewModel
{
    private readonly IRequiredActor<MonitoringSupervisor> _supervisor;
    private readonly IGpuMetrics _gpuMetrics;
    private readonly SettingsService _settingsService;
    private readonly UpdateService _updateService;
    private readonly PinService _pinService;
    private readonly IToastService _toast;
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
    public ReactiveProperty<string> StatusHint { get; } = new("");
    public ReactiveProperty<bool> IsSettingsOpen { get; } = new(false);

    private readonly Subject<Unit> _detailContentChanged = new();
    public Observable<Unit> DetailContentChanged => _detailContentChanged.AsObservable();

    private readonly Subject<Unit> _settingsContentChanged = new();
    public Observable<Unit> SettingsContentChanged => _settingsContentChanged.AsObservable();

    private static readonly int[] RefreshOptions = [250, 500, 1000, 2000, 5000];

    public PerformanceViewModel(
        IRequiredActor<MonitoringSupervisor> supervisor,
        IGpuMetrics gpuMetrics,
        SettingsService settingsService,
        UpdateService updateService,
        PinService pinService,
        IToastService toast)
    {
        _supervisor = supervisor;
        _gpuMetrics = gpuMetrics;
        _settingsService = settingsService;
        _updateService = updateService;
        _pinService = pinService;
        _toast = toast;

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
        UpdateStatusHint();

        Input.OfType<IInputEvent, KeyPressed>()
            .Subscribe(HandleKey)
            .DisposeWith(Subscriptions);
    }

    private void UpdateStatusHint()
    {
        StatusHint.Value = IsDetailOpen.Value
            ? Strings.HintPerfDetailKeys
            : $" {Strings.PerfStatusBar}";
    }

    private async Task InitializeAsync()
    {
        var ct = _cts!.Token;
        var supervisor = await _supervisor.GetAsync(ct);

        _ = ConnectStream<CpuSnapshot>(supervisor, new StartCpuMonitoring(), ct, snapshot =>
        {
            CpuName.Value = snapshot.Name;
            CpuTotal.Value = snapshot.TotalPercent;
            CpuCores.Value = snapshot.CorePercents;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Cpu)
            {
                _detailContentChanged.OnNext(Unit.Default);
            }
        });

        _ = ConnectStream<MemorySnapshot>(supervisor, new StartMemoryMonitoring(), ct, snapshot =>
        {
            RamTotal.Value = snapshot.TotalBytes;
            RamUsed.Value = snapshot.UsedBytes;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Ram)
            {
                _detailContentChanged.OnNext(Unit.Default);
            }
        });

        _ = ConnectStream<List<DiskSnapshot>>(supervisor, new StartDiskMonitoring(), ct, disks =>
        {
            Disks.Value = disks;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Disk)
            {
                _detailContentChanged.OnNext(Unit.Default);
            }
        });

        _ = ConnectStream<List<NetworkSnapshot>>(supervisor, new StartNetworkMonitoring(), ct, nets =>
        {
            Networks.Value = nets;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Network)
            {
                _detailContentChanged.OnNext(Unit.Default);
            }
        });

        if (_gpuMetrics.IsAvailable)
        {
            _ = ConnectStream<GpuSnapshot>(supervisor, new StartGpuMonitoring(), ct, snapshot =>
            {
                Gpu.Value = snapshot;
                if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Gpu)
                {
                    _detailContentChanged.OnNext(Unit.Default);
                }
            });
        }
    }

    private async Task ConnectStream<TData>(
        IActorRef supervisor, object startMessage, CancellationToken ct, Action<TData> handler)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var stream = await supervisor.Ask<MonitoringStream<TData>>(startMessage, TimeSpan.FromSeconds(60));
                await foreach (var item in stream.Data.WithCancellation(ct))
                    handler(item);
            }
            catch (OperationCanceledException) { return; }
            catch
            {
                try { await Task.Delay(2000, ct); } catch (OperationCanceledException) { return; }
            }
        }
    }

    private void HandleKey(KeyPressed key)
    {
        if (IsSettingsOpen.Value) { HandleSettingsKey(key); return; }
        if (IsDetailOpen.Value)
        {
            HandleDetailKey(key);
            return;
        }

        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.Enter:
                DetailSection.Value = PerfDetailSection.Cpu;
                IsDetailOpen.Value = true;
                UpdateStatusHint();
                _detailContentChanged.OnNext(Unit.Default);
                break;
            case ConsoleKey.D1: Navigate("/"); break;
            case ConsoleKey.D3: Navigate("/services"); break;
            case ConsoleKey.D4: Navigate("/network"); break;
            case ConsoleKey.D5: Navigate("/docker"); break;
            case ConsoleKey.F10:
                IsSettingsOpen.Value = true;
                _settingsContentChanged.OnNext(Unit.Default);
                break;

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
                UpdateStatusHint();
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
                else if (DetailSection.Value == PerfDetailSection.Network)
                {
                    NetworkListNode?.MoveUp();
                }
                break;
            case ConsoleKey.DownArrow:
                if (DetailSection.Value == PerfDetailSection.Disk &&
                    DiskDetailIndex.Value < Disks.Value.Count - 1)
                {
                    DiskDetailIndex.Value++;
                    _detailContentChanged.OnNext(Unit.Default);
                }
                else if (DetailSection.Value == PerfDetailSection.Network)
                {
                    NetworkListNode?.MoveDown();
                }
                break;
            case ConsoleKey.P:
                if (DetailSection.Value == PerfDetailSection.Network && GetSelectedAdapter?.Invoke() is { } adapter)
                {
                    _pinService.ToggleAdapterPin(adapter.Name);
                    _detailContentChanged.OnNext(Unit.Default);
                }
                break;
        }
    }

    public IScrollableList? NetworkListNode { get; set; }
    public Func<NetworkSnapshot?>? GetSelectedAdapter { get; set; }

    public bool IsAdapterPinned(string name) => _pinService.IsAdapterPinned(name);

    private void HandleSettingsKey(KeyPressed key)
    {
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.Escape:
                IsSettingsOpen.Value = false;
                break;
            case ConsoleKey.LeftArrow:
                CycleRefreshRate(-1);
                break;
            case ConsoleKey.RightArrow:
                CycleRefreshRate(1);
                break;
            case ConsoleKey.U:
                if (_updateService.UpdateAvailable)
                {
                    _ = PerformUpdateAsync();
                }
                break;
        }
    }

    private void CycleRefreshRate(int direction)
    {
        var current = _settingsService.Settings.RefreshIntervalMs;
        var idx = Array.IndexOf(RefreshOptions, current);
        if (idx < 0)
        {
            idx = 2;
        }

        var newIdx = (idx + direction + RefreshOptions.Length) % RefreshOptions.Length;
        _settingsService.Settings.RefreshIntervalMs = RefreshOptions[newIdx];
        _settingsService.Save();
        _settingsContentChanged.OnNext(Unit.Default);
    }

    private async Task PerformUpdateAsync()
    {
        _toast.Show(Strings.UpdateDownloading, new ToastOptions(Duration: TimeSpan.FromSeconds(3)));
        var success = await _updateService.PerformUpdateAsync(progress =>
        {
            _toast.Show(progress switch
            {
                "Downloading..." => Strings.UpdateDownloading,
                "Extracting..." => Strings.UpdateInstalling,
                _ => progress
            }, new ToastOptions(Duration: TimeSpan.FromSeconds(3)));
        });

        if (success)
        {
            _toast.Show(Strings.UpdateComplete, new ToastOptions(Duration: TimeSpan.FromSeconds(3)));
            await Task.Delay(1500);
            Shutdown();
        }
        else
        {
            _toast.Show(Strings.UpdateFailed, new ToastOptions(Color: Color.BrightRed, Duration: TimeSpan.FromSeconds(5)));
        }
    }

    public string GetRefreshRateDisplay()
    {
        return _settingsService.Settings.RefreshIntervalMs switch
        {
            250 => "250ms",
            500 => "500ms",
            1000 => "1s",
            2000 => "2s",
            5000 => "5s",
            _ => $"{_settingsService.Settings.RefreshIntervalMs}ms"
        };
    }

    public string GetSettingsFilePath() => SettingsService.FilePath;
    public bool IsUpdateAvailable => _updateService.UpdateAvailable;
    public string CurrentVersionDisplay => string.Format(Strings.CurrentVersion, _updateService.CurrentVersion);
    public string? LatestVersionDisplay => _updateService.UpdateAvailable
        ? string.Format(Strings.UpdateAvailable, _updateService.LatestVersion)
        : null;

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
        StatusHint.Dispose();
        IsSettingsOpen.Dispose();
        _detailContentChanged.Dispose();
        _settingsContentChanged.Dispose();
        base.Dispose();
    }
}
