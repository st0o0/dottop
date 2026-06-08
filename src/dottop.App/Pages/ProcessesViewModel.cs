using Akka.Actor;
using Akka.Hosting;
using dottop.App.Actors;
using dottop.App.Nodes;
using dottop.App.Resources;
using dottop.App.Services;
using dottop.Core.Messages;
using dottop.Core.Models;
using R3;
using Servus;
using Servus.Diagnostics;
using Termina.Input;
using Termina.Notifications;
using Termina.Reactive;
using Termina.Terminal;

namespace dottop.App.Pages;

public enum SortColumn { Name, Cpu, Ram, Pid }

public class ProcessesViewModel : ReactiveViewModel
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("ViewModel.Processes");
    private readonly IRequiredActor<MonitoringSupervisor> _supervisor;
    private readonly SettingsService _settingsService;
    private readonly UpdateService _updateService;
    private readonly IToastService _toast;
    private IActorRef? _supervisorActor;
    private CancellationTokenSource? _cts;

    private readonly Dictionary<int, Queue<double>> _cpuHistory = new();
    private const int CpuHistoryLength = 8;

    public IScrollableList? ListNode { get; set; }
    public IScrollableList? OverlayListNode { get; set; }
    public Func<ProcessSnapshot?>? GetSelectedItem { get; set; }

    private readonly Subject<Unit> _overlayContentChanged = new();
    public Observable<Unit> OverlayContentChanged => _overlayContentChanged.AsObservable();

    public ReactiveProperty<List<ProcessSnapshot>> AllProcesses { get; } = new([]);
    public ReactiveProperty<List<ProcessSnapshot>> FilteredProcesses { get; } = new([]);
    public ReactiveProperty<string> SearchText { get; } = new("");
    public ReactiveProperty<ProcessGroup?> SelectedGroup { get; } = new((ProcessGroup?)null);
    public ReactiveProperty<SortColumn> SortColumn { get; } = new(Pages.SortColumn.Ram);
    public ReactiveProperty<bool> IsSearchActive { get; } = new(false);
    public ReactiveProperty<bool> IsOverlayOpen { get; } = new(false);
    public ReactiveProperty<ProcessSnapshot?> SelectedProcess { get; } = new(null);
    public ReactiveProperty<int> OverlayTabIndex { get; } = new(0);
    public ReactiveProperty<string> StatusMessage { get; } = new("");
    public ReactiveProperty<ProcessTreeResult?> ProcessTree { get; } = new(null);
    public ReactiveProperty<IReadOnlyDictionary<string, string>?> ProcessEnv { get; } = new(null);
    public ReactiveProperty<IReadOnlyList<string>?> ProcessHandles { get; } = new(null);
    public ReactiveProperty<bool> IsKillConfirmPending { get; } = new(false);
    public ReactiveProperty<bool> IsSettingsOpen { get; } = new(false);

    private readonly Subject<Unit> _settingsContentChanged = new();
    public Observable<Unit> SettingsContentChanged => _settingsContentChanged.AsObservable();

    private static readonly int[] RefreshOptions = [250, 500, 1000, 2000, 5000];

    public ProcessesViewModel(
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
        SortColumn.Value = _settingsService.Settings.DefaultSort switch
        {
            "cpu" => Pages.SortColumn.Cpu,
            "name" => Pages.SortColumn.Name,
            "pid" => Pages.SortColumn.Pid,
            _ => Pages.SortColumn.Ram,
        };

        SelectedGroup.Value = _settingsService.Settings.DefaultGroup switch
        {
            "apps" => ProcessGroup.Apps,
            "background" => ProcessGroup.Background,
            "system" => ProcessGroup.Windows,
            _ => null,
        };

        _cts = new CancellationTokenSource();
        _ = InitializeAsync();

        SearchText.Subscribe(_ => ApplyFilter()).DisposeWith(Subscriptions);
        SelectedGroup.Subscribe(_ => ApplyFilter()).DisposeWith(Subscriptions);
        SortColumn.Subscribe(_ => ApplyFilter()).DisposeWith(Subscriptions);

        Input.OfType<IInputEvent, KeyPressed>()
            .Subscribe(HandleKey)
            .DisposeWith(Subscriptions);

        UpdateStatus();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var ct = _cts!.Token;

            _supervisorActor = await _supervisor.GetAsync(ct);

            var stream = await _supervisorActor.Ask<MonitoringStream<List<ProcessSnapshot>>>(
                new StartProcessMonitoring(), TimeSpan.FromSeconds(60));
            await foreach (var list in stream.Data.WithCancellation(ct))
            {
                AllProcesses.Value = list;
                UpdateCpuHistory(list);
                ApplyFilter();

                if (IsOverlayOpen.Value && SelectedProcess.Value is { } current
                    && OverlayTabIndex.Value == 0)
                {
                    var updated = list.FirstOrDefault(p => p.Pid == current.Pid);
                    if (updated is not null)
                    {
                        SelectedProcess.Value = updated;
                        _overlayContentChanged.OnNext(Unit.Default);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    private void ApplyFilter()
    {
        var source = AllProcesses.Value.AsEnumerable();

        if (!string.IsNullOrEmpty(SearchText.Value))
        {
            source = source.Where(p =>
                p.Name.Contains(SearchText.Value, StringComparison.OrdinalIgnoreCase) ||
                p.Pid.ToString().Contains(SearchText.Value));
        }

        if (SelectedGroup.Value is { } group)
        {
            source = source.Where(p => p.Group == group);
        }

        source = SortColumn.Value switch
        {
            Pages.SortColumn.Cpu => source.OrderByDescending(p => p.CpuPercent),
            Pages.SortColumn.Ram => source.OrderByDescending(p => p.WorkingSetBytes),
            Pages.SortColumn.Pid => source.OrderBy(p => p.Pid),
            Pages.SortColumn.Name => source.OrderBy(p => p.Name),
            _ => source
        };

        FilteredProcesses.Value = source.ToList();
        UpdateStatus();
    }

    private void HandleKey(KeyPressed key)
    {
        if (IsSearchActive.Value) { HandleSearchKey(key); return; }
        if (IsSettingsOpen.Value) { HandleSettingsKey(key); return; }
        if (IsOverlayOpen.Value) { HandleOverlayKey(key); return; }

        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.UpArrow: ListNode?.MoveUp(); break;
            case ConsoleKey.DownArrow: ListNode?.MoveDown(); break;
            case ConsoleKey.Home: ListNode?.MoveToTop(); break;
            case ConsoleKey.End: ListNode?.MoveToEnd(); break;
            case ConsoleKey.PageUp: ListNode?.PageUp(); break;
            case ConsoleKey.PageDown: ListNode?.PageDown(); break;
            case ConsoleKey.Enter:
                if (GetSelectedItem?.Invoke() is { } proc)
                {
                    SelectedProcess.Value = proc;
                    OverlayTabIndex.Value = 0;
                    IsOverlayOpen.Value = true;
                    UpdateStatus();
                    _overlayContentChanged.OnNext(Unit.Default);
                    LoadOverlayTab();
                }
                break;
            default:
                if (key.KeyInfo.KeyChar == '/')
                {
                    IsSearchActive.Value = true;
                }

                break;
            case ConsoleKey.Tab or ConsoleKey.F6: CycleSortColumn(); break;
            case ConsoleKey.G: CycleGroupFilter(); break;
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

    private void HandleSearchKey(KeyPressed key)
    {
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.Escape: IsSearchActive.Value = false; SearchText.Value = ""; break;
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
    }

    private void HandleOverlayKey(KeyPressed key)
    {
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.Escape:
                if (IsKillConfirmPending.Value)
                {
                    IsKillConfirmPending.Value = false;
                }
                else
                {
                    CloseOverlay();
                }

                break;
            case ConsoleKey.Tab:
                OverlayListNode = null;
                if (key.KeyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift))
                    OverlayTabIndex.Value = OverlayTabIndex.Value <= 0 ? 3 : OverlayTabIndex.Value - 1;
                else
                    OverlayTabIndex.Value = OverlayTabIndex.Value >= 3 ? 0 : OverlayTabIndex.Value + 1;
                UpdateStatus();
                _overlayContentChanged.OnNext(Unit.Default); LoadOverlayTab(); break;
            case ConsoleKey.UpArrow: OverlayListNode?.MoveUp(); break;
            case ConsoleKey.DownArrow: OverlayListNode?.MoveDown(); break;
            case ConsoleKey.Home: OverlayListNode?.MoveToTop(); break;
            case ConsoleKey.End: OverlayListNode?.MoveToEnd(); break;
            case ConsoleKey.PageUp: OverlayListNode?.PageUp(); break;
            case ConsoleKey.PageDown: OverlayListNode?.PageDown(); break;
            case ConsoleKey.K:
                if (SelectedProcess.Value is not null && _supervisorActor is not null)
                {
                    IsKillConfirmPending.Value = true;
                }

                break;
            case ConsoleKey.Y:
                if (IsKillConfirmPending.Value && SelectedProcess.Value is { } killTarget && _supervisorActor is not null)
                {
                    _supervisorActor.Tell(new KillProcess(killTarget.Pid));
                    IsKillConfirmPending.Value = false;
                    _toast.Show($"Process {killTarget.Name} ({killTarget.Pid}) killed", new ToastOptions(Duration: TimeSpan.FromSeconds(3)));
                }
                break;
            case ConsoleKey.N:
                IsKillConfirmPending.Value = false;
                break;
        }
    }

    public void CloseOverlay()
    {
        // Clear data before setting flags to avoid rendering null data
        ProcessTree.Value = null;
        ProcessEnv.Value = null;
        ProcessHandles.Value = null;
        SelectedProcess.Value = null;
        OverlayListNode = null;
        // Then update state — IsOverlayOpen triggers UI update, data should be cleared first
        IsKillConfirmPending.Value = false;
        IsOverlayOpen.Value = false;
        UpdateStatus();
    }

    public async void LoadOverlayTab()
    {
        if (SelectedProcess.Value is not { } proc || _supervisorActor is null)
        {
            return;
        }

        try
        {
            switch (OverlayTabIndex.Value)
            {
                case 1 when ProcessTree.Value is null:
                    var treeResponse = await _supervisorActor.Ask<object>(
                        new GetProcessTree(proc.Pid), TimeSpan.FromSeconds(10));
                    if (treeResponse is ProcessTreeResult tree)
                    {
                        ProcessTree.Value = tree;
                        _overlayContentChanged.OnNext(Unit.Default);
                    }
                    else if (treeResponse is ActionFailure treeFail)
                    {
                        _toast?.Show(treeFail.Error, new ToastOptions(Color: Color.BrightRed, Icon: "⚠"));
                        ProcessTree.Value = new ProcessTreeResult(proc.Pid, proc.Name, []);
                        _overlayContentChanged.OnNext(Unit.Default);
                    }
                    break;
                case 2 when ProcessEnv.Value is null:
                    var envResponse = await _supervisorActor.Ask<object>(
                        new GetProcessEnvironment(proc.Pid), TimeSpan.FromSeconds(10));
                    if (envResponse is ProcessEnvironmentResult env)
                    {
                        ProcessEnv.Value = env.Variables;
                        _overlayContentChanged.OnNext(Unit.Default);
                    }
                    else if (envResponse is ActionFailure envFail)
                    {
                        _toast?.Show(envFail.Error, new ToastOptions(Color: Color.BrightRed, Icon: "⚠"));
                        ProcessEnv.Value = new Dictionary<string, string>();
                        _overlayContentChanged.OnNext(Unit.Default);
                    }
                    break;
                case 3 when ProcessHandles.Value is null:
                    var handlesResponse = await _supervisorActor.Ask<object>(
                        new GetProcessHandles(proc.Pid), TimeSpan.FromSeconds(10));
                    if (handlesResponse is ProcessHandlesResult handles)
                    {
                        ProcessHandles.Value = handles.Handles;
                        _overlayContentChanged.OnNext(Unit.Default);
                    }
                    else if (handlesResponse is ActionFailure handlesFail)
                    {
                        _toast?.Show(handlesFail.Error, new ToastOptions(Color: Color.BrightRed, Icon: "⚠"));
                        ProcessHandles.Value = [];
                        _overlayContentChanged.OnNext(Unit.Default);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            Trace.Warning(this, "Failed to load overlay tab {0}: {1}", OverlayTabIndex.Value, ex.Message);
            _toast.Show("⚠ Failed to load: " + ex.Message, new ToastOptions(Position: ToastPosition.TopCenter, Color: Color.BrightRed, Duration: TimeSpan.FromSeconds(5)));

            // Set fallback data so the UI doesn't show "Loading..." forever
            switch (OverlayTabIndex.Value)
            {
                case 1: ProcessTree.Value = new ProcessTreeResult(SelectedProcess.Value!.Pid, SelectedProcess.Value.Name, []); break;
                case 2: ProcessEnv.Value = new Dictionary<string, string>(); break;
                case 3: ProcessHandles.Value = []; break;
            }
            _overlayContentChanged.OnNext(Unit.Default);
        }
    }

    private void UpdateCpuHistory(List<ProcessSnapshot> processes)
    {
        var activePids = new HashSet<int>(processes.Count);
        foreach (var proc in processes)
        {
            activePids.Add(proc.Pid);
            if (!_cpuHistory.TryGetValue(proc.Pid, out var queue))
            {
                queue = new Queue<double>(CpuHistoryLength);
                _cpuHistory[proc.Pid] = queue;
            }
            queue.Enqueue(proc.CpuPercent);
            if (queue.Count > CpuHistoryLength)
                queue.Dequeue();
        }
        var stale = _cpuHistory.Keys.Where(pid => !activePids.Contains(pid)).ToList();
        foreach (var pid in stale)
            _cpuHistory.Remove(pid);
    }

    public IReadOnlyList<double> GetCpuHistory(int pid)
    {
        return _cpuHistory.TryGetValue(pid, out var queue) ? queue.ToArray() : [];
    }

    public long GetMaxWorkingSet()
    {
        var list = FilteredProcesses.Value;
        return list.Count == 0 ? 1 : list.Max(p => p.WorkingSetBytes);
    }

    private void CycleSortColumn()
    {
        SortColumn.Value = SortColumn.Value switch
        {
            Pages.SortColumn.Ram => Pages.SortColumn.Cpu,
            Pages.SortColumn.Cpu => Pages.SortColumn.Name,
            Pages.SortColumn.Name => Pages.SortColumn.Pid,
            _ => Pages.SortColumn.Ram,
        };
    }

    private void CycleGroupFilter()
    {
        SelectedGroup.Value = SelectedGroup.Value switch
        {
            null => ProcessGroup.Apps,
            ProcessGroup.Apps => ProcessGroup.Background,
            ProcessGroup.Background => ProcessGroup.Windows,
            _ => null,
        };
    }

    private void UpdateStatus()
    {
        if (IsOverlayOpen.Value)
        {
            StatusMessage.Value = OverlayTabIndex.Value == 0
                ? Strings.HintProcessOverviewKeys
                : Strings.HintProcessDetailKeys;
        }
        else
        {
            StatusMessage.Value = string.Format(Strings.ProcessStatusFormat, FilteredProcesses.Value.Count, SelectedGroup.Value?.ToString() ?? Strings.GroupAll, SortColumn.Value);
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
        if (idx < 0) idx = 2; // default to 1000ms
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
        _cpuHistory.Clear();
        base.OnDeactivating();
    }

    public override void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        AllProcesses.Dispose(); FilteredProcesses.Dispose();
        SearchText.Dispose(); SelectedGroup.Dispose();
        SortColumn.Dispose();
        IsSearchActive.Dispose(); IsOverlayOpen.Dispose();
        SelectedProcess.Dispose(); OverlayTabIndex.Dispose();
        StatusMessage.Dispose(); IsKillConfirmPending.Dispose(); IsSettingsOpen.Dispose();
        _settingsContentChanged.Dispose(); ProcessTree.Dispose();
        ProcessEnv.Dispose(); ProcessHandles.Dispose();
        base.Dispose();
    }
}
