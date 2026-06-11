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
using Termina.Reactive;
using Termina.Terminal;

namespace dtop.Pages;

public enum OverviewSortField { CpuPercent, RamPercent, Name, Pid }

public class OverviewViewModel : ReactiveViewModel
{
    private readonly IRequiredActor<MonitoringSupervisor> _supervisor;
    private readonly IGpuMetrics _gpuMetrics;
    private readonly SettingsService _settingsService;
    private CancellationTokenSource? _cts;

    // ── Metrics ─────────────────────────────────────────────────────────────
    public ReactiveProperty<double> CpuTotal { get; } = new(0);
    public ReactiveProperty<IReadOnlyList<double>> CpuCores { get; } = new([]);
    public ReactiveProperty<string> CpuName { get; } = new("Loading...");
    public ReactiveProperty<ulong> RamTotal { get; } = new(0);
    public ReactiveProperty<ulong> RamUsed { get; } = new(0);
    public ReactiveProperty<IReadOnlyList<DiskSnapshot>> Disks { get; } = new([]);
    public ReactiveProperty<IReadOnlyList<NetworkSnapshot>> Networks { get; } = new([]);
    public ReactiveProperty<GpuSnapshot?> Gpu { get; } = new(null);
    public bool GpuAvailable => _gpuMetrics.IsAvailable;

    // ── Processes ────────────────────────────────────────────────────────────
    public ReactiveProperty<IReadOnlyList<ProcessSnapshot>> AllProcesses { get; } = new([]);

    // ── UI state ─────────────────────────────────────────────────────────────
    public ReactiveProperty<string> ProcessFilter { get; } = new("");
    public ReactiveProperty<bool> IsFilterMode { get; } = new(false);
    public ReactiveProperty<bool> IsPaused { get; } = new(false);
    public ReactiveProperty<OverviewSortField> SortField { get; } = new(OverviewSortField.CpuPercent);
    public ReactiveProperty<bool> SortDescending { get; } = new(true);
    public ReactiveProperty<int> ActivePreset { get; }
    public ReactiveProperty<string> StatusHint { get; } = new("");

    // Panel visibility toggles (NumPad1–4 keys)
    public ReactiveProperty<bool> ShowCpu { get; } = new(true);
    public ReactiveProperty<bool> ShowMemory { get; } = new(true);
    public ReactiveProperty<bool> ShowNetDisk { get; } = new(true);
    public ReactiveProperty<bool> ShowProcesses { get; } = new(true);

    public GraphStyle GraphStyleSetting { get; }

    // ListNode reference set by OverviewPage so arrow keys can scroll it
    public IScrollableList? ProcessListNode { get; set; }

    private static readonly string[] PresetNames = ["Standard", "CPU Focus", "Resource Grid", "Minimal"];

    public OverviewViewModel(
        IRequiredActor<MonitoringSupervisor> supervisor,
        IGpuMetrics gpuMetrics,
        SettingsService settingsService)
    {
        _supervisor = supervisor;
        _gpuMetrics = gpuMetrics;
        _settingsService = settingsService;

        ActivePreset = new ReactiveProperty<int>(
            Math.Clamp(settingsService.Settings.OverviewPreset, 0, 3));

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
        if (IsFilterMode.Value)
        {
            StatusHint.Value = ProcessFilter.Value.Length > 0
                ? $" Filter: \"{ProcessFilter.Value}\"  [Esc] Clear"
                : " Filter: █   (type to filter processes, Esc to cancel)";
            return;
        }

        var cpu = $"CPU: {CpuTotal.Value:F1}%";
        var ramUsedGb = RamUsed.Value / 1024.0 / 1024 / 1024;
        var ramTotalGb = RamTotal.Value / 1024.0 / 1024 / 1024;
        var ram = $"RAM: {ramUsedGb:F1}/{ramTotalGb:F1} GiB";
        var layout = $"Layout: {PresetNames[ActivePreset.Value]}";
        var sort = $"[M] Sort: {SortField.Value}";
        var dir = SortDescending.Value ? "↓" : "↑";
        StatusHint.Value = $" {cpu}  {ram}  |  {layout}  |  [F] Filter  [P] Preset  {sort}  [R] {dir}";
    }

