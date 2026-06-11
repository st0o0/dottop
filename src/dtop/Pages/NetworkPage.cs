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

public class NetworkPage : ReactivePage<NetworkViewModel>
{
    private DataListNode<ConnectionSnapshot>? _list;
    private ModalNode? _detailModal;
    private ModalNode? _settingsModal;

    public override ILayoutNode BuildLayout()
    {
        _list = new DataListNode<ConnectionSnapshot>(
            c =>
            {
                var name = c.ProcessName.Length > 16 ? c.ProcessName[..15] + "…" : c.ProcessName;
                var local = c.LocalEndpoint.Length > 22 ? c.LocalEndpoint[..21] + "…" : c.LocalEndpoint;
                var remote = c.RemoteEndpoint.Length > 22 ? c.RemoteEndpoint[..21] + "…" : c.RemoteEndpoint;
                var icon = c.State switch
                {
                    "Established" => "●",
                    "LISTEN" => "●",
                    "TimeWait" or "CloseWait" => "●",
                    _ => "●"
                };
                return $" {icon} {name,-16} {c.Pid,6} {c.Protocol,-4} {local,-22} {remote,-22} {c.State}";
            },
            c => c.State switch
            {
                "Established" => ThemeService.Instance.Current.Foreground,
                "LISTEN" => ThemeService.Instance.Current.TextDim,
                "TimeWait" or "CloseWait" => ThemeService.Instance.Current.TextDim,
                _ => ThemeService.Instance.Current.TextDim
            });

        ViewModel.ListNode = _list;
        ViewModel.GetSelectedItem = () => _list.SelectedItem;

        _detailModal = new ModalNode()
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(ThemeService.Instance.Current.Accent)
            .WithBackdrop(BackdropStyle.Solid)
            .WithBackdropColor(Color.Black)
            .WithDismissOnEscape(false)
            .WithPadding(1);

        var conditionalDetail = new ConditionalNode(ViewModel.IsDetailOpen, _detailModal);

        _settingsModal = new ModalNode()
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(ThemeService.Instance.Current.Accent)
            .WithBackdrop(BackdropStyle.Solid)
            .WithBackdropColor(Color.Black)
            .WithDismissOnEscape(false)
            .WithPadding(1);

        var conditionalSettings = new ConditionalNode(ViewModel.IsSettingsOpen, _settingsModal);

        var mainLayout = Layouts.Vertical()
            .WithChild(new TabBarNode(4))
            .WithChild(BuildSearchBar())
            .WithChild(new PanelNode()
                .WithTitle(Strings.PanelNetworkConnections)
                .WithBorder(BorderStyle.Rounded)
                .WithBorderColor(ThemeService.Instance.Current.Accent)
                .WithContent(Layouts.Vertical()
                    .WithChild(new TextNode($"   {Strings.HeaderProcess,-16} {Strings.HeaderPid,6} {"Proto",-4} {Strings.HeaderLocal,-22} {Strings.HeaderRemote,-22} {Strings.HeaderStatus}")
                        .WithForeground(ThemeService.Instance.Current.Header).Height(1))
                    .WithChild(_list.Fill()))
                .Fill())
            .WithChild(BuildStatusBar());

        return Layouts.Stack(mainLayout, conditionalDetail, conditionalSettings);
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        ViewModel.FilteredConnections.Subscribe(connections => _list?.SetItems(connections))
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
        if (_detailModal is null || ViewModel.SelectedConnection.Value is not { } conn)
        {
            return;
        }

        _detailModal.WithTitle($" {conn.ProcessName} ").WithTitleColor(ThemeService.Instance.Current.Accent);
        _detailModal.WithFooter(Strings.HintNetworkDetailKeys).WithFooterColor(ThemeService.Instance.Current.TextDim);
        _detailModal.Content = Layouts.Vertical()
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode($"  Process:   {conn.ProcessName} (PID: {conn.Pid})").WithForeground(ThemeService.Instance.Current.TextDim).Height(1))
            .WithChild(new TextNode($"  Protocol:  {conn.Protocol}").WithForeground(ThemeService.Instance.Current.TextDim).Height(1))
            .WithChild(new TextNode($"  Local:     {conn.LocalEndpoint}").WithForeground(ThemeService.Instance.Current.TextDim).Height(1))
            .WithChild(new TextNode($"  Remote:    {conn.RemoteEndpoint}").WithForeground(ThemeService.Instance.Current.TextDim).Height(1))
            .WithChild(new TextNode($"  State:     {conn.State}").WithForeground(ThemeService.Instance.Current.TextDim).Height(1));
    }

    private ILayoutNode BuildSearchBar()
    {
        return ViewModel.IsSearchActive.CombineLatest(ViewModel.SearchText, ILayoutNode (active, search) =>
            {
                if (active)
                {
                    return new TextNode($" / {search}█  Esc: Exit")
                        .WithForeground(ThemeService.Instance.Current.Warning);
                }

                return new TextNode(Strings.NetworkSearchHint)
                    .WithForeground(ThemeService.Instance.Current.TextDim);
            }).AsLayout().Height(1);
    }

    private ILayoutNode BuildStatusBar()
    {
        return ViewModel.StatusMessage
            .Select<string, ILayoutNode>(msg =>
                new TextNode($" {msg}").WithForeground(ThemeService.Instance.Current.StatusBarText).WithBackground(ThemeService.Instance.Current.StatusBar))
            .AsLayout().Height(1);
    }
}
