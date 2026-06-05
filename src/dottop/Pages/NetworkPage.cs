using dottop.Nodes;
using dottop.Resources;
using R3;
using Termina.Extensions;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop.Pages;

public class NetworkPage : ReactivePage<NetworkViewModel>
{
    private DataListNode<ConnectionInfo>? _list;

    public override ILayoutNode BuildLayout()
    {
        _list = new DataListNode<ConnectionInfo>(
            c =>
            {
                var local = c.LocalEndpoint.Length > 24 ? c.LocalEndpoint[..23] + "…" : c.LocalEndpoint;
                var remote = c.RemoteEndpoint.Length > 24 ? c.RemoteEndpoint[..23] + "…" : c.RemoteEndpoint;
                var icon = c.State switch
                {
                    "Established" => "⬤",
                    "LISTEN" => "◉",
                    "TimeWait" or "CloseWait" => "◌",
                    _ => "○"
                };
                return $" {icon} {local,-24} {remote,-24} {c.State}";
            },
            c => c.State switch
            {
                "Established" => Color.BrightGreen,
                "LISTEN" => Color.BrightBlue,
                "TimeWait" or "CloseWait" => Color.BrightYellow,
                _ => Color.Gray
            });

        ViewModel.ListNode = _list;

        return Layouts.Vertical()
            .WithChild(new TabBarNode(3))
            .WithChild(BuildSearchBar())
            .WithChild(new PanelNode()
                .WithTitle(Strings.PanelNetworkConnections)
                .WithBorder(BorderStyle.Rounded)
                .WithBorderColor(Color.BrightMagenta)
                .WithContent(Layouts.Vertical()
                    .WithChild(new TextNode($"   {Strings.HeaderLocal,-24} {Strings.HeaderRemote,-24} {Strings.HeaderStatus}")
                        .WithForeground(Color.BrightBlack).Height(1))
                    .WithChild(_list.Fill()))
                .Fill())
            .WithChild(BuildStatusBar());
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        ViewModel.FilteredConnections.Subscribe(connections => _list?.SetItems(connections))
            .DisposeWith(Subscriptions);
    }

    private ILayoutNode BuildSearchBar()
    {
        return ViewModel.IsSearchActive.CombineLatest(ViewModel.SearchText,
            (active, search) =>
            {
                if (active)
                    return (ILayoutNode)new TextNode($" / {search}█")
                        .WithForeground(Color.BrightYellow);
                return new TextNode(Strings.NetworkSearchHint)
                    .WithForeground(Color.BrightGreen);
            }).AsLayout().Height(1);
    }

    private ILayoutNode BuildStatusBar()
    {
        return ViewModel.StatusMessage
            .Select<string, ILayoutNode>(msg =>
                new TextNode($" {msg}").WithForeground(Color.Black).WithBackground(Color.BrightCyan))
            .AsLayout().Height(1);
    }
}