    private async ValueTask InitializeAsync()
    {
        var ct = _cts!.Token;
        var supervisor = await _supervisor.GetAsync(ct);

        _ = ConnectStream<CpuSnapshot>(supervisor, new StartCpuMonitoring(), ct, snapshot =>
        {
            if (IsPaused.Value) return;
            CpuName.Value = snapshot.Name;
            CpuTotal.Value = snapshot.TotalPercent;
            CpuCores.Value = snapshot.CorePercents;
            if (!IsFilterMode.Value) UpdateStatusHint();
        });

        _ = ConnectStream<MemorySnapshot>(supervisor, new StartMemoryMonitoring(), ct, snapshot =>
        {
            if (IsPaused.Value) return;
            RamTotal.Value = snapshot.TotalBytes;
            RamUsed.Value = snapshot.UsedBytes;
            if (!IsFilterMode.Value) UpdateStatusHint();
        });

        _ = ConnectStream<List<DiskSnapshot>>(supervisor, new StartDiskMonitoring(), ct, disks =>
        {
            if (IsPaused.Value) return;
            Disks.Value = disks;
        });

        _ = ConnectStream<List<NetworkSnapshot>>(supervisor, new StartNetworkMonitoring(), ct, nets =>
        {
            if (IsPaused.Value) return;
            Networks.Value = nets;
        });

        _ = ConnectStream<List<ProcessSnapshot>>(supervisor, new StartProcessMonitoring(), ct, processes =>
        {
            if (IsPaused.Value) return;
            AllProcesses.Value = processes;
        });

        if (_gpuMetrics.IsAvailable)
        {
            _ = ConnectStream<GpuSnapshot>(supervisor, new StartGpuMonitoring(), ct, snapshot =>
            {
                if (IsPaused.Value) return;
                Gpu.Value = snapshot;
            });
        }
    }

