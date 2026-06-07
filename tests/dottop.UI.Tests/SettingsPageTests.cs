using dottop.UI.Tests.Fixtures;
using dottop.UI.Tests.Helpers;
using Xunit;

namespace dottop.UI.Tests;

public class SettingsPageTests : IAsyncLifetime
{
    private DottopAppFixture _app = null!;

    public async Task InitializeAsync()
    {
        _app = new DottopAppFixture();
        await _app.StartAsync();
        await _app.SendKeysAsync(50, ConsoleKey.D5); // Navigate to Settings
        await _app.WaitForRenderAsync(500);
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ShowsAllSettings()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "Theme");
        ScreenAssert.Contains(_app.Terminal, "Refresh");
        ScreenAssert.Contains(_app.Terminal, "Sort");
        ScreenAssert.Contains(_app.Terminal, "Group");
        ScreenAssert.Contains(_app.Terminal, "Graph");
        ScreenAssert.Contains(_app.Terminal, "Language");
    }

    [Fact]
    public async Task ArrowDown_NavigatesRows()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "Theme");

        // Press Down to move to next row, then Right to change value
        // If Refresh Rate changes, we successfully navigated down
        await _app.SendKeysAsync(50, ConsoleKey.DownArrow);
        await _app.WaitForRenderAsync();

        await _app.SendKeysAsync(50, ConsoleKey.RightArrow);
        await _app.WaitForRenderAsync();

        // Refresh Rate default is 1000ms (1s), cycling right goes to 2000ms (2s)
        ScreenAssert.Contains(_app.Terminal, "2s");
    }

    [Fact]
    public async Task ArrowRight_ChangesValue()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "Theme");

        // Theme starts at "dark", pressing Right cycles to "light"
        await _app.SendKeysAsync(50, ConsoleKey.RightArrow);
        await _app.WaitForRenderAsync();

        ScreenAssert.Contains(_app.Terminal, "Light");
    }

    [Fact]
    public async Task Home_JumpsToFirstRow()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "Theme");

        // Navigate down 3 rows
        await _app.SendKeysAsync(50, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.DownArrow);
        await _app.WaitForRenderAsync();

        // Press Home to jump to first row
        await _app.SendKeysAsync(50, ConsoleKey.Home);
        await _app.WaitForRenderAsync();

        // Press Right to change the value of whatever row is selected
        // If Theme changes (dark -> light), Home jumped to first row
        await _app.SendKeysAsync(50, ConsoleKey.RightArrow);
        await _app.WaitForRenderAsync();

        ScreenAssert.Contains(_app.Terminal, "Light");
    }

    [Fact]
    public async Task End_JumpsToLastRow()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "Theme");

        // Press End to jump to last row (Language)
        await _app.SendKeysAsync(50, ConsoleKey.End);
        await _app.WaitForRenderAsync();

        // Press Right to change Language value (system -> de)
        await _app.SendKeysAsync(50, ConsoleKey.RightArrow);
        await _app.WaitForRenderAsync();

        // "Deutsch" is the display value for "de"
        ScreenAssert.Contains(_app.Terminal, "Deutsch");
    }

    [Fact]
    public async Task SaveIndicator_ShowsOnChange()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "Theme");

        // Change a value to trigger the save indicator
        await _app.SendKeysAsync(50, ConsoleKey.RightArrow);
        await _app.WaitForRenderAsync();

        // The SetValue method sets StatusMessage to " ✓ Settings saved"
        ScreenAssert.Contains(_app.Terminal, "✓");
    }
}
