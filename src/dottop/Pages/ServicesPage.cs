using dottop.Models;
using dottop.Nodes;
using dottop.Resources;
using R3;
using Termina.Extensions;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop.Pages;

public class ServicesPage : ReactivePage<ServicesViewModel>
{
    private DataListNode<WindowsServiceInfo>? _list;

    public override ILayoutNode BuildLayout()
    {
        _list = new DataListNode<WindowsServiceInfo>(
            s =>
            {
                var name = s.DisplayName.Length > 30 ? s.DisplayName[..29] + "…" : s.DisplayName;
                var statusIcon = s.Status == ServiceStatus.Running ? "●" : "○";
                var pid = s.Pid?.ToString() ?? "—";
                return $" {name,-30} {statusIcon} {s.Status,-10} {s.StartType,-12} {pid,6}";
            },
            s => s.Status == ServiceStatus.Running ? Color.BrightCyan : Color.Gray);

        ViewModel.ListNode = _list;
        ViewModel.GetSelectedItem = () => _list.SelectedItem;

        return Layouts.Vertical()
            .WithChild(new TabBarNode(2))
            .WithChild(BuildSearchBar())
            .WithChild(new PanelNode()
                .WithTitle(Strings.PanelServices)
                .WithBorder(BorderStyle.Rounded)
                .WithBorderColor(Color.BrightYellow)
                .WithContent(Layouts.Vertical()
                    .WithChild(new TextNode($" {Strings.HeaderName,-30} {Strings.HeaderStatus,-12} {Strings.HeaderStartType,-12} {"PID",6}")
                        .WithForeground(Color.Gray).Height(1))
                    .WithChild(_list.Fill()))
                .Fill())
            .WithChild(BuildStatusBar());
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        ViewModel.FilteredServices.Subscribe(services => _list?.SetItems(services))
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
                return new TextNode(Strings.ServicesSearchHint)
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
