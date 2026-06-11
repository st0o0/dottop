using dtop.Core.Models;
using dtop.Nodes;
using dtop.Resources;
using dtop.Themes;
using R3;
using Termina.Extensions;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dtop.Pages;

public class OverviewPage : ReactivePage<OverviewViewModel>
{
    private GraphNode? _cpuGraph;
    private GraphNode? _gpuGraph;
    private GraphNode? _ramGraph;
    private CpuCoresNode? _coresNode;
    private DataListNode<ProcessSnapshot>? _processList;

    private readonly MetricHistory _cpuHistory = new();
    private readonly MetricHistory _ramHistory = new();
    private readonly MetricHistory _gpuHistory = new();

    public override ILayoutNode BuildLayout()
    {
        var graphStyle = ViewModel.GraphStyleSetting;

        _cpuGraph = new GraphNode()
            .WithStyle(graphStyle)
            .WithColor(ThemeService.Instance.Current.Accent)
            .WithRange(0, 100);

        _ramGraph = new GraphNode()
            .WithStyle(graphStyle)
            .WithColor(ThemeService.Instance.Current.Accent)
            .WithRange(0, 100);

        _gpuGraph = new GraphNode()
            .WithStyle(graphStyle)
            .WithColor(ThemeService.Instance.Current.Accent)
            .WithRange(0, 100);

        _coresNode = new CpuCoresNode();

        _processList = new DataListNode<ProcessSnapshot>(
            p =>
            {
                var ramMb = p.WorkingSetBytes / 1024 / 1024;
                var name = p.Name.Length > 22 ? p.Name[..21] + "…" : p.Name;
                var ramStr = ramMb >= 1024 ? $"{ramMb / 1024.0,4:F1}GB" : $"{ramMb,4}MB";
                return $" {p.Pid,6}  {name,-22} {p.CpuPercent,5:F1}%  {ramStr,7}";
            },
            p => p.CpuPercent switch
            {
                > 80 => Color.BrightRed,
                > 50 => Color.BrightYellow,
                _ => ThemeService.Instance.Current.Foreground,
            });

        ViewModel.ProcessListNode = _processList;

        return Layouts.Vertical()
            .WithChild(new TabBarNode(0))
            .WithChild(ViewModel.ActivePreset
                .CombineLatest(ViewModel.ShowCpu, ViewModel.ShowMemory, ViewModel.ShowNetDisk, ViewModel.ShowProcesses,
                    (preset, _, _, _, _) => BuildGridForPreset(preset))
                .AsLayout().Fill())
            .WithChild(ViewModel.StatusHint
                .Select<string, ILayoutNode>(hint =>
                    new TextNode(hint).WithForeground(ThemeService.Instance.Current.TextDim).WithBackground(ThemeService.Instance.Current.StatusBar))
                .AsLayout().Height(1));
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();

        Observable.Interval(TimeSpan.FromMilliseconds(500))
            .Subscribe(_ =>
            {
                if (ViewModel.IsPaused.Value) return;

                _cpuHistory.Push(ViewModel.CpuTotal.Value);
                _cpuGraph?.SetData(_cpuHistory.Snapshot());

                var total = ViewModel.RamTotal.Value;
                var used = ViewModel.RamUsed.Value;
                _ramHistory.Push(total > 0 ? (double)used / total * 100 : 0);
                _ramGraph?.SetData(_ramHistory.Snapshot());

                if (ViewModel is { GpuAvailable: true, Gpu.Value: { } gpu })
                {
                    _gpuHistory.Push(gpu.UsagePercent);
                    _gpuGraph?.SetData(_gpuHistory.Snapshot());
                }
            })
            .DisposeWith(Subscriptions);

        ViewModel.CpuCores.Subscribe(cores => _coresNode?.SetCores(cores))
            .DisposeWith(Subscriptions);

        Observable.Merge(
                ViewModel.AllProcesses.Select(_ => Unit.Default),
                ViewModel.ProcessFilter.Select(_ => Unit.Default),
                ViewModel.SortField.Select(_ => Unit.Default),
                ViewModel.SortDescending.Select(_ => Unit.Default))
            .Subscribe(_ => _processList?.SetItems(ViewModel.GetFilteredProcesses()))
            .DisposeWith(Subscriptions);
    }

