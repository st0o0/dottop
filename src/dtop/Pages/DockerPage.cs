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

public class DockerPage : ReactivePage<DockerViewModel>
{
    private DataListNode<DockerListItem>? _list;
    private DataListNode<NetworkInfo>? _networkList;
    private DataListNode<VolumeInfo>? _volumeList;
    private DataListNode<ImageInfo>? _imageList;
    private DataListNode<string>? _logList;
    private ModalNode? _detailModal;
    private ModalNode? _settingsModal;
    private ModalNode? _inputModal;

    public override ILayoutNode BuildLayout()
    {
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

        _inputModal = new ModalNode()
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Theme.Primary)
            .WithBackdrop(BackdropStyle.Solid)
            .WithBackdropColor(Color.Black)
            .WithDismissOnEscape(false)
            .WithPadding(1);

        var conditionalInput = new ConditionalNode(ViewModel.IsInputMode, _inputModal);

        var mainLayout = ViewModel.ActiveSubTab
            .Select<DockerSubTab, ILayoutNode>(subTab =>
            {
                var content = subTab switch
                {
                    DockerSubTab.Container => BuildContainerContent(),
                    DockerSubTab.Networks => BuildNetworkContent(),
                    DockerSubTab.Volumes => BuildVolumeContent(),
                    DockerSubTab.Images => BuildImageContent(),
                    _ => Layouts.Vertical()
                };

                return Layouts.Vertical()
                    .WithChild(new TabBarNode(4))
                    .WithChild(BuildSubTabBar(subTab))
                    .WithChild(BuildSearchBar())
                    .WithChild(new PanelNode()
                        .WithTitle(Strings.PanelDocker)
                        .WithBorder(BorderStyle.Rounded)
                        .WithBorderColor(Theme.Primary)
                        .WithContent(content)
                        .Fill())
                    .WithChild(BuildStatusBar());
            }).AsLayout();

        return Layouts.Stack(mainLayout, conditionalDetail, conditionalSettings, conditionalInput);
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        ViewModel.DisplayItems.Subscribe(items => _list?.SetItems(items))
            .DisposeWith(Subscriptions);

        ViewModel.Networks.Subscribe(items => _networkList?.SetItems(items))
            .DisposeWith(Subscriptions);

        ViewModel.Volumes.Subscribe(items => _volumeList?.SetItems(items))
            .DisposeWith(Subscriptions);

        ViewModel.Images.Subscribe(items => _imageList?.SetItems(items))
            .DisposeWith(Subscriptions);

        ViewModel.DetailContentChanged.Subscribe(_ =>
            {
                UpdateDetailModal();
                UpdateInputModal();
            })
            .DisposeWith(Subscriptions);

        ViewModel.IsDetailOpen.Subscribe(open => { if (!open)
                {
                    _logList = null;
                }
            })
            .DisposeWith(Subscriptions);

