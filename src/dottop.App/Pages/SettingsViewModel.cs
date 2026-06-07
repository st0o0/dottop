using R3;
using dottop.Resources;
using dottop.Services;
using Termina.Input;
using Termina.Reactive;

namespace dottop.Pages;

public class SettingsViewModel : ReactiveViewModel
{
    private readonly SettingsService _settingsService;
    private readonly UpdateService _updateService;

    public ReactiveProperty<int> SelectedRow { get; } = new(0);
    public ReactiveProperty<string> StatusMessage { get; } = new("");

    private readonly Subject<Unit> _settingsChanged = new();
    public Observable<Unit> SettingsChanged => _settingsChanged.AsObservable();

    public Func<int>? GetViewportHeight { get; set; }

    private static readonly string[][] OptionKeys =
    [
        ["dark", "light", "nord"],
        ["250", "500", "1000", "2000", "5000"],
        ["cpu", "ram", "name", "pid"],
        ["all", "apps", "background", "system"],
        ["blocks", "braille", "outline", "ascii"],
        ["system", "de", "en"],
    ];

    public SettingsViewModel(SettingsService settingsService, UpdateService updateService)
    {
        _settingsService = settingsService;
        _updateService = updateService;
    }

    public int RowCount => 6;

    public string GetLabel(int row) => row switch
    {
        0 => Strings.SettingsTheme,
        1 => Strings.SettingsRefreshRate,
        2 => Strings.SettingsDefaultSort,
        3 => Strings.SettingsDefaultGroup,
        4 => Strings.SettingsGraphStyle,
        5 => Strings.SettingsLanguage,
        _ => ""
    };

    public string GetDisplayValue(int row) => row switch
    {
        0 => GetThemeDisplay(_settingsService.Settings.Theme),
        1 => GetRefreshDisplay(_settingsService.Settings.RefreshIntervalMs),
        2 => GetSortDisplay(_settingsService.Settings.DefaultSort),
        3 => GetGroupDisplay(_settingsService.Settings.DefaultGroup),
        4 => GetGraphStyleDisplay(_settingsService.Settings.GraphStyle),
        5 => GetLanguageDisplay(_settingsService.Settings.Language),
        _ => ""
    };

    public int GetCurrentIndex(int row) => row switch
    {
        0 => Array.IndexOf(OptionKeys[0], _settingsService.Settings.Theme),
        1 => Array.IndexOf(OptionKeys[1], _settingsService.Settings.RefreshIntervalMs.ToString()),
        2 => Array.IndexOf(OptionKeys[2], _settingsService.Settings.DefaultSort),
        3 => Array.IndexOf(OptionKeys[3], _settingsService.Settings.DefaultGroup),
        4 => Array.IndexOf(OptionKeys[4], _settingsService.Settings.GraphStyle),
        5 => Array.IndexOf(OptionKeys[5], _settingsService.Settings.Language),
        _ => 0
    };

    private void SetValue(int row, int optionIndex)
    {
        var key = OptionKeys[row][optionIndex];
        var settings = _settingsService.Settings;
        switch (row)
        {
            case 0: settings.Theme = key; break;
            case 1: settings.RefreshIntervalMs = int.Parse(key); break;
            case 2: settings.DefaultSort = key; break;
            case 3: settings.DefaultGroup = key; break;
            case 4: settings.GraphStyle = key; break;
            case 5: settings.Language = key; break;
        }
        _settingsService.Save();
        _settingsService.ApplyAll();
        RequestRedraw();
        StatusMessage.Value = $" ✓ {Strings.SettingsSaved}";
        _settingsChanged.OnNext(Unit.Default);
    }

    public override void OnActivated()
    {
        UpdateStatusMessage();
        Input.OfType<IInputEvent, KeyPressed>().Subscribe(HandleKey).DisposeWith(Subscriptions);
    }

    private void UpdateStatusMessage()
    {
        var versionInfo = string.Format(Strings.CurrentVersion, _updateService.CurrentVersion);
        if (_updateService.UpdateAvailable)
        {
            var updateInfo = string.Format(Strings.UpdateAvailable, _updateService.LatestVersion);
            StatusMessage.Value = $" {versionInfo} | {updateInfo} | {Strings.UpdatePressU}";
        }
        else
        {
            StatusMessage.Value = $" {versionInfo} | {_settingsService.FilePath}";
        }
    }

