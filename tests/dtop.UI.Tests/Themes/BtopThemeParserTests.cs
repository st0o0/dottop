using dtop.Themes;
using Termina.Terminal;

namespace dtop.UI.Tests.Themes;

public class BtopThemeParserTests
{
    private const string MinimalTheme = """
        # btop color scheme
        theme[main_bg]="#1a1b26"
        theme[main_fg]="#c0caf5"
        theme[title]="#7aa2f7"
        theme[hi_fg]="#bb9af7"
        theme[selected_bg]="#33467c"
        theme[selected_fg]="#c0caf5"
        theme[inactive_fg]="#565f89"
        theme[proc_misc]="#7dcfff"
        theme[div_line]="#565f89"
        theme[cpu_start]="#7aa2f7"
        theme[cpu_mid]="#7dcfff"
        theme[cpu_end]="#bb9af7"
        theme[free_start]="#9ece6a"
        theme[free_mid]="#e0af68"
        theme[free_end]="#f7768e"
        """;

    [Fact]
    public void Parse_ExtractsBackground()
    {
        var theme = BtopThemeParser.Parse(MinimalTheme);
        Assert.Equal(Color.FromHex("#1a1b26"), theme.Background);
    }

    [Fact]
    public void Parse_ExtractsForeground()
    {
        var theme = BtopThemeParser.Parse(MinimalTheme);
        Assert.Equal(Color.FromHex("#c0caf5"), theme.Foreground);
    }

    [Fact]
    public void Parse_ExtractsCpuGradient()
    {
        var theme = BtopThemeParser.Parse(MinimalTheme);
        Assert.Equal(Color.FromHex("#7aa2f7"), theme.CpuGradient.Sample(0f));
        Assert.Equal(Color.FromHex("#7dcfff"), theme.CpuGradient.Sample(0.5f));
        Assert.Equal(Color.FromHex("#bb9af7"), theme.CpuGradient.Sample(1f));
    }

    [Fact]
    public void Parse_ExtractsSelection()
    {
        var theme = BtopThemeParser.Parse(MinimalTheme);
        Assert.Equal(Color.FromHex("#33467c"), theme.Selection);
        Assert.Equal(Color.FromHex("#c0caf5"), theme.SelectionText);
    }

    [Fact]
    public void Parse_ExtractsBorder()
    {
        var theme = BtopThemeParser.Parse(MinimalTheme);
        Assert.Equal(Color.FromHex("#565f89"), theme.Border);
    }

    [Fact]
    public void Parse_IgnoresComments()
    {
        var input = """
            # comment
            theme[main_bg]="#000000"
            theme[main_fg]="#ffffff"
            """;
        var theme = BtopThemeParser.Parse(input);
        Assert.Equal(Color.FromHex("#000000"), theme.Background);
    }

    [Fact]
    public void Parse_MissingKeys_UsesDefaults()
    {
        var input = """
            theme[main_bg]="#000000"
            theme[main_fg]="#ffffff"
            """;
        var theme = BtopThemeParser.Parse(input);
        Assert.NotNull(theme.CpuGradient);
    }
}
