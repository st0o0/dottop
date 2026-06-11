using System.Text.RegularExpressions;
using Termina.Terminal;

namespace dtop.Themes;

public static partial class BtopThemeParser
{
    [GeneratedRegex("""^theme\[(\w+)\]="(#[0-9a-fA-F]{6})"$""")]
    private static partial Regex ThemeLineRegex();

    public static ThemeDefinition Parse(string content)
    {
        var values = new Dictionary<string, string>();

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var match = ThemeLineRegex().Match(trimmed);
            if (match.Success)
                values[match.Groups[1].Value] = match.Groups[2].Value;
        }

        return new ThemeDefinition
        {
            Background = GetColor(values, "main_bg", Color.Default),
            Foreground = GetColor(values, "main_fg", Color.White),
            TextDim = GetColor(values, "inactive_fg", Color.Gray),
            Border = GetColor(values, "div_line", Color.BrightCyan),
            PanelTitle = GetColor(values, "title", Color.BrightCyan),
            Selection = GetColor(values, "selected_bg", Color.BrightCyan),
            SelectionText = GetColor(values, "selected_fg", Color.Black),
            Warning = Color.BrightYellow,
            Error = Color.BrightRed,
            Success = Color.BrightGreen,
            Header = GetColor(values, "inactive_fg", Color.BrightBlack),
            Accent = GetColor(values, "hi_fg", Color.Cyan),
            CpuGradient = GetGradient(values, "cpu"),
            MemGradient = GetGradient(values, "free"),
            GpuGradient = GetGradient(values, "temp"),
            NetGradient = GetGradient(values, "download"),
            DiskGradient = GetGradient(values, "upload"),
            ProcessLow = GetColor(values, "proc_misc", Color.BrightGreen),
            ProcessMed = Color.BrightYellow,
            ProcessHigh = Color.BrightRed,
        };
    }

    public static ThemeDefinition ParseFile(string path)
    {
        var content = File.ReadAllText(path);
        return Parse(content);
    }

    private static Color GetColor(Dictionary<string, string> values, string key, Color fallback)
    {
        return values.TryGetValue(key, out var hex) ? Color.FromHex(hex) : fallback;
    }

    private static Gradient GetGradient(Dictionary<string, string> values, string prefix)
    {
        if (!values.ContainsKey($"{prefix}_start"))
            return Gradient.Create(Color.FromHex("#50fa7b"), Color.FromHex("#f1fa8c"), Color.FromHex("#ff5555"));

        var start = GetColor(values, $"{prefix}_start", Color.FromHex("#50fa7b"));
        var mid = GetColor(values, $"{prefix}_mid", Color.FromHex("#f1fa8c"));
        var end = GetColor(values, $"{prefix}_end", Color.FromHex("#ff5555"));
        return Gradient.Create(start, mid, end);
    }
}
