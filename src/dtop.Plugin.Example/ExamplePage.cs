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
                        .WithBorderColor(ThemeService.Instance.Current.Accent)
                        .WithTitleColor(ThemeService.Instance.Current.PanelTitle)
                        .WithContent(
                            Layouts.Vertical()
                                .WithChild(new TextNode("").Height(1))
                                .WithChild(new TextNode("  Hello from Example Plugin!")
                                    .WithForeground(ThemeService.Instance.Current.Accent).Height(1))
                                .WithChild(new TextNode("").Height(1))
                                .WithChild(new TextNode($"  Counter: {count}")
                                    .WithForeground(ThemeService.Instance.Current.Foreground).Height(1))
                                .WithChild(new TextNode("").Height(1))
                                .WithChild(new TextNode("  Press Enter to increment the counter.")
                                    .WithForeground(ThemeService.Instance.Current.TextDim).Height(1)))
                        .Fill())
                    .WithChild(new TextNode(" Example Plugin | Enter: Increment | Q: Quit")
                        .WithForeground(ThemeService.Instance.Current.StatusBarText)
                        .WithBackground(ThemeService.Instance.Current.StatusBar)
                        .Height(1)))
            .AsLayout();

        return content;
    }
}
