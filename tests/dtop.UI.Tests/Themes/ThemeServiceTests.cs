using dtop.Themes;
using Termina.Terminal;

namespace dtop.UI.Tests.Themes;

public class ThemeServiceTests
{
    [Fact]
    public void Current_ReturnsDefaultTheme()
    {
        var service = new ThemeService();
        Assert.NotNull(service.Current);
        Assert.Equal(Color.White, service.Current.Foreground);
    }

    [Fact]
    public void Apply_ChangesCurrentTheme()
    {
        var service = new ThemeService();
        var custom = new ThemeDefinition { Foreground = Color.FromRgb(100, 200, 50) };
        service.Apply(custom);
        Assert.Equal(Color.FromRgb(100, 200, 50), service.Current.Foreground);
    }

    [Fact]
    public void LoadFromDirectory_LoadsThemeFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"dottop-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "test.theme"), """
                theme[main_bg]="#1a1b26"
                theme[main_fg]="#c0caf5"
                """);
            var service = new ThemeService();
            service.LoadFromDirectory(dir);
            Assert.Contains("test", service.AvailableThemes);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ApplyByName_LoadsAndAppliesTheme()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"dottop-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "tokyo.theme"), """
                theme[main_bg]="#1a1b26"
                theme[main_fg]="#c0caf5"
                """);
            var service = new ThemeService();
            service.LoadFromDirectory(dir);
            service.ApplyByName("tokyo");
            Assert.Equal(Color.FromHex("#c0caf5"), service.Current.Foreground);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ApplyByName_UnknownTheme_ReturnsFalse()
    {
        var service = new ThemeService();
        Assert.False(service.ApplyByName("nonexistent"));
    }
}
