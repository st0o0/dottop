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
        var list = new DataListNode<ConnectionInfo>(
            c => $" {c.ProcessName,-16}  {c.Pid,6}  {c.LocalEndpoint,-22}  {c.RemoteEndpoint,-22}  {c.State}",
            c => c.State == "LISTEN" ? Color.Gray : Color.BrightMagenta);

        ViewModel.ListNode = list;

        return Layouts.Vertical()
            .WithChild(new TabBarNode(3))
            .WithChild(BuildSearchBar())
            .WithChild(BuildHeader())
            .WithChild(list.Fill())
            .WithChild(BuildStatusBar());
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        ViewModel.FilteredConnections.Subscribe(connections => ViewModel.ListNode?.SetItems(connections))
            .DisposeWith(Subscriptions);
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

    private ILayoutNode BuildStatusBar()
    {
        return ViewModel.StatusMessage
            .Select<string, ILayoutNode>(msg =>
                new TextNode($" {msg}").WithForeground(Color.Black).WithBackground(Color.BrightCyan))
            .AsLayout().Height(1);
    }
}
