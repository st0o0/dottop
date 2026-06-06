using dottop.Models;
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
    private GraphNode? _detailGraph;
    private GraphNode? _diskActiveGraph;
    private GraphNode? _diskTransferGraph;
    private GraphNode? _cpuGraph;
    private GraphNode? _ramGraph;
    private GraphNode? _gpuGraph;
    private CpuCoresNode? _coresNode;
    private PanelNode? _detailPanel;

    public override ILayoutNode BuildLayout()
    {
        var graphStyle = ViewModel.GraphStyleSetting;

        _cpuGraph = new GraphNode().WithStyle(graphStyle).WithColor(Theme.Graph).WithRange(0, 100);
        _ramGraph = new GraphNode().WithStyle(graphStyle).WithColor(Theme.Graph).WithRange(0, 100);
        _gpuGraph = new GraphNode().WithStyle(graphStyle).WithColor(Theme.Graph).WithRange(0, 100);
        _coresNode = new CpuCoresNode();

        _detailPanel = new PanelNode()
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Theme.Primary);

        var bottomRow = Layouts.Horizontal()
            .WithChild(BuildDiskPanel()).WithSpacing(1)
            .WithChild(BuildNetworkPanel());
        if (ViewModel.GpuAvailable)
            bottomRow.WithSpacing(1).WithChild(BuildGpuPanel());

        var overviewLayout = Layouts.Vertical()
            .WithChild(new TabBarNode(1))
            .WithChild(Layouts.Horizontal()
                .WithChild(BuildCpuPanel()).WithSpacing(1).WithChild(BuildRamPanel())
                .HeightPercent(50))
            .WithChild(bottomRow.Fill())
            .WithChild(new TextNode($" {Strings.PerfStatusBar}")
                .WithForeground(Theme.StatusBarText).WithBackground(Theme.StatusBar).Height(1));

        var detailLayout = Layouts.Vertical()
            .WithChild(new TabBarNode(1))
            .WithChild(_detailPanel.Fill())
            .WithChild(new TextNode($" Esc: {Strings.HintClose}  ←→/Tab: Sections")
                .WithForeground(Theme.StatusBarText).WithBackground(Theme.StatusBar).Height(1));

        return new ConditionalNode(ViewModel.IsDetailOpen, detailLayout, overviewLayout);
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
                    _gpuGraph?.Push(gpu.UsagePercent);

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
                        _detailGraph?.Push(gpuDetail.UsagePercent);
                }
            })
            .DisposeWith(Subscriptions);

        ViewModel.CpuCores.Subscribe(cores => _coresNode?.SetCores(cores))
            .DisposeWith(Subscriptions);

        ViewModel.DetailContentChanged.Subscribe(_ => UpdateDetailPanel())
            .DisposeWith(Subscriptions);
    }

    private void UpdateDetailPanel()
    {
        if (_detailPanel is null) return;

        var section = ViewModel.DetailSection.Value;
        var sections = new List<string> { "CPU", "RAM", "Disk", Strings.DetailSectionNetwork };
        if (ViewModel.GpuAvailable) sections.Add("GPU");

        var tabBar = Layouts.Horizontal();
        for (var i = 0; i < sections.Count; i++)
        {
            var node = new TextNode($" {sections[i]} ");
            if (i == (int)section)
                node.WithForeground(Theme.SelectionText).WithBackground(Theme.Selection);
            else
                node.WithForeground(Theme.Secondary);
            tabBar.WithChild(node);
        }

        var graphStyle = ViewModel.GraphStyleSetting;
        var (title, info) = section switch
        {
            PerfDetailSection.Cpu => ("CPU", BuildCpuDetailInfo()),
            PerfDetailSection.Ram => ("RAM", BuildRamDetailInfo()),
            PerfDetailSection.Disk => ("Disk", BuildDiskDetailInfo()),
            PerfDetailSection.Network => (Strings.DetailSectionNetwork, BuildNetworkDetailInfo()),
            PerfDetailSection.Gpu => ("GPU", BuildGpuDetailInfo()),
            _ => ("", (ILayoutNode)Layouts.Vertical())
        };

        _detailPanel.WithTitle(string.Format(Strings.DetailTitle, title)).WithTitleColor(Theme.Primary);

        if (section is PerfDetailSection.Cpu or PerfDetailSection.Ram or PerfDetailSection.Gpu)
        {
            _detailGraph = new GraphNode().WithStyle(graphStyle).WithColor(Theme.Graph).WithRange(0, 100);
            _detailPanel.WithContent(Layouts.Vertical()
                .WithChild(tabBar.Height(1))
                .WithChild(info)
                .WithChild(_detailGraph.Fill()));
        }
        else
        {
            _detailPanel.WithContent(Layouts.Vertical()
                .WithChild(tabBar.Height(1))
                .WithChild(Layouts.Vertical().WithChild(info).Fill()));
        }
    }

    private ILayoutNode BuildCpuDetailInfo()
    {
        var coresNode = new CpuCoresNode();
        coresNode.SetCores(ViewModel.CpuCores.Value);
        return Layouts.Vertical()
            .WithChild(new TextNode($" {ViewModel.CpuName.Value}  —  {Strings.TotalLabel} {ViewModel.CpuTotal.Value:F1}%")
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
                .WithForeground(Theme.Accent).Height(1))
            .WithChild(new TextNode($" {BuildBar(pct, 40)}").WithForeground(Theme.Graph).Height(1));
    }

    private ILayoutNode BuildDiskDetailInfo()
    {
        var disks = ViewModel.Disks.Value;
        if (disks.Count == 0)
            return new TextNode(Strings.PerfNoDisks).WithForeground(Theme.TextDim);

        var idx = Math.Clamp(ViewModel.DiskDetailIndex.Value, 0, disks.Count - 1);
        var disk = disks[idx];
        var usedGb = disk.UsedBytes / 1024.0 / 1024 / 1024;
        var totalGb = disk.TotalBytes / 1024.0 / 1024 / 1024;
        var graphStyle = ViewModel.GraphStyleSetting;

        var diskTabs = Layouts.Horizontal();
        for (var i = 0; i < disks.Count; i++)
        {
            var label = new TextNode($" {disks[i].Name} ");
            if (i == idx) label.WithForeground(Theme.SelectionText).WithBackground(Theme.Selection);
            else label.WithForeground(Theme.Secondary);
            diskTabs.WithChild(label);
        }

        _diskActiveGraph = new GraphNode().WithStyle(graphStyle).WithColor(Theme.Graph).WithRange(0, 100);
        _diskTransferGraph = new GraphNode().WithStyle(graphStyle).WithColor(Theme.Graph).WithRange(0, 100_000_000);

        return Layouts.Vertical()
            .WithChild(diskTabs.Height(1))
            .WithChild(new TextNode($" {disk.Name}  {usedGb:F1}/{totalGb:F1} GB ({disk.UsedPercent:F0}%)  {BuildBar(disk.UsedPercent, 20)}")
                .WithForeground(Theme.Accent).Height(1))
            .WithChild(new TextNode($" Read: {FormatBytes(disk.ReadBytesPerSec)}  Write: {FormatBytes(disk.WriteBytesPerSec)}  Active: {disk.ActiveTimePercent:F0}%")
                .WithForeground(Theme.Text).Height(1))
            .WithChild(new PanelNode().WithTitle($" {Strings.PerfActiveTime} ").WithBorder(BorderStyle.Rounded)
                .WithBorderColor(Theme.Border).WithContent(_diskActiveGraph).HeightPercent(50).Fill())
            .WithChild(new PanelNode().WithTitle($" {Strings.PerfTransferRate} ").WithBorder(BorderStyle.Rounded)
                .WithBorderColor(Theme.Border).WithContent(_diskTransferGraph).Fill());
    }

    private ILayoutNode BuildNetworkDetailInfo()
    {
        var nets = ViewModel.Networks.Value;
        var layout = Layouts.Vertical();
        if (nets.Count == 0)
        {
            layout.WithChild(new TextNode(Strings.PerfNoAdapters).WithForeground(Theme.TextDim).Height(1));
            return layout;
        }
        foreach (var net in nets)
        {
            layout.WithChild(new TextNode($" {net.Name}").WithForeground(Theme.Accent).Height(1));
            layout.WithChild(new TextNode($"   ↓ {FormatBytes(net.RxBytesPerSec),-14}  ↑ {FormatBytes(net.TxBytesPerSec)}")
                .WithForeground(Theme.Text).Height(1));
        }
        return layout;
    }

    private ILayoutNode BuildGpuDetailInfo()
    {
        var gpu = ViewModel.Gpu.Value;
        if (gpu is null) return new TextNode(Strings.GpuNoData).WithForeground(Theme.TextDim);

        var vramUsedMb = gpu.VramUsedBytes / 1024.0 / 1024;
        var vramTotalMb = gpu.VramTotalBytes / 1024.0 / 1024;
        return Layouts.Vertical()
            .WithChild(new TextNode($" {gpu.Name}").WithForeground(Theme.Accent).Height(1))
            .WithChild(new TextNode($" {Strings.GpuUsage} {gpu.UsagePercent:F0}%  {BuildBar(gpu.UsagePercent, 20)}")
                .WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($" VRAM: {vramUsedMb:F0}/{vramTotalMb:F0} MB ({gpu.VramUsedPercent:F1}%)  {BuildBar(gpu.VramUsedPercent, 20)}")
                .WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($" {Strings.GpuTemperature} {gpu.TemperatureCelsius:F0}°C")
                .WithForeground(gpu.TemperatureCelsius > 80 ? Color.BrightRed : Theme.Text).Height(1));
    }

    // --- Overview Panels ---

    private ILayoutNode BuildCpuPanel() =>
        new PanelNode().WithTitle(" CPU ").WithBorder(BorderStyle.Rounded).WithBorderColor(Theme.Border)
            .WithContent(Layouts.Vertical()
                .WithChild(ViewModel.CpuTotal
                    .Select<double, ILayoutNode>(pct => new TextNode($" {Strings.TotalLabel} {pct:F1}%").WithForeground(Theme.Accent))
                    .AsLayout().Height(1))
                .WithChild(_coresNode!)
                .WithChild(_cpuGraph!.Fill()))
            .Fill();

    private ILayoutNode BuildRamPanel() =>
        new PanelNode().WithTitle(" RAM ").WithBorder(BorderStyle.Rounded).WithBorderColor(Theme.Border)
            .WithContent(Layouts.Vertical()
                .WithChild(ViewModel.RamUsed.CombineLatest<ulong, ulong, ILayoutNode>(ViewModel.RamTotal,
                    (used, total) =>
                    {
                        var usedGb = used / 1024.0 / 1024 / 1024;
                        var totalGb = total / 1024.0 / 1024 / 1024;
                        var pct = total > 0 ? (double)used / total * 100 : 0;
                        return new TextNode($" {usedGb:F1} / {totalGb:F1} GiB  {pct:F1}%").WithForeground(Theme.Accent);
                    }).AsLayout().Height(1))
                .WithChild(_ramGraph!.Fill()).Fill())
            .Fill();

    private ILayoutNode BuildDiskPanel() =>
        new PanelNode().WithTitle(" Disks ").WithBorder(BorderStyle.Rounded).WithBorderColor(Theme.Border)
            .WithContent(ViewModel.Disks
                .Select<IReadOnlyList<DiskSnapshot>, ILayoutNode>(disks =>
                {
                    if (disks.Count == 0) return new TextNode(Strings.PerfNoDisks).WithForeground(Theme.TextDim);
                    var layout = Layouts.Vertical();
                    foreach (var disk in disks)
                    {
                        var usedGb = disk.UsedBytes / 1024.0 / 1024 / 1024;
                        var totalGb = disk.TotalBytes / 1024.0 / 1024 / 1024;
                        layout.WithChild(new TextNode($" {disk.Name,-4} {usedGb:F0}/{totalGb:F0}GB {disk.UsedPercent:F0}%")
                            .WithForeground(Theme.Text).Height(1));
                    }
                    return layout;
                }).AsLayout())
            .Fill();

    private ILayoutNode BuildNetworkPanel() =>
        new PanelNode().WithTitle($" {Strings.DetailSectionNetwork} ").WithBorder(BorderStyle.Rounded).WithBorderColor(Theme.Border)
            .WithContent(ViewModel.Networks
                .Select<IReadOnlyList<NetworkSnapshot>, ILayoutNode>(nets =>
                {
                    if (nets.Count == 0) return new TextNode(Strings.PerfNoAdapters).WithForeground(Theme.TextDim);
                    var layout = Layouts.Vertical();
                    foreach (var net in nets.Take(4))
                        layout.WithChild(new TextNode($" {net.Name}  ↓{FormatBytes(net.RxBytesPerSec)}  ↑{FormatBytes(net.TxBytesPerSec)}")
                            .WithForeground(Theme.Text).Height(1));
                    return layout;
                }).AsLayout())
            .Fill();

    private ILayoutNode BuildGpuPanel() =>
        new PanelNode().WithTitle(" GPU ").WithBorder(BorderStyle.Rounded).WithBorderColor(Theme.Border)
            .WithContent(Layouts.Vertical()
                .WithChild(ViewModel.Gpu
                    .Select<GpuSnapshot?, ILayoutNode>(g =>
                        g is not null
                            ? new TextNode($" {g.UsagePercent:F0}%  {g.TemperatureCelsius:F0}°C").WithForeground(Theme.Accent)
                            : new TextNode(Strings.GpuNoData).WithForeground(Theme.TextDim))
                    .AsLayout().Height(1))
                .WithChild(_gpuGraph!.Fill()).Fill())
            .Fill();

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