        ViewModel.SettingsContentChanged.Subscribe(_ => UpdateSettingsModal())
            .DisposeWith(Subscriptions);
    }

    private ILayoutNode BuildSubTabBar(DockerSubTab activeSubTab)
    {
        var tabs = new[] { "Container", "Networks", "Volumes", "Images" };
        var activeIdx = (int)activeSubTab;
        var bar = Layouts.Horizontal();
        for (var i = 0; i < tabs.Length; i++)
        {
            var node = new TextNode($" {tabs[i]} ");
            if (i == activeIdx)
            {
                node.WithForeground(Theme.SelectionText).WithBackground(Theme.Selection);
            }
            else
            {
                node.WithForeground(Theme.Secondary);
            }

            bar.WithChild(node);
        }
        return bar.Height(1);
    }

    private ILayoutNode BuildContainerContent()
    {
        _list = new DataListNode<DockerListItem>(
            item =>
            {
                if (item.IsGroup)
                {
                    var arrow = item.IsExpanded ? "▼" : "▶";
                    var running = item.GroupCount;
                    return $" {arrow} {item.GroupName} ({running} containers)";
                }

                var c = item.Container!;
                var pinIcon = ViewModel.IsPinned(c.Id) ? " ● " : (c.ComposeProject is not null ? "   " : " ");
                var name = c.Name.Length > 24 ? c.Name[..23] + "…" : c.Name;
                var image = c.Image.Length > 24 ? c.Image[..23] + "…" : c.Image;
                var statusIcon = c.Status is "running" ? "▶" : "■";
                var cpuStr = $"{c.CpuPercent,5:F1}%";
                var ramMb = c.MemoryUsageBytes / 1024 / 1024;
                var ramStr = ramMb >= 1024 ? $"{ramMb / 1024.0:F1}GB" : $"{ramMb,4}MB";
                var port = c.Ports.Count > 0 ? c.Ports[0] : "—";
                if (port.Length > 14)
                {
                    port = port[..13] + "…";
                }

                return $"{pinIcon}{statusIcon} {name,-24} {image,-24} {cpuStr} {ramStr,7}  {port,-14} {c.State}";
            },
            item =>
            {
                if (item.IsGroup)
                {
                    return Theme.Primary;
                }

                if (item.Container is not null && ViewModel.IsPinned(item.Container.Id))
                {
                    return Theme.Accent;
                }

                return item.Container?.Status switch
                {
                    "running" => Theme.Text,
                    "restarting" => Theme.Warning,
                    _ => Theme.TextDim
                };
            });

        ViewModel.ListNode = _list;
        ViewModel.GetSelectedItem = () => _list.SelectedItem?.Container;
        ViewModel.GetSelectedDisplayItem = () => _list.SelectedItem;
        _list.SetItems(ViewModel.DisplayItems.Value);

        return Layouts.Vertical()
            .WithChild(new TextNode($"     {"Name",-24} {"Image",-24} {"CPU",6} {"RAM",7}  {"Port",-14} {Strings.HeaderStatus}")
                .WithForeground(Theme.Header).Height(1))
            .WithChild(_list.Fill());
    }

    private ILayoutNode BuildNetworkContent()
    {
        _networkList = new DataListNode<NetworkInfo>(
            n => $" {n.Name,-24} {n.Driver,-10} {n.Scope,-10} {n.Containers.Count,4}",
            n => n.Containers.Count > 0 ? Theme.Text : Theme.TextDim);
        _networkList.SetItems(ViewModel.Networks.Value);
        ViewModel.NetworkListNode = _networkList;
        ViewModel.GetSelectedNetwork = () => _networkList.SelectedItem;

        return Layouts.Vertical()
            .WithChild(new TextNode($" {"Name",-24} {"Driver",-10} {"Scope",-10} {"#",4}")
                .WithForeground(Theme.Header).Height(1))
            .WithChild(_networkList.Fill());
    }

    private ILayoutNode BuildVolumeContent()
    {
        _volumeList = new DataListNode<VolumeInfo>(
            v =>
            {
                var sizeMb = v.SizeBytes / 1024.0 / 1024;
                var sizeStr = sizeMb >= 1024 ? $"{sizeMb / 1024:F1}GB" : $"{sizeMb:F0}MB";
                return $" {v.Name,-30} {v.Driver,-10} {sizeStr,8} {v.MountCount,3}";
            },
            v => v.MountCount > 0 ? Theme.Text : Theme.TextDim);
        _volumeList.SetItems(ViewModel.Volumes.Value);
        ViewModel.VolumeListNode = _volumeList;
        ViewModel.GetSelectedVolume = () => _volumeList.SelectedItem;

        return Layouts.Vertical()
            .WithChild(new TextNode($" {"Name",-30} {"Driver",-10} {"Size",8} {"#",3}")
                .WithForeground(Theme.Header).Height(1))
            .WithChild(_volumeList.Fill());
    }

    private ILayoutNode BuildImageContent()
    {
        _imageList = new DataListNode<ImageInfo>(
            i =>
            {
                var sizeMb = i.SizeBytes / 1024.0 / 1024;
                var sizeStr = sizeMb >= 1024 ? $"{sizeMb / 1024:F1}GB" : $"{sizeMb:F0}MB";
                var repo = i.Repository.Length > 24 ? i.Repository[..23] + "…" : i.Repository;
                var tag = i.Tag.Length > 12 ? i.Tag[..11] + "…" : i.Tag;
                return $" {repo,-24} {tag,-12} {sizeStr,8} {i.Created:yyyy-MM-dd}";
            },
            i => i.ContainerCount > 0 ? Theme.Text : Theme.TextDim);
        _imageList.SetItems(ViewModel.Images.Value);
        ViewModel.ImageListNode = _imageList;
        ViewModel.GetSelectedImage = () => _imageList.SelectedItem;

        return Layouts.Vertical()
            .WithChild(new TextNode($" {"Repository",-24} {"Tag",-12} {"Size",8} {"Created"}")
                .WithForeground(Theme.Header).Height(1))
            .WithChild(_imageList.Fill());
    }

    private void UpdateSettingsModal()
    {
        if (_settingsModal is null)
        {
            return;
        }

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
        if (_detailModal is null)
        {
            return;
        }

        switch (ViewModel.ActiveSubTab.Value)
        {
            case DockerSubTab.Container:
                UpdateContainerDetailModal();
                break;
            case DockerSubTab.Networks:
                UpdateNetworkDetailModal();
                break;
            case DockerSubTab.Volumes:
                UpdateVolumeDetailModal();
                break;
            case DockerSubTab.Images:
                UpdateImageDetailModal();
                break;
        }
    }

    private void UpdateContainerDetailModal()
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

        _detailModal.WithTitle($" {container.Name} ").WithTitleColor(Theme.Primary);
        _detailModal.WithFooter(Strings.HintDockerDetailKeys).WithFooterColor(Theme.TextDim);

        var infoContent = Layouts.Vertical()
            .WithChild(new TextNode($"  ID        {container.Id}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  Image     {container.Image}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  Status    {statusIcon} {container.State}").WithForeground(statusColor).Height(1))
            .WithChild(new TextNode($"  Created   {container.Created:yyyy-MM-dd HH:mm}").WithForeground(Theme.TextDim).Height(1));

        var infoPanel = new PanelNode()
            .WithTitle(" Container ")
            .WithTitleColor(Theme.Accent)
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Theme.Border)
            .WithContent(infoContent);

        var cpuColor = container.CpuPercent > 80 ? Theme.Error
            : container.CpuPercent > 50 ? Theme.Warning : Theme.Graph;
        var cpuGraph = new GraphNode()
            .WithStyle(GraphStyle.Blocks)
            .WithColor(cpuColor)
            .WithRange(0, 100);
        cpuGraph.Push(container.CpuPercent);

        var cpuPanel = new PanelNode()
            .WithTitle($" CPU {container.CpuPercent:F1}% ")
            .WithTitleColor(cpuColor)
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Theme.Border)
            .WithContent(cpuGraph);

        var ramMb = container.MemoryUsageBytes / 1024.0 / 1024;
        var ramLimitMb = container.MemoryLimitBytes / 1024.0 / 1024;
        var ramPercent = ramLimitMb > 0 ? ramMb / ramLimitMb * 100 : 0;
        var ramStr = ramMb >= 1024 ? $"{ramMb / 1024:F1} GB" : $"{ramMb:F0} MB";
        var ramLimitStr = ramLimitMb >= 1024 ? $"{ramLimitMb / 1024:F1} GB" : $"{ramLimitMb:F0} MB";

        var ramGraph = new GraphNode()
            .WithStyle(GraphStyle.Blocks)
            .WithColor(Theme.Graph)
            .WithRange(0, 100);
        ramGraph.Push(Math.Clamp(ramPercent, 0, 100));

        var ramPanel = new PanelNode()
            .WithTitle($" RAM {ramStr} / {ramLimitStr} ")
            .WithTitleColor(Theme.Text)
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Theme.Border)
            .WithContent(ramGraph);

        var topRow = Layouts.Horizontal()
            .WithChild(infoPanel.Fill())
            .WithSpacing(1)
            .WithChild(Layouts.Vertical()
                .WithChild(cpuPanel.Fill())
                .WithChild(ramPanel.Fill())
                .WidthPercent(40));

        var logLines = ViewModel.LogContent.Value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => $" {line}")
            .ToList();

        if (logLines.Count == 0)
        {
            logLines = [$" {Strings.DockerLoadingLogs}"];
        }

        if (_logList is null)
        {
            _logList = new DataListNode<string>(line => line, line =>
                line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("ERR", StringComparison.OrdinalIgnoreCase)
                    ? Theme.Error
                    : line.Contains("WARN", StringComparison.OrdinalIgnoreCase)
                        ? Theme.Warning
                        : Theme.TextDim);
            _logList.SetItems(logLines);
            _logList.MoveToEnd();
            ViewModel.OverlayListNode = _logList;
        }
        else
        {
            _logList.SetItems(logLines);
        }

        var logPanel = new PanelNode()
            .WithTitle($" {Strings.DockerLogsHeader} ")
            .WithTitleColor(Theme.Accent)
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Theme.Border)
            .WithContent(_logList.Fill());

        _detailModal.Content = Layouts.Vertical()
            .WithChild(topRow.Height(10))
            .WithChild(logPanel.Fill());
    }

    private void UpdateNetworkDetailModal()
    {
        if (_detailModal is null || ViewModel.GetSelectedNetwork?.Invoke() is not { } network)
        {
            return;
        }

        _detailModal.WithTitle($" Network: {network.Name} ").WithTitleColor(Theme.Primary);
        _detailModal.WithFooter(" Esc: Close ").WithFooterColor(Theme.TextDim);

        var info = Layouts.Vertical()
            .WithChild(new TextNode($"  ID        {network.Id}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  Driver    {network.Driver}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  Scope     {network.Scope}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  Subnet    {network.Subnet}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  Internal  {network.Internal}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode($"  IPv6      {network.IPv6}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode($"  Connected Containers ({network.Containers.Count}):").WithForeground(Theme.Accent).Height(1));

        foreach (var c in network.Containers)
            info.WithChild(new TextNode($"    {c.Name,-24} {c.IPv4Address}").WithForeground(Theme.Text).Height(1));

        _detailModal.Content = info;
    }

    private void UpdateVolumeDetailModal()
    {
        if (_detailModal is null || ViewModel.GetSelectedVolume?.Invoke() is not { } volume)
        {
            return;
        }

        _detailModal.WithTitle($" Volume: {volume.Name} ").WithTitleColor(Theme.Primary);
        _detailModal.WithFooter(" Esc: Close ").WithFooterColor(Theme.TextDim);

        var sizeMb = volume.SizeBytes / 1024.0 / 1024;
        var sizeStr = sizeMb >= 1024 ? $"{sizeMb / 1024:F1} GB" : $"{sizeMb:F0} MB";

        var info = Layouts.Vertical()
            .WithChild(new TextNode($"  Name        {volume.Name}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  Driver      {volume.Driver}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  Mountpoint  {volume.Mountpoint}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  Size        {sizeStr}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  Mounts      {volume.MountCount}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  Created     {volume.Created:yyyy-MM-dd HH:mm}").WithForeground(Theme.TextDim).Height(1));

        if (volume.Labels.Count > 0)
        {
            info.WithChild(new TextNode("").Height(1));
            info.WithChild(new TextNode("  Labels:").WithForeground(Theme.Accent).Height(1));
            foreach (var (k, v) in volume.Labels)
                info.WithChild(new TextNode($"    {k} = {v}").WithForeground(Theme.Text).Height(1));
        }

        _detailModal.Content = info;
    }

    private void UpdateImageDetailModal()
    {
        if (_detailModal is null || ViewModel.GetSelectedImage?.Invoke() is not { } image)
        {
            return;
        }

        _detailModal.WithTitle($" Image: {image.Repository}:{image.Tag} ").WithTitleColor(Theme.Primary);
        _detailModal.WithFooter(" Esc: Close ").WithFooterColor(Theme.TextDim);

        var sizeMb = image.SizeBytes / 1024.0 / 1024;
        var sizeStr = sizeMb >= 1024 ? $"{sizeMb / 1024:F1} GB" : $"{sizeMb:F0} MB";

        var info = Layouts.Vertical()
            .WithChild(new TextNode($"  ID          {image.Id}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  Repository  {image.Repository}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  Tag         {image.Tag}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  Size        {sizeStr}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  OS/Arch     {image.OsArch}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  Containers  {image.ContainerCount}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode($"  Created     {image.Created:yyyy-MM-dd HH:mm}").WithForeground(Theme.TextDim).Height(1));

        _detailModal.Content = info;
    }

    private void UpdateInputModal()
    {
        if (_inputModal is null || !ViewModel.IsInputMode.Value)
        {
            return;
        }

        var label = ViewModel.InputPromptLabel;
        _inputModal.WithTitle($" {label} ").WithTitleColor(Theme.Primary);
        _inputModal.WithFooter(" Enter: Submit | Esc: Cancel ").WithFooterColor(Theme.TextDim);

        _inputModal.Content = Layouts.Vertical()
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode($"  {label}: {ViewModel.InputText.Value}█")
                .WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode("").Height(1));
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

                var hints = ViewModel.ActiveSubTab.Value switch
                {
                    DockerSubTab.Container => " /: Search  S: Start  X: Stop  R: Restart  N: Pull  P: Pin",
                    DockerSubTab.Networks => " /: Search  N: Create  D: Delete  Enter: Detail",
                    DockerSubTab.Volumes => " /: Search  N: Create  D: Delete  Shift+D: Prune",
                    DockerSubTab.Images => " /: Search  N: Pull  D: Delete  Shift+D: Prune",
                    _ => " /: Search"
                };

                return new TextNode(hints)
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
