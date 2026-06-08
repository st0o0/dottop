using Akka.Actor;
using Akka.Hosting;
using dtop.App.Actors;
using dtop.App.Nodes;
using dtop.App.Resources;
using dtop.App.Services;
using dtop.Core.Messages;
using dtop.Core.Models;
using R3;
using Servus;
using Servus.Diagnostics;
using Termina.Input;
using Termina.Notifications;
using Termina.Reactive;
using Termina.Terminal;

namespace dtop.App.Pages;

public enum DockerSubTab { Container, Networks, Volumes, Images }

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
    private static readonly TraceChannel Trace = Senf.Tracing.For("ViewModel.Docker");
    private readonly HashSet<string> _expandedGroups = new();
    private readonly HashSet<string> _knownGroups = new();

    private readonly IRequiredActor<DockerMonitorActor> _dockerActor;
    private readonly SettingsService _settingsService;
    private readonly UpdateService _updateService;
    private readonly PinService _pinService;
    private readonly IToastService _toast;
    private IActorRef? _dockerActorRef;
    private CancellationTokenSource? _cts;

    public IScrollableList? ListNode { get; set; }
    public IScrollableList? OverlayListNode { get; set; }
    public IScrollableList? NetworkListNode { get; set; }
    public IScrollableList? VolumeListNode { get; set; }
    public IScrollableList? ImageListNode { get; set; }
    public Func<ContainerSnapshot?>? GetSelectedItem { get; set; }
    public Func<DockerListItem?>? GetSelectedDisplayItem { get; set; }
    public Func<NetworkInfo?>? GetSelectedNetwork { get; set; }
    public Func<VolumeInfo?>? GetSelectedVolume { get; set; }
    public Func<ImageInfo?>? GetSelectedImage { get; set; }

    private readonly Subject<Unit> _detailContentChanged = new();
    public Observable<Unit> DetailContentChanged => _detailContentChanged.AsObservable();

    public ReactiveProperty<DockerSubTab> ActiveSubTab { get; } = new(DockerSubTab.Container);
    public ReactiveProperty<List<ContainerSnapshot>> AllContainers { get; } = new([]);
    public ReactiveProperty<List<ContainerSnapshot>> FilteredContainers { get; } = new([]);
    public ReactiveProperty<List<DockerListItem>> DisplayItems { get; } = new([]);
    public ReactiveProperty<List<NetworkInfo>> Networks { get; } = new([]);
    public ReactiveProperty<List<VolumeInfo>> Volumes { get; } = new([]);
    public ReactiveProperty<List<ImageInfo>> Images { get; } = new([]);
    public ReactiveProperty<string> SearchText { get; } = new("");
    public ReactiveProperty<bool> IsSearchActive { get; } = new(false);
    public ReactiveProperty<string> StatusMessage { get; } = new("");
    public ReactiveProperty<bool> IsDetailOpen { get; } = new(false);
    public ReactiveProperty<ContainerSnapshot?> SelectedContainer { get; } = new(null);
    public ReactiveProperty<bool> IsSettingsOpen { get; } = new(false);
    public ReactiveProperty<bool> IsInputMode { get; } = new(false);
    public ReactiveProperty<string> InputText { get; } = new("");
    public ReactiveProperty<string> LogContent { get; } = new(Strings.DockerLoadingLogs);

    private readonly Subject<Unit> _settingsContentChanged = new();
    public Observable<Unit> SettingsContentChanged => _settingsContentChanged.AsObservable();

    private static readonly int[] RefreshOptions = [250, 500, 1000, 2000, 5000];

    public DockerViewModel(
        IRequiredActor<DockerMonitorActor> dockerActor,
        SettingsService settingsService,
        UpdateService updateService,
        PinService pinService,
        IToastService toast)
    {
        _dockerActor = dockerActor;
        _settingsService = settingsService;
        _updateService = updateService;
        _pinService = pinService;
        _toast = toast;
    }

    public override void OnActivated()
    {
        _cts = new CancellationTokenSource();
        _ = InitializeAsync();
        SearchText.Subscribe(_ => ApplyFilter()).DisposeWith(Subscriptions);
        Input.OfType<IInputEvent, KeyPressed>().Subscribe(HandleKey).DisposeWith(Subscriptions);
    }

    private async Task InitializeAsync()
    {
        _dockerActorRef = await _dockerActor.GetAsync(CancellationToken.None);
        await ConnectStreamAsync();
    }

    private async Task ConnectStreamAsync()
    {
        if (_dockerActorRef is null || _cts is null)
        {
            return;
        }

        var ct = _cts.Token;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var stream = await _dockerActorRef.Ask<MonitoringStream<List<ContainerSnapshot>>>(
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
                try { await Task.Delay(2000, ct); } catch (OperationCanceledException) { return; }
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
                {
                    _expandedGroups.Add(group.Key);
                }

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
                    var groupContainers = PinService.SortWithPinnedFirst(group, c => _pinService.IsContainerPinned(c.Id));
                    foreach (var c in groupContainers)
                        items.Add(new DockerListItem { Container = c });
                }
            }
            else
            {
                var ungroupedContainers = PinService.SortWithPinnedFirst(group, c => _pinService.IsContainerPinned(c.Id));
                foreach (var c in ungroupedContainers)
                    items.Add(new DockerListItem { Container = c });
            }
        }

        DisplayItems.Value = items;
        UpdateStatus();
    }

    public void ToggleGroup(string groupName)
    {
        if (!_expandedGroups.Remove(groupName))
        {
            _expandedGroups.Add(groupName);
        }

        ApplyFilter();
    }

    public bool IsPinned(string id) => _pinService.IsContainerPinned(id);

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
        if (IsInputMode.Value)
        {
            switch (key.KeyInfo.Key)
            {
                case ConsoleKey.Escape:
                    IsInputMode.Value = false;
                    InputText.Value = "";
                    _detailContentChanged.OnNext(Unit.Default);
                    break;
                case ConsoleKey.Enter:
                    _ = SubmitInputAsync();
                    break;
                case ConsoleKey.Backspace:
                    if (InputText.Value.Length > 0)
                    {
                        InputText.Value = InputText.Value[..^1];
                    }

                    _detailContentChanged.OnNext(Unit.Default);
                    break;
                default:
                    if (key.KeyInfo.KeyChar is >= ' ' and <= '~')
                    {
                        InputText.Value += key.KeyInfo.KeyChar;
                        _detailContentChanged.OnNext(Unit.Default);
                    }
                    break;
            }
            return;
        }
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
        var activeList = ActiveSubTab.Value switch
        {
            DockerSubTab.Networks => NetworkListNode,
            DockerSubTab.Volumes => VolumeListNode,
            DockerSubTab.Images => ImageListNode,
            _ => ListNode
        };
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.UpArrow: activeList?.MoveUp(); break;
            case ConsoleKey.DownArrow: activeList?.MoveDown(); break;
            case ConsoleKey.Home: activeList?.MoveToTop(); break;
            case ConsoleKey.End: activeList?.MoveToEnd(); break;
            case ConsoleKey.PageUp: activeList?.PageUp(); break;
            case ConsoleKey.PageDown: activeList?.PageDown(); break;
            case ConsoleKey.Tab:
                if (key.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift))
                {
                    ActiveSubTab.Value = ActiveSubTab.Value == DockerSubTab.Container ? DockerSubTab.Images : (DockerSubTab)((int)ActiveSubTab.Value - 1);
                }
                else
                {
                    ActiveSubTab.Value = ActiveSubTab.Value == DockerSubTab.Images ? DockerSubTab.Container : (DockerSubTab)((int)ActiveSubTab.Value + 1);
                }

                _ = LoadSubTabDataAsync();
                break;
            default:
                if (key.KeyInfo.KeyChar == '/')
                {
                    IsSearchActive.Value = true;
                }

                break;
            case ConsoleKey.Enter:
                if (ActiveSubTab.Value == DockerSubTab.Container)
                {
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
                }
                else
                {
                    IsDetailOpen.Value = true;
                    UpdateStatus();
                    _detailContentChanged.OnNext(Unit.Default);
                }
                break;
            case ConsoleKey.N:
                IsInputMode.Value = true;
                InputText.Value = "";
                _detailContentChanged.OnNext(Unit.Default);
                break;
            case ConsoleKey.D:
                if (key.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift))
                {
                    _ = PruneAsync();
                }
                else
                {
                    _ = DeleteSelectedAsync();
                }

                break;
            case ConsoleKey.P:
                if (ActiveSubTab.Value == DockerSubTab.Container && GetSelectedItem?.Invoke() is { } pinContainer)
                {
                    _pinService.ToggleContainerPin(pinContainer.Id);
                    ApplyFilter();
                }
                break;
            case ConsoleKey.S: if (ActiveSubTab.Value == DockerSubTab.Container)
                {
                    ActionOnSelected();
                }

                break;
            case ConsoleKey.X: if (ActiveSubTab.Value == DockerSubTab.Container)
                {
                    ActionOnSelected(ActionType.Stop);
                }

                break;
            case ConsoleKey.R: if (ActiveSubTab.Value == DockerSubTab.Container)
                {
                    ActionOnSelected(ActionType.Restart);
                }

                break;
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
        if (_dockerActorRef is null)
        {
            return;
        }

        try
        {
            var result = await _dockerActorRef.Ask<object>(new GetContainerLogs(containerId), TimeSpan.FromSeconds(10));
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
        if (_dockerActorRef is null || SelectedContainer.Value is not { } container)
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
            var result = await _dockerActorRef.Ask<object>(msg, TimeSpan.FromSeconds(10));
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
        if (_dockerActorRef is null)
        {
            return;
        }

        var selected = GetSelectedDisplayItem?.Invoke();
        if (selected is { IsGroup: true, GroupName: { } groupName })
        {
            var containers = FilteredContainers.Value
                .Where(c => c.ComposeProject == groupName)
                .ToList();
            if (containers.Count == 0)
            {
                return;
            }

            var actionName = action switch { ActionType.Stop => "Stopping", ActionType.Restart => "Restarting", _ => "Starting" };
            _toast.Show($"{actionName} {containers.Count} containers...", new ToastOptions(Duration: TimeSpan.FromSeconds(2)));

            var tasks = containers.Select(c => ExecuteContainerActionAsync(c.Id, action));
            await Task.WhenAll(tasks);

            _toast.Show($"{action} completed for {groupName}", new ToastOptions(Duration: TimeSpan.FromSeconds(3)));
            return;
        }

        if (GetSelectedItem?.Invoke() is not { } container)
        {
            return;
        }

        await ExecuteContainerActionAsync(container.Id, action);
    }

    private async Task ExecuteContainerActionAsync(string containerId, ActionType action)
    {
        if (_dockerActorRef is null)
        {
            return;
        }

        object msg = action switch
        {
            ActionType.Stop => new StopContainer(containerId),
            ActionType.Restart => new RestartContainer(containerId),
            _ => new StartContainer(containerId),
        };
        try
        {
            var result = await _dockerActorRef.Ask<object>(msg, TimeSpan.FromSeconds(30));
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

    private async Task LoadSubTabDataAsync()
    {
        if (_dockerActorRef is null)
        {
            return;
        }

        try
        {
            switch (ActiveSubTab.Value)
            {
                case DockerSubTab.Networks:
                    var netResult = await _dockerActorRef.Ask<object>(new GetNetworks(), TimeSpan.FromSeconds(5));
                    if (netResult is NetworksResult nr)
                    {
                        Networks.Value = nr.Networks.ToList();
                    }

                    break;
                case DockerSubTab.Volumes:
                    var volResult = await _dockerActorRef.Ask<object>(new GetVolumes(), TimeSpan.FromSeconds(5));
                    if (volResult is VolumesResult vr)
                    {
                        Volumes.Value = vr.Volumes.ToList();
                    }

                    break;
                case DockerSubTab.Images:
                    var imgResult = await _dockerActorRef.Ask<object>(new GetImages(), TimeSpan.FromSeconds(5));
                    if (imgResult is ImagesResult ir)
                    {
                        Images.Value = ir.Images.ToList();
                    }

                    break;
            }
        }
        catch (Exception ex) { Trace.Warning(this, "Failed to load sub-tab data: {0}", ex.Message); }
        _detailContentChanged.OnNext(Unit.Default);
    }

    private async Task DeleteSelectedAsync()
    {
        if (_dockerActorRef is null)
        {
            return;
        }

        object? msg = ActiveSubTab.Value switch
        {
            DockerSubTab.Networks when GetSelectedNetwork?.Invoke() is { } n => new DeleteNetwork(n.Id),
            DockerSubTab.Volumes when GetSelectedVolume?.Invoke() is { } v => new DeleteVolume(v.Name),
            DockerSubTab.Images when GetSelectedImage?.Invoke() is { } i => new DeleteImage(i.Id),
            _ => null
        };
        if (msg is null)
        {
            return;
        }

        try
        {
            var result = await _dockerActorRef.Ask<object>(msg, TimeSpan.FromSeconds(10));
            if (result is ActionSuccess s)
            {
                _toast.Show(s.Message, new ToastOptions(Duration: TimeSpan.FromSeconds(3)));
            }
            else if (result is ActionFailure f)
            {
                _toast.Show("Error: " + f.Error, new ToastOptions(Position: ToastPosition.TopCenter, Color: Color.BrightRed, Duration: TimeSpan.FromSeconds(5)));
            }

            _ = LoadSubTabDataAsync();
        }
        catch (Exception ex)
        {
            _toast.Show("Error: " + ex.Message, new ToastOptions(Position: ToastPosition.TopCenter, Color: Color.BrightRed, Duration: TimeSpan.FromSeconds(5)));
        }
    }

    private async Task PruneAsync()
    {
        if (_dockerActorRef is null)
        {
            return;
        }

        object? msg = ActiveSubTab.Value switch
        {
            DockerSubTab.Volumes => new PruneVolumes(),
            DockerSubTab.Images => new PruneImages(),
            _ => null
        };
        if (msg is null)
        {
            return;
        }

        try
        {
            var result = await _dockerActorRef.Ask<object>(msg, TimeSpan.FromSeconds(30));
            if (result is ActionSuccess s)
            {
                _toast.Show(s.Message, new ToastOptions(Duration: TimeSpan.FromSeconds(3)));
            }
            else if (result is ActionFailure f)
            {
                _toast.Show("Error: " + f.Error, new ToastOptions(Position: ToastPosition.TopCenter, Color: Color.BrightRed, Duration: TimeSpan.FromSeconds(5)));
            }

            _ = LoadSubTabDataAsync();
        }
        catch (Exception ex)
        {
            _toast.Show("Error: " + ex.Message, new ToastOptions(Position: ToastPosition.TopCenter, Color: Color.BrightRed, Duration: TimeSpan.FromSeconds(5)));
        }
    }

    private async Task SubmitInputAsync()
    {
        if (_dockerActorRef is null || string.IsNullOrWhiteSpace(InputText.Value))
        {
            return;
        }

        var text = InputText.Value.Trim();
        IsInputMode.Value = false;
        InputText.Value = "";

        object msg = ActiveSubTab.Value switch
        {
            DockerSubTab.Networks => new CreateNetwork(text),
            DockerSubTab.Volumes => new CreateVolume(text),
            DockerSubTab.Images => new PullImage(text),
            _ => new PullImage(text) // Container tab: pull image
        };

        try
        {
            _toast.Show($"Working...", new ToastOptions(Duration: TimeSpan.FromSeconds(2)));
            var result = await _dockerActorRef.Ask<object>(msg, TimeSpan.FromSeconds(60));
            if (result is ActionSuccess s)
            {
                _toast.Show(s.Message, new ToastOptions(Duration: TimeSpan.FromSeconds(3)));
            }
            else if (result is ActionFailure f)
            {
                _toast.Show("Error: " + f.Error, new ToastOptions(Position: ToastPosition.TopCenter, Color: Color.BrightRed, Duration: TimeSpan.FromSeconds(5)));
            }

            _ = LoadSubTabDataAsync();
        }
        catch (Exception ex)
        {
            _toast.Show("Error: " + ex.Message, new ToastOptions(Position: ToastPosition.TopCenter, Color: Color.BrightRed, Duration: TimeSpan.FromSeconds(5)));
        }
        _detailContentChanged.OnNext(Unit.Default);
    }

    public string InputPromptLabel => ActiveSubTab.Value switch
    {
        DockerSubTab.Networks => "Network Name",
        DockerSubTab.Volumes => "Volume Name",
        DockerSubTab.Images => "Image to Pull",
        _ => "Image to Pull"
    };

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

    public override void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        ActiveSubTab.Dispose();
        AllContainers.Dispose(); FilteredContainers.Dispose(); SearchText.Dispose();
        Networks.Dispose(); Volumes.Dispose(); Images.Dispose();
        IsSearchActive.Dispose(); StatusMessage.Dispose(); IsDetailOpen.Dispose();
        SelectedContainer.Dispose(); IsSettingsOpen.Dispose(); LogContent.Dispose();
        IsInputMode.Dispose(); InputText.Dispose();
        _detailContentChanged.Dispose(); _settingsContentChanged.Dispose();
        base.Dispose();
    }
}
