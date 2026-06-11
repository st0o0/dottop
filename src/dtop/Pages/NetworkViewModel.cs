using dtop.Core.Messages;
using dtop.Core.Models;
using dtop.Nodes;
using dtop.Resources;
using dtop.Services;
using R3;
using Servus;
using Servus.Diagnostics;
using Termina.Input;
using Termina.Notifications;
using Termina.Reactive;
using Termina.Terminal;

namespace dtop.Pages;

public class NetworkViewModel : ReactiveViewModel
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("ViewModel.Network");
    private readonly MetricStore _store;
    private readonly IMonitorDemand _demand;
    private readonly SettingsService _settingsService;
    private readonly UpdateService _updateService;
    private readonly IToastService _toast;
    private readonly IRefreshService _refreshService;
    private readonly List<IDisposable> _demandHandles = [];

    public IRefreshService RefreshService => _refreshService;

    public IScrollableList? ListNode { get; set; }
    public Func<ConnectionSnapshot?>? GetSelectedItem { get; set; }

    private readonly Subject<Unit> _detailContentChanged = new();
    public Observable<Unit> DetailContentChanged => _detailContentChanged.AsObservable();

    public ReactiveProperty<List<ConnectionSnapshot>> Connections { get; } = new([]);
    public ReactiveProperty<List<ConnectionSnapshot>> FilteredConnections { get; } = new([]);
    public ReactiveProperty<string> SearchText { get; } = new("");
    public ReactiveProperty<bool> IsSearchActive { get; } = new(false);
    public ReactiveProperty<string> StatusMessage { get; } = new("");
    public ReactiveProperty<bool> IsDetailOpen { get; } = new(false);
    public ReactiveProperty<ConnectionSnapshot?> SelectedConnection { get; } = new(null);
    public ReactiveProperty<bool> IsSettingsOpen { get; } = new(false);

    private readonly Subject<Unit> _settingsContentChanged = new();
    public Observable<Unit> SettingsContentChanged => _settingsContentChanged.AsObservable();

    private static readonly int[] RefreshOptions = [250, 500, 1000, 2000, 5000];

    public NetworkViewModel(
        MetricStore store,
        IMonitorDemand demand,
        SettingsService settingsService,
        UpdateService updateService,
        IToastService toast,
        IRefreshService refreshService)
    {
        _store = store;
        _demand = demand;
        _settingsService = settingsService;
        _updateService = updateService;
        _toast = toast;
        _refreshService = refreshService;
    }

    public override void OnActivated()
    {
        _demandHandles.Add(_demand.Acquire(MetricKind.NetworkConnections));

        _store.Connections.Subscribe(conns =>
        {
            Connections.Value = conns.ToList();
            ApplyFilter();
        }).DisposeWith(Subscriptions);

        SearchText.Subscribe(_ => ApplyFilter()).DisposeWith(Subscriptions);
        Input.OfType<IInputEvent, KeyPressed>().Subscribe(HandleKey).DisposeWith(Subscriptions);
    }

    private void ApplyFilter()
    {
        var source = Connections.Value.AsEnumerable();
        if (!string.IsNullOrEmpty(SearchText.Value))
        {
            source = source.Where(c =>
                c.ProcessName.Contains(SearchText.Value, StringComparison.OrdinalIgnoreCase) ||
                c.Pid.ToString().Contains(SearchText.Value) ||
                c.LocalEndpoint.Contains(SearchText.Value) ||
                c.RemoteEndpoint.Contains(SearchText.Value));
        }

        FilteredConnections.Value = source.ToList();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (IsDetailOpen.Value)
        {
            StatusMessage.Value = Strings.HintNetworkDetailKeys;
        }
        else
        {
            StatusMessage.Value = string.Format(Strings.NetworkStatusFormat, FilteredConnections.Value.Count);
        }
    }

    private void HandleKey(KeyPressed key)
    {
        if (IsSearchActive.Value)
        {
            switch (key.KeyInfo.Key)
            {
                case ConsoleKey.Escape: IsSearchActive.Value = false; SearchText.Value = ""; break;
                case ConsoleKey.Backspace: if (SearchText.Value.Length > 0)
                    {
                        SearchText.Value = SearchText.Value[..^1];
                    }

                    break;
                default: if (key.KeyInfo.KeyChar is >= ' ' and <= '~')
                    {
                        SearchText.Value += key.KeyInfo.KeyChar;
                    }

                    break;
            }
            return;
        }
        if (IsSettingsOpen.Value) { HandleSettingsKey(key); return; }
        if (IsDetailOpen.Value)
        {
            if (key.KeyInfo.Key == ConsoleKey.Escape)
            {
                IsDetailOpen.Value = false;
                SelectedConnection.Value = null;
                UpdateStatus();
            }
            return;
        }
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.UpArrow: ListNode?.MoveUp(); break;
            case ConsoleKey.DownArrow: ListNode?.MoveDown(); break;
            case ConsoleKey.Home: ListNode?.MoveToTop(); break;
            case ConsoleKey.End: ListNode?.MoveToEnd(); break;
            case ConsoleKey.PageUp: ListNode?.PageUp(); break;
            case ConsoleKey.PageDown: ListNode?.PageDown(); break;
            default:
                if (key.KeyInfo.KeyChar == '/')
                {
                    IsSearchActive.Value = true;
                }

                break;
            case ConsoleKey.Enter:
                if (GetSelectedItem?.Invoke() is { } conn)
                {
                    SelectedConnection.Value = conn;
                    IsDetailOpen.Value = true;
                    UpdateStatus();
                    _detailContentChanged.OnNext(Unit.Default);
                }
                break;
            case ConsoleKey.D1: Navigate("/overview"); break;
            case ConsoleKey.D2: Navigate("/"); break;
            case ConsoleKey.D3: Navigate("/performance"); break;
            case ConsoleKey.D4: Navigate("/services"); break;
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

            case ConsoleKey.Q: Shutdown(); break;
        }
    }

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
        Connections.Dispose(); FilteredConnections.Dispose();
        SearchText.Dispose(); IsSearchActive.Dispose(); StatusMessage.Dispose();
        IsDetailOpen.Dispose(); SelectedConnection.Dispose(); IsSettingsOpen.Dispose();
        _detailContentChanged.Dispose(); _settingsContentChanged.Dispose();
        base.Dispose();
    }
}
