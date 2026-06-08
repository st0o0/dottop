using dottop.App.Nodes;
using dottop.App.Resources;
using dottop.App.Themes;
using dottop.Core.Models;
using R3;
using Termina.Extensions;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop.App.Pages;

public class DockerPage : ReactivePage<DockerViewModel>
{
    private DataListNode<ContainerSnapshot>? _list;
    private DataListNode<string>? _logList;
    private ModalNode? _detailModal;
    private ModalNode? _settingsModal;

    public override ILayoutNode BuildLayout()
    {
        _list = new DataListNode<ContainerSnapshot>(
            c =>
            {
                var name = c.Name.Length > 20 ? c.Name[..19] + "…" : c.Name;
                var image = c.Image.Length > 20 ? c.Image[..19] + "…" : c.Image;
                var statusIcon = c.Status is "running" ? "▶" : "■";
                var cpuStr = c.CpuPercent > 0 ? $"{c.CpuPercent,5:F1}%" : "    —";
                var ramMb = c.MemoryUsageBytes / 1024 / 1024;
                var ramStr = ramMb > 0 ? (ramMb >= 1024 ? $"{ramMb / 1024.0:F1}GB" : $"{ramMb}MB") : "  —";
                var port = c.Ports.Count > 0 ? c.Ports[0] : "—";
                if (port.Length > 12) port = port[..11] + "…";
                return $" {statusIcon} {name,-20} {image,-20} {cpuStr} {ramStr,7} {port,-12} {c.State}";
            },
            c => c.Status switch
            {
                "running" => Theme.Text,
                "restarting" => Theme.Warning,
                _ => Theme.TextDim
            });

        ViewModel.ListNode = _list;
        ViewModel.GetSelectedItem = () => _list.SelectedItem;

        _detailModal = new ModalNode()
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Theme.Primary)
            .WithBackdrop(BackdropStyle.Solid)
            .WithBackdropColor(Color.Black)
            .WithDismissOnEscape(false)
            .WithPadding(1);

        var conditionalDetail = new ConditionalNode(ViewModel.IsDetailOpen, _detailModal);

