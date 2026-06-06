using dottop.Actors;
using dottop.Models;
using dottop.Nodes;
using dottop.Resources;
using dottop.Themes;
using R3;
using Termina.Extensions;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop.Pages;

public class ProcessesPage : ReactivePage<ProcessesViewModel>
{
    private ModalNode? _overlay;
    private DataListNode<ProcessSnapshot>? _list;
    private DataListNode<KeyValuePair<string, string>>? _envList;
    private DataListNode<string>? _handlesList;

    public override ILayoutNode BuildLayout()
    {
        _list = new DataListNode<ProcessSnapshot>(
            p =>
            {
                var ramMb = p.WorkingSetBytes / 1024 / 1024;
                var name = p.Name.Length > 20 ? p.Name[..19] + "…" : p.Name;
                var cpuBar = MiniBar(p.CpuPercent, 8);
                var ramStr = ramMb >= 1024 ? $"{ramMb / 1024.0:F1}GB" : $"{ramMb}MB";
                return $" {p.Pid,6}  {name,-20} {cpuBar} {p.CpuPercent,5:F1}%  {ramStr,7}  {p.Group}";
            },
            p => p.CpuPercent switch
            {
                > 80 => Color.BrightRed,
                > 50 => Color.BrightYellow,
                _ => Color.White,
            });

        ViewModel.ListNode = _list;
        ViewModel.GetSelectedItem = () => _list.SelectedItem;

        _overlay = new ModalNode()
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Theme.Primary)
            .WithBackdrop(BackdropStyle.Solid)
            .WithBackdropColor(Color.Black)
            .WithDismissOnEscape(false)
            .WithPadding(1);

        var conditionalOverlay = new ConditionalNode(
            ViewModel.IsOverlayOpen,
            _overlay);

        var mainLayout = Layouts.Vertical()
            .WithChild(new TabBarNode(0))
            .WithChild(BuildSearchBar())
            .WithChild(new PanelNode()
                .WithTitle(Strings.PanelProcesses)
                .WithBorder(BorderStyle.Rounded)
                .WithBorderColor(Theme.Primary)
                .WithContent(Layouts.Vertical()
                    .WithChild(BuildHeader())
                    .WithChild(_list.Fill()))
                .Fill())
            .WithChild(BuildStatusBar());

        return Layouts.Stack(mainLayout, conditionalOverlay);
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();

        ViewModel.FilteredProcesses.Subscribe(list => _list?.SetItems(list))
            .DisposeWith(Subscriptions);

        ViewModel.OverlayContentChanged.Subscribe(_ => UpdateOverlayContent())
            .DisposeWith(Subscriptions);

        ViewModel.IsKillConfirmPending.Subscribe(_ => UpdateOverlayContent())
            .DisposeWith(Subscriptions);

