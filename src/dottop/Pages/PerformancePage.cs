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
    public override ILayoutNode BuildLayout()
    {
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
            .WithChild(new TextNode(" 1-5: Tab wechseln  |  Q: Beenden")
                .WithForeground(Color.Black).WithBackground(Color.BrightCyan).Height(1));
    }

    private ILayoutNode BuildCpuPanel()
    {
        var cpuGraph = new GraphNode()
            .WithStyle(GraphStyle.Blocks)
            .WithColor(Color.BrightGreen)
            .WithRange(0, 100);

        Observable.Interval(TimeSpan.FromMilliseconds(200))
            .Subscribe(_ => cpuGraph.Push(ViewModel.CpuTotal.Value))
            .DisposeWith(Subscriptions);

        return new PanelNode()
            .WithTitle(" CPU ")
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Color.BrightGreen)
            .WithContent(
                Layouts.Vertical()
                    .WithChild(
                        ViewModel.CpuCores
                            .Select<IReadOnlyList<double>, ILayoutNode>(cores =>
                            {
                                var layout = Layouts.Horizontal();
                                for (var i = 0; i < cores.Count; i++)
                                    layout.WithChild(
                                        new TextNode($" C{i}:{cores[i],3:F0}%")
                                            .WithForeground(Color.BrightGreen));
                                return layout;
                            }).AsLayout().Height(1))
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
                                new TextNode($" {disk.Name,-12} {usedGb:F0}/{totalGb:F0} GB  {disk.UsedPercent:F0}%")
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
                                new TextNode($" {net.Name}  ↓ {FormatBytes(net.RxBytesPerSec)}  ↑ {FormatBytes(net.TxBytesPerSec)}")
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