        _settingsModal = new ModalNode()
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Theme.Primary)
            .WithBackdrop(BackdropStyle.Solid)
            .WithBackdropColor(Color.Black)
            .WithDismissOnEscape(false)
            .WithPadding(1);

        var conditionalSettings = new ConditionalNode(ViewModel.IsSettingsOpen, _settingsModal);

        var mainLayout = Layouts.Vertical()
            .WithChild(new TabBarNode(4))
            .WithChild(BuildSearchBar())
            .WithChild(new PanelNode()
                .WithTitle(Strings.PanelDocker)
                .WithBorder(BorderStyle.Rounded)
                .WithBorderColor(Theme.Primary)
                .WithContent(Layouts.Vertical()
                    .WithChild(new TextNode($"   {"Name",-20} {"Image",-20} {"CPU",6} {"RAM",7} {"Port",-12} {Strings.HeaderStatus}")
                        .WithForeground(Theme.Header).Height(1))
                    .WithChild(_list.Fill()))
                .Fill())
            .WithChild(BuildStatusBar());

        return Layouts.Stack(mainLayout, conditionalDetail, conditionalSettings);
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        ViewModel.FilteredContainers.Subscribe(containers => _list?.SetItems(containers))
            .DisposeWith(Subscriptions);

        ViewModel.DetailContentChanged.Subscribe(_ => UpdateDetailModal())
            .DisposeWith(Subscriptions);

        ViewModel.SettingsContentChanged.Subscribe(_ => UpdateSettingsModal())
            .DisposeWith(Subscriptions);
    }

    private void UpdateSettingsModal()
    {
        if (_settingsModal is null) return;

        _settingsModal.WithTitle($" {Strings.SettingsTitle} ").WithTitleColor(Theme.Primary);
        _settingsModal.WithFooter(Strings.HintSettingsModalKeys).WithFooterColor(Theme.TextDim);

        var layout = Layouts.Vertical()
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode($"  {Strings.SettingsRefreshRate,-20} ◀ {ViewModel.GetRefreshRateDisplay()} ▶")
                .WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode("").Height(1));

        if (ViewModel.IsUpdateAvailable)
        {
            layout.WithChild(new TextNode($"  {ViewModel.LatestVersionDisplay}").WithForeground(Theme.Warning).Height(1));
            layout.WithChild(new TextNode($"  [U] {Strings.UpdatePressU}").WithForeground(Theme.Accent).Height(1));
        }
        else
        {
            layout.WithChild(new TextNode($"  {ViewModel.CurrentVersionDisplay}").WithForeground(Theme.TextDim).Height(1));
        }

        layout.WithChild(new TextNode("").Height(1));
        layout.WithChild(new TextNode($"  {ViewModel.GetSettingsFilePath()}").WithForeground(Theme.TextDim).Height(1));

        _settingsModal.Content = layout;
    }

    private void UpdateDetailModal()
    {
        if (_detailModal is null || ViewModel.SelectedContainer.Value is not { } container)
        {
            return;
        }

        var statusIcon = container.Status is "running" ? "▶" : "■";
        var statusColor = container.Status switch
        {
            "running" => Theme.Success,
            "restarting" => Theme.Warning,
            _ => Theme.TextDim
        };

        var ports = container.Ports.Count > 0
            ? string.Join(", ", container.Ports)
            : "—";

        _detailModal.WithTitle($" {container.Name} ").WithTitleColor(Theme.Primary);
        _detailModal.WithFooter(Strings.HintDockerDetailKeys).WithFooterColor(Theme.TextDim);

        var infoPanel = new PanelNode()
            .WithTitle(" Container ")
            .WithTitleColor(Theme.Accent)
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Theme.Border)
            .WithContent(Layouts.Vertical()
                .WithChild(new TextNode($"  ID        {container.Id}").WithForeground(Theme.Text).Height(1))
                .WithChild(new TextNode($"  Image     {container.Image}").WithForeground(Theme.Text).Height(1))
                .WithChild(new TextNode($"  Status    {statusIcon} {container.State}").WithForeground(statusColor).Height(1))
                .WithChild(new TextNode($"  Created   {container.Created:yyyy-MM-dd HH:mm}").WithForeground(Theme.TextDim).Height(1))
                .WithChild(new TextNode($"  Ports     {ports}").WithForeground(Theme.TextDim).Height(1)));

        var logLines = ViewModel.LogContent.Value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => $" {line}")
            .ToList();

        if (logLines.Count == 0)
            logLines = [$" {Strings.DockerLoadingLogs}"];

        _logList = new DataListNode<string>(line => line, line =>
            line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("ERR", StringComparison.OrdinalIgnoreCase)
                ? Theme.Error
                : line.Contains("WARN", StringComparison.OrdinalIgnoreCase)
                    ? Theme.Warning
                    : Theme.TextDim);
        _logList.SetItems(logLines);
        _logList.MoveToEnd();
        ViewModel.OverlayListNode = _logList;

        var logPanel = new PanelNode()
            .WithTitle($" {Strings.DockerLogsHeader} ")
            .WithTitleColor(Theme.Accent)
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Theme.Border)
            .WithContent(_logList.Fill());

        _detailModal.Content = Layouts.Vertical()
            .WithChild(infoPanel.Height(9))
            .WithChild(logPanel.Fill());
    }

    private ILayoutNode BuildSearchBar()
    {
        return ViewModel.IsSearchActive.CombineLatest(ViewModel.SearchText,
            (active, search) =>
            {
                if (active)
                {
                    return (ILayoutNode)new TextNode($" / {search}█  Esc: Exit")
                        .WithForeground(Theme.Warning);
                }

                return new TextNode(" /: Search  S: Start  X: Stop  R: Restart")
                    .WithForeground(Theme.TextDim);
            }).AsLayout().Height(1);
    }

    private ILayoutNode BuildStatusBar()
    {
        return ViewModel.StatusMessage
            .Select<string, ILayoutNode>(msg =>
                new TextNode($" {msg}").WithForeground(Theme.StatusBarText).WithBackground(Theme.StatusBar))
            .AsLayout().Height(1);
    }
}
