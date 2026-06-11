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

public class PerformancePage : ReactivePage<PerformanceViewModel>
{
    private ModalNode? _detailModal;
    private ModalNode? _settingsModal;
    private GraphNode? _diskActiveGraph;
    private GraphNode? _diskTransferGraph;
    private GraphNode? _cpuGraph;
    private GraphNode? _ramGraph;
    private GraphNode? _gpuGraph;
    private GraphNode? _cpuDetailGraph;
    private GraphNode? _ramDetailGraph;
    private GraphNode? _gpuDetailGraph;
    private DataListNode<NetworkSnapshot>? _networkList;
    private CpuCoresNode? _coresNode;

    // History is decoupled from the graph nodes so the panel graph and its detail
    // graph share one continuously-fed buffer per metric. Disk histories are keyed
    // by disk name so each disk keeps its own history across selection changes.
    private readonly MetricHistory _cpuHistory = new();
    private readonly MetricHistory _ramHistory = new();
    private readonly MetricHistory _gpuHistory = new();
    private readonly Dictionary<string, MetricHistory> _diskActiveHistory = [];
    private readonly Dictionary<string, MetricHistory> _diskTransferHistory = [];

    private static MetricHistory GetOrAdd(Dictionary<string, MetricHistory> map, string key)
    {
        if (!map.TryGetValue(key, out var history))
        {
            history = new MetricHistory();
            map[key] = history;
        }

        return history;
    }

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

        _coresNode = new CpuCoresNode();

        _detailModal = new ModalNode()
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(ThemeService.Instance.Current.Accent)
            .WithBackdrop(BackdropStyle.Solid)
            .WithBackdropColor(Color.Black)
            .WithDismissOnEscape(false)
            .WithPadding(1);

        _cpuDetailGraph = new GraphNode()
            .WithStyle(graphStyle)
            .WithColor(ThemeService.Instance.Current.Accent)
            .WithRange(0, 100);

        _ramDetailGraph = new GraphNode()
            .WithStyle(graphStyle)
            .WithColor(ThemeService.Instance.Current.Accent)
            .WithRange(0, 100);

        _gpuDetailGraph = new GraphNode()
            .WithStyle(graphStyle)
            .WithColor(ThemeService.Instance.Current.Accent)
            .WithRange(0, 100);

        _diskActiveGraph = new GraphNode()
            .WithStyle(graphStyle)
            .WithColor(ThemeService.Instance.Current.Accent)
            .WithRange(0, 100);

        _diskTransferGraph = new GraphNode()
            .WithStyle(graphStyle)
            .WithColor(ThemeService.Instance.Current.Accent)
            .WithRange(0, 100_000_000);

        _gpuGraph = new GraphNode()
            .WithStyle(graphStyle)
            .WithColor(ThemeService.Instance.Current.Accent)
            .WithRange(0, 100);

        var conditionalDetail = new ConditionalNode(ViewModel.IsDetailOpen, _detailModal);

        _settingsModal = new ModalNode()
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(ThemeService.Instance.Current.Accent)
            .WithBackdrop(BackdropStyle.Solid)
            .WithBackdropColor(Color.Black)
            .WithDismissOnEscape(false)
            .WithPadding(1);

        var conditionalSettings = new ConditionalNode(ViewModel.IsSettingsOpen, _settingsModal);

        var bottomRow = Layouts.Horizontal()
            .WithChild(BuildDiskPanel())
            .WithSpacing(1)
            .WithChild(BuildNetworkPanel());

        if (ViewModel.GpuAvailable)
        {
            bottomRow.WithSpacing(1).WithChild(BuildGpuPanel());
        }

        var mainLayout = Layouts.Vertical()
            .WithChild(new TabBarNode(2))
            .WithChild(Layouts.Horizontal()
                .WithChild(BuildCpuPanel())
                .WithSpacing(1)
                .WithChild(BuildRamPanel())
                .HeightPercent(50))
            .WithChild(bottomRow.Fill())
            .WithChild(ViewModel.StatusHint
                .Select<string, ILayoutNode>(hint =>
                    new TextNode(hint).WithForeground(ThemeService.Instance.Current.TextDim).WithBackground(ThemeService.Instance.Current.StatusBar))
                .AsLayout().Height(1));

        return Layouts.Stack(mainLayout, conditionalDetail, conditionalSettings);
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();

