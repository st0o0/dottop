using dottop.Nodes;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dottop.Pages;

public class NetworkPage : ReactivePage<NetworkViewModel>
{
    public override ILayoutNode BuildLayout()
    {
        return Layouts.Vertical()
            .WithChild(new TabBarNode(3))
            .WithChild(new TextNode(" Netzwerk — wird implementiert...")
                .WithForeground(Color.BrightCyan));
    }
}
