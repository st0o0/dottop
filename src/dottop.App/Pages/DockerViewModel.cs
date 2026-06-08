using Akka.Actor;
using Akka.Hosting;
using dottop.App.Actors;
using dottop.App.Nodes;
using dottop.App.Resources;
using dottop.App.Services;
using dottop.Core.Messages;
using dottop.Core.Models;
using R3;
using Termina.Input;
using Termina.Notifications;
using Termina.Reactive;
using Termina.Terminal;

namespace dottop.App.Pages;

public record DockerListItem
{
    public bool IsGroup { get; init; }
    public string? GroupName { get; init; }
    public int GroupCount { get; init; }
    public bool IsExpanded { get; init; }
    public ContainerSnapshot? Container { get; init; }
}

public class DockerViewModel : ReactiveViewModel
{
    private readonly HashSet<string> _expandedGroups = new();
    private readonly HashSet<string> _knownGroups = new();

    private readonly IRequiredActor<MonitoringSupervisor> _supervisor;
    private readonly SettingsService _settingsService;
    private readonly UpdateService _updateService;
    private readonly IToastService _toast;
    private IActorRef? _supervisorActor;
    private CancellationTokenSource? _cts;

    public IScrollableList? ListNode { get; set; }
    public IScrollableList? OverlayListNode { get; set; }
    public Func<ContainerSnapshot?>? GetSelectedItem { get; set; }
    public Func<DockerListItem?>? GetSelectedDisplayItem { get; set; }

    private readonly Subject<Unit> _detailContentChanged = new();
    public Observable<Unit> DetailContentChanged => _detailContentChanged.AsObservable();

    public ReactiveProperty<List<ContainerSnapshot>> AllContainers { get; } = new([]);
    public ReactiveProperty<List<ContainerSnapshot>> FilteredContainers { get; } = new([]);
    public ReactiveProperty<List<DockerListItem>> DisplayItems { get; } = new([]);
    public ReactiveProperty<string> SearchText { get; } = new("");
    public ReactiveProperty<bool> IsSearchActive { get; } = new(false);
    public ReactiveProperty<string> StatusMessage { get; } = new("");
    public ReactiveProperty<bool> IsDetailOpen { get; } = new(false);
    public ReactiveProperty<ContainerSnapshot?> SelectedContainer { get; } = new(null);
    public ReactiveProperty<bool> IsSettingsOpen { get; } = new(false);
    public ReactiveProperty<string> LogContent { get; } = new(Strings.DockerLoadingLogs);

    private readonly Subject<Unit> _settingsContentChanged = new();
    public Observable<Unit> SettingsContentChanged => _settingsContentChanged.AsObservable();

    private static readonly int[] RefreshOptions = [250, 500, 1000, 2000, 5000];

    public DockerViewModel(
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
        _cts = new CancellationTokenSource();
        _supervisorActor = _supervisor.GetAsync(CancellationToken.None).GetAwaiter().GetResult();
        _ = ConnectStreamAsync();
        SearchText.Subscribe(_ => ApplyFilter()).DisposeWith(Subscriptions);
        Input.OfType<IInputEvent, KeyPressed>().Subscribe(HandleKey).DisposeWith(Subscriptions);
    }

