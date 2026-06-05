using dottop.Models;
using dottop.Nodes;
using R3;
using Termina.Extensions;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop.Pages;

public class AutostartPage : ReactivePage<AutostartViewModel>
{
    public override ILayoutNode BuildLayout()
    {
        var list = new DataListNode<StartupEntry>(
            e =>
            {
                var statusIcon = e.Enabled ? "✓" : "✗";
                var statusLabel = e.Enabled ? "Aktiv" : "Deaktiviert";
                var path = e.Path.Length > 30 ? e.Path[..30] + "..." : e.Path;
                return $" {e.Name,-24}  {e.Publisher,-18}  {statusIcon} {statusLabel,-12}  {e.Impact,-10}  {path}";
            },
            e => e.Enabled ? Color.BrightCyan : Color.Gray);

        ViewModel.ListNode = list;

        return Layouts.Vertical()
            .WithChild(new TabBarNode(4))
            .WithChild(BuildHeader())
            .WithChild(list.Fill())
            .WithChild(BuildStatusBar());
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        ViewModel.Entries.Subscribe(entries => ViewModel.ListNode?.SetItems(entries))
            .DisposeWith(Subscriptions);
    }

    private ILayoutNode BuildHeader()
    {
        return new TextNode($" {"Name",-24}  {"Publisher",-18}  {"Status",-14}  {"Einfluss",-10}  Pfad")
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
