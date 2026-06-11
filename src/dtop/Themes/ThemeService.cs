namespace dtop.Themes;

public sealed class ThemeService
{
    private readonly Dictionary<string, string> _themePaths = new(StringComparer.OrdinalIgnoreCase);

    public ThemeDefinition Current { get; private set; } = new();

    public IReadOnlyCollection<string> AvailableThemes => _themePaths.Keys;

    public void Apply(ThemeDefinition theme)
    {
        Current = theme;
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
