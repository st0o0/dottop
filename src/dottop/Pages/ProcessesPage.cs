using dottop.Actors;
using dottop.Models;
using dottop.Nodes;
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

    public override ILayoutNode BuildLayout()
    {
        _overlay = new ModalNode()
            .WithBorder(BorderStyle.Rounded)
            .WithBorderColor(Color.BrightCyan)
            .WithBackdrop(BackdropStyle.Dim);

        ViewModel.IsOverlayOpen.Subscribe(open =>
        {
            if (open)
            {
                UpdateOverlayContent();
                Focus.PushFocus(_overlay!);
            }
            else
            {
                Focus.PopFocus();
            }
        }).DisposeWith(Subscriptions);

        ViewModel.OverlayTabIndex.Subscribe(_ => UpdateOverlayContent()).DisposeWith(Subscriptions);

        _overlay.Dismissed.Subscribe(_ => ViewModel.CloseOverlay()).DisposeWith(Subscriptions);

        return Layouts.Vertical()
            .WithChild(new TabBarNode(0))
            .WithChild(BuildToolbar())
            .WithChild(BuildHeader())
            .WithChild(BuildProcessList())
            .WithChild(BuildStatusBar())
            .WithChild(_overlay);
    }

    private ILayoutNode BuildToolbar()
    {
        return ViewModel.SortColumn
            .Select<SortColumn, ILayoutNode>(sort =>
            {
                var groupLabel = ViewModel.SelectedGroup.Value?.ToString() ?? "Alle";
                var search = ViewModel.SearchText.Value;
                var searchDisplay = ViewModel.IsSearchActive.Value ? $"/ {search}_" : "";
                return new TextNode($" {searchDisplay}  Gruppe: [{groupLabel}]  Sort: {sort} ↓")
                    .WithForeground(Color.BrightGreen);
            })
            .AsLayout()
            .Height(1);
    }

    private ILayoutNode BuildHeader()
    {
        return new TextNode($" {"PID",5}  {"Name",-20}  {"CPU%",6}  {"RAM",10}  Gruppe")
            .WithForeground(Color.Gray)
            .Height(1);
    }

    private ILayoutNode BuildProcessList()
    {
        return ViewModel.FilteredProcesses.CombineLatest<List<ProcessSnapshot>, int, ILayoutNode>(
            ViewModel.SelectedIndex,
            (processes, selectedIdx) =>
            {
                var container = new ScrollableContainerNode().WithScrollbar(true);
                var layout = Layouts.Vertical();
                for (var i = 0; i < processes.Count; i++)
                {
                    var p = processes[i];
                    var ramMb = p.WorkingSetBytes / 1024 / 1024;
                    var text = $" {p.Pid,5}  {p.Name,-20}  {p.CpuPercent,5:F1}%  {ramMb,6} MB  {p.Group}";
                    var node = new TextNode(text);
                    if (i == selectedIdx)
                        node.WithForeground(Color.White).WithBackground(Color.BrightBlue);
                    else
                        node.WithForeground(p.Group switch
                        {
                            ProcessGroup.Apps => Color.BrightCyan,
                            ProcessGroup.Background => Color.Gray,
                            ProcessGroup.Windows => Color.DarkGray,
                            _ => Color.White,
                        });
                    layout.WithChild(node.Height(1));
                }
                return container.WithContent(layout);
            }).AsLayout().Fill();
    }

    private void UpdateOverlayContent()
    {
        if (_overlay is null || ViewModel.SelectedProcess.Value is not { } proc) return;

        var tabLabels = new[] { "Übersicht", "Prozessbaum", "Umgebung", "Handles" };
        var activeTab = ViewModel.OverlayTabIndex.Value;

        var header = Layouts.Horizontal();
        for (var i = 0; i < tabLabels.Length; i++)
        {
            var tabNode = new TextNode($" {tabLabels[i]} ");
            if (i == activeTab)
                tabNode.WithForeground(Color.Black).WithBackground(Color.BrightCyan);
            else
                tabNode.WithForeground(Color.Gray);
            header.WithChild(tabNode.Height(1));
        }

        _overlay.WithTitle($" {proc.Name} — PID {proc.Pid} ");
        _overlay.WithTitleColor(Color.BrightCyan);
        _overlay.Content = Layouts.Vertical()
            .WithChild(header.Height(1))
            .WithChild(BuildOverlayTab(proc, activeTab));
    }

    private ILayoutNode BuildOverlayTab(ProcessSnapshot proc, int tab)
    {
        ILayoutNode content = tab switch
        {
            0 => BuildOverviewTab(proc),
            1 => BuildTreeTab(),
            2 => BuildEnvTab(),
            3 => BuildHandlesTab(),
            _ => new TextNode("").WithForeground(Color.Red)
        };
        // Wrap in a vertical layout so we can apply Fill()
        return Layouts.Vertical().WithChild(content).Fill();
    }

    private static ILayoutNode BuildOverviewTab(ProcessSnapshot proc)
    {
        var ramMb = proc.WorkingSetBytes / 1024 / 1024;
        return Layouts.Vertical()
            .WithChild(new TextNode($" CPU: {proc.CpuPercent:F1}%     RAM: {ramMb} MB    Threads: {proc.ThreadCount}    Handles: {proc.HandleCount}").WithForeground(Color.BrightCyan).Height(1))
            .WithChild(new TextNode($" User: {proc.UserName}    PID: {proc.Pid}    Parent: {proc.ParentPid}    Gruppe: {proc.Group}").WithForeground(Color.BrightCyan).Height(1))
            .WithChild(new TextNode("").Height(1))
            .WithChild(new TextNode(" [K] Kill   ←→ Tabs   Esc Schließen").WithForeground(Color.BrightGreen).Height(1));
    }

    private ILayoutNode BuildTreeTab()
    {
        if (ViewModel.ProcessTree.Value is not { } tree)
            return new TextNode(" Lade Prozessbaum...").WithForeground(Color.Gray);
        var layout = Layouts.Vertical();
        RenderTree(layout, tree, 0);
        return layout;
    }

    private static void RenderTree(VerticalLayout layout, ProcessTreeResult node, int depth)
    {
        var indent = new string(' ', depth * 2 + 1);
        var prefix = depth > 0 ? "├─ " : "● ";
        layout.WithChild(new TextNode($"{indent}{prefix}{node.Name} (PID {node.Pid})")
            .WithForeground(Color.BrightCyan).Height(1));
        foreach (var child in node.Children)
            RenderTree(layout, child, depth + 1);
    }

    private ILayoutNode BuildEnvTab()
    {
        if (ViewModel.ProcessEnv.Value is not { } env)
            return new TextNode(" Lade Umgebungsvariablen...").WithForeground(Color.Gray);
        var layout = Layouts.Vertical();
        foreach (var (key, value) in env.OrderBy(kv => kv.Key).Take(50))
            layout.WithChild(new TextNode($" {key}={value}").WithForeground(Color.BrightCyan).Height(1));
        return new ScrollableContainerNode().WithContent(layout).WithScrollbar(true);
    }

    private ILayoutNode BuildHandlesTab()
    {
        if (ViewModel.ProcessHandles.Value is not { } handles)
            return new TextNode(" Lade Handles...").WithForeground(Color.Gray);
        if (handles.Count == 0)
            return new TextNode(" Keine Handle-Informationen verfügbar").WithForeground(Color.Gray);
        var layout = Layouts.Vertical();
        foreach (var handle in handles.Take(50))
            layout.WithChild(new TextNode($" {handle}").WithForeground(Color.BrightCyan).Height(1));
        return new ScrollableContainerNode().WithContent(layout).WithScrollbar(true);
    }

    private ILayoutNode BuildStatusBar()
    {
        return ViewModel.StatusMessage
            .Select<string, ILayoutNode>(msg =>
                new TextNode($" {msg}").WithForeground(Color.Black).WithBackground(Color.BrightCyan))
            .AsLayout()
            .Height(1);
    }
}