    private void HandleKey(KeyPressed key)
    {
        switch (key.KeyInfo.Key)
        {
            case ConsoleKey.UpArrow:
                if (SelectedRow.Value > 0)
                {
                    SelectedRow.Value--;
                    _settingsChanged.OnNext(Unit.Default);
                }
                break;
            case ConsoleKey.DownArrow:
                if (SelectedRow.Value < RowCount - 1)
                {
                    SelectedRow.Value++;
                    _settingsChanged.OnNext(Unit.Default);
                }
                break;
            case ConsoleKey.Home:
                if (SelectedRow.Value != 0)
                {
                    SelectedRow.Value = 0;
                    _settingsChanged.OnNext(Unit.Default);
                }
                break;
            case ConsoleKey.End:
                if (SelectedRow.Value != RowCount - 1)
                {
                    SelectedRow.Value = RowCount - 1;
                    _settingsChanged.OnNext(Unit.Default);
                }
                break;
            case ConsoleKey.LeftArrow:
                CycleOption(-1);
                break;
            case ConsoleKey.RightArrow:
                CycleOption(1);
                break;
            case ConsoleKey.S:
                _settingsService.Save();
                _settingsService.ApplyAll();
                RequestRedraw();
                StatusMessage.Value = $" ✓ {Strings.SettingsSaved}";
                _settingsChanged.OnNext(Unit.Default);
                break;
            case ConsoleKey.D1: Navigate("/"); break;
            case ConsoleKey.D2: Navigate("/performance"); break;
            case ConsoleKey.D3: Navigate("/services"); break;
            case ConsoleKey.D4: Navigate("/network"); break;
            case ConsoleKey.U:
                if (_updateService.UpdateAvailable)
                {
                    _ = PerformUpdateAsync();
                }
                break;
            case ConsoleKey.Q: Shutdown(); break;
        }
    }

    private async Task PerformUpdateAsync()
    {
        StatusMessage.Value = $" {Strings.UpdateDownloading}";
        var success = await _updateService.PerformUpdateAsync(progress =>
        {
            StatusMessage.Value = progress switch
            {
                "Downloading..." => $" {Strings.UpdateDownloading}",
                "Extracting..." => $" {Strings.UpdateInstalling}",
                _ => $" {progress}"
            };
        });

        if (success)
        {
            StatusMessage.Value = $" {Strings.UpdateComplete}";
            await Task.Delay(1500);
            Shutdown();
        }
        else
        {
            StatusMessage.Value = $" {Strings.UpdateFailed}";
        }
    }

    private void CycleOption(int direction)
    {
        var row = SelectedRow.Value;
        var options = OptionKeys[row];
        var currentIdx = Math.Max(0, GetCurrentIndex(row));
        var newIdx = (currentIdx + direction + options.Length) % options.Length;
        SetValue(row, newIdx);
    }

    private static string GetThemeDisplay(string key) => key switch
    {
        "dark" => Strings.ThemeDark,
        "light" => Strings.ThemeLight,
        "nord" => Strings.ThemeNord,
        _ => key
    };

    private static string GetRefreshDisplay(int ms) => ms switch
    {
        250 => "250ms",
        500 => "500ms",
        1000 => "1s",
        2000 => "2s",
        5000 => "5s",
        _ => $"{ms}ms"
    };

    private static string GetSortDisplay(string key) => key switch
    {
        "cpu" => Strings.SortCpu,
        "ram" => Strings.SortRam,
        "name" => Strings.SortName,
        "pid" => Strings.SortPid,
        _ => key
    };

    private static string GetGroupDisplay(string key) => key switch
    {
        "all" => Strings.GroupAll,
        "apps" => Strings.GroupApps,
        "background" => Strings.GroupBackground,
        "system" => Strings.GroupSystem,
        _ => key
    };

    private static string GetGraphStyleDisplay(string key) => key switch
    {
        "blocks" => Strings.GraphBlocks,
        "braille" => Strings.GraphBraille,
        "outline" => Strings.GraphOutline,
        "ascii" => Strings.GraphAscii,
        _ => key
    };

    private static string GetLanguageDisplay(string key) => key switch
    {
        "system" => Strings.LangSystem,
        "de" => Strings.LangDe,
        "en" => Strings.LangEn,
        _ => key
    };

    public override void Dispose()
    {
        SelectedRow.Dispose();
        StatusMessage.Dispose();
        _settingsChanged.Dispose();
        base.Dispose();
    }
}