        Observable.Interval(TimeSpan.FromSeconds(1))
            .Subscribe(_ =>
            {
                if (ViewModel.IsOverlayOpen.Value && ViewModel.OverlayTabIndex.Value == 0
                    && ViewModel.SelectedProcess.Value is { } current)
                {
                    var updated = ViewModel.AllProcesses.Value.FirstOrDefault(p => p.Pid == current.Pid);
                    if (updated is not null)
                    {
                        ViewModel.SelectedProcess.Value = updated;
                        UpdateOverlayContent();
                    }
                }
            })
            .DisposeWith(Subscriptions);
    }

    private ILayoutNode BuildSearchBar()
    {
        return ViewModel.IsSearchActive
            .CombineLatest(ViewModel.SearchText, (a, b) => (a, b))
            .CombineLatest(ViewModel.SortColumn, (ab, s) => (ab.a, ab.b, s))
            .CombineLatest(ViewModel.SelectedGroup, (abs, g) => (Active: abs.a, Search: abs.b, Sort: abs.s, Group: g))
            .Select(t =>
            {
                var groupLabel = t.Group?.ToString() ?? Strings.GroupAll;
                if (t.Active)
                {
                    return (ILayoutNode)new TextNode(string.Format(Strings.SearchBarActiveFormat, t.Search + "█", groupLabel, t.Sort) + " ↓")
                        .WithForeground(Theme.Warning);
                }

                return new TextNode(string.Format(Strings.SearchBarInactiveFormat, groupLabel, t.Sort) + " ↓")
                    .WithForeground(Theme.TextDim);
            })
            .AsLayout()
            .Height(1);
    }

    private ILayoutNode BuildHeader()
    {
        return new TextNode($" {Strings.HeaderPid,6}  {Strings.HeaderName,-20} {"",8} {Strings.HeaderCpuPercent,6}  {Strings.HeaderRam,7}  {Strings.HeaderGroup}")
            .WithForeground(Theme.Header)
            .Height(1);
    }

    private void UpdateOverlayContent()
    {
        if (_overlay is null || ViewModel.SelectedProcess.Value is not { } proc)
        {
            return;
        }

        var tabLabels = new[] { Strings.OverlayTabOverview, Strings.OverlayTabProcessTree, Strings.OverlayTabEnvironment, Strings.OverlayTabHandles };
        var activeTab = ViewModel.OverlayTabIndex.Value;

        var header = Layouts.Horizontal();
        for (var i = 0; i < tabLabels.Length; i++)
        {
            var tabNode = new TextNode($" {tabLabels[i]} ");
            if (i == activeTab)
            {
                tabNode.WithForeground(Theme.SelectionText).WithBackground(Theme.Selection);
            }
            else
            {
                tabNode.WithForeground(Theme.Secondary);
            }

            header.WithChild(tabNode.Height(1));
        }

        _overlay.WithTitle($" {proc.Name} — PID {proc.Pid} ");
        _overlay.WithTitleColor(Theme.Primary);
        _overlay.Content = Layouts.Vertical()
            .WithChild(header.Height(1))
            .WithChild(BuildOverlayTab(proc, activeTab));
    }

    private ILayoutNode BuildOverlayTab(ProcessSnapshot proc, int tab)
    {
        ILayoutNode content = tab switch
        {
            0 => BuildOverviewTab(proc, ViewModel.IsKillConfirmPending.Value),
            1 => BuildTreeTab(),
            2 => BuildEnvTab(),
            3 => BuildHandlesTab(),
            _ => new TextNode("").WithForeground(Color.Red)
        };
        return Layouts.Vertical().WithChild(content).Fill();
    }

    private static ILayoutNode BuildOverviewTab(ProcessSnapshot proc, bool isKillConfirmPending)
    {
        var ramMb = proc.WorkingSetBytes / 1024 / 1024;
        var ramStr = ramMb >= 1024 ? $"{ramMb / 1024.0:F1} GB" : $"{ramMb} MB";
        var cpuBar = MiniBar(proc.CpuPercent, 20);
        var cpuColor = proc.CpuPercent > 80 ? Color.BrightRed : proc.CpuPercent > 50 ? Color.BrightYellow : Color.Cyan;

        var layout = Layouts.Vertical()
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode($"  CPU   {cpuBar}  {proc.CpuPercent:F1}%").WithForeground(cpuColor).Height(1))
            .WithChild(new TextNode($"  RAM   {ramStr}").WithForeground(Theme.Text).Height(1))
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode($"  PID        {proc.Pid}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode($"  Parent     {proc.ParentPid}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode($"  Threads    {proc.ThreadCount}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode($"  Handles    {proc.HandleCount}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode($"  {Strings.HeaderGroup}     {proc.Group}").WithForeground(Theme.TextDim).Height(1))
            .WithChild(new TextNode("").Height(1));

        if (isKillConfirmPending)
        {
            layout.WithChild(new TextNode(string.Format(Strings.KillConfirmFormat, proc.Name, proc.Pid))
                .WithForeground(Theme.Error).Height(1));
        }
        else
        {
            layout.WithChild(new TextNode(Strings.OverlayKeyboardHints).WithForeground(Theme.TextDim).Height(1));
        }

        return layout;
    }

    private ILayoutNode BuildTreeTab()
    {
        if (ViewModel.ProcessTree.Value is not { } tree)
        {
            return new TextNode(Strings.LoadingProcessTree).WithForeground(Theme.TextDim);
        }

        var layout = Layouts.Vertical();
        RenderTree(layout, tree, 0);
        return layout;
    }

    private static void RenderTree(VerticalLayout layout, ProcessTreeResult node, int depth, bool isLast = true)
    {
        var indent = new string(' ', depth * 3);
        var connector = depth == 0 ? " ●" : isLast ? " └─" : " ├─";
        var color = depth == 0 ? Theme.Primary : Theme.TextDim;
        layout.WithChild(new TextNode($"{indent}{connector} {node.Name} ({node.Pid})")
            .WithForeground(color).Height(1));
        for (var i = 0; i < node.Children.Count; i++)
            RenderTree(layout, node.Children[i], depth + 1, i == node.Children.Count - 1);
    }

    private ILayoutNode BuildEnvTab()
    {
        if (ViewModel.ProcessEnv.Value is not { } env)
        {
            return new TextNode(Strings.LoadingEnvironmentVars).WithForeground(Theme.TextDim);
        }

        _envList = new DataListNode<KeyValuePair<string, string>>(
            kv => $" {kv.Key}={kv.Value}",
            _ => Theme.Text);
        _envList.SetItems(env.OrderBy(kv => kv.Key).ToList());
        ViewModel.OverlayListNode = _envList;
        return _envList.Fill();
    }

    private ILayoutNode BuildHandlesTab()
    {
        if (ViewModel.ProcessHandles.Value is not { } handles)
        {
            return new TextNode(Strings.LoadingHandles).WithForeground(Theme.TextDim);
        }

        if (handles.Count == 0)
        {
            return new TextNode(Strings.NoHandleInfo).WithForeground(Theme.TextDim);
        }

        _handlesList = new DataListNode<string>(
            h => $" {h}",
            _ => Theme.Text);
        _handlesList.SetItems(handles.ToList());
        ViewModel.OverlayListNode = _handlesList;
        return _handlesList.Fill();
    }

    private ILayoutNode BuildStatusBar()
    {
        return ViewModel.StatusMessage
            .Select<string, ILayoutNode>(msg =>
                new TextNode($" {msg}").WithForeground(Theme.StatusBarText).WithBackground(Theme.StatusBar))
            .AsLayout()
            .Height(1);
    }

    private static string MiniBar(double percent, int width)
    {
        var blocks = "░▒▓█";
        var filled = percent / 100.0 * width;
        var sb = new System.Text.StringBuilder(width);
        for (var i = 0; i < width; i++)
        {
            var level = filled - i;
            sb.Append(level switch
            {
                >= 1 => '█',
                >= 0.75 => '▓',
                >= 0.5 => '▒',
                >= 0.25 => '░',
                _ => ' '
            });
        }
        return sb.ToString();
    }
}