    private async Task ConnectStreamAsync()
    {
        if (_supervisorActor is null || _cts is null) return;
        var ct = _cts.Token;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var stream = await _supervisorActor.Ask<MonitoringStream<List<ContainerSnapshot>>>(
                    new StartDockerMonitoring(), TimeSpan.FromSeconds(60));
                await foreach (var containers in stream.Data.WithCancellation(ct))
                {
                    AllContainers.Value = containers;
                    ApplyFilter();

                    if (IsDetailOpen.Value && SelectedContainer.Value is { } current)
                    {
                        var updated = containers.FirstOrDefault(c => c.Id == current.Id);
                        if (updated is not null)
                        {
                            SelectedContainer.Value = updated;
                            _detailContentChanged.OnNext(Unit.Default);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { return; }
            catch
            {
                try { await Task.Delay(2000, ct); } catch { return; }
            }
        }
    }

    private void ApplyFilter()
    {
        var source = AllContainers.Value.AsEnumerable();
        if (!string.IsNullOrEmpty(SearchText.Value))
        {
            source = source.Where(c =>
                c.Name.Contains(SearchText.Value, StringComparison.OrdinalIgnoreCase) ||
                c.Image.Contains(SearchText.Value, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = source.ToList();
        FilteredContainers.Value = filtered;

        // Build grouped display items
        var items = new List<DockerListItem>();
        var grouped = filtered.GroupBy(c => c.ComposeProject ?? "").OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            if (!string.IsNullOrEmpty(group.Key))
            {
                if (_knownGroups.Add(group.Key))
                    _expandedGroups.Add(group.Key);
                var expanded = _expandedGroups.Contains(group.Key);
                items.Add(new DockerListItem
                {
                    IsGroup = true,
                    GroupName = group.Key,
                    GroupCount = group.Count(),
                    IsExpanded = expanded
                });
                if (expanded)
                {
                    foreach (var c in group)
                        items.Add(new DockerListItem { Container = c });
                }
            }
            else
            {
                foreach (var c in group)
                    items.Add(new DockerListItem { Container = c });
            }
        }

        DisplayItems.Value = items;
        UpdateStatus();
    }

    public void ToggleGroup(string groupName)
    {
        if (!_expandedGroups.Remove(groupName))
            _expandedGroups.Add(groupName);
        ApplyFilter();
    }


    private void UpdateStatus()
    {
        if (IsDetailOpen.Value)
        {
            StatusMessage.Value = Strings.HintDockerDetailKeys;
        }
        else
        {
            StatusMessage.Value = string.Format(Strings.DockerStatusFormat, FilteredContainers.Value.Count);
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
                var selectedItem = GetSelectedDisplayItem?.Invoke();
                if (selectedItem is { IsGroup: true, GroupName: { } groupName })
                {
                    ToggleGroup(groupName);
                }
                else if (GetSelectedItem?.Invoke() is { } container)
                {
                    SelectedContainer.Value = container;
                    IsDetailOpen.Value = true;
                    LogContent.Value = Strings.DockerLoadingLogs;
                    UpdateStatus();
                    _detailContentChanged.OnNext(Unit.Default);
                    _ = LoadLogsAsync(container.Id);
                }
                break;
            case ConsoleKey.S: ActionOnSelected(); break;
            case ConsoleKey.X: ActionOnSelected(ActionType.Stop); break;
            case ConsoleKey.R: ActionOnSelected(ActionType.Restart); break;
            case ConsoleKey.D1: Navigate("/"); break;
            case ConsoleKey.D2: Navigate("/performance"); break;
            case ConsoleKey.D3: Navigate("/services"); break;
            case ConsoleKey.D4: Navigate("/network"); break;
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
                SelectedContainer.Value = null;
                OverlayListNode = null;
                UpdateStatus();
                break;
            case ConsoleKey.S:
                ActionOnDetailContainer();
                break;
            case ConsoleKey.X:
                ActionOnDetailContainer(ActionType.Stop);
                break;
            case ConsoleKey.R:
                ActionOnDetailContainer(ActionType.Restart);
                break;
            case ConsoleKey.UpArrow: OverlayListNode?.MoveUp(); break;
            case ConsoleKey.DownArrow: OverlayListNode?.MoveDown(); break;
            case ConsoleKey.Home: OverlayListNode?.MoveToTop(); break;
            case ConsoleKey.End: OverlayListNode?.MoveToEnd(); break;
            case ConsoleKey.PageUp: OverlayListNode?.PageUp(); break;
            case ConsoleKey.PageDown: OverlayListNode?.PageDown(); break;
        }
    }

    private async Task LoadLogsAsync(string containerId)
    {
        if (_supervisorActor is null) return;
        try
        {
            var result = await _supervisorActor.Ask<object>(new GetContainerLogs(containerId), TimeSpan.FromSeconds(10));
            if (result is ContainerLogsResult logs)
            {
                LogContent.Value = string.Join("\n", logs.Lines);
            }
            else if (result is ActionFailure failure)
            {
                LogContent.Value = "Error: " + failure.Error;
            }
        }
        catch (Exception ex)
        {
            LogContent.Value = "Error: " + ex.Message;
        }
        _detailContentChanged.OnNext(Unit.Default);
    }

    private enum ActionType { Start, Stop, Restart }

    private async void ActionOnDetailContainer(ActionType action = ActionType.Start)
    {
        if (_supervisorActor is null || SelectedContainer.Value is not { } container)
        {
            return;
        }

        object msg = action switch
        {
            ActionType.Stop => new StopContainer(container.Id),
            ActionType.Restart => new RestartContainer(container.Id),
            _ => new StartContainer(container.Id),
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
                _toast.Show("Error: " + error, new ToastOptions(Position: ToastPosition.TopCenter, Color: Color.BrightRed, Duration: TimeSpan.FromSeconds(5)));
            }

            // Refresh detail if still open
            if (IsDetailOpen.Value)
            {
                var updated = AllContainers.Value.FirstOrDefault(x => x.Id == container.Id);
                if (updated is not null)
                {
                    SelectedContainer.Value = updated;
                    _detailContentChanged.OnNext(Unit.Default);
                }
            }
        }
        catch (Exception ex)
        {
            _toast.Show("Error: " + ex.Message, new ToastOptions(Position: ToastPosition.TopCenter, Color: Color.BrightRed, Duration: TimeSpan.FromSeconds(5)));
        }
    }

    private async void ActionOnSelected(ActionType action = ActionType.Start)
    {
        if (_supervisorActor is null) return;

        var selected = GetSelectedDisplayItem?.Invoke();
        if (selected is { IsGroup: true, GroupName: { } groupName })
        {
            var containers = FilteredContainers.Value
                .Where(c => c.ComposeProject == groupName)
                .ToList();
            if (containers.Count == 0) return;

            var actionName = action switch { ActionType.Stop => "Stopping", ActionType.Restart => "Restarting", _ => "Starting" };
            _toast.Show($"{actionName} {containers.Count} containers...", new ToastOptions(Duration: TimeSpan.FromSeconds(2)));

            var tasks = containers.Select(c => ExecuteContainerActionAsync(c.Id, action));
            await Task.WhenAll(tasks);

            _toast.Show($"{action} completed for {groupName}", new ToastOptions(Duration: TimeSpan.FromSeconds(3)));
            return;
        }

        if (GetSelectedItem?.Invoke() is not { } container) return;
        await ExecuteContainerActionAsync(container.Id, action);
    }

    private async Task ExecuteContainerActionAsync(string containerId, ActionType action)
    {
        if (_supervisorActor is null) return;

        object msg = action switch
        {
            ActionType.Stop => new StopContainer(containerId),
            ActionType.Restart => new RestartContainer(containerId),
            _ => new StartContainer(containerId),
        };
        try
        {
            var result = await _supervisorActor.Ask<object>(msg, TimeSpan.FromSeconds(30));
            if (result is ActionSuccess s)
            {
                _toast.Show(s.Message, new ToastOptions(Duration: TimeSpan.FromSeconds(3)));
            }
            else if (result is ActionFailure f)
            {
                _toast.Show("Error: " + f.Error, new ToastOptions(Position: ToastPosition.TopCenter, Color: Color.BrightRed, Duration: TimeSpan.FromSeconds(5)));
            }
        }
        catch (Exception ex)
        {
            _toast.Show("Error: " + ex.Message, new ToastOptions(Position: ToastPosition.TopCenter, Color: Color.BrightRed, Duration: TimeSpan.FromSeconds(5)));
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
        if (idx < 0) idx = 2;
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

    public override void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        AllContainers.Dispose(); FilteredContainers.Dispose(); SearchText.Dispose();
        IsSearchActive.Dispose(); StatusMessage.Dispose(); IsDetailOpen.Dispose();
        SelectedContainer.Dispose(); IsSettingsOpen.Dispose(); LogContent.Dispose();
        _detailContentChanged.Dispose(); _settingsContentChanged.Dispose();
        base.Dispose();
    }
}
