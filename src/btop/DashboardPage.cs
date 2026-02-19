using System.Reactive.Linq;
using Termina.Extensions;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace btop;

public class DashboardPage : ReactivePage<DashboardViewModel>
{
    private GraphNode _cpuGraph = null!;
    private GraphNode _ramGraph = null!;

    public override ILayoutNode BuildLayout()
    {
        _cpuGraph = new GraphNode()
            .WithStyle(GraphStyle.Blocks)
            .WithColor(Color.BrightGreen)
            .WithRange(0, 100);

        _ramGraph = new GraphNode()
            .WithStyle(GraphStyle.Braille)
            .WithColor(Color.BrightBlue)
            .WithRange(0, 100);

        ViewModel.CpuHistoryChanged
            .Subscribe(h => _cpuGraph.SetData(h))
            .DisposeWith(Subscriptions);

        ViewModel.RamHistoryChanged
            .Subscribe(h => _ramGraph.SetData(h))
            .DisposeWith(Subscriptions);

        return Layouts.Vertical()
            .WithChild(BuildTitleBar())
            .WithChild(
                Layouts.Horizontal()
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
                    .Fill())
            .WithChild(BuildStatusBar());
    }

    private ILayoutNode BuildTitleBar()
    {
        return new TextNode(" ⚡ btop.net")
            .WithForeground(Color.BrightCyan)
            .WithBackground(Color.DarkGray)
            .Height(1);
    }

    private ILayoutNode BuildCpuPanel()
    {
        return new PanelNode()
            .WithTitle(" CPU ")
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Color.BrightGreen)
            .WithContent(
                Layouts.Vertical()
                    .WithChild(
                        ViewModel.CpuNameChanged.CombineLatest(
                            ViewModel.CpuUsageChanged,
                            (name, usage) => Layouts.Horizontal()
                                .WithChild(new TextNode($" {name}").WithForeground(Color.BrightGreen).Fill())
                                .WithChild(new TextNode($"{usage:F1}% ").WithForeground(Color.BrightGreen).WidthFill())
                        ).AsLayout().Height(1))
                    .WithChild(
                        ViewModel.CpuUsageChanged
                            .Select(u => new TextNode($" {BuildBar(u, 30)}").WithForeground(Color.Green))
                            .AsLayout().Height(1))
                    .WithChild(_cpuGraph.Fill()))
            .Fill();
    }

    private ILayoutNode BuildRamPanel()
    {
        return new PanelNode()
            .WithTitle(" RAM ")
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Color.BrightBlue)
            .WithContent(
                Layouts.Vertical()
                    .WithChild(
                        ViewModel.RamUsedChanged.CombineLatest(
                            ViewModel.RamTotalChanged,
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
                        ViewModel.RamUsedChanged.CombineLatest(ViewModel.RamTotalChanged,
                            (used, total) =>
                            {
                                var pct = total > 0 ? (double)used / total * 100 : 0;
                                return new TextNode($" {BuildBar(pct, 30)}").WithForeground(Color.Blue);
                            }).AsLayout().Height(1))
                    .WithChild(_ramGraph.Fill())
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
                ViewModel.DisksChanged
                    .Select<List<DiskInfo>, ILayoutNode>(disks =>
                    {
                        if (disks.Count == 0)
                        {
                            return new TextNode(" No disks found").WithForeground(Color.Gray);
                        }

                        var layout = Layouts.Vertical();
                        foreach (var disk in disks.Take(5))
                        {
                            var name = CleanDiskName(disk.Name);
                            var usedGb = disk.Used / 1024.0 / 1024 / 1024;
                            var totalGb = disk.Total / 1024.0 / 1024 / 1024;

                            layout
                                .WithChild(
                                    new TextNode($" {name,-12} {usedGb:F0}/{totalGb:F0} GB  {disk.UsedPercent:F0}%")
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
                ViewModel.NetworksChanged
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

    private ILayoutNode BuildStatusBar()
    {
        return ViewModel.StatusMessageChanged
            .Select(msg => new TextNode($" {msg}")
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

    private static string CleanDiskName(string raw)
    {
        if (raw.StartsWith(@"\\.\PHYSICALDRIVE", StringComparison.OrdinalIgnoreCase))
        {
            return "Disk " + raw[^1];
        }

        if (raw.StartsWith("/dev/", StringComparison.Ordinal))
        {
            return raw[5..];
        }

        return raw.Length > 12 ? raw[..12] : raw;
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