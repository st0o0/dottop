using dtop.Nodes;
using dtop.Themes;
using R3;
using Termina.Extensions;
using Termina.Layout;
using Termina.Reactive;
using Termina.Rendering;
using Termina.Terminal;

namespace dtop.Plugin.Example;

public class ExamplePage : ReactivePage<ExampleViewModel>
{
    public override ILayoutNode BuildLayout()
    {
        var content = ViewModel.Counter
            .Select<int, ILayoutNode>(count =>
                Layouts.Vertical()
                    .WithChild(new TabBarNode(5))
                    .WithChild(new PanelNode()
                        .WithTitle(" Example Plugin ")
                        .WithBorder(BorderStyle.Rounded)
                        .WithBorderColor(Theme.Primary)
                        .WithContent(
                            Layouts.Vertical()
                                .WithChild(new TextNode("").Height(1))
                                .WithChild(new TextNode("  Hello from Example Plugin!")
                                    .WithForeground(Theme.Primary).Height(1))
                                .WithChild(new TextNode("").Height(1))
                                .WithChild(new TextNode($"  Counter: {count}")
                                    .WithForeground(Theme.Text).Height(1))
                                .WithChild(new TextNode("").Height(1))
                                .WithChild(new TextNode("  Press Enter to increment the counter.")
                                    .WithForeground(Theme.TextDim).Height(1)))
                        .Fill())
                    .WithChild(new TextNode(" Example Plugin | Enter: Increment | Q: Quit")
                        .WithForeground(Theme.StatusBarText)
                        .WithBackground(Theme.StatusBar)
                        .Height(1)))
            .AsLayout();

        return content;
    }
}
