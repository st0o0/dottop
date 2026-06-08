using dottop.App.Resources;
using dottop.App.Themes;
using Termina.Layout;
using Termina.Rendering;

namespace dottop.App.Nodes;

public sealed class TabBarNode : LayoutNode
{
    private static readonly string[] CoreLabels =
        [Strings.TabProcesses, Strings.TabPerformance, Strings.TabServices, Strings.TabNetwork];

    private static readonly string[] CoreRoutes =
        ["/", "/performance", "/services", "/network"];

    private readonly int _activeIndex;
    private readonly IReadOnlyList<string> _allLabels;
    private static IReadOnlyList<string> _allRoutes = CoreRoutes;

    public TabBarNode(int activeIndex, PluginRegistry? pluginRegistry = null)
    {
        _activeIndex = activeIndex;
        var labels = new List<string>(CoreLabels);
        var routes = new List<string>(CoreRoutes);
        if (pluginRegistry is not null)
        {
            foreach (var tab in pluginRegistry.PluginTabs)
            {
                labels.Add(tab.Label);
                routes.Add(tab.Route);
            }
        }

        _allLabels = labels;
        _allRoutes = routes;
        HeightConstraint = new SizeConstraint.Fixed(1);
        WidthConstraint = new SizeConstraint.Fill();
    }

    public override Size Measure(Size available) => new(available.Width, 1);

    public override void Render(IRenderContext context, Rect bounds)
    {
        if (!bounds.HasArea)
        {
            return;
        }

        context.Fill(0, 0, bounds.Width, 1, ' ');
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
