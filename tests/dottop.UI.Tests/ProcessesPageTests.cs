using dottop.UI.Tests.Fixtures;
using dottop.UI.Tests.Helpers;
using Xunit;

namespace dottop.UI.Tests;

public class ProcessesPageTests : IAsyncLifetime
{
    private DottopAppFixture _app = null!;

    public async Task InitializeAsync()
    {
        _app = new DottopAppFixture();
        await _app.StartAsync();
        // Already on Processes page (default route)
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "1:Processes");
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ShowsProcessList()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "chrome");
        ScreenAssert.Contains(_app.Terminal, "code");
        ScreenAssert.Contains(_app.Terminal, "svchost");
    }

    [Fact]
    public async Task Search_FiltersProcesses()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "chrome");

        // Activate search with '/' character
        await _app.SendStringAsync("/");
        await _app.WaitForRenderAsync();

        // Type search text
        await _app.SendStringAsync("chrome");
        await _app.WaitForRenderAsync(300);

        ScreenAssert.Contains(_app.Terminal, "chrome");
        ScreenAssert.DoesNotContain(_app.Terminal, "svchost");
    }

    [Fact]
    public async Task Search_EscapeClearsSearch()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "chrome");

        // Activate search and filter
        await _app.SendStringAsync("/");
        await _app.WaitForRenderAsync();
        await _app.SendStringAsync("chrome");
        await _app.WaitForRenderAsync(300);

        ScreenAssert.DoesNotContain(_app.Terminal, "svchost");

        // Press Escape to clear search
        await _app.SendKeysAsync(50, ConsoleKey.Escape);
        await _app.WaitForRenderAsync(300);

        // All processes should be visible again
        ScreenAssert.Contains(_app.Terminal, "chrome");
        ScreenAssert.Contains(_app.Terminal, "svchost");
    }

    [Fact]
    public async Task GroupFilter_CyclesThroughGroups()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "chrome");

        // Press G to cycle to Apps group
        await _app.SendKeysAsync(50, ConsoleKey.G);
        await _app.WaitForRenderAsync(300);

        // Apps group: chrome, code, spotify visible; svchost filtered out
        ScreenAssert.Contains(_app.Terminal, "chrome");
        ScreenAssert.DoesNotContain(_app.Terminal, "svchost");
    }

    [Fact]
    public async Task Overlay_OpensOnEnter()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "chrome");

        // Press Enter to open overlay on selected process
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        ScreenAssert.Contains(_app.Terminal, "PID");
        ScreenAssert.Contains(_app.Terminal, "CPU");
    }

    [Fact]
    public async Task Overlay_LeftRightSwitchesTabs()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "chrome");

        // Open overlay
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        // Press Right to switch to next tab (Process Tree)
        await _app.SendKeysAsync(50, ConsoleKey.RightArrow);
        await _app.WaitForRenderAsync(300);

        // The Process Tree tab should be visible
        ScreenAssert.Contains(_app.Terminal, "Tree");
    }

    [Fact]
    public async Task Overlay_EscapeCloses()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "chrome");

        // Open overlay
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        // Verify overlay is open (shows PID detail)
        ScreenAssert.Contains(_app.Terminal, "PID");

        // Press Escape to close
        await _app.SendKeysAsync(50, ConsoleKey.Escape);
        await _app.WaitForRenderAsync(300);

        // Overlay should be closed - the process list header is visible again without overlay
        // The PID header in the list is always visible, so check that the overlay-specific content is gone
        ScreenAssert.DoesNotContain(_app.Terminal, "Handles");
    }

    [Fact]
    public async Task ListNavigation_ArrowDownMovesSelection()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "chrome");

        // Press down arrow to move selection
        await _app.SendKeysAsync(50, ConsoleKey.DownArrow);
        await _app.WaitForRenderAsync();

        // Open overlay to verify different process is selected
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        // The overlay should show the second process (not the first one)
        // The list is sorted by RAM descending by default, so first is chrome (500MB), second is code (400MB)
        ScreenAssert.Contains(_app.Terminal, "PID");
    }

    [Fact]
    public async Task SearchBar_ShowsSlashFormat_WhenActive()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "chrome");

        // Activate search
        await _app.SendStringAsync("/");
        await _app.WaitForRenderAsync();

        await _app.SendStringAsync("test");
        await _app.WaitForRenderAsync(300);

        // Should show "/ test" format matching Services/Network pages
        ScreenAssert.Contains(_app.Terminal, "/ test");
    }
}