    // ── Preset layouts ───────────────────────────────────────────────────────

    private ILayoutNode BuildGridForPreset(int preset) => preset switch
    {
        1 => BuildPreset1CpuFocus(),
        2 => BuildPreset2ResourceGrid(),
        3 => BuildPreset3Minimal(),
        _ => BuildPreset0Standard(),
    };

    /// <summary>Preset 0 – Standard: top CPU[+GPU], middle MEM|NET/DISK, bottom Processes</summary>
    private ILayoutNode BuildPreset0Standard()
    {
        var showCpu = ViewModel.ShowCpu.Value;
        var showMem = ViewModel.ShowMemory.Value;
        var showNet = ViewModel.ShowNetDisk.Value;
        var showProc = ViewModel.ShowProcesses.Value;

        if (ViewModel.GpuAvailable)
        {
            var hasMiddleRow = showMem || showNet;

            GridNode grid;
            if (hasMiddleRow)
            {
                grid = new GridNode(3, 2)
                    .WithColumnWidths(new SizeConstraint.Percent(60), new SizeConstraint.Fill())
                    .WithRowHeights(
                        new SizeConstraint.Percent(30),
                        new SizeConstraint.Percent(20),
                        new SizeConstraint.Fill());

                if (showCpu) grid.SetCell(0, 0, BuildCpuPanel());
                grid.SetCell(0, showCpu ? 1 : 0, BuildGpuPanel(), colSpan: showCpu ? 1 : 2);

                switch (showMem)
                {
                    case true when showNet:
                        grid.SetCell(1, 0, BuildMemoryPanel());
                        grid.SetCell(1, 1, BuildNetDiskPanel());
                        break;
                    case true:
                        grid.SetCell(1, 0, BuildMemoryPanel(), colSpan: 2);
                        break;
                    default:
                    {
                        if (showNet)
                        {
                            grid.SetCell(1, 0, BuildNetDiskPanel(), colSpan: 2);
                        }

                        break;
                    }
                }

                if (showProc) grid.SetCell(2, 0, BuildProcessPanel(), colSpan: 2);
            }
            else
            {
                grid = new GridNode(2, 2)
                    .WithColumnWidths(new SizeConstraint.Percent(60), new SizeConstraint.Fill())
                    .WithRowHeights(
                        new SizeConstraint.Percent(30),
                        new SizeConstraint.Fill());

                if (showCpu) grid.SetCell(0, 0, BuildCpuPanel());
                grid.SetCell(0, showCpu ? 1 : 0, BuildGpuPanel(), colSpan: showCpu ? 1 : 2);

                if (showProc) grid.SetCell(1, 0, BuildProcessPanel(), colSpan: 2);
            }

            return grid;
        }
        else
        {
            // No GPU: 2-column grid, CPU spans both columns
            var hasMiddleRow = showMem || showNet;

            GridNode grid;
            if (hasMiddleRow)
            {
                grid = new GridNode(3, 2)
                    .WithColumnWidths(new SizeConstraint.Percent(60), new SizeConstraint.Fill())
                    .WithRowHeights(
                        new SizeConstraint.Percent(30),
                        new SizeConstraint.Percent(20),
                        new SizeConstraint.Fill());

                if (showCpu) grid.SetCell(0, 0, BuildCpuPanel(), colSpan: 2);

                switch (showMem)
                {
                    case true when showNet:
                        grid.SetCell(1, 0, BuildMemoryPanel());
                        grid.SetCell(1, 1, BuildNetDiskPanel());
                        break;
                    case true:
                        grid.SetCell(1, 0, BuildMemoryPanel(), colSpan: 2);
                        break;
                    default:
                    {
                        if (showNet)
                        {
                            grid.SetCell(1, 0, BuildNetDiskPanel(), colSpan: 2);
                        }

                        break;
                    }
                }

                if (showProc) grid.SetCell(2, 0, BuildProcessPanel(), colSpan: 2);
            }
            else
            {
                grid = new GridNode(2, 2)
                    .WithColumnWidths(new SizeConstraint.Percent(60), new SizeConstraint.Fill())
                    .WithRowHeights(
                        new SizeConstraint.Percent(30),
                        new SizeConstraint.Fill());

                if (showCpu) grid.SetCell(0, 0, BuildCpuPanel(), colSpan: 2);

                if (showProc) grid.SetCell(1, 0, BuildProcessPanel(), colSpan: 2);
            }

            return grid;
        }
    }

