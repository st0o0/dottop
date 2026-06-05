using dottop.Nodes;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop.Pages;

public class ServicesPage : ReactivePage<ServicesViewModel>
{
    public override ILayoutNode BuildLayout()
    {
        return Layouts.Vertical()
            .WithChild(new TabBarNode(2))
            .WithChild(new TextNode(" Dienste — wird implementiert...")
                .WithForeground(Color.BrightCyan));
    }
}
