using dottop.Core.Models;
using dottop.Nodes;
using dottop.Resources;
using dottop.Themes;
using R3;
using Termina.Extensions;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop.Pages;

public class PerformancePage : ReactivePage<PerformanceViewModel>
{
    private ModalNode? _detailModal;
    private GraphNode? _detailGraph;
    private GraphNode? _diskActiveGraph;
    private GraphNode? _diskTransferGraph;
    private GraphNode? _cpuGraph;
    private GraphNode? _ramGraph;
    private GraphNode? _gpuGraph;
    private CpuCoresNode? _coresNode;

    public override ILayoutNode BuildLayout()
    {
        var graphStyle = ViewModel.GraphStyleSetting;

        _cpuGraph = new GraphNode()
            .WithStyle(graphStyle)
            .WithColor(Theme.Graph)
            .WithRange(0, 100);

        _ramGraph = new GraphNode()
            .WithStyle(graphStyle)
            .WithColor(Theme.Graph)
            .WithRange(0, 100);

        _coresNode = new CpuCoresNode();

        _detailModal = new ModalNode()
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Theme.Primary)
            .WithBackdrop(BackdropStyle.Solid)
            .WithBackdropColor(Color.Black)
            .WithDismissOnEscape(false)
            .WithPadding(1);

        _detailGraph = new GraphNode()
            .WithStyle(graphStyle)
            .WithColor(Theme.Graph)
            .WithRange(0, 100);

        _diskActiveGraph = new GraphNode()
            .WithStyle(graphStyle)
            .WithColor(Theme.Graph)
            .WithRange(0, 100);

        _diskTransferGraph = new GraphNode()
            .WithStyle(graphStyle)
            .WithColor(Theme.Graph)
            .WithRange(0, 100_000_000);

        _gpuGraph = new GraphNode()
            .WithStyle(graphStyle)
            .WithColor(Theme.Graph)
            .WithRange(0, 100);

        var conditionalDetail = new ConditionalNode(ViewModel.IsDetailOpen, _detailModal);

        var bottomRow = Layouts.Horizontal()
            .WithChild(BuildDiskPanel())
            .WithSpacing(1)
            .WithChild(BuildNetworkPanel());

        if (ViewModel.GpuAvailable)
        {
            bottomRow.WithSpacing(1).WithChild(BuildGpuPanel());
        }

        var mainLayout = Layouts.Vertical()
            .WithChild(new TabBarNode(1))
            .WithChild(Layouts.Horizontal()
                .WithChild(BuildCpuPanel())
                .WithSpacing(1)
                .WithChild(BuildRamPanel())
                .HeightPercent(50))
            .WithChild(bottomRow.Fill())
            .WithChild(ViewModel.StatusHint
                .Select<string, ILayoutNode>(hint =>
                    new TextNode(hint).WithForeground(Theme.TextDim).WithBackground(Theme.StatusBar))
                .AsLayout().Height(1));

        return Layouts.Stack(mainLayout, conditionalDetail);
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();

        Observable.Interval(TimeSpan.FromMilliseconds(200))
            .Subscribe(_ =>
            {
                _cpuGraph?.Push(ViewModel.CpuTotal.Value);

                var total = ViewModel.RamTotal.Value;
                var used = ViewModel.RamUsed.Value;
                _ramGraph?.Push(total > 0 ? (double)used / total * 100 : 0);

                if (ViewModel is { GpuAvailable: true, Gpu.Value: { } gpu })
                {
                    _gpuGraph?.Push(gpu.UsagePercent);
                }

                if (ViewModel.IsDetailOpen.Value)
                {
                    if (ViewModel.DetailSection.Value is PerfDetailSection.Cpu or PerfDetailSection.Ram
                        && _detailGraph is not null)
                    {
                        var value = ViewModel.DetailSection.Value switch
                        {
                            PerfDetailSection.Cpu => ViewModel.CpuTotal.Value,
                            PerfDetailSection.Ram => total > 0 ? (double)used / total * 100 : 0,
                            _ => 0
                        };
                        _detailGraph.Push(value);
                    }

                    if (ViewModel.DetailSection.Value == PerfDetailSection.Disk)
                    {
                        var disks = ViewModel.Disks.Value;
                        var idx = ViewModel.DiskDetailIndex.Value;
                        if (idx >= 0 && idx < disks.Count)
                        {
                            _diskActiveGraph?.Push(disks[idx].ActiveTimePercent);
                            _diskTransferGraph?.Push(disks[idx].TransferBytesPerSec);
                        }
                    }

                    if (ViewModel.DetailSection.Value == PerfDetailSection.Gpu
                        && ViewModel.Gpu.Value is { } gpuDetail)
                    {
                        _detailGraph?.Push(gpuDetail.UsagePercent);
                    }
                }
            })
            .DisposeWith(Subscriptions);

        ViewModel.CpuCores.Subscribe(cores => _coresNode?.SetCores(cores))
            .DisposeWith(Subscriptions);

