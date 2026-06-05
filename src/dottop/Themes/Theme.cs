using Termina.Terminal;

namespace dottop.Themes;

public static class Theme
{
    public static Color Primary { get; private set; } = Color.BrightCyan;
    public static Color Text { get; private set; } = Color.White;
    public static Color Secondary { get; private set; } = Color.Gray;
    public static Color Warning { get; private set; } = Color.BrightYellow;
    public static Color Error { get; private set; } = Color.BrightRed;
    public static Color Selection { get; private set; } = Color.BrightCyan;
    public static Color SelectionText { get; private set; } = Color.Black;
    public static Color StatusBar { get; private set; } = Color.BrightCyan;
    public static Color StatusBarText { get; private set; } = Color.Black;

    public static void Apply(string theme)
    {
        switch (theme)
        {
            case "light":
                Primary = Color.Blue;
                Text = Color.Black;
                Secondary = Color.DarkGray;
                Selection = Color.Blue;
                SelectionText = Color.White;
                StatusBar = Color.Blue;
                StatusBarText = Color.White;
                break;
            case "nord":
                Primary = Color.Cyan;
                Text = Color.White;
                Secondary = Color.BrightBlack;
                Selection = Color.Cyan;
                SelectionText = Color.Black;
                StatusBar = Color.Cyan;
                StatusBarText = Color.Black;
                break;
            default: // dark
                Primary = Color.BrightCyan;
                Text = Color.White;
                Secondary = Color.Gray;
                Selection = Color.BrightCyan;
                SelectionText = Color.Black;
                StatusBar = Color.BrightCyan;
                StatusBarText = Color.Black;
                break;
        }
    }
}