        Observable.Interval(TimeSpan.FromMilliseconds(500))
            .Subscribe(_ =>
            {
                // Feed every metric's history continuously and independently, regardless
                // of which (if any) detail section is open. This guarantees each graph
                // always shows its own full history the moment it becomes visible, and
                // makes it impossible for one metric's samples to leak into another's.
                _cpuHistory.Push(ViewModel.CpuTotal.Value);
                _cpuGraph?.SetData(_cpuHistory.Snapshot());
                _cpuDetailGraph?.SetData(_cpuHistory.Snapshot());

                var total = ViewModel.RamTotal.Value;
                var used = ViewModel.RamUsed.Value;
                _ramHistory.Push(total > 0 ? (double)used / total * 100 : 0);
                _ramGraph?.SetData(_ramHistory.Snapshot());
                _ramDetailGraph?.SetData(_ramHistory.Snapshot());

                if (ViewModel is { GpuAvailable: true, Gpu.Value: { } gpu })
                {
                    _gpuHistory.Push(gpu.UsagePercent);
                    _gpuGraph?.SetData(_gpuHistory.Snapshot());
                    _gpuDetailGraph?.SetData(_gpuHistory.Snapshot());
                }

                foreach (var disk in ViewModel.Disks.Value)
                {
                    GetOrAdd(_diskActiveHistory, disk.Name).Push(disk.ActiveTimePercent);
                    GetOrAdd(_diskTransferHistory, disk.Name).Push(disk.TransferBytesPerSec);
                }
            })
            .DisposeWith(Subscriptions);

        ViewModel.CpuCores.Subscribe(cores => _coresNode?.SetCores(cores))
            .DisposeWith(Subscriptions);

        ViewModel.DetailContentChanged.Subscribe(_ => UpdateDetailModal())
            .DisposeWith(Subscriptions);