    private async ValueTask ConnectStream<TData>(IActorRef supervisor, object startMessage,
        CancellationToken ct, Action<TData> handler)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var stream = await supervisor.Ask<MonitoringStream<TData>>(startMessage, TimeSpan.FromSeconds(60));
                await foreach (var item in stream.Data.WithCancellation(ct))
                {
                    handler(item);
                }
            }
            catch (OperationCanceledException) { return; }
            catch
            {
                try { await Task.Delay(2000, ct); } catch (OperationCanceledException) { return; }
            }
        }
    }

    public IReadOnlyList<ProcessSnapshot> GetFilteredProcesses()
    {
        var source = AllProcesses.Value.AsEnumerable();

        if (!string.IsNullOrEmpty(ProcessFilter.Value))
        {
            source = source.Where(p =>
                p.Name.Contains(ProcessFilter.Value, StringComparison.OrdinalIgnoreCase) ||
                p.Pid.ToString().Contains(ProcessFilter.Value));
        }

        source = SortField.Value switch
        {
            OverviewSortField.CpuPercent => SortDescending.Value
                ? source.OrderByDescending(p => p.CpuPercent)
                : source.OrderBy(p => p.CpuPercent),
            OverviewSortField.RamPercent => SortDescending.Value
                ? source.OrderByDescending(p => p.WorkingSetBytes)
                : source.OrderBy(p => p.WorkingSetBytes),
            OverviewSortField.Name => SortDescending.Value
                ? source.OrderByDescending(p => p.Name)
                : source.OrderBy(p => p.Name),
            OverviewSortField.Pid => SortDescending.Value
                ? source.OrderByDescending(p => p.Pid)
                : source.OrderBy(p => p.Pid),
            _ => source.OrderByDescending(p => p.CpuPercent)
        };

        return source.ToList();
    }

    private void HandleKey(KeyPressed key)
    {
        if (IsFilterMode.Value)
        {
            HandleFilterKey(key);
            return;
        }

        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.P:
                var next = (ActivePreset.Value + 1) % 4;
                ActivePreset.Value = next;
                _settingsService.Settings.OverviewPreset = next;
                _settingsService.Save();
                UpdateStatusHint();
                break;

            case ConsoleKey.D1: Navigate("/"); break;
            case ConsoleKey.D2: Navigate("/performance"); break;
            case ConsoleKey.D3: Navigate("/services"); break;
            case ConsoleKey.D4: Navigate("/network"); break;
            case ConsoleKey.D5: Navigate("/docker"); break;

            // NOTE: Panel toggles use NumPad1-4 because D1-D5 are reserved for cross-tab
            // navigation (D1=Processes, D2=Performance, etc.). btop uses 1-4 because it
            // has no multi-page navigation; we resolve the conflict by using NumPad keys.
            case ConsoleKey.NumPad1:
                if (CountVisible() > 1 || !ShowCpu.Value) ShowCpu.Value = !ShowCpu.Value;
                break;
            case ConsoleKey.NumPad2:
                if (CountVisible() > 1 || !ShowMemory.Value) ShowMemory.Value = !ShowMemory.Value;
                break;
            case ConsoleKey.NumPad3:
                if (CountVisible() > 1 || !ShowNetDisk.Value) ShowNetDisk.Value = !ShowNetDisk.Value;
                break;
            case ConsoleKey.NumPad4:
                if (CountVisible() > 1 || !ShowProcesses.Value) ShowProcesses.Value = !ShowProcesses.Value;
                break;

            case ConsoleKey.UpArrow: ProcessListNode?.MoveUp(); break;
            case ConsoleKey.DownArrow: ProcessListNode?.MoveDown(); break;
            case ConsoleKey.PageUp: ProcessListNode?.PageUp(); break;
            case ConsoleKey.PageDown: ProcessListNode?.PageDown(); break;

            case ConsoleKey.M:
                SortField.Value = SortField.Value switch
                {
                    OverviewSortField.CpuPercent => OverviewSortField.RamPercent,
                    OverviewSortField.RamPercent => OverviewSortField.Name,
                    OverviewSortField.Name => OverviewSortField.Pid,
                    _ => OverviewSortField.CpuPercent,
                };
                UpdateStatusHint();
                break;

            case ConsoleKey.R:
                SortDescending.Value = !SortDescending.Value;
                UpdateStatusHint();
                break;

            case ConsoleKey.F:
                IsFilterMode.Value = true;
                UpdateStatusHint();
                break;

            case ConsoleKey.Spacebar:
                IsPaused.Value = !IsPaused.Value;
                break;

            case ConsoleKey.Q or ConsoleKey.Escape:
                Shutdown();
                break;
        }
    }

    private void HandleFilterKey(KeyPressed key)
    {
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.Escape:
                IsFilterMode.Value = false;
                ProcessFilter.Value = "";
                UpdateStatusHint();
                break;
            case ConsoleKey.Backspace:
                if (ProcessFilter.Value.Length > 0)
                {
                    ProcessFilter.Value = ProcessFilter.Value[..^1];
                    UpdateStatusHint();
                }
                break;
            default:
                if (key.KeyInfo.KeyChar is >= ' ' and <= '~')
                {
                    ProcessFilter.Value += key.KeyInfo.KeyChar;
                    UpdateStatusHint();
                }
                break;
        }
    }

    private int CountVisible() =>
        (ShowCpu.Value ? 1 : 0) + (ShowMemory.Value ? 1 : 0) +
        (ShowNetDisk.Value ? 1 : 0) + (ShowProcesses.Value ? 1 : 0);

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
        AllProcesses.Dispose();
        ProcessFilter.Dispose();
        IsFilterMode.Dispose();
        IsPaused.Dispose();
        SortField.Dispose();
        SortDescending.Dispose();
        ActivePreset.Dispose();
        StatusHint.Dispose();
        ShowCpu.Dispose();
        ShowMemory.Dispose();
        ShowNetDisk.Dispose();
        ShowProcesses.Dispose();
        base.Dispose();
    }
}
