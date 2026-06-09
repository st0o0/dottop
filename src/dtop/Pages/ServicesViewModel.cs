using Akka.Actor;
using Akka.Hosting;
using dtop.Actors;
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

public class ServicesViewModel : ReactiveViewModel
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("ViewModel.Services");
    private readonly IRequiredActor<MonitoringSupervisor> _supervisor;
    private readonly SettingsService _settingsService;
    private readonly UpdateService _updateService;
    private readonly IToastService _toast;
    private IActorRef? _supervisorActor;

    public IScrollableList? ListNode { get; set; }
    public Func<ServiceInfo?>? GetSelectedItem { get; set; }

    private readonly Subject<Unit> _detailContentChanged = new();
    public Observable<Unit> DetailContentChanged => _detailContentChanged.AsObservable();

    public ReactiveProperty<List<ServiceInfo>> AllServices { get; } = new([]);
    public ReactiveProperty<List<ServiceInfo>> FilteredServices { get; } = new([]);
    public ReactiveProperty<string> SearchText { get; } = new("");
    public ReactiveProperty<bool> IsSearchActive { get; } = new(false);
    public ReactiveProperty<string> StatusMessage { get; } = new("");
    public ReactiveProperty<bool> IsDetailOpen { get; } = new(false);
    public ReactiveProperty<ServiceInfo?> SelectedService { get; } = new(null);
    public ReactiveProperty<bool> IsSettingsOpen { get; } = new(false);

    private readonly Subject<Unit> _settingsContentChanged = new();
    public Observable<Unit> SettingsContentChanged => _settingsContentChanged.AsObservable();

    private static readonly int[] RefreshOptions = [250, 500, 1000, 2000, 5000];

    public ServicesViewModel(
        IRequiredActor<MonitoringSupervisor> supervisor,
        SettingsService settingsService,
        UpdateService updateService,
        IToastService toast)
    {
        _supervisor = supervisor;
        _settingsService = settingsService;
        _updateService = updateService;
        _toast = toast;
    }

    public override void OnActivated()
    {
        _ = InitializeAsync();
        SearchText.Subscribe(_ => ApplyFilter()).DisposeWith(Subscriptions);
        Input.OfType<IInputEvent, KeyPressed>().Subscribe(HandleKey).DisposeWith(Subscriptions);
    }

    private async Task InitializeAsync()
    {
        _supervisorActor = await _supervisor.GetAsync(CancellationToken.None);
        _ = RefreshServices();
    }

    private async ValueTask RefreshServices()
    {
        if (_supervisorActor is null)
        {
            return;
        }

        try
        {
            var result = await _supervisorActor.Ask<List<ServiceInfo>>(new GetServices(), TimeSpan.FromSeconds(10));
            AllServices.Value = result;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            Trace.Warning(this, "Failed to load services: {0}", ex.Message);
            StatusMessage.Value = Strings.ErrorLoadingServices;
        }
    }

    private void ApplyFilter()
    {
        var source = AllServices.Value.AsEnumerable();
        if (!string.IsNullOrEmpty(SearchText.Value))
        {
            source = source.Where(s =>
                s.DisplayName.Contains(SearchText.Value, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(SearchText.Value, StringComparison.OrdinalIgnoreCase));
        }

        FilteredServices.Value = source.ToList();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (IsDetailOpen.Value)
        {
            StatusMessage.Value = Strings.HintServiceDetailKeys;
        }
        else
        {
            StatusMessage.Value = string.Format(Strings.ServicesStatusFormat, FilteredServices.Value.Count);
        }
    }

    private void HandleKey(KeyPressed key)
    {
        if (IsSearchActive.Value)
        {
            switch (key.KeyInfo.Key)
            {
                case ConsoleKey.Escape:
                    IsSearchActive.Value = false;
                    SearchText.Value = "";
                    break;
                case ConsoleKey.Backspace:
                    if (SearchText.Value.Length > 0)
                    {
                        SearchText.Value = SearchText.Value[..^1];
                    }

                    break;
                default:
                    if (key.KeyInfo.KeyChar is >= ' ' and <= '~')
                    {
                        SearchText.Value += key.KeyInfo.KeyChar;
                    }

                    break;
            }

            return;
        }

        if (IsSettingsOpen.Value)
        {
            HandleSettingsKey(key);
            return;
        }

        if (IsDetailOpen.Value)
        {
            HandleDetailKey(key);
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
                if (GetSelectedItem?.Invoke() is { } svc)
                {
                    SelectedService.Value = svc;
                    IsDetailOpen.Value = true;
                    UpdateStatus();
                    _detailContentChanged.OnNext(Unit.Default);
                }

                break;
            case ConsoleKey.S: _ = ActionOnSelected(); break;
            case ConsoleKey.X: _ = ActionOnSelected(ActionType.Stop); break;
            case ConsoleKey.R: _ = ActionOnSelected(ActionType.Restart); break;
            case ConsoleKey.D1: Navigate("/"); break;
            case ConsoleKey.D2: Navigate("/performance"); break;
            case ConsoleKey.D4: Navigate("/network"); break;
            case ConsoleKey.D5: Navigate("/docker"); break;
            case ConsoleKey.F10:
                IsSettingsOpen.Value = true;
                _settingsContentChanged.OnNext(Unit.Default);
                break;

            case ConsoleKey.Q: Shutdown(); break;
        }
    }

    private void HandleDetailKey(KeyPressed key)
    {
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.Escape:
                IsDetailOpen.Value = false;
                SelectedService.Value = null;
                UpdateStatus();
                break;
            case ConsoleKey.S:
                _ = ActionOnDetailService();
                break;
            case ConsoleKey.X:
                _ = ActionOnDetailService(ActionType.Stop);
                break;
            case ConsoleKey.R:
                _ = ActionOnDetailService(ActionType.Restart);
                break;
        }
    }

    private async ValueTask ActionOnDetailService(ActionType action = ActionType.Start)
    {
        if (_supervisorActor is null || SelectedService.Value is not { } svc)
        {
            return;
        }

        object msg = action switch
        {
            ActionType.Stop => new StopService(svc.Name),
            ActionType.Restart => new RestartService(svc.Name),
            _ => new StartService(svc.Name),
        };
        try
        {
            var result = await _supervisorActor.Ask<object>(msg, TimeSpan.FromSeconds(10));
            if (result is ActionSuccess s)
            {
                _toast.Show(s.Message, new ToastOptions(Duration: TimeSpan.FromSeconds(3)));
            }
            else
            {
                var error = ((ActionFailure)result).Error;
                _toast.Show("Error: " + error,
                    new ToastOptions(Position: ToastPosition.TopCenter, Color: Color.BrightRed,
                        Duration: TimeSpan.FromSeconds(5)));
            }

            _ = RefreshServices();

            // Update the detail modal with refreshed data
            if (IsDetailOpen.Value)
            {
                var updated = AllServices.Value.FirstOrDefault(x => x.Name == svc.Name);
                if (updated is not null)
                {
                    SelectedService.Value = updated;
                    _detailContentChanged.OnNext(Unit.Default);
                }
            }
        }
        catch (Exception ex)
        {
            _toast.Show("Error: " + ex.Message,
                new ToastOptions(Position: ToastPosition.TopCenter, Color: Color.BrightRed,
                    Duration: TimeSpan.FromSeconds(5)));
        }
    }

    private enum ActionType
    {
        Start,
        Stop,
        Restart
    }

    private async ValueTask ActionOnSelected(ActionType action = ActionType.Start)
    {
        if (_supervisorActor is null || GetSelectedItem?.Invoke() is not { } svc)
        {
            return;
        }

        object msg = action switch
        {
            ActionType.Stop => new StopService(svc.Name),
            ActionType.Restart => new RestartService(svc.Name),
            _ => new StartService(svc.Name),
        };
        try
        {
            var result = await _supervisorActor.Ask<object>(msg, TimeSpan.FromSeconds(10));
            if (result is ActionSuccess s)
            {
                _toast.Show(s.Message, new ToastOptions(Duration: TimeSpan.FromSeconds(3)));
            }
            else
            {
                var error = ((ActionFailure)result).Error;
                _toast.Show("Error: " + error,
                    new ToastOptions(Position: ToastPosition.TopCenter, Color: Color.BrightRed,
                        Duration: TimeSpan.FromSeconds(5)));
            }

            await RefreshServices();
        }
        catch (Exception ex)
        {
            _toast.Show("Error: " + ex.Message,
                new ToastOptions(Position: ToastPosition.TopCenter, Color: Color.BrightRed,
                    Duration: TimeSpan.FromSeconds(5)));
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
            _toast.Show(Strings.UpdateFailed,
                new ToastOptions(Color: Color.BrightRed, Duration: TimeSpan.FromSeconds(5)));
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

    public override void Dispose()
    {
        AllServices.Dispose();
        FilteredServices.Dispose();
        SearchText.Dispose();
        IsSearchActive.Dispose();
        StatusMessage.Dispose();
        IsDetailOpen.Dispose();
        SelectedService.Dispose();
        IsSettingsOpen.Dispose();
        _detailContentChanged.Dispose();
        _settingsContentChanged.Dispose();
        base.Dispose();
    }
}