        ViewModel.DetailContentChanged.Subscribe(_ => UpdateDetailModal())
            .DisposeWith(Subscriptions);
    }

    private void UpdateDetailModal()
    {
        if (_detailModal is null || _detailGraph is null)
        {
            return;
        }

        var section = ViewModel.DetailSection.Value;
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
                node.WithForeground(Theme.SelectionText).WithBackground(Theme.Selection);
            }
            else
            {
                node.WithForeground(Theme.Secondary);
            }

            tabBar.WithChild(node);
        }

        var (color, title, info) = section switch
        {
            PerfDetailSection.Cpu => (Theme.Graph, "CPU", BuildCpuDetailInfo()),
            PerfDetailSection.Ram => (Theme.Graph, "RAM", BuildRamDetailInfo()),
            PerfDetailSection.Disk => (Theme.Graph, "Disk", BuildDiskDetailInfo()),
            PerfDetailSection.Network => (Theme.Graph, Strings.DetailSectionNetwork, BuildNetworkDetailInfo()),
            PerfDetailSection.Gpu => (Theme.Graph, "GPU", BuildGpuDetailInfo()),
            _ => (Theme.Text, "", Layouts.Vertical())
        };

        _detailModal.WithTitle(string.Format(Strings.DetailTitle, title)).WithTitleColor(Theme.Primary).WithBorderColor(Theme.Primary);

        var graphStyle = ViewModel.GraphStyleSetting;

        var footerHint = section == PerfDetailSection.Disk
            ? Strings.HintPerfDiskDetailKeys
            : Strings.HintPerfDetailKeys;
        _detailModal.WithFooter(footerHint).WithFooterColor(Theme.TextDim);

        if (section is PerfDetailSection.Cpu or PerfDetailSection.Ram or PerfDetailSection.Gpu)
        {
            _detailGraph!.WithColor(color).WithRange(0, 100);
            _detailModal.Content = Layouts.Vertical()
                .WithChild(tabBar.Height(1))
                .WithChild(info)
                .WithChild(_detailGraph.Height(999));
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
                .WithForeground(Theme.Accent).Height(1))
            .WithChild(coresNode);
    }

    private ILayoutNode BuildRamDetailInfo()
    {
        var usedGb = ViewModel.RamUsed.Value / 1024.0 / 1024 / 1024;
        var totalGb = ViewModel.RamTotal.Value / 1024.0 / 1024 / 1024;
        var pct = ViewModel.RamTotal.Value > 0 ? (double)ViewModel.RamUsed.Value / ViewModel.RamTotal.Value * 100 : 0;

        return Layouts.Vertical()
            .WithChild(new TextNode(string.Format(Strings.UsedFormat, usedGb, totalGb, pct))
                .WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($" {BuildBar(pct, 40)}")
                .WithForeground(Theme.Accent).Height(1));
    }

    private ILayoutNode BuildDiskDetailInfo()
    {
        var disks = ViewModel.Disks.Value;
        if (disks.Count == 0)
        {
            return new TextNode(Strings.NoDisksFound).WithForeground(Theme.TextDim);
        }

        var idx = Math.Clamp(ViewModel.DiskDetailIndex.Value, 0, disks.Count - 1);
        var disk = disks[idx];

        var usedGb = disk.UsedBytes / 1024.0 / 1024 / 1024;
        var totalGb = disk.TotalBytes / 1024.0 / 1024 / 1024;

        var diskTabs = Layouts.Horizontal();
        for (var i = 0; i < disks.Count; i++)
        {
            var label = new TextNode($" {disks[i].Name} ");
            if (i == idx)
            {
                label.WithForeground(Theme.SelectionText).WithBackground(Theme.Selection);
            }
            else
            {
                label.WithForeground(Theme.TextDim);
            }

            diskTabs.WithChild(label);
        }

        return Layouts.Vertical()
            .WithChild(diskTabs.Height(1))
            .WithChild(new TextNode(string.Format(Strings.DiskUsedFormat, disk.Name, usedGb, totalGb, disk.UsedPercent,
                    BuildBar(disk.UsedPercent, 20)))
                .WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode(
                    $" Read: {FormatBytes(disk.ReadBytesPerSec)}  Write: {FormatBytes(disk.WriteBytesPerSec)}  Active: {disk.ActiveTimePercent:F0}%")
                .WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode("").Height(1))
            .WithChild(new PanelNode()
                .WithTitle(Strings.PanelActiveTime)
                .WithBorder(BorderStyle.Rounded)
                .WithBorderColor(Theme.Primary)
                .WithContent(_diskActiveGraph!)
                .HeightPercent(50)
                .Fill())
            .WithChild(new PanelNode()
                .WithTitle(Strings.PanelTransferRate)
                .WithBorder(BorderStyle.Rounded)
                .WithBorderColor(Theme.Primary)
                .WithContent(_diskTransferGraph!)
                .Fill());
    }

    private ILayoutNode BuildNetworkDetailInfo()
    {
        var nets = ViewModel.Networks.Value;
        var layout = Layouts.Vertical();
        if (nets.Count == 0)
        {
            layout.WithChild(new TextNode(Strings.NoActiveAdapters).WithForeground(Theme.TextDim).Height(1));
            return layout;
        }

        foreach (var net in nets)
        {
            layout.WithChild(new TextNode($" {net.Name}").WithForeground(Theme.Accent).Height(1));
            layout.WithChild(
                new TextNode($"   ↓ {FormatBytes(net.RxBytesPerSec),-14}  ↑ {FormatBytes(net.TxBytesPerSec)}")
                    .WithForeground(Theme.Text).Height(1));
        }

        return layout;
    }

    private ILayoutNode BuildGpuDetailInfo()
    {
        var gpu = ViewModel.Gpu.Value;
        if (gpu is null)
        {
            return new TextNode(Strings.GpuNoData).WithForeground(Theme.TextDim);
        }

        var vramUsedMb = gpu.VramUsedBytes / 1024.0 / 1024;
        var vramTotalMb = gpu.VramTotalBytes / 1024.0 / 1024;

        return Layouts.Vertical()
            .WithChild(new TextNode($" {gpu.Name}").WithForeground(Theme.Accent).Height(1))
            .WithChild(new TextNode($" {Strings.GpuUsage} {gpu.UsagePercent:F0}%  {BuildBar(gpu.UsagePercent, 20)}")
                .WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode(
                    $" VRAM: {vramUsedMb:F0} / {vramTotalMb:F0} MB  ({gpu.VramUsedPercent:F1}%)  {BuildBar(gpu.VramUsedPercent, 20)}")
                .WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($" {Strings.GpuTemperature} {gpu.TemperatureCelsius:F0}°C")
                .WithForeground(gpu.TemperatureCelsius > 80 ? Color.BrightRed : Theme.Text).Height(1));
    }

    private ILayoutNode BuildCpuPanel()
    {
        return new PanelNode()
            .WithTitle(Strings.PanelCpu)
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Theme.Primary)
            .WithContent(
                Layouts.Vertical()
                    .WithChild(
                        ViewModel.CpuTotal
                            .Select<double, ILayoutNode>(pct =>
                                new TextNode($" {Strings.TotalLabel} {pct:F1}%")
                                    .WithForeground(Theme.Accent))
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
            .WithBorderColor(Theme.Primary)
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
                                    .WithForeground(Theme.Text);
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
            .WithBorderColor(Theme.Primary)
            .WithContent(
                ViewModel.Disks
                    .Select<IReadOnlyList<DiskSnapshot>, ILayoutNode>(disks =>
                    {
                        if (disks.Count == 0)
                        {
                            return new TextNode(Strings.NoDisksFound).WithForeground(Theme.TextDim);
                        }

                        var layout = Layouts.Vertical();
                        foreach (var disk in disks)
                        {
                            var usedGb = disk.UsedBytes / 1024.0 / 1024 / 1024;
                            var totalGb = disk.TotalBytes / 1024.0 / 1024 / 1024;
                            layout.WithChild(
                                new TextNode($" {disk.Name,-4} {usedGb:F0}/{totalGb:F0}GB {disk.UsedPercent:F0}%")
                                    .WithForeground(Theme.Text).Height(1));
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
            .WithBorderColor(Theme.Primary)
            .WithContent(
                ViewModel.Networks
                    .Select<IReadOnlyList<NetworkSnapshot>, ILayoutNode>(nets =>
                    {
                        if (nets.Count == 0)
                        {
                            return new TextNode(Strings.NoActiveAdapters).WithForeground(Theme.TextDim);
                        }

                        var layout = Layouts.Vertical();
                        foreach (var net in nets.Take(4))
                            layout.WithChild(
                                new TextNode(
                                        $" {net.Name}  ↓{FormatBytes(net.RxBytesPerSec)}  ↑{FormatBytes(net.TxBytesPerSec)}")
                                    .WithForeground(Theme.Text).Height(1));
                        return layout;
                    }).AsLayout())
            .Fill();
    }

    private ILayoutNode BuildGpuPanel()
    {
        return new PanelNode()
            .WithTitle(Strings.PanelGpu)
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Theme.Primary)
            .WithContent(
                Layouts.Vertical()
                    .WithChild(
                        ViewModel.Gpu
                            .Select<GpuSnapshot?, ILayoutNode>(gpu =>
                            {
                                if (gpu is null)
                                {
                                    return new TextNode(Strings.GpuNoData).WithForeground(Theme.TextDim);
                                }

                                var vramMb = gpu.VramUsedBytes / 1024.0 / 1024;
                                var vramTotalMb = gpu.VramTotalBytes / 1024.0 / 1024;
                                return Layouts.Vertical()
                                    .WithChild(new TextNode($" {gpu.UsagePercent:F0}%  {gpu.TemperatureCelsius:F0}°C")
                                        .WithForeground(Theme.Text).Height(1))
                                    .WithChild(new TextNode($" VRAM {vramMb:F0}/{vramTotalMb:F0}MB")
                                        .WithForeground(Theme.Text).Height(1));
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