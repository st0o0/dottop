using Termina.Terminal;

namespace dtop.App.Themes;

public static class Theme
{
    public static Color Background { get; private set; } = Color.Default;
    public static Color Primary { get; private set; } = Color.BrightCyan;
    public static Color Text { get; private set; } = Color.White;
    public static Color TextDim { get; private set; } = Color.Gray;
    public static Color Secondary { get; private set; } = Color.Gray;
    public static Color Border { get; private set; } = Color.BrightCyan;
    public static Color Warning { get; private set; } = Color.BrightYellow;
    public static Color Error { get; private set; } = Color.BrightRed;
    public static Color Success { get; private set; } = Color.BrightGreen;
    public static Color Selection { get; private set; } = Color.BrightCyan;
    public static Color SelectionText { get; private set; } = Color.Black;
    public static Color StatusBar { get; private set; } = Color.BrightCyan;
    public static Color StatusBarText { get; private set; } = Color.Black;
    public static Color Graph { get; private set; } = Color.BrightCyan;
    public static Color PanelTitle { get; private set; } = Color.BrightCyan;
    public static Color Header { get; private set; } = Color.BrightBlack;
    public static Color Accent { get; private set; } = Color.Cyan;

    public static void SetTerminalBackground()
    {
        if (Background == Color.Default)
        {
            return;
        }

        var code = Background == Color.White ? "47" : "40";
        Console.Write($"\x1b[{code}m\x1b[2J\x1b[H");
    }

    public static void ResetTerminalBackground()
    {
        Console.Write("\x1b[0m\x1b[2J\x1b[H");
    }

    public static void Apply(string theme)
    {
        switch (theme)
        {
            case "light":
                Background = Color.White;
                Primary = Color.Blue;
                Text = Color.Black;
                TextDim = Color.DarkGray;
                Secondary = Color.DarkGray;
                Border = Color.Blue;
                Warning = Color.Yellow;
                Error = Color.Red;
                Success = Color.Green;
                Selection = Color.Blue;
                SelectionText = Color.White;
                StatusBar = Color.Blue;
                StatusBarText = Color.White;
                Graph = Color.Blue;
                PanelTitle = Color.Blue;
                Header = Color.DarkGray;
                Accent = Color.Blue;
                break;
            case "nord":
                Background = Color.Default;
                Primary = Color.Cyan;
                Text = Color.White;
                TextDim = Color.BrightBlack;
                Secondary = Color.BrightBlack;
                Border = Color.Cyan;
                Warning = Color.BrightYellow;
                Error = Color.BrightRed;
                Success = Color.BrightGreen;
                Selection = Color.Cyan;
                SelectionText = Color.Black;
                StatusBar = Color.Cyan;
                StatusBarText = Color.Black;
                Graph = Color.Cyan;
                PanelTitle = Color.Cyan;
                Header = Color.BrightBlack;
                Accent = Color.BrightCyan;
                break;
            default: // dark
                Background = Color.Default;
                Primary = Color.BrightCyan;
                Text = Color.White;
                TextDim = Color.Gray;
                Secondary = Color.Gray;
                Border = Color.BrightCyan;
                Warning = Color.BrightYellow;
                Error = Color.BrightRed;
                Success = Color.BrightGreen;
                Selection = Color.BrightCyan;
                SelectionText = Color.Black;
                StatusBar = Color.BrightCyan;
                StatusBarText = Color.Black;
                Graph = Color.BrightCyan;
                PanelTitle = Color.BrightCyan;
                Header = Color.BrightBlack;
                Accent = Color.Cyan;
                break;
        }
    }
}
