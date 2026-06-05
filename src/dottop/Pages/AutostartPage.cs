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
        return Layouts.Vertical()
            .WithChild(new TabBarNode(4))
            .WithChild(BuildHeader())
            .WithChild(BuildEntryList())
            .WithChild(BuildStatusBar());
    }

    private ILayoutNode BuildHeader()
    {
        return new TextNode($" {"Name",-24}  {"Publisher",-18}  {"Status",-14}  {"Einfluss",-10}  Pfad")
            .WithForeground(Color.Gray).Height(1);
    }

    private ILayoutNode BuildEntryList()
    {
        return ViewModel.Entries.CombineLatest<List<StartupEntry>, int, ILayoutNode>(
            ViewModel.SelectedIndex,
            (entries, selectedIdx) =>
            {
                var container = new ScrollableContainerNode().WithScrollbar(true);
                var layout = Layouts.Vertical();
                for (var i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    var statusIcon = e.Enabled ? "✓" : "✗";
                    var statusLabel = e.Enabled ? "Aktiv" : "Deaktiviert";
                    var path = e.Path.Length > 30 ? e.Path[..30] + "..." : e.Path;
                    var text = $" {e.Name,-24}  {e.Publisher,-18}  {statusIcon} {statusLabel,-12}  {e.Impact,-10}  {path}";
                    var node = new TextNode(text);
                    if (i == selectedIdx)
                        node.WithForeground(Color.White).WithBackground(Color.BrightBlue);
                    else
                        node.WithForeground(e.Enabled ? Color.BrightCyan : Color.Gray);
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
