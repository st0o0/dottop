using Termina.Terminal;

namespace dtop.Themes;

public sealed class ThemeService
{
    private static ThemeService? _instance;

    /// <summary>
    /// Global singleton instance. Set once during application startup via <see cref="ServicesSetup"/>.
    /// </summary>
    public static ThemeService Instance => _instance
        ?? throw new InvalidOperationException("ThemeService has not been initialized.");

    private readonly Dictionary<string, string> _themePaths = new(StringComparer.OrdinalIgnoreCase);

    public ThemeDefinition Current { get; private set; } = new();

    public IReadOnlyCollection<string> AvailableThemes => _themePaths.Keys;

    public ThemeService()
    {
        _instance = this;
    }

    public void Apply(ThemeDefinition theme)
    {
        Current = theme;
    }

    /// <summary>
    /// Applies a built-in theme by name ("dark", "light", "nord").
    /// Falls back to the default (dark) theme for unrecognized names.
    /// </summary>
    public void ApplyBuiltIn(string theme)
    {
        Current = theme switch
        {
            "light" => new ThemeDefinition
            {
                Background = Color.White,
                Foreground = Color.Black,
                TextDim = Color.DarkGray,
                Border = Color.Blue,
                PanelTitle = Color.Blue,
                Selection = Color.Blue,
                SelectionText = Color.White,
                StatusBar = Color.Blue,
                StatusBarText = Color.White,
                Warning = Color.Yellow,
                Error = Color.Red,
                Success = Color.Green,
                Header = Color.DarkGray,
                Accent = Color.Blue,
            },
            "nord" => new ThemeDefinition
            {
                Background = Color.Default,
                Foreground = Color.White,
                TextDim = Color.BrightBlack,
                Border = Color.Cyan,
                PanelTitle = Color.Cyan,
                Selection = Color.Cyan,
                SelectionText = Color.Black,
                StatusBar = Color.Cyan,
                StatusBarText = Color.Black,
                Warning = Color.BrightYellow,
                Error = Color.BrightRed,
                Success = Color.BrightGreen,
                Header = Color.BrightBlack,
                Accent = Color.BrightCyan,
            },
            _ => new ThemeDefinition(), // dark (defaults)
        };
    }

    public void SetTerminalBackground()
    {
        if (Current.Background == Color.Default)
        {
            return;
        }

        var code = Current.Background == Color.White ? "47" : "40";
        Console.Write($"\x1b[{code}m\x1b[2J\x1b[H");
    }

    public static void ResetTerminalBackground()
    {
        Console.Write("\x1b[0m\x1b[2J\x1b[H");
    }

    public void LoadFromDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var file in Directory.GetFiles(directory, "*.theme"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            _themePaths[name] = file;
        }
    }

    public bool ApplyByName(string name)
    {
        if (!_themePaths.TryGetValue(name, out var path))
            return false;

        Current = BtopThemeParser.ParseFile(path);
        return true;
    }
}
