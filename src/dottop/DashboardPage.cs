using R3;
using Termina.Extensions;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop;

public class DashboardPage : ReactivePage<DashboardViewModel>
{
    public override ILayoutNode BuildLayout()
    {
        return Layouts.Vertical()
            .WithChild(BuildTitleBar())
            .WithChild(Layouts.Horizontal()
                .WithChild(BuildCpuPanel())
                .WithSpacing(1)
                .WithChild(BuildRamPanel())
                .HeightPercent(50)
                .Fill())
            .WithChild(
                Layouts.Horizontal()
                    .WithChild(BuildDiskPanel())
                    .WithSpacing(1)
                    .WithChild(BuildNetworkPanel())
                    .WithSpacing(1)
                    .WithChild(BuildProcessPanel())
                    .Fill())
            .WithChild(BuildStatusBar());
    }

    private ILayoutNode BuildTitleBar()
    {
        return new TextNode(" ⚡ dottop")
            .AlignCenter()
            .WithForeground(Color.BrightCyan)
            .Height(1);
    }

    private ILayoutNode BuildCpuPanel()
    {
        var cpuGraph = new GraphNode()
            .WithStyle(GraphStyle.Blocks)
            .WithColor(Color.BrightGreen)
            .WithRange(0, 100);

        ViewModel.CpuUsage
            .Subscribe(h => cpuGraph.Push(h))
            .DisposeWith(Subscriptions);

        return new PanelNode()
            .WithTitle(" CPU ")
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Color.BrightGreen)
            .WithContent(
                Layouts.Horizontal()
                    .WithChild(
                        ViewModel.CpuCores
                            .Select<List<double>, ILayoutNode>(cores =>
                            {
                                var layout = Layouts.Vertical();
                                for (var i = 0; i < cores.Count; i++)
                                {
                                    layout.WithChild(
                                        new TextNode($" C{i,-2} {BuildBar(cores[i], 10)} {cores[i],5:F0}%")
                                            .WithForeground(Color.BrightGreen)
                                            .Height(1));
                                }

                                return layout;
                            }).AsLayout().Width(25).Height(4))
                    .WithSpacing(1)
                    .WithChild(Layouts.Vertical().WithChild(cpuGraph).Fill()));
    }

    private ILayoutNode BuildRamPanel()
    {
        var ramGraph = new GraphNode()
            .WithStyle(GraphStyle.Braille)
            .WithColor(Color.BrightBlue)
            .WithRange(0, 100);


        ViewModel.RamUsed.CombineLatest(ViewModel.RamTotal, (used, total) => total > 0 ? (double)used / total * 100 : 0)
            .Subscribe(h => ramGraph.Push(h))
            .DisposeWith(Subscriptions);

        return new PanelNode()
            .WithTitle(" RAM ")
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Color.BrightBlue)
            .WithContent(
                Layouts.Vertical()
                    .WithChild(
                        ViewModel.RamUsed.CombineLatest<ulong, ulong, ILayoutNode>(
                            ViewModel.RamTotal,
                            (used, total) =>
                            {
                                var usedGb = used / 1024.0 / 1024 / 1024;
                                var totalGb = total / 1024.0 / 1024 / 1024;
                                var pct = total > 0 ? (double)used / total * 100 : 0;
                                return Layouts.Horizontal()
                                    .WithChild(new TextNode($" {usedGb:F1} / {totalGb:F1} GiB")
                                        .WithForeground(Color.BrightBlue).Fill())
                                    .WithChild(new TextNode($"{pct:F1}% ").WithForeground(Color.BrightBlue)
                                        .WidthFill());
                            }).AsLayout().Height(1))
                    .WithChild(
                        ViewModel.RamUsed.CombineLatest<ulong, ulong, ILayoutNode>(ViewModel.RamTotal,
                            (used, total) =>
                            {
                                var pct = total > 0 ? (double)used / total * 100 : 0;
                                return new TextNode($" {BuildBar(pct, 30)}").WithForeground(Color.Blue);
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
                    .Select<List<DiskInfo>, ILayoutNode>(disks =>
                    {
                        if (disks.Count == 0)
                        {
                            return new TextNode(" No disks found").WithForeground(Color.Gray);
                        }

                        var layout = Layouts.Vertical();
                        foreach (var disk in disks)
                        {
                            var usedGb = disk.Used / 1024.0 / 1024 / 1024;
                            var totalGb = disk.Total / 1024.0 / 1024 / 1024;

                            layout
                                .WithChild(
                                    new TextNode(
                                            $" {disk.Name,-12} {usedGb:F0}/{totalGb:F0} GB  {disk.UsedPercent:F0}%")
                                        .WithForeground(Color.BrightYellow).Height(1))
                                .WithChild(
                                    new TextNode($" {BuildBar(disk.UsedPercent, 34)}")
                                        .WithForeground(GetDiskColor(disk.UsedPercent)).Height(1));
                        }

                        return layout;
                    })
                    .AsLayout())
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
                    .Select<List<NetworkInfo>, ILayoutNode>(nets =>
                    {
                        if (nets.Count == 0)
                        {
                            return new TextNode(" No active adapters").WithForeground(Color.Gray);
                        }

                        var layout = Layouts.Vertical();
                        foreach (var net in nets.Take(4))
                        {
                            layout
                                .WithChild(
                                    new TextNode($" {net.Name}")
                                        .WithForeground(Color.BrightMagenta).Height(1))
                                .WithChild(
                                    new TextNode($"   ↓ {FormatBytes(net.RxPerSec),-12}  ↑ {FormatBytes(net.TxPerSec)}")
                                        .WithForeground(Color.Magenta).Height(1));
                        }

                        return layout;
                    })
                    .AsLayout())
            .Fill();
    }

    private ILayoutNode BuildProcessPanel()
    {
        var layout = Layouts.Vertical();

        var node = new ScrollableContainerNode()
            .WithContent(layout)
            .WithScrollbar(true);

        ViewModel.ScrollEvent
            .Subscribe(t =>
            {
                if (t is -1)
                {
                    node.ScrollDown();
                }

                if (t is 1)
                {
                    node.ScrollUp();
                }
            })
            .DisposeWith(Subscriptions);

        ViewModel.Processes.Subscribe(value =>
        {
            // if (value.Count == 0)
            //     layout.
            //
            // for (var i = 0; i < value.Count; i++)
            // {
            //     if (i < list.Count)
            //     {
            //         var p = list[i];
            //
            //         _processRows[i].SetText(
            //             $" {p.PId,5} {p.Name,-18} {p.WorkingSet64 / 1024 / 1024,6} MB");
            //
            //         _processRows[i].IsVisible = true;
            //     }
            //     else
            //     {
            //         _processRows[i].SetText("");
            //         _processRows[i].IsVisible = false;
            //     }
            // }
        });

        return new PanelNode()
            .WithTitle(" Processes ")
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Color.Cyan)
            .WithContent(
                ViewModel.Processes
                    .Select<List<ProcessInfo>, ILayoutNode>(list =>
                    {
                        var content = Layouts.Vertical();

                        if (list.Count == 0)
                        {
                            content.WithChild(
                                new TextNode(" No processes found")
                                    .WithForeground(Color.Gray)
                                    .Height(1));
                        }
                        else
                        {
                            foreach (var p in list)
                            {
                                content.WithChild(
                                    new TextNode($" {p.PId,5} {p.Name,-18} {p.WorkingSet64 / 1024 / 1024,6} MB")
                                        .WithForeground(Color.Cyan)
                                        .Height(1));
                            }
                        }

                        return node.WithContent(content);
                    })
                    .AsLayout())
            .Fill();
    }

    private ILayoutNode BuildStatusBar()
    {
        return ViewModel.StatusMessage
            .Select<string, ILayoutNode>(msg => new TextNode($" {msg}")
                .WithForeground(Color.Black)
                .WithBackground(Color.BrightCyan))
            .AsLayout()
            .Height(1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string BuildBar(double percent, int width)
    {
        var filled = Math.Clamp((int)(percent / 100.0 * width), 0, width);
        return $"[{"".PadRight(filled, '█')}{new string('░', width - filled)}]";
    }

    private static Color GetDiskColor(double pct) => pct switch
    {
        >= 90 => Color.BrightRed,
        >= 75 => Color.BrightYellow,
        _ => Color.BrightGreen,
    };

    private static string FormatBytes(ulong bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB/s",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB/s",
        >= 1_024 => $"{bytes / 1_024.0:F1} KB/s",
        _ => $"{bytes} B/s",
    };
}