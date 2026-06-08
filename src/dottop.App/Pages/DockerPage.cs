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
    private ModalNode? _detailModal;
    private ModalNode? _settingsModal;

    public override ILayoutNode BuildLayout()
    {
        _list = new DataListNode<ContainerSnapshot>(
            c =>
            {
                var name = c.Name.Length > 22 ? c.Name[..21] + "…" : c.Name;
                var image = c.Image.Length > 22 ? c.Image[..21] + "…" : c.Image;
                var statusIcon = c.Status is "running" ? "▶" : "■";
                return $" {statusIcon} {name,-22} {image,-22} {c.State}";
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
                    .WithChild(new TextNode($"   {"Name",-22} {"Image",-22} {Strings.HeaderStatus}")
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
            "running" => Theme.Text,
            "restarting" => Theme.Warning,
            _ => Theme.TextDim
        };

        var ports = container.Ports.Count > 0
            ? string.Join(", ", container.Ports)
            : "-";

        _detailModal.WithTitle($" {container.Name} ").WithTitleColor(Theme.Primary);
        _detailModal.WithFooter(Strings.HintDockerDetailKeys).WithFooterColor(Theme.TextDim);

        var contentLayout = Layouts.Vertical()
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode($"  ID:       {container.Id}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode($"  Image:    {container.Image}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode($"  Status:   {statusIcon} {container.Status} ({container.State})").WithForeground(statusColor).Height(1))
            .WithChild(new TextNode($"  Created:  {container.Created:yyyy-MM-dd HH:mm}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode($"  Ports:    {ports}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode($"  {Strings.DockerLogsHeader}").WithForeground(Theme.Primary).Height(1));

        var logLines = ViewModel.LogContent.Value.Split('\n');
        foreach (var line in logLines)
        {
            contentLayout.WithChild(new TextNode($"  {line}").WithForeground(Theme.TextDim).Height(1));
        }

        _detailModal.Content = contentLayout;
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
