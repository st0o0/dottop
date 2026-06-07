using Akka.Actor;
using Akka.Hosting;
using R3;
using dottop.Actors;
using dottop.Core.Messages;
using dottop.Core.Models;
using dottop.Nodes;
using dottop.Resources;
using dottop.Services;
using Termina.Input;
using Termina.Reactive;

namespace dottop.Pages;

public enum SortColumn { Name, Cpu, Ram, Pid }

public class ProcessesViewModel : ReactiveViewModel
{
    private readonly IRequiredActor<MonitoringSupervisor> _supervisor;
    private readonly SettingsService _settingsService;
    private IActorRef? _supervisorActor;
    private CancellationTokenSource? _cts;

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

    public ProcessesViewModel(
        IRequiredActor<MonitoringSupervisor> supervisor,
        SettingsService settingsService)
    {
        _supervisor = supervisor;
        _settingsService = settingsService;
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
            case ConsoleKey.D5: Navigate("/settings"); break;

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
            case ConsoleKey.LeftArrow:
                OverlayListNode = null;
                OverlayTabIndex.Value = Math.Max(0, OverlayTabIndex.Value - 1);
                UpdateStatus();
                _overlayContentChanged.OnNext(Unit.Default); LoadOverlayTab(); break;
            case ConsoleKey.RightArrow:
                OverlayListNode = null;
                OverlayTabIndex.Value = Math.Min(3, OverlayTabIndex.Value + 1);
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
                }
                break;
            case ConsoleKey.N:
                IsKillConfirmPending.Value = false;
                break;
        }
    }

    public void CloseOverlay()
    {
        IsOverlayOpen.Value = false;
        IsKillConfirmPending.Value = false;
        SelectedProcess.Value = null;
        ProcessTree.Value = null;
        ProcessEnv.Value = null;
        ProcessHandles.Value = null;
        OverlayListNode = null;
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
                    var tree = await _supervisorActor.Ask<ProcessTreeResult>(
                        new GetProcessTree(proc.Pid), TimeSpan.FromSeconds(5));
                    ProcessTree.Value = tree;
                    _overlayContentChanged.OnNext(Unit.Default);
                    break;
                case 2 when ProcessEnv.Value is null:
                    var env = await _supervisorActor.Ask<ProcessEnvironmentResult>(
                        new GetProcessEnvironment(proc.Pid), TimeSpan.FromSeconds(5));
                    ProcessEnv.Value = env.Variables;
                    _overlayContentChanged.OnNext(Unit.Default);
                    break;
                case 3 when ProcessHandles.Value is null:
                    var handles = await _supervisorActor.Ask<ProcessHandlesResult>(
                        new GetProcessHandles(proc.Pid), TimeSpan.FromSeconds(5));
                    ProcessHandles.Value = handles.Handles;
                    _overlayContentChanged.OnNext(Unit.Default);
                    break;
            }
        }
        catch { }
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
                ? " ←→: Tabs | K: Kill | Esc: Close"
                : " ←→: Tabs | ↑↓: Scroll | Esc: Close";
        }
        else
        {
            StatusMessage.Value = string.Format(Strings.ProcessStatusFormat, FilteredProcesses.Value.Count, SelectedGroup.Value?.ToString() ?? Strings.GroupAll, SortColumn.Value);
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
        AllProcesses.Dispose(); FilteredProcesses.Dispose();
        SearchText.Dispose(); SelectedGroup.Dispose();
        SortColumn.Dispose();
        IsSearchActive.Dispose(); IsOverlayOpen.Dispose();
        SelectedProcess.Dispose(); OverlayTabIndex.Dispose();
        StatusMessage.Dispose(); IsKillConfirmPending.Dispose(); ProcessTree.Dispose();
        ProcessEnv.Dispose(); ProcessHandles.Dispose();
        base.Dispose();
    }
}
