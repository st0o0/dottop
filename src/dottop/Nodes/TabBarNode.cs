using Termina.Layout;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop.Nodes;

public sealed class TabBarNode : LayoutNode
{
    private static readonly string[] TabLabels =
        ["1:Prozesse", "2:Performance", "3:Dienste", "4:Netzwerk"];

    private static readonly string[] TabRoutes =
        ["/", "/performance", "/services", "/network"];

    private readonly int _activeIndex;

    public TabBarNode(int activeIndex)
    {
        _activeIndex = activeIndex;
        HeightConstraint = new SizeConstraint.Fixed(1);
        WidthConstraint = new SizeConstraint.Fill();
    }

    public override Size Measure(Size available) => new(available.Width, 1);

    public override void Render(IRenderContext context, Rect bounds)
    {
        if (!bounds.HasArea) return;

        var x = 1;
        for (var i = 0; i < TabLabels.Length; i++)
        {
            var label = $" {TabLabels[i]} ";
            if (i == _activeIndex)
            {
                context.SetForeground(Color.Black);
                context.SetBackground(Color.BrightCyan);
            }
            else
            {
                context.SetForeground(Color.Gray);
            }

            context.WriteAt(x, 0, label);
            context.ResetColors();
            x += label.Length + 1;
        }
    }

    public static string GetRoute(int index) => TabRoutes[Math.Clamp(index, 0, TabRoutes.Length - 1)];
}
