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
                var name = e.Name.Length > 22 ? e.Name[..21] + "…" : e.Name;
                var statusIcon = e.Enabled ? "✓" : "✗";
                var statusLabel = e.Enabled ? "Aktiv" : "Deakt.";
                var path = e.Path.Length > 30 ? e.Path[..29] + "…" : e.Path;
                return $" {name,-22} {statusIcon} {statusLabel,-6} {path}";
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
        return new TextNode($" {"Name",-22} {"Status",-8} Pfad")
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