        ViewModel.SettingsContentChanged.Subscribe(_ => UpdateSettingsModal())
            .DisposeWith(Subscriptions);
    }

    private void UpdateSettingsModal()
    {
        if (_settingsModal is null)
        {
            return;
        }

        _settingsModal.WithTitle($" {Strings.SettingsTitle} ").WithTitleColor(ThemeService.Instance.Current.Accent);
        _settingsModal.WithFooter(Strings.HintSettingsModalKeys).WithFooterColor(ThemeService.Instance.Current.TextDim);

        var layout = Layouts.Vertical()
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode($"  {Strings.SettingsRefreshRate,-20} ◀ {ViewModel.GetRefreshRateDisplay()} ▶")
                .WithForeground(ThemeService.Instance.Current.Foreground).Height(1))
            .WithChild(new TextNode("").Height(1));

        if (ViewModel.IsUpdateAvailable)
        {
            layout.WithChild(new TextNode($"  {ViewModel.LatestVersionDisplay}").WithForeground(ThemeService.Instance.Current.Warning).Height(1));
            layout.WithChild(new TextNode($"  [U] {Strings.UpdatePressU}").WithForeground(ThemeService.Instance.Current.Accent).Height(1));
        }
        else
        {
            layout.WithChild(new TextNode($"  {ViewModel.CurrentVersionDisplay}").WithForeground(ThemeService.Instance.Current.TextDim).Height(1));
        }

        layout.WithChild(new TextNode("").Height(1));
        layout.WithChild(new TextNode($"  {ViewModel.GetSettingsFilePath()}").WithForeground(ThemeService.Instance.Current.TextDim).Height(1));

        _settingsModal.Content = layout;
    }

    private void UpdateDetailModal()
    {
        if (_detailModal is null)
        {
            return;
        }

        var section = ViewModel.DetailSection.Value;
        if (section != PerfDetailSection.Network)
        {
            _networkList = null;
        }

        var sections = new List<string> { "CPU", "RAM", "Disk", Strings.DetailSectionNetwork };
        if (ViewModel.GpuAvailable)
        {
            sections.Add("GPU");
        }

        var sectionIdx = (int)section;

        var tabBar = Layouts.Horizontal();
        for (var i = 0; i < sections.Count; i++)
        {
            var node = new TextNode($" {sections[i]} ");
            if (i == sectionIdx)
            {
                node.WithForeground(ThemeService.Instance.Current.SelectionText).WithBackground(ThemeService.Instance.Current.Selection);
            }
            else
            {
                node.WithForeground(ThemeService.Instance.Current.TextDim);
            }

            tabBar.WithChild(node);
        }

        var (color, title, info) = section switch
        {
            PerfDetailSection.Cpu => (ThemeService.Instance.Current.Accent, "CPU", BuildCpuDetailInfo()),
            PerfDetailSection.Ram => (ThemeService.Instance.Current.Accent, "RAM", BuildRamDetailInfo()),
            PerfDetailSection.Disk => (ThemeService.Instance.Current.Accent, "Disk", BuildDiskDetailInfo()),
            PerfDetailSection.Network => (ThemeService.Instance.Current.Accent, Strings.DetailSectionNetwork, BuildNetworkDetailInfo()),
            PerfDetailSection.Gpu => (ThemeService.Instance.Current.Accent, "GPU", BuildGpuDetailInfo()),
            _ => (ThemeService.Instance.Current.Foreground, "", Layouts.Vertical())
        };

        _detailModal.WithTitle(string.Format(Strings.DetailTitle, title)).WithTitleColor(ThemeService.Instance.Current.Accent).WithBorderColor(ThemeService.Instance.Current.Accent);

        var footerHint = section switch
        {
            PerfDetailSection.Disk => Strings.HintPerfDiskDetailKeys,
            PerfDetailSection.Network => Strings.HintPerfNetworkDetailKeys,
            _ => Strings.HintPerfDetailKeys
        };
        _detailModal.WithFooter(footerHint).WithFooterColor(ThemeService.Instance.Current.TextDim);

        if (section is PerfDetailSection.Cpu or PerfDetailSection.Ram or PerfDetailSection.Gpu)
        {
            var detailGraph = section switch
            {
                PerfDetailSection.Cpu => _cpuDetailGraph!,
                PerfDetailSection.Ram => _ramDetailGraph!,
                _ => _gpuDetailGraph!,
            };
            detailGraph.WithColor(color).WithRange(0, 100);
            _detailModal.Content = Layouts.Vertical()
                .WithChild(tabBar.Height(1))
                .WithChild(info)
                .WithChild(detailGraph.Height(999));
        }
        else if (section == PerfDetailSection.Disk)
        {
            _detailModal.Content = Layouts.Vertical()
                .WithChild(tabBar.Height(1))
                .WithChild(Layouts.Vertical().WithChild(info).Height(999));
        }
        else
        {
            _detailModal.Content = Layouts.Vertical()
                .WithChild(tabBar.Height(1))
                .WithChild(Layouts.Vertical().WithChild(info).Height(999));
        }
    }

    private ILayoutNode BuildCpuDetailInfo()
    {
        var coresNode = new CpuCoresNode();
        coresNode.SetCores(ViewModel.CpuCores.Value);

        return Layouts.Vertical()
            .WithChild(new TextNode(
                    $" {ViewModel.CpuName.Value}  —  {Strings.TotalLabel} {ViewModel.CpuTotal.Value:F1}%")
                .WithForeground(ThemeService.Instance.Current.Accent).Height(1))
            .WithChild(coresNode);
    }

    private ILayoutNode BuildRamDetailInfo()
    {
        var usedGb = ViewModel.RamUsed.Value / 1024.0 / 1024 / 1024;
        var totalGb = ViewModel.RamTotal.Value / 1024.0 / 1024 / 1024;
        var pct = ViewModel.RamTotal.Value > 0 ? (double)ViewModel.RamUsed.Value / ViewModel.RamTotal.Value * 100 : 0;

        return Layouts.Vertical()
            .WithChild(new TextNode(string.Format(Strings.UsedFormat, usedGb, totalGb, pct))
                .WithForeground(ThemeService.Instance.Current.Foreground).Height(1))
            .WithChild(new TextNode($" {BuildBar(pct, 40)}")
                .WithForeground(ThemeService.Instance.Current.Accent).Height(1));
    }

    private ILayoutNode BuildDiskDetailInfo()
    {
        var disks = ViewModel.Disks.Value;
        if (disks.Count == 0)
        {
            return new TextNode(Strings.NoDisksFound).WithForeground(ThemeService.Instance.Current.TextDim);
        }

        var idx = Math.Clamp(ViewModel.DiskDetailIndex.Value, 0, disks.Count - 1);
        var disk = disks[idx];

        // Point the disk graphs at the selected disk's own continuously-fed history,
        // so switching disks shows that disk's full history with no carry-over.
        _diskActiveGraph!.SetData(GetOrAdd(_diskActiveHistory, disk.Name).Snapshot());
        _diskTransferGraph!.SetData(GetOrAdd(_diskTransferHistory, disk.Name).Snapshot());

        var usedGb = disk.UsedBytes / 1024.0 / 1024 / 1024;
        var totalGb = disk.TotalBytes / 1024.0 / 1024 / 1024;

        var diskTabs = Layouts.Horizontal();
        for (var i = 0; i < disks.Count; i++)
        {
            var label = new TextNode($" {disks[i].Name} ");
            if (i == idx)
            {
                label.WithForeground(ThemeService.Instance.Current.SelectionText).WithBackground(ThemeService.Instance.Current.Selection);
            }
            else
            {
                label.WithForeground(ThemeService.Instance.Current.TextDim);
            }

            diskTabs.WithChild(label);
        }

        return Layouts.Vertical()
            .WithChild(diskTabs.Height(1))
            .WithChild(new TextNode(string.Format(Strings.DiskUsedFormat, disk.Name, usedGb, totalGb, disk.UsedPercent,
                    BuildBar(disk.UsedPercent, 20)))
                .WithForeground(ThemeService.Instance.Current.Foreground).Height(1))
            .WithChild(new TextNode(
                    $" Read: {FormatBytes(disk.ReadBytesPerSec)}  Write: {FormatBytes(disk.WriteBytesPerSec)}  Active: {disk.ActiveTimePercent:F0}%")
                .WithForeground(ThemeService.Instance.Current.Foreground).Height(1))
            .WithChild(new TextNode("").Height(1))
            .WithChild(new PanelNode()
                .WithTitle(Strings.PanelActiveTime)
                .WithBorder(BorderStyle.Rounded)
                .WithBorderColor(ThemeService.Instance.Current.Accent)
                .WithTitleColor(ThemeService.Instance.Current.PanelTitle)
                .WithContent(_diskActiveGraph!)
                .HeightPercent(50)
                .Fill())
            .WithChild(new PanelNode()
                .WithTitle(Strings.PanelTransferRate)
                .WithBorder(BorderStyle.Rounded)
                .WithBorderColor(ThemeService.Instance.Current.Accent)
                .WithTitleColor(ThemeService.Instance.Current.PanelTitle)
                .WithContent(_diskTransferGraph!)
                .Fill());
    }

    private ILayoutNode BuildNetworkDetailInfo()
    {
        var nets = ViewModel.Networks.Value;
        if (nets.Count == 0)
        {
            return new TextNode(Strings.NoActiveAdapters).WithForeground(ThemeService.Instance.Current.TextDim);
        }

        var sorted = nets
            .OrderByDescending(n => ViewModel.IsAdapterPinned(n.Name))
            .ThenByDescending(n => n.RxBytesPerSec + n.TxBytesPerSec)
            .ToList();

        if (_networkList is null)
        {
            _networkList = new DataListNode<NetworkSnapshot>(
                net =>
                {
                    var pin = ViewModel.IsAdapterPinned(net.Name) ? "● " : "  ";
                    var name = net.Name.Length > 20 ? net.Name[..19] + "…" : net.Name;
                    return $" {pin}{name,-20} ↓ {FormatBytes(net.RxBytesPerSec),10}  ↑ {FormatBytes(net.TxBytesPerSec),10}";
                },
                net => ViewModel.IsAdapterPinned(net.Name) ? ThemeService.Instance.Current.Accent
                    : net.RxBytesPerSec > 0 || net.TxBytesPerSec > 0 ? ThemeService.Instance.Current.Foreground
                    : ThemeService.Instance.Current.TextDim);
            ViewModel.NetworkListNode = _networkList;
            ViewModel.GetSelectedAdapter = () => _networkList.SelectedItem;
        }

        _networkList.SetItems(sorted);

        var header = new TextNode($"   {"Adapter",-20} {"Download",13}  {"Upload",13}")
            .WithForeground(ThemeService.Instance.Current.Header).Height(1);

        return Layouts.Vertical()
            .WithChild(header)
            .WithChild(_networkList.Fill());
    }

    private ILayoutNode BuildGpuDetailInfo()
    {
        var gpu = ViewModel.Gpu.Value;
        if (gpu is null)
        {
            return new TextNode(Strings.GpuNoData).WithForeground(ThemeService.Instance.Current.TextDim);
        }

        var vramUsedMb = gpu.VramUsedBytes / 1024.0 / 1024;
        var vramTotalMb = gpu.VramTotalBytes / 1024.0 / 1024;

        return Layouts.Vertical()
            .WithChild(new TextNode($" {gpu.Name}").WithForeground(ThemeService.Instance.Current.Accent).Height(1))
            .WithChild(new TextNode($" {Strings.GpuUsage} {gpu.UsagePercent:F0}%  {BuildBar(gpu.UsagePercent, 20)}")
                .WithForeground(ThemeService.Instance.Current.Foreground).Height(1))
            .WithChild(new TextNode(
                    $" VRAM: {vramUsedMb:F0} / {vramTotalMb:F0} MB  ({gpu.VramUsedPercent:F1}%)  {BuildBar(gpu.VramUsedPercent, 20)}")
                .WithForeground(ThemeService.Instance.Current.Foreground).Height(1))
            .WithChild(new TextNode($" {Strings.GpuTemperature} {gpu.TemperatureCelsius:F0}°C")
                .WithForeground(gpu.TemperatureCelsius > 80 ? Color.BrightRed : ThemeService.Instance.Current.Foreground).Height(1));
    }

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
                                new TextNode($" {Strings.TotalLabel} {pct:F1}%")
                                    .WithForeground(ThemeService.Instance.Current.Accent))
                            .AsLayout().Height(1))
                    .WithChild(_coresNode!)
                    .WithChild(_cpuGraph!.Fill()))
            .Fill();
    }

    private ILayoutNode BuildRamPanel()
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

    private ILayoutNode BuildDiskPanel()
    {
        return new PanelNode()
            .WithTitle(Strings.PanelDisks)
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(ThemeService.Instance.Current.Accent)
            .WithTitleColor(ThemeService.Instance.Current.PanelTitle)
            .WithContent(
                ViewModel.Disks
                    .Select<IReadOnlyList<DiskSnapshot>, ILayoutNode>(disks =>
                    {
                        if (disks.Count == 0)
                        {
                            return new TextNode(Strings.NoDisksFound).WithForeground(ThemeService.Instance.Current.TextDim);
                        }

                        var layout = Layouts.Vertical();
                        foreach (var disk in disks)
                        {
                            var usedGb = disk.UsedBytes / 1024.0 / 1024 / 1024;
                            var totalGb = disk.TotalBytes / 1024.0 / 1024 / 1024;
                            layout.WithChild(
                                new TextNode($" {disk.Name,-4} {usedGb:F0}/{totalGb:F0}GB {disk.UsedPercent:F0}%")
                                    .WithForeground(ThemeService.Instance.Current.Foreground).Height(1));
                        }

                        return layout;
                    }).AsLayout())
            .Fill();
    }

    private ILayoutNode BuildNetworkPanel()
    {
        return new PanelNode()
            .WithTitle(Strings.PanelNetwork)
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(ThemeService.Instance.Current.Accent)
            .WithTitleColor(ThemeService.Instance.Current.PanelTitle)
            .WithContent(
                ViewModel.Networks
                    .Select<IReadOnlyList<NetworkSnapshot>, ILayoutNode>(nets =>
                    {
                        if (nets.Count == 0)
                        {
                            return new TextNode(Strings.NoActiveAdapters).WithForeground(ThemeService.Instance.Current.TextDim);
                        }

                        var layout = Layouts.Vertical();
                        foreach (var net in nets.Take(4))
                        {
                            layout.WithChild(
                                new TextNode(
                                        $" {net.Name}  ↓{FormatBytes(net.RxBytesPerSec)}  ↑{FormatBytes(net.TxBytesPerSec)}")
                                    .WithForeground(ThemeService.Instance.Current.Foreground).Height(1));
                        }

                        return layout;
                    }).AsLayout())
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
                                    .WithChild(new TextNode($" {gpu.UsagePercent:F0}%  {gpu.TemperatureCelsius:F0}°C")
                                        .WithForeground(ThemeService.Instance.Current.Foreground).Height(1))
                                    .WithChild(new TextNode($" VRAM {vramMb:F0}/{vramTotalMb:F0}MB")
                                        .WithForeground(ThemeService.Instance.Current.Foreground).Height(1));
                            }).AsLayout())
                    .WithChild(_gpuGraph!.Fill()))
            .Fill();
    }

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