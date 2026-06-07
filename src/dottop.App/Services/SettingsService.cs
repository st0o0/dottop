using System.Globalization;
using System.Text.Json;
using dottop.Core.Models;
using dottop.Themes;

namespace dottop.Services;

public class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "dottop");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    public AppSettings Settings { get; private set; } = new();
    public string FilePath => SettingsPath;

    public event Action? OnSettingsApplied;

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new();
            }
        }
        catch { Settings = new(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
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

        OnSettingsApplied?.Invoke();
    }
}
