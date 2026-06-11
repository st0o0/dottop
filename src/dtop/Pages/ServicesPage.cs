using dtop.Core.Models;
using dtop.Nodes;
using dtop.Resources;
using dtop.Themes;
using R3;
using Termina.Extensions;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dtop.Pages;

public class ServicesPage : ReactivePage<ServicesViewModel>
{
    private DataListNode<ServiceInfo>? _list;
    private ModalNode? _detailModal;
    private ModalNode? _settingsModal;

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
            s => s.Status == ServiceStatus.Running ? Theme.Text : Theme.TextDim);

        ViewModel.ListNode = _list;
        ViewModel.GetSelectedItem = () => _list.SelectedItem;

        _detailModal = new ModalNode()
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Theme.Primary)
            .WithBackdrop(BackdropStyle.Solid)
            .WithBackdropColor(Color.Black)
            .WithDismissOnEscape(false)
            .WithPadding(1);

        var conditionalDetail = new ConditionalNode(ViewModel.IsDetailOpen, _detailModal);

        _settingsModal = new ModalNode()
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Theme.Primary)
            .WithBackdrop(BackdropStyle.Solid)
            .WithBackdropColor(Color.Black)
            .WithDismissOnEscape(false)
            .WithPadding(1);

        var conditionalSettings = new ConditionalNode(ViewModel.IsSettingsOpen, _settingsModal);

        var mainLayout = Layouts.Vertical()
            .WithChild(new TabBarNode(3))
            .WithChild(BuildSearchBar())
            .WithChild(new PanelNode()
                .WithTitle(Strings.PanelServices)
                .WithBorder(BorderStyle.Rounded)
                .WithBorderColor(Theme.Primary)
                .WithContent(Layouts.Vertical()
                    .WithChild(new TextNode($"   {Strings.HeaderName,-32} {Strings.HeaderStatus,-8} {Strings.HeaderStartType,-10}")
                        .WithForeground(Theme.Header).Height(1))
                    .WithChild(_list.Fill()))
                .Fill())
            .WithChild(BuildStatusBar());

        return Layouts.Stack(mainLayout, conditionalDetail, conditionalSettings);
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        ViewModel.FilteredServices.Subscribe(services => _list?.SetItems(services))
            .DisposeWith(Subscriptions);

        ViewModel.DetailContentChanged.Subscribe(_ => UpdateDetailModal())
            .DisposeWith(Subscriptions);

        ViewModel.SettingsContentChanged.Subscribe(_ => UpdateSettingsModal())
            .DisposeWith(Subscriptions);
    }

    private void UpdateSettingsModal()
    {
        if (_settingsModal is null)
        {
            return;
        }

        _settingsModal.WithTitle($" {Strings.SettingsTitle} ").WithTitleColor(Theme.Primary);
        _settingsModal.WithFooter(Strings.HintSettingsModalKeys).WithFooterColor(Theme.TextDim);

        var layout = Layouts.Vertical()
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode($"  {Strings.SettingsRefreshRate,-20} ◀ {ViewModel.GetRefreshRateDisplay()} ▶")
                .WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode("").Height(1));

        if (ViewModel.IsUpdateAvailable)
        {
            layout.WithChild(new TextNode($"  {ViewModel.LatestVersionDisplay}").WithForeground(Theme.Warning).Height(1));
            layout.WithChild(new TextNode($"  [U] {Strings.UpdatePressU}").WithForeground(Theme.Accent).Height(1));
        }
        else
        {
            layout.WithChild(new TextNode($"  {ViewModel.CurrentVersionDisplay}").WithForeground(Theme.TextDim).Height(1));
        }

        layout.WithChild(new TextNode("").Height(1));
        layout.WithChild(new TextNode($"  {ViewModel.GetSettingsFilePath()}").WithForeground(Theme.TextDim).Height(1));

        _settingsModal.Content = layout;
    }

    private void UpdateDetailModal()
    {
        if (_detailModal is null || ViewModel.SelectedService.Value is not { } svc)
        {
            return;
        }

        var statusIcon = svc.Status == ServiceStatus.Running ? "▶" : "■";
        var statusColor = svc.Status == ServiceStatus.Running ? Theme.Text : Theme.TextDim;
        var desc = string.IsNullOrWhiteSpace(svc.Description)
            ? Strings.ServiceNoDescription
            : svc.Description;

        _detailModal.WithTitle($" {svc.DisplayName} ").WithTitleColor(Theme.Primary);
        _detailModal.WithFooter(Strings.ServiceDetailHints).WithFooterColor(Theme.TextDim);
        _detailModal.Content = Layouts.Vertical()
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode($"  {Strings.ServiceDetailName}     {svc.Name}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode($"  {Strings.ServiceDetailDisplay}  {svc.DisplayName}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode($"  {Strings.HeaderStatus}      {statusIcon} {svc.Status}").WithForeground(statusColor).Height(1))
            .WithChild(new TextNode($"  {Strings.HeaderStartType}  {svc.StartType}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode($"  {Strings.ServiceDetailDescription}").WithForeground(Theme.Primary).Height(1))
            .WithChild(new TextNode($"  {desc}").WithForeground(Theme.Text).Height(1));
    }

    private ILayoutNode BuildSearchBar()
    {
        return ViewModel.IsSearchActive.CombineLatest(ViewModel.SearchText,
            (active, search) =>
            {
                if (active)
                {
                    return (ILayoutNode)new TextNode($" / {search}█  Esc: Exit")
                        .WithForeground(Theme.Warning);
                }

                return new TextNode(Strings.ServicesSearchHint)
                    .WithForeground(Theme.TextDim);
            }).AsLayout().Height(1);
    }

    private ILayoutNode BuildStatusBar()
    {
        return ViewModel.StatusMessage
            .Select<string, ILayoutNode>(msg =>
                new TextNode($" {msg}").WithForeground(Theme.StatusBarText).WithBackground(Theme.StatusBar))
            .AsLayout().Height(1);
    }
}
