using dottop.Models;
using dottop.Nodes;
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

    public override ILayoutNode BuildLayout()
    {
        _detailModal = new ModalNode()
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Color.BrightCyan)
            .WithBackdrop(BackdropStyle.Solid)
            .WithBackdropColor(Color.Black)
            .WithDismissOnEscape(false)
            .WithPadding(1);

        _detailGraph = new GraphNode()
            .WithStyle(GraphStyle.Blocks)
            .WithColor(Color.BrightGreen)
            .WithRange(0, 100);

        ViewModel.DetailContentChanged.Subscribe(_ => UpdateDetailModal())
            .DisposeWith(Subscriptions);

        Observable.Interval(TimeSpan.FromMilliseconds(200))
            .Subscribe(_ =>
            {
                if (ViewModel.IsDetailOpen.Value)
                {
                    var value = ViewModel.DetailSection.Value switch
                    {
                        PerfDetailSection.Cpu => ViewModel.CpuTotal.Value,
                        PerfDetailSection.Ram => ViewModel.RamTotal.Value > 0
                            ? (double)ViewModel.RamUsed.Value / ViewModel.RamTotal.Value * 100 : 0,
                        _ => 0
                    };
                    _detailGraph?.Push(value);
                }
            })
            .DisposeWith(Subscriptions);

        var conditionalDetail = new ConditionalNode(ViewModel.IsDetailOpen, _detailModal);

        return Layouts.Vertical()
            .WithChild(new TabBarNode(1))
            .WithChild(Layouts.Horizontal()
                .WithChild(BuildCpuPanel())
                .WithSpacing(1)
                .WithChild(BuildRamPanel())
                .HeightPercent(50)
                .Fill())
            .WithChild(Layouts.Horizontal()
                .WithChild(BuildDiskPanel())
                .WithSpacing(1)
                .WithChild(BuildNetworkPanel())
                .Fill())
            .WithChild(new TextNode(" Enter/Tab: Detail  |  1-5: Tab  |  Q: Beenden")
                .WithForeground(Color.Black).WithBackground(Color.BrightCyan).Height(1))
            .WithChild(conditionalDetail);
    }

    private void UpdateDetailModal()
    {
        if (_detailModal is null || _detailGraph is null) return;

        var section = ViewModel.DetailSection.Value;
        var sections = new[] { "CPU", "RAM", "Disk", "Netzwerk" };
        var sectionIdx = (int)section;

        var tabBar = Layouts.Horizontal();
        for (var i = 0; i < sections.Length; i++)
        {
            var node = new TextNode($" {sections[i]} ");
            if (i == sectionIdx)
                node.WithForeground(Color.Black).WithBackground(Color.BrightCyan);
            else
                node.WithForeground(Color.Gray);
            tabBar.WithChild(node);
        }

        var (color, title, info) = section switch
        {
            PerfDetailSection.Cpu => (Color.BrightGreen, "CPU", BuildCpuDetailInfo()),
            PerfDetailSection.Ram => (Color.BrightBlue, "RAM", BuildRamDetailInfo()),
            PerfDetailSection.Disk => (Color.BrightYellow, "Disk", BuildDiskDetailInfo()),
            PerfDetailSection.Network => (Color.BrightMagenta, "Netzwerk", BuildNetworkDetailInfo()),
            _ => (Color.White, "", Layouts.Vertical() as ILayoutNode)
        };

        _detailGraph.WithColor(color).WithRange(0, 100);
        _detailModal.WithTitle($" {title} Detail ").WithTitleColor(color).WithBorderColor(color);

        var showGraph = section is PerfDetailSection.Cpu or PerfDetailSection.Ram;

        if (showGraph)
        {
            _detailModal.Content = Layouts.Vertical()
                .WithChild(tabBar.Height(1))
                .WithChild(info)
                .WithChild(_detailGraph.Fill());
        }
        else
        {
            _detailModal.Content = Layouts.Vertical()
                .WithChild(tabBar.Height(1))
                .WithChild(Layouts.Vertical().WithChild(info).Fill());
        }
    }

    private ILayoutNode BuildCpuDetailInfo()
    {
        var coresNode = new CpuCoresNode();
        coresNode.SetCores(ViewModel.CpuCores.Value);

        return Layouts.Vertical()
            .WithChild(new TextNode($" {ViewModel.CpuName.Value}  —  Gesamt: {ViewModel.CpuTotal.Value:F1}%")
                .WithForeground(Color.BrightGreen).Height(1))
            .WithChild(coresNode);
    }

    private ILayoutNode BuildRamDetailInfo()
    {
        var usedGb = ViewModel.RamUsed.Value / 1024.0 / 1024 / 1024;
        var totalGb = ViewModel.RamTotal.Value / 1024.0 / 1024 / 1024;
        var pct = ViewModel.RamTotal.Value > 0 ? (double)ViewModel.RamUsed.Value / ViewModel.RamTotal.Value * 100 : 0;

        return Layouts.Vertical()
            .WithChild(new TextNode($" Benutzt: {usedGb:F1} GiB / {totalGb:F1} GiB  ({pct:F1}%)")
                .WithForeground(Color.BrightBlue).Height(1))
            .WithChild(new TextNode($" {BuildBar(pct, 40)}")
                .WithForeground(Color.BrightBlue).Height(1));
    }

    private ILayoutNode BuildDiskDetailInfo()
    {
        var disks = ViewModel.Disks.Value;
        var layout = Layouts.Vertical();
        foreach (var disk in disks)
        {
            var usedGb = disk.UsedBytes / 1024.0 / 1024 / 1024;
            var totalGb = disk.TotalBytes / 1024.0 / 1024 / 1024;
            layout.WithChild(new TextNode($" {disk.Name,-4} {BuildBar(disk.UsedPercent, 30)} {usedGb:F0}/{totalGb:F0} GB ({disk.UsedPercent:F0}%)")
                .WithForeground(disk.UsedPercent > 90 ? Color.BrightRed : disk.UsedPercent > 75 ? Color.BrightYellow : Color.BrightGreen)
                .Height(1));
        }
        return layout;
    }

    private ILayoutNode BuildNetworkDetailInfo()
    {
        var nets = ViewModel.Networks.Value;
        var layout = Layouts.Vertical();
        if (nets.Count == 0)
        {
            layout.WithChild(new TextNode(" Keine aktiven Adapter").WithForeground(Color.Gray).Height(1));
            return layout;
        }
        foreach (var net in nets)
        {
            layout.WithChild(new TextNode($" {net.Name}")
                .WithForeground(Color.BrightMagenta).Height(1));
            layout.WithChild(new TextNode($"   ↓ {FormatBytes(net.RxBytesPerSec),-14}  ↑ {FormatBytes(net.TxBytesPerSec)}")
                .WithForeground(Color.Magenta).Height(1));
        }
        return layout;
    }

    // --- Main panels (overview) ---

    private ILayoutNode BuildCpuPanel()
    {
        var cpuGraph = new GraphNode()
            .WithStyle(GraphStyle.Blocks)
            .WithColor(Color.BrightGreen)
            .WithRange(0, 100);

        Observable.Interval(TimeSpan.FromMilliseconds(200))
            .Subscribe(_ => cpuGraph.Push(ViewModel.CpuTotal.Value))
            .DisposeWith(Subscriptions);

        var coresNode = new CpuCoresNode();
        ViewModel.CpuCores.Subscribe(cores => coresNode.SetCores(cores))
            .DisposeWith(Subscriptions);

        return new PanelNode()
            .WithTitle(" CPU ")
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Color.BrightGreen)
            .WithContent(
                Layouts.Vertical()
                    .WithChild(
                        ViewModel.CpuTotal
                            .Select<double, ILayoutNode>(pct =>
                                new TextNode($" Gesamt: {pct:F1}%")
                                    .WithForeground(Color.BrightGreen))
                            .AsLayout().Height(1))
                    .WithChild(coresNode)
                    .WithChild(cpuGraph.Fill()))
            .Fill();
    }

    private ILayoutNode BuildRamPanel()
    {
        var ramGraph = new GraphNode()
            .WithStyle(GraphStyle.Braille)
            .WithColor(Color.BrightBlue)
            .WithRange(0, 100);

        Observable.Interval(TimeSpan.FromMilliseconds(200))
            .Subscribe(_ =>
            {
                var total = ViewModel.RamTotal.Value;
                var used = ViewModel.RamUsed.Value;
                var pct = total > 0 ? (double)used / total * 100 : 0;
                ramGraph.Push(pct);
            })
            .DisposeWith(Subscriptions);

        return new PanelNode()
            .WithTitle(" RAM ")
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Color.BrightBlue)
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
                                    .WithForeground(Color.BrightBlue);
                            }).AsLayout().Height(1))
                    .WithChild(ramGraph.Fill())
                    .Fill())
            .Fill();
    }

    private ILayoutNode BuildDiskPanel()
    {
        return new PanelNode()
            .WithTitle(" Disks ")
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Color.BrightYellow)
            .WithContent(
                ViewModel.Disks
                    .Select<IReadOnlyList<DiskSnapshot>, ILayoutNode>(disks =>
                    {
                        if (disks.Count == 0)
                            return new TextNode(" No disks found").WithForeground(Color.Gray);
                        var layout = Layouts.Vertical();
                        foreach (var disk in disks)
                        {
                            var usedGb = disk.UsedBytes / 1024.0 / 1024 / 1024;
                            var totalGb = disk.TotalBytes / 1024.0 / 1024 / 1024;
                            layout.WithChild(
                                new TextNode($" {disk.Name,-4} {usedGb:F0}/{totalGb:F0}GB {disk.UsedPercent:F0}%")
                                    .WithForeground(Color.BrightYellow).Height(1));
                        }
                        return layout;
                    }).AsLayout())
            .Fill();
    }

    private ILayoutNode BuildNetworkPanel()
    {
        return new PanelNode()
            .WithTitle(" Network ")
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Color.BrightMagenta)
            .WithContent(
                ViewModel.Networks
                    .Select<IReadOnlyList<NetworkSnapshot>, ILayoutNode>(nets =>
                    {
                        if (nets.Count == 0)
                            return new TextNode(" No active adapters").WithForeground(Color.Gray);
                        var layout = Layouts.Vertical();
                        foreach (var net in nets.Take(4))
                            layout.WithChild(
                                new TextNode($" {net.Name}  ↓{FormatBytes(net.RxBytesPerSec)}  ↑{FormatBytes(net.TxBytesPerSec)}")
                                    .WithForeground(Color.BrightMagenta).Height(1));
                        return layout;
                    }).AsLayout())
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
