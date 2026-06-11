using dtop.Core.Messages;
using dtop.Core.Models;
using dtop.Core.Platform;
using dtop.Nodes;
using Termina.Layout;
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
    private readonly MetricStore _store;
    private readonly IMonitorDemand _demand;
    private readonly IGpuMetrics _gpuMetrics;
    private readonly SettingsService _settingsService;
    private readonly UpdateService _updateService;
    private readonly PinService _pinService;
    private readonly IToastService _toast;
    private readonly IRefreshService _refreshService;
    private readonly List<IDisposable> _demandHandles = [];

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

    public MetricStore Store => _store;
    public IRefreshService RefreshService => _refreshService;

    public PerformanceViewModel(
        MetricStore store,
        IMonitorDemand demand,
        IGpuMetrics gpuMetrics,
        SettingsService settingsService,
        UpdateService updateService,
        PinService pinService,
        IToastService toast,
        IRefreshService refreshService)
    {
        _store = store;
        _demand = demand;
        _gpuMetrics = gpuMetrics;
        _settingsService = settingsService;
        _updateService = updateService;
        _pinService = pinService;
        _toast = toast;
        _refreshService = refreshService;

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
        _demandHandles.Add(_demand.Acquire(MetricKind.Disk));
        _demandHandles.Add(_demand.Acquire(MetricKind.Network));
        if (_gpuMetrics.IsAvailable)
            _demandHandles.Add(_demand.Acquire(MetricKind.Gpu));

        _store.Cpu.Subscribe(s =>
        {
            if (s is null) return;
            CpuName.Value = s.Name;
            CpuTotal.Value = s.TotalPercent;
            CpuCores.Value = s.CorePercents;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Cpu)
                _detailContentChanged.OnNext(Unit.Default);
        }).DisposeWith(Subscriptions);

        _store.Memory.Subscribe(s =>
        {
            if (s is null) return;
            RamTotal.Value = s.TotalBytes;
            RamUsed.Value = s.UsedBytes;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Ram)
                _detailContentChanged.OnNext(Unit.Default);
        }).DisposeWith(Subscriptions);

        _store.Disks.Subscribe(d =>
        {
            Disks.Value = d;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Disk)
                _detailContentChanged.OnNext(Unit.Default);
        }).DisposeWith(Subscriptions);

        _store.Networks.Subscribe(n =>
        {
            Networks.Value = n;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Network)
                _detailContentChanged.OnNext(Unit.Default);
        }).DisposeWith(Subscriptions);

        _store.Gpu.Subscribe(g =>
        {
            if (g is null) return;
            Gpu.Value = g;
            if (IsDetailOpen.Value && DetailSection.Value == PerfDetailSection.Gpu)
                _detailContentChanged.OnNext(Unit.Default);
        }).DisposeWith(Subscriptions);

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
            case ConsoleKey.D1: Navigate("/overview"); break;
            case ConsoleKey.D2: Navigate("/"); break;
            case ConsoleKey.D4: Navigate("/services"); break;
            case ConsoleKey.D5: Navigate("/network"); break;
            case ConsoleKey.D6: Navigate("/docker"); break;
            case ConsoleKey.F10:
                IsSettingsOpen.Value = true;
                _settingsContentChanged.OnNext(Unit.Default);
                break;

            case ConsoleKey.Spacebar:
                _refreshService.IsPaused.Value = !_refreshService.IsPaused.Value;
                break;
            case ConsoleKey.Add or ConsoleKey.OemPlus:
                _refreshService.SpeedUp();
                break;
            case ConsoleKey.Subtract or ConsoleKey.OemMinus:
                _refreshService.SlowDown();
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
        foreach (var d in _demandHandles) d.Dispose();
        _demandHandles.Clear();
        base.OnDeactivating();
    }

    public override void Dispose()
    {
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