    /// <summary>Preset 1 – CPU Focus: top CPU (full-width), bottom Processes (full-width)</summary>
    private ILayoutNode BuildPreset1CpuFocus()
    {
        var showCpu = ViewModel.ShowCpu.Value;
        var showProc = ViewModel.ShowProcesses.Value;

        var grid = new GridNode(2, 2)
            .WithColumnWidths(new SizeConstraint.Fill(), new SizeConstraint.Fill())
            .WithRowHeights(new SizeConstraint.Percent(50), new SizeConstraint.Fill());

        if (showCpu) grid.SetCell(0, 0, BuildCpuPanel(), colSpan: 2);
        if (showProc) grid.SetCell(1, 0, BuildProcessPanel(), colSpan: 2);

        return grid;
    }

    /// <summary>Preset 2 – Resource Grid: 3-col resource row, then NET/DISK + Processes</summary>
    private ILayoutNode BuildPreset2ResourceGrid()
    {
        var showCpu = ViewModel.ShowCpu.Value;
        var showMem = ViewModel.ShowMemory.Value;
        var showNet = ViewModel.ShowNetDisk.Value;
        var showProc = ViewModel.ShowProcesses.Value;

        if (ViewModel.GpuAvailable)
        {
            var grid = new GridNode(2, 3)
                .WithColumnWidths(new SizeConstraint.Fill(), new SizeConstraint.Fill(), new SizeConstraint.Fill())
                .WithRowHeights(new SizeConstraint.Percent(35), new SizeConstraint.Fill());

            // Row 0: CPU | Memory | GPU — place only visible panels, pack left
            var row0Col = 0;
            if (showCpu) grid.SetCell(0, row0Col++, BuildCpuPanel());
            if (showMem) grid.SetCell(0, row0Col++, BuildMemoryPanel());
            grid.SetCell(0, row0Col, BuildGpuPanel(), colSpan: 3 - row0Col);

            switch (showNet)
            {
                // Row 1: Net/Disk | Processes
                case true when showProc:
                    grid.SetCell(1, 0, BuildNetDiskPanel());
                    grid.SetCell(1, 1, BuildProcessPanel(), colSpan: 2);
                    break;
                case true:
                    grid.SetCell(1, 0, BuildNetDiskPanel(), colSpan: 3);
                    break;
                default:
                {
                    if (showProc)
                    {
                        grid.SetCell(1, 0, BuildProcessPanel(), colSpan: 3);
                    }

                    break;
                }
            }

            return grid;
        }
        else
        {
            var grid = new GridNode(2, 3)
                .WithColumnWidths(new SizeConstraint.Fill(), new SizeConstraint.Fill(), new SizeConstraint.Fill())
                .WithRowHeights(new SizeConstraint.Percent(35), new SizeConstraint.Fill());

            // Row 0: CPU | Memory | Net/Disk — place only visible panels, pack left
            var row0Col = 0;
            if (showCpu) grid.SetCell(0, row0Col++, BuildCpuPanel());
            if (showMem) grid.SetCell(0, row0Col++, BuildMemoryPanel());
            if (showNet) grid.SetCell(0, row0Col, BuildNetDiskPanel());

            if (showProc) grid.SetCell(1, 0, BuildProcessPanel(), colSpan: 3);

            return grid;
        }
    }

