using dottop.UI.Tests.Fixtures;
using dottop.UI.Tests.Helpers;

namespace dottop.UI.Tests;

public class EdgeCaseTests : IAsyncLifetime
{
    private DottopAppFixture _app = null!;

    public async ValueTask InitializeAsync()
    {
        _app = new DottopAppFixture();
        await _app.StartAsync();
        await _app.Terminal.WaitForTextAsync("1:Processes");
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task RapidTabSwitching_DoesNotCrash()
    {
        // Switch D1->D2->D3->D4->D5->D1 rapidly with minimal delays
        await _app.SendKeysAsync(20, ConsoleKey.D1, ConsoleKey.D2, ConsoleKey.D3, ConsoleKey.D4, ConsoleKey.D5, ConsoleKey.D1);
        await _app.WaitForRenderAsync(500);

        // App should land on Processes page and not crash
        await _app.Terminal.WaitForTextAsync("1:Processes");
        ScreenAssert.Contains(_app.Terminal, "PID");
    }

    [Fact]
    public async Task SearchThenEscapeThenNavigateAway_SearchCleared()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

        // Activate search on Processes
        await _app.SendStringAsync("/");
        await _app.WaitForRenderAsync();

        // Type search text
        await _app.SendStringAsync("chrome");
        await _app.WaitForRenderAsync(300);

        // Verify search is active and filtering
        ScreenAssert.Contains(_app.Terminal, "/ chrome");
        _app.Terminal.DoesNotContain("svchost");

        // Escape to exit search mode (search is modal -- must exit before navigating)
        await _app.SendKeysAsync(50, ConsoleKey.Escape);
        await _app.WaitForRenderAsync(300);

        // All processes should be visible after clearing search
        ScreenAssert.Contains(_app.Terminal, "chrome");
        ScreenAssert.Contains(_app.Terminal, "svchost");

        // Now navigate to Services
        await _app.SendKeysAsync(50, ConsoleKey.D3);
        await _app.Terminal.WaitForTextAsync("3:Services", 3000);

        // Come back to Processes
        await _app.SendKeysAsync(50, ConsoleKey.D1);
        await _app.Terminal.WaitForTextAsync("1:Processes", 3000);
        await _app.WaitForRenderAsync(500);

        // All processes should still be visible
        await _app.Terminal.WaitForTextAsync("chrome", 3000);
        ScreenAssert.Contains(_app.Terminal, "svchost");
    }

    [Fact]
    public async Task OpenOverlayThenEscapeThenNavigate_OverlayClosesPageSwitches()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

        // Open overlay on Processes
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        // Verify overlay is open
        ScreenAssert.Contains(_app.Terminal, "Overview");

        // Overlay is modal -- must close it first before navigating
        await _app.SendKeysAsync(50, ConsoleKey.Escape);
        await _app.WaitForRenderAsync(300);

        // Overlay should be closed
        _app.Terminal.DoesNotContain("Handles");

        // Now navigate to Services
        await _app.SendKeysAsync(50, ConsoleKey.D3);
        await _app.Terminal.WaitForTextAsync("3:Services");

        // Services page should be displayed
        ScreenAssert.Contains(_app.Terminal, "Windows Update");
    }

    [Fact]
    public async Task EmptySearchReturnsAllItems()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

        // Activate search
        await _app.SendStringAsync("/");
        await _app.WaitForRenderAsync();

        // Type nothing, press Escape
        await _app.SendKeysAsync(50, ConsoleKey.Escape);
        await _app.WaitForRenderAsync(300);

        // All items should still be visible
        ScreenAssert.Contains(_app.Terminal, "chrome");
        ScreenAssert.Contains(_app.Terminal, "code");
        ScreenAssert.Contains(_app.Terminal, "svchost");
        ScreenAssert.Contains(_app.Terminal, "explorer");
        ScreenAssert.Contains(_app.Terminal, "spotify");
    }

    [Fact]
    public async Task SearchWithNoResults_NoCrash()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

        // Activate search
        await _app.SendStringAsync("/");
        await _app.WaitForRenderAsync();

        // Search for something that doesn't exist
        await _app.SendStringAsync("zzzznonexistent");
        await _app.WaitForRenderAsync(300);

        // No processes should be visible, but app should not crash
        _app.Terminal.DoesNotContain("chrome");
        _app.Terminal.DoesNotContain("svchost");
        _app.Terminal.DoesNotContain("spotify");

        // Escape should recover
        await _app.SendKeysAsync(50, ConsoleKey.Escape);
        await _app.WaitForRenderAsync(300);

        // All processes should be visible again
        ScreenAssert.Contains(_app.Terminal, "chrome");
    }

    [Fact]
    public async Task MultipleOverlayOpenClose_NoCrash()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

        // Open and close overlay 5 times rapidly
        for (var i = 0; i < 5; i++)
        {
            await _app.SendKeysAsync(30, ConsoleKey.Enter);
            await _app.WaitForRenderAsync();
            await _app.SendKeysAsync(30, ConsoleKey.Escape);
            await _app.WaitForRenderAsync();
        }

        // App should still be functional -- process list visible
        ScreenAssert.Contains(_app.Terminal, "chrome");
        ScreenAssert.Contains(_app.Terminal, "svchost");
    }

    [Fact]
    public async Task NavigateToSameTab_IsNoOp()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

        // Press D1 while already on Processes -- should be a no-op
        await _app.SendKeysAsync(50, ConsoleKey.D1);
        await _app.WaitForRenderAsync(300);

        // Page should still show Processes without crash
        ScreenAssert.Contains(_app.Terminal, "1:Processes");
        ScreenAssert.Contains(_app.Terminal, "chrome");
    }

    [Fact]
    public async Task SettingsChangeAndNavigateBack_ValuePersists()
    {
        // Navigate to Settings
        await _app.SendKeysAsync(50, ConsoleKey.D5);
        await _app.Terminal.WaitForTextAsync("Theme", 3000);

        // Change theme from dark to light (press Right)
        await _app.SendKeysAsync(50, ConsoleKey.RightArrow);
        await _app.WaitForRenderAsync(300);
        ScreenAssert.Contains(_app.Terminal, "Light");

        // Navigate to Processes
        await _app.SendKeysAsync(50, ConsoleKey.D1);
        await _app.Terminal.WaitForTextAsync("1:Processes", 3000);

        // Navigate back to Settings
        await _app.SendKeysAsync(50, ConsoleKey.D5);
        await _app.Terminal.WaitForTextAsync("Theme", 3000);
        await _app.WaitForRenderAsync(300);

        // Changed value should persist
        ScreenAssert.Contains(_app.Terminal, "Light");
    }
}
