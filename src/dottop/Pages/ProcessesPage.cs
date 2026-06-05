using dottop.Nodes;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop.Pages;

public class ProcessesPage : ReactivePage<ProcessesViewModel>
{
    public override ILayoutNode BuildLayout()
    {
        return Layouts.Vertical()
            .WithChild(new TabBarNode(0))
            .WithChild(new TextNode(" Prozesse — wird implementiert...")
                .WithForeground(Color.BrightCyan));
    }
}
