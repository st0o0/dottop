using dtop.App.Resources;
using dtop.App.Themes;
using Termina.Layout;
using Termina.Rendering;

namespace dtop.App.Nodes;

public sealed class TabBarNode : LayoutNode
{
    private static readonly string[] CoreLabels =
        [Strings.TabProcesses, Strings.TabPerformance, Strings.TabServices, Strings.TabNetwork];

    private static readonly string[] CoreRoutes = ["/", "/performance", "/services", "/network"];

    private readonly int _activeIndex;
    private static List<string> _allLabels = [..CoreLabels];
    private static IReadOnlyList<string> _allRoutes = CoreRoutes;

    public static void  RegisterPluginTabs(PluginRegistry registry)
    {
        var labels = new List<string>(CoreLabels);
        var routes = new List<string>(CoreRoutes);
        foreach (var tab in registry.PluginTabs)
        {
            labels.Add(tab.Label);
            routes.Add(tab.Route);
        }

        _allLabels = labels;
        _allRoutes = routes;
    }

    public TabBarNode(int activeIndex)
    {
        _activeIndex = activeIndex;
        HeightConstraint = new SizeConstraint.Fixed(1);
        WidthConstraint = new SizeConstraint.Fill();
    }

    public override Size Measure(Size available) => available with { Height = 1 };

    public override void Render(IRenderContext context, Rect bounds)
    {
        if (!bounds.HasArea)
        {
            return;
        }

        context.Fill(0, 0, bounds.Width, 1);
        var x = 1;
        for (var i = 0; i < _allLabels.Count; i++)
        {
            var label = $" {_allLabels[i]} ";
            if (i == _activeIndex)
            {
                context.SetForeground(Theme.SelectionText);
                context.SetBackground(Theme.Selection);
            }
            else
            {
                context.SetForeground(Theme.Secondary);
            }

            context.WriteAt(x, 0, label);
            context.ResetColors();
            x += label.Length + 1;
        }
    }

    public static string GetRoute(int index) => _allRoutes[Math.Clamp(index, 0, _allRoutes.Count - 1)];
}