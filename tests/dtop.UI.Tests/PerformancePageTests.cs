using dtop.UI.Tests.Fixtures;
using dtop.UI.Tests.Helpers;

namespace dtop.UI.Tests;

public class PerformancePageTests : IAsyncLifetime
{
    private DtopAppFixture _app = null!;

    public async ValueTask InitializeAsync()
    {
        _app = new DtopAppFixture();
        await _app.StartAsync();
        await _app.SendKeysAsync(50, ConsoleKey.D3); // Navigate to Performance
        await _app.WaitForRenderAsync(500);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ShowsCpuAndRamPanels()
    {
        await _app.Terminal.WaitForTextAsync("CPU");
        await _app.Terminal.WaitForTextAsync("RAM");
    }

    [Fact]
    public async Task ShowsCpuData()
    {
        // CPU total should show percentage data from test snapshot (42.5%)
        await _app.Terminal.WaitForTextAsync("42.5%");
    }

    [Fact]
    public async Task StatusBar_ShowsKeyboardHints()
    {
        await _app.Terminal.WaitForTextAsync("Enter", 5000);
        await _app.Terminal.WaitForTextAsync("Detail", 3000);
    }

    [Fact]
    public async Task DetailModal_OpensOnEnter()
    {
        await _app.Terminal.WaitForTextAsync("CPU");

        // Press Enter to open detail modal
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        // Detail modal should show CPU name from test data
        ScreenAssert.Contains(_app.Terminal, "Test CPU");
    }

    [Fact]
    public async Task DetailModal_LeftRightCyclesSections()
    {
        await _app.Terminal.WaitForTextAsync("CPU");

        // Open detail modal (starts on CPU section)
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        // Press Right to cycle to RAM section
        await _app.SendKeysAsync(50, ConsoleKey.RightArrow);
        await _app.WaitForRenderAsync(300);

        // RAM detail should show GiB unit
        ScreenAssert.Contains(_app.Terminal, "GiB");
    }

    [Fact]
    public async Task DetailModal_EscapeCloses()
    {
        await _app.Terminal.WaitForTextAsync("CPU");

        // Open detail modal
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        // Verify modal is open (shows CPU name)
        ScreenAssert.Contains(_app.Terminal, "Test CPU");

        // Press Escape to close
        await _app.SendKeysAsync(50, ConsoleKey.Escape);
        await _app.WaitForRenderAsync(300);

        // Detail-specific content should no longer be visible
        _app.Terminal.DoesNotContain("Test CPU");
    }
}