    /// <summary>Preset 3 – Minimal: compact CPU strip, then Processes</summary>
    private ILayoutNode BuildPreset3Minimal()
    {
        var showCpu = ViewModel.ShowCpu.Value;
        var showProc = ViewModel.ShowProcesses.Value;

        var grid = new GridNode(2, 1)
            .WithColumnWidths(new SizeConstraint.Fill())
            .WithRowHeights(new SizeConstraint.Percent(15), new SizeConstraint.Fill());

        if (showCpu) grid.SetCell(0, 0, BuildCpuStripPanel());
        if (showProc) grid.SetCell(1, 0, BuildProcessPanel());

        return grid;
    }

    // ── Panel builders ───────────────────────────────────────────────────────

    private ILayoutNode BuildCpuPanel()
    {
        return new PanelNode()
            .WithTitle(Strings.PanelCpu)
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(ThemeService.Instance.Current.Accent)
            .WithTitleColor(ThemeService.Instance.Current.PanelTitle)
            .WithContent(
                Layouts.Vertical()
                    .WithChild(
                        ViewModel.CpuTotal
                            .Select<double, ILayoutNode>(pct =>
                                new TextNode($" {ViewModel.CpuName.Value}  —  {Strings.TotalLabel} {pct:F1}%")
                                    .WithForeground(ThemeService.Instance.Current.Accent))
                            .AsLayout().Height(1))
                    .WithChild(_coresNode!)
                    .WithChild(_cpuGraph!.Fill()))
            .Fill();
    }

    private ILayoutNode BuildCpuStripPanel()
    {
        return new PanelNode()
            .WithTitle(Strings.PanelCpu)
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(ThemeService.Instance.Current.Accent)
            .WithTitleColor(ThemeService.Instance.Current.PanelTitle)
            .WithContent(
                ViewModel.CpuTotal
                    .Select<double, ILayoutNode>(pct =>
                        new TextNode($" {Strings.TotalLabel} {pct:F1}%  {BuildBar(pct, 30)}  {ViewModel.CpuName.Value}")
                            .WithForeground(ThemeService.Instance.Current.Accent))
                    .AsLayout())
            .Fill();
    }

    private ILayoutNode BuildMemoryPanel()
    {
        return new PanelNode()
            .WithTitle(Strings.PanelRam)
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(ThemeService.Instance.Current.Accent)
            .WithTitleColor(ThemeService.Instance.Current.PanelTitle)
            .WithContent(
                Layouts.Vertical()
                    .WithChild(
                        ViewModel.RamUsed.CombineLatest<ulong, ulong, ILayoutNode>(ViewModel.RamTotal,
                            (used, total) =>
                            {
                                var usedGb = used / 1024.0 / 1024 / 1024;
                                var totalGb = total / 1024.0 / 1024 / 1024;
                                var pct = total > 0 ? (double)used / total * 100 : 0;
                                return new TextNode($" {usedGb:F1} / {totalGb:F1} GiB  {pct:F1}%")
                                    .WithForeground(ThemeService.Instance.Current.Foreground);
                            }).AsLayout().Height(1))
                    .WithChild(_ramGraph!.Fill())
                    .Fill())
            .Fill();
    }

    private ILayoutNode BuildGpuPanel()
    {
        return new PanelNode()
            .WithTitle(Strings.PanelGpu)
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(ThemeService.Instance.Current.Accent)
            .WithTitleColor(ThemeService.Instance.Current.PanelTitle)
            .WithContent(
                Layouts.Vertical()
                    .WithChild(
                        ViewModel.Gpu
                            .Select<GpuSnapshot?, ILayoutNode>(gpu =>
                            {
                                if (gpu is null)
                                {
                                    return new TextNode(Strings.GpuNoData).WithForeground(ThemeService.Instance.Current.TextDim);
                                }

                                var vramMb = gpu.VramUsedBytes / 1024.0 / 1024;
                                var vramTotalMb = gpu.VramTotalBytes / 1024.0 / 1024;
                                return Layouts.Vertical()
                                    .WithChild(new TextNode($" {gpu.Name}").WithForeground(ThemeService.Instance.Current.Accent).Height(1))
                                    .WithChild(new TextNode(
                                            $" {Strings.GpuUsage} {gpu.UsagePercent:F0}%  {Strings.GpuTemperature} {gpu.TemperatureCelsius:F0}°C")
                                        .WithForeground(ThemeService.Instance.Current.Foreground).Height(1))
                                    .WithChild(new TextNode($" VRAM {vramMb:F0}/{vramTotalMb:F0}MB")
                                        .WithForeground(ThemeService.Instance.Current.Foreground).Height(1));
                            }).AsLayout())
                    .WithChild(_gpuGraph!.Fill()))
            .Fill();
    }

