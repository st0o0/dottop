using dottop.Nodes;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop.Pages;

public class AutostartPage : ReactivePage<AutostartViewModel>
{
    public override ILayoutNode BuildLayout()
    {
        return Layouts.Vertical()
            .WithChild(new TabBarNode(4))
            .WithChild(new TextNode(" Autostart — wird implementiert...")
                .WithForeground(Color.BrightCyan));
    }
}
