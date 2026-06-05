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
    public override ILayoutNode BuildLayout()
    {
        return Layouts.Vertical()
            .WithChild(new TabBarNode(3))
            .WithChild(BuildSearchBar())
            .WithChild(BuildHeader())
            .WithChild(BuildConnectionList())
            .WithChild(BuildStatusBar());
    }

    private ILayoutNode BuildSearchBar()
    {
        return ViewModel.SearchText
            .Select<string, ILayoutNode>(search =>
            {
                var display = ViewModel.IsSearchActive.Value ? $"/ {search}_" : "";
                return new TextNode($" {display}").WithForeground(Color.BrightGreen);
            }).AsLayout().Height(1);
    }

    private ILayoutNode BuildHeader()
    {
        return new TextNode($" {"Prozess",-16}  {"PID",6}  {"Lokal",-22}  {"Remote",-22}  Status")
            .WithForeground(Color.Gray).Height(1);
    }

    private ILayoutNode BuildConnectionList()
    {
        return ViewModel.FilteredConnections.CombineLatest<List<ConnectionInfo>, int, ILayoutNode>(
            ViewModel.SelectedIndex,
            (connections, selectedIdx) =>
            {
                var container = new ScrollableContainerNode().WithScrollbar(true);
                var layout = Layouts.Vertical();
                for (var i = 0; i < connections.Count; i++)
                {
                    var c = connections[i];
                    var text = $" {c.ProcessName,-16}  {c.Pid,6}  {c.LocalEndpoint,-22}  {c.RemoteEndpoint,-22}  {c.State}";
                    var node = new TextNode(text);
                    if (i == selectedIdx)
                        node.WithForeground(Color.White).WithBackground(Color.BrightBlue);
                    else
                        node.WithForeground(c.State == "LISTEN" ? Color.Gray : Color.BrightMagenta);
                    layout.WithChild(node.Height(1));
                }
                return container.WithContent(layout);
            }).AsLayout().Fill();
    }

    private ILayoutNode BuildStatusBar()
    {
        return ViewModel.StatusMessage
            .Select<string, ILayoutNode>(msg =>
                new TextNode($" {msg}").WithForeground(Color.Black).WithBackground(Color.BrightCyan))
            .AsLayout().Height(1);
    }
}
