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
    private DataListNode<ServiceInfo>? _list;
    private ModalNode? _detailModal;

    public override ILayoutNode BuildLayout()
    {
        _list = new DataListNode<ServiceInfo>(
            s =>
            {
                var name = s.DisplayName.Length > 32 ? s.DisplayName[..31] + "…" : s.DisplayName;
                var statusIcon = s.Status == ServiceStatus.Running ? "▶" : "■";
                var statusText = s.Status == ServiceStatus.Running ? "Running" : "Stopped";
                return $" {statusIcon} {name,-32} {statusText,-8} {s.StartType,-10}";
            },
            s => s.Status == ServiceStatus.Running ? Color.White : Color.Gray);

        ViewModel.ListNode = _list;
        ViewModel.GetSelectedItem = () => _list.SelectedItem;

        _detailModal = new ModalNode()
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Color.Cyan)
            .WithBackdrop(BackdropStyle.Solid)
            .WithBackdropColor(Color.Black)
            .WithDismissOnEscape(false)
            .WithPadding(1);

        var conditionalDetail = new ConditionalNode(ViewModel.IsDetailOpen, _detailModal);

        return Layouts.Vertical()
            .WithChild(new TabBarNode(2))
            .WithChild(BuildSearchBar())
            .WithChild(new PanelNode()
                .WithTitle(Strings.PanelServices)
                .WithBorder(BorderStyle.Rounded)
                .WithBorderColor(Color.Cyan)
                .WithContent(Layouts.Vertical()
                    .WithChild(new TextNode($"   {Strings.HeaderName,-32} {Strings.HeaderStatus,-8} {Strings.HeaderStartType,-10}")
                        .WithForeground(Color.BrightBlack).Height(1))
                    .WithChild(_list.Fill()))
                .Fill())
            .WithChild(BuildStatusBar())
            .WithChild(conditionalDetail);
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        ViewModel.FilteredServices.Subscribe(services => _list?.SetItems(services))
            .DisposeWith(Subscriptions);

        ViewModel.DetailContentChanged.Subscribe(_ => UpdateDetailModal())
            .DisposeWith(Subscriptions);
    }

    private void UpdateDetailModal()
    {
        if (_detailModal is null || ViewModel.SelectedService.Value is not { } svc)
        {
            return;
        }

        var statusIcon = svc.Status == ServiceStatus.Running ? "▶" : "■";
        var statusColor = svc.Status == ServiceStatus.Running ? Color.White : Color.Gray;
        var desc = string.IsNullOrWhiteSpace(svc.Description)
            ? Strings.ServiceNoDescription
            : svc.Description;

        _detailModal.WithTitle($" {svc.DisplayName} ").WithTitleColor(Color.BrightCyan);
        _detailModal.Content = Layouts.Vertical()
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode($"  {Strings.ServiceDetailName}     {svc.Name}").WithForeground(Color.Gray).Height(1))
            .WithChild(new TextNode($"  {Strings.ServiceDetailDisplay}  {svc.DisplayName}").WithForeground(Color.Gray).Height(1))
            .WithChild(new TextNode($"  {Strings.HeaderStatus}      {statusIcon} {svc.Status}").WithForeground(statusColor).Height(1))
            .WithChild(new TextNode($"  {Strings.HeaderStartType}  {svc.StartType}").WithForeground(Color.Gray).Height(1))
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode($"  {Strings.ServiceDetailDescription}").WithForeground(Color.BrightCyan).Height(1))
            .WithChild(new TextNode($"  {desc}").WithForeground(Color.White).Height(1))
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode(Strings.ServiceDetailHints).WithForeground(Color.Gray).Height(1));
    }

    private ILayoutNode BuildSearchBar()
    {
        return ViewModel.IsSearchActive.CombineLatest(ViewModel.SearchText,
            (active, search) =>
            {
                if (active)
                {
                    return (ILayoutNode)new TextNode($" / {search}█")
                        .WithForeground(Color.BrightYellow);
                }

                return new TextNode(Strings.ServicesSearchHint)
                    .WithForeground(Color.Gray);
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
