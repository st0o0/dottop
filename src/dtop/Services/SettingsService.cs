using System.Globalization;
using System.Text.Json;
using dtop.Core.Models;
using dtop.Themes;

namespace dtop.Services;

public class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "dtop");

    public AppSettings Settings { get; private set; } = new();
    public static string FilePath { get; } = Path.Combine(SettingsDir, "settings.json");

    public void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            Settings = new();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch
        {
        }
    }

    private string _lastTheme = "";

    public void ApplyAll()
    {
        var themeChanged = _lastTheme != Settings.Theme;
        _lastTheme = Settings.Theme;

        Theme.Apply(Settings.Theme);

        if (themeChanged)
        {
            Theme.SetTerminalBackground();
            Console.Write("\x1b[2J\x1b[H");
        }

        if (Settings.Language != "system")
        {
            var culture = new CultureInfo(Settings.Language);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
        }
    }
}