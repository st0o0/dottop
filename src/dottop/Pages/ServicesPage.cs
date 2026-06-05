using dottop.Models;
using dottop.Nodes;
using R3;
using Termina.Extensions;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop.Pages;

public class ServicesPage : ReactivePage<ServicesViewModel>
{
    public override ILayoutNode BuildLayout()
    {
        return Layouts.Vertical()
            .WithChild(new TabBarNode(2))
            .WithChild(BuildSearchBar())
            .WithChild(BuildHeader())
            .WithChild(BuildServiceList())
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
        return new TextNode($" {"Name",-28}  {"Status",-12}  {"Starttyp",-14}  {"PID",6}")
            .WithForeground(Color.Gray).Height(1);
    }

    private ILayoutNode BuildServiceList()
    {
        return ViewModel.FilteredServices.CombineLatest<List<WindowsServiceInfo>, int, ILayoutNode>(
            ViewModel.SelectedIndex,
            (services, selectedIdx) =>
            {
                var container = new ScrollableContainerNode().WithScrollbar(true);
                var layout = Layouts.Vertical();
                for (var i = 0; i < services.Count; i++)
                {
                    var s = services[i];
                    var statusIcon = s.Status == ServiceStatus.Running ? "●" : "○";
                    var text = $" {s.DisplayName,-28}  {statusIcon} {s.Status,-10}  {s.StartType,-14}  {s.Pid?.ToString() ?? "—",6}";
                    var node = new TextNode(text);
                    if (i == selectedIdx)
                        node.WithForeground(Color.White).WithBackground(Color.BrightBlue);
                    else
                        node.WithForeground(s.Status == ServiceStatus.Running ? Color.BrightCyan : Color.Gray);
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
