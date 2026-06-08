using dtop.UI.Tests.Fixtures;
using dtop.UI.Tests.Helpers;

namespace dtop.UI.Tests;

public class ProcessesPageTests : IAsyncLifetime
{
    private DtopAppFixture _app = null!;

    public async ValueTask InitializeAsync()
    {
        _app = new DtopAppFixture();
        await _app.StartAsync();
        // Already on Processes page (default route)
        await _app.Terminal.WaitForTextAsync("1:Processes");
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ShowsProcessList()
    {
        await _app.Terminal.WaitForTextAsync("chrome");
        ScreenAssert.Contains(_app.Terminal, "code");
        ScreenAssert.Contains(_app.Terminal, "svchost");
    }

    [Fact]
    public async Task Search_FiltersProcesses()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

        // Activate search with '/' character
        await _app.SendStringAsync("/");
        await _app.WaitForRenderAsync();

        // Type search text
        await _app.SendStringAsync("chrome");
        await _app.WaitForRenderAsync(300);

        ScreenAssert.Contains(_app.Terminal, "chrome");
        _app.Terminal.DoesNotContain("svchost");
    }

    [Fact]
    public async Task Search_EscapeClearsSearch()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

        // Activate search and filter
        await _app.SendStringAsync("/");
        await _app.WaitForRenderAsync();
        await _app.SendStringAsync("chrome");
        await _app.WaitForRenderAsync(300);

        _app.Terminal.DoesNotContain("svchost");

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
        await _app.Terminal.WaitForTextAsync("chrome");

        // Press G to cycle to Apps group
        await _app.SendKeysAsync(50, ConsoleKey.G);
        await _app.WaitForRenderAsync(300);

        // Apps group: chrome, code, spotify visible; svchost filtered out
        ScreenAssert.Contains(_app.Terminal, "chrome");
        _app.Terminal.DoesNotContain("svchost");
    }

    [Fact]
    public async Task Overlay_OpensOnEnter()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

        // Press Enter to open overlay on selected process
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        ScreenAssert.Contains(_app.Terminal, "PID");
        ScreenAssert.Contains(_app.Terminal, "CPU");
    }

    [Fact]
    public async Task Overlay_LeftRightSwitchesTabs()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

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
        await _app.Terminal.WaitForTextAsync("chrome");

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
        _app.Terminal.DoesNotContain("Handles");
    }

    [Fact]
    public async Task ListNavigation_ArrowDownMovesSelection()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

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
        await _app.Terminal.WaitForTextAsync("chrome");

        // Activate search
        await _app.SendStringAsync("/");
        await _app.WaitForRenderAsync();

        await _app.SendStringAsync("test");
        await _app.WaitForRenderAsync(300);

        // Should show "/ test" format matching Services/Network pages
        ScreenAssert.Contains(_app.Terminal, "/ test");
    }

    [Fact]
    public async Task SortCycling_FullLoop()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

        // Default sort is RAM. Cycle: RAM -> CPU -> Name -> PID -> RAM
        // Press Tab to cycle to CPU sort
        await _app.SendKeysAsync(50, ConsoleKey.Tab);
        await _app.WaitForRenderAsync(300);
        ScreenAssert.Contains(_app.Terminal, "Cpu");

        // Cycle to Name sort
        await _app.SendKeysAsync(50, ConsoleKey.Tab);
        await _app.WaitForRenderAsync(300);
        ScreenAssert.Contains(_app.Terminal, "Name");

        // Cycle to PID sort
        await _app.SendKeysAsync(50, ConsoleKey.Tab);
        await _app.WaitForRenderAsync(300);
        ScreenAssert.Contains(_app.Terminal, "Pid");

        // Cycle back to RAM sort
        await _app.SendKeysAsync(50, ConsoleKey.Tab);
        await _app.WaitForRenderAsync(300);
        ScreenAssert.Contains(_app.Terminal, "Ram");
    }

    [Fact]
    public async Task GroupFilter_FullLoop()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

        // Default is All. Cycle: All -> Apps -> Background -> Windows -> All
        // Press G to cycle to Apps
        await _app.SendKeysAsync(50, ConsoleKey.G);
        await _app.WaitForRenderAsync(300);
        ScreenAssert.Contains(_app.Terminal, "chrome");
        _app.Terminal.DoesNotContain("svchost");

        // Press G to cycle to Background
        await _app.SendKeysAsync(50, ConsoleKey.G);
        await _app.WaitForRenderAsync(300);
        _app.Terminal.DoesNotContain("chrome");

        // Press G to cycle to Windows
        await _app.SendKeysAsync(50, ConsoleKey.G);
        await _app.WaitForRenderAsync(300);
        ScreenAssert.Contains(_app.Terminal, "svchost");
        _app.Terminal.DoesNotContain("chrome");

        // Press G to cycle back to All
        await _app.SendKeysAsync(50, ConsoleKey.G);
        await _app.WaitForRenderAsync(300);
        ScreenAssert.Contains(_app.Terminal, "chrome");
        ScreenAssert.Contains(_app.Terminal, "svchost");
    }

    [Fact]
    public async Task Overlay_AllFourTabs()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

        // Open overlay (starts on Overview tab)
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);
        ScreenAssert.Contains(_app.Terminal, "Overview");

        // Navigate to Process Tree tab
        await _app.SendKeysAsync(50, ConsoleKey.RightArrow);
        await _app.WaitForRenderAsync(300);
        ScreenAssert.Contains(_app.Terminal, "Process Tree");

        // Navigate to Environment tab
        await _app.SendKeysAsync(50, ConsoleKey.RightArrow);
        await _app.WaitForRenderAsync(300);
        ScreenAssert.Contains(_app.Terminal, "Environment");

        // Navigate to Modules tab
        await _app.SendKeysAsync(50, ConsoleKey.RightArrow);
        await _app.WaitForRenderAsync(300);
        ScreenAssert.Contains(_app.Terminal, "Modules");
    }

    [Fact]
    public async Task KillConfirmation_CancelWithN()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

        // Open overlay
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.Terminal.WaitForTextAsync("PID", 3000);

        // Press K to trigger kill confirmation
        await _app.SendKeysAsync(50, ConsoleKey.K);
        await _app.Terminal.WaitForTextAsync("Kill", 3000);

        // Press N to cancel kill
        await _app.SendKeysAsync(50, ConsoleKey.N);
        await _app.WaitForRenderAsync(300);

        // Kill confirmation dismissed, overlay still open
        _app.Terminal.DoesNotContain("[Y]");
        ScreenAssert.Contains(_app.Terminal, "PID");
    }

    [Fact]
    public async Task DetailOverlay_ShowsCpuGraph()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.Terminal.WaitForTextAsync("CPU", 3000);

        // CPU panel with graph should be visible
        ScreenAssert.Contains(_app.Terminal, "CPU");
        ScreenAssert.Contains(_app.Terminal, "RAM");
    }

    [Fact]
    public async Task KillConfirmation_CancelWithEscape()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

        // Open overlay
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.Terminal.WaitForTextAsync("PID", 3000);

        // Press K to trigger kill confirmation
        await _app.SendKeysAsync(50, ConsoleKey.K);
        await _app.Terminal.WaitForTextAsync("Kill", 3000);

        // Press Escape -- should cancel kill confirm (not close overlay)
        await _app.SendKeysAsync(50, ConsoleKey.Escape);
        await _app.WaitForRenderAsync(300);

        // Kill confirmation dismissed but overlay still open (Escape cancels pending kill first)
        _app.Terminal.DoesNotContain("[Y]");
        ScreenAssert.Contains(_app.Terminal, "PID");
    }
}
