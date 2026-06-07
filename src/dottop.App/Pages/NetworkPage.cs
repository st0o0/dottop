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

public class NetworkPage : ReactivePage<NetworkViewModel>
{
    private DataListNode<ConnectionSnapshot>? _list;
    private ModalNode? _detailModal;

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
                "Established" => Theme.Text,
                "LISTEN" => Theme.TextDim,
                "TimeWait" or "CloseWait" => Theme.TextDim,
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

        var mainLayout = Layouts.Vertical()
            .WithChild(new TabBarNode(3))
            .WithChild(BuildSearchBar())
            .WithChild(new PanelNode()
                .WithTitle(Strings.PanelNetworkConnections)
                .WithBorder(BorderStyle.Rounded)
                .WithBorderColor(Theme.Primary)
                .WithContent(Layouts.Vertical()
                    .WithChild(new TextNode($"   {Strings.HeaderProcess,-16} {Strings.HeaderPid,6} {"Proto",-4} {Strings.HeaderLocal,-22} {Strings.HeaderRemote,-22} {Strings.HeaderStatus}")
                        .WithForeground(Theme.Header).Height(1))
                    .WithChild(_list.Fill()))
                .Fill())
            .WithChild(BuildStatusBar());

        return Layouts.Stack(mainLayout, conditionalDetail);
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        ViewModel.FilteredConnections.Subscribe(connections => _list?.SetItems(connections))
            .DisposeWith(Subscriptions);

        ViewModel.DetailContentChanged.Subscribe(_ => UpdateDetailModal())
            .DisposeWith(Subscriptions);
    }

    private void UpdateDetailModal()
    {
        if (_detailModal is null || ViewModel.SelectedConnection.Value is not { } conn)
        {
            return;
        }

        _detailModal.WithTitle($" {conn.ProcessName} ").WithTitleColor(Theme.Primary);
        _detailModal.WithFooter(Strings.HintNetworkDetailKeys).WithFooterColor(Theme.TextDim);
        _detailModal.Content = Layouts.Vertical()
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode($"  Process:   {conn.ProcessName} (PID: {conn.Pid})").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode($"  Protocol:  {conn.Protocol}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode($"  Local:     {conn.LocalEndpoint}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode($"  Remote:    {conn.RemoteEndpoint}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode($"  State:     {conn.State}").WithForeground(Theme.TextDim).Height(1));
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

                return new TextNode(Strings.NetworkSearchHint)
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