    private ILayoutNode BuildNetDiskPanel()
    {
        return new PanelNode()
            .WithTitle(" NET/DISK ")
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(ThemeService.Instance.Current.Accent)
            .WithTitleColor(ThemeService.Instance.Current.PanelTitle)
            .WithContent(
                ViewModel.Networks.CombineLatest<IReadOnlyList<NetworkSnapshot>, IReadOnlyList<DiskSnapshot>, ILayoutNode>(
                    ViewModel.Disks,
                    (nets, disks) =>
                    {
                        var layout = Layouts.Vertical();

                        var topNets = nets
                            .OrderByDescending(n => n.RxBytesPerSec + n.TxBytesPerSec)
                            .Take(2)
                            .ToList();

                        foreach (var net in topNets)
                        {
                            var name = net.Name.Length > 12 ? net.Name[..11] + "…" : net.Name;
                            layout.WithChild(new TextNode(
                                    $" {name,-12} ↓{FormatBytes(net.RxBytesPerSec),9}  ↑{FormatBytes(net.TxBytesPerSec),9}")
                                .WithForeground(net.RxBytesPerSec > 0 || net.TxBytesPerSec > 0 ? ThemeService.Instance.Current.Foreground : ThemeService.Instance.Current.TextDim)
                                .Height(1));
                        }

                        if (topNets.Count > 0 && disks.Count > 0)
                        {
                            layout.WithChild(new TextNode("").Height(1));
                        }

                        foreach (var disk in disks)
                        {
                            var usedGb = disk.UsedBytes / 1024.0 / 1024 / 1024;
                            var totalGb = disk.TotalBytes / 1024.0 / 1024 / 1024;
                            layout.WithChild(
                                new TextNode($" {disk.Name,-4} {usedGb:F0}/{totalGb:F0}GB {disk.UsedPercent:F0}%")
                                    .WithForeground(ThemeService.Instance.Current.Foreground).Height(1));
                        }

                        if (topNets.Count == 0 && disks.Count == 0)
                        {
                            layout.WithChild(new TextNode(Strings.NoActiveAdapters).WithForeground(ThemeService.Instance.Current.TextDim));
                        }

                        return layout;
                    }).AsLayout())
            .Fill();
    }

    private ILayoutNode BuildProcessPanel()
    {
        var header = new TextNode($" {"PID",6}  {"Name",-22} {"CPU%",6}  {"RAM",7}")
            .WithForeground(ThemeService.Instance.Current.Header)
            .Height(1);

        return new PanelNode()
            .WithTitle(Strings.PanelProcesses)
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(ThemeService.Instance.Current.Accent)
            .WithTitleColor(ThemeService.Instance.Current.PanelTitle)
            .WithContent(
                Layouts.Vertical()
                    .WithChild(header)
                    .WithChild(_processList!.Fill()))
            .Fill();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string BuildBar(double percent, int width)
    {
        var filled = Math.Clamp((int)(percent / 100.0 * width), 0, width);
        return $"[{"".PadRight(filled, '█')}{new string('░', width - filled)}]";
    }

    private static string FormatBytes(ulong bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB/s",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB/s",
        >= 1_024 => $"{bytes / 1_024.0:F1} KB/s",
        _ => $"{bytes} B/s",
    };
}
