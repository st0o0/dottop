using dottop.Nodes;
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
                var local = c.LocalEndpoint.Length > 22 ? c.LocalEndpoint[..21] + "…" : c.LocalEndpoint;
                var remote = c.RemoteEndpoint.Length > 22 ? c.RemoteEndpoint[..21] + "…" : c.RemoteEndpoint;
                return $" {local,-22} {remote,-22} {c.State,-12}";
            },
            c => c.State == "LISTEN" ? Color.Gray : Color.BrightMagenta);

        ViewModel.ListNode = _list;

        return Layouts.Vertical()
            .WithChild(new TabBarNode(3))
            .WithChild(BuildSearchBar())
            .WithChild(new PanelNode()
                .WithTitle(" Netzwerk ")
                .WithBorder(BorderStyle.Rounded)
                .WithBorderColor(Color.BrightMagenta)
                .WithContent(Layouts.Vertical()
                    .WithChild(new TextNode($" {"Lokal",-22} {"Remote",-22} {"Status",-12}")
                        .WithForeground(Color.Gray).Height(1))
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
        return ViewModel.SearchText
            .Select<string, ILayoutNode>(search =>
            {
                if (ViewModel.IsSearchActive.Value)
                    return new TextNode($" / {search}█")
                        .WithForeground(Color.BrightYellow);
                return new TextNode(" /: Suche nach IP/Port")
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
