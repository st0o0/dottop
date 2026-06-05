using dottop.Nodes;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop.Pages;

public class PerformancePage : ReactivePage<PerformanceViewModel>
{
    public override ILayoutNode BuildLayout()
    {
        return Layouts.Vertical()
            .WithChild(new TabBarNode(1))
            .WithChild(new TextNode(" Performance — wird implementiert...")
                .WithForeground(Color.BrightCyan));
    }
}
