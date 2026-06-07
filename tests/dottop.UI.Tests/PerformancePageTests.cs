using dottop.UI.Tests.Fixtures;
using dottop.UI.Tests.Helpers;
using Xunit;

namespace dottop.UI.Tests;

public class PerformancePageTests : IAsyncLifetime
{
    private DottopAppFixture _app = null!;

    public async Task InitializeAsync()
    {
        _app = new DottopAppFixture();
        await _app.StartAsync();
        await _app.SendKeysAsync(50, ConsoleKey.D2); // Navigate to Performance
        await _app.WaitForRenderAsync(500);
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ShowsCpuAndRamPanels()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "CPU");
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "RAM");
    }

    [Fact]
    public async Task ShowsCpuData()
    {
        // CPU total should show percentage data from test snapshot (42.5%)
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "42.5%");
    }

    [Fact]
    public async Task StatusBar_ShowsKeyboardHints()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "Enter");
        ScreenAssert.Contains(_app.Terminal, "Detail");
    }

    [Fact]
    public async Task DetailModal_OpensOnEnter()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "CPU");

        // Press Enter to open detail modal
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        // Detail modal should show CPU name from test data
        ScreenAssert.Contains(_app.Terminal, "Test CPU");
    }

    [Fact]
    public async Task DetailModal_LeftRightCyclesSections()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "CPU");

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
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "CPU");

        // Open detail modal
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        // Verify modal is open (shows CPU name)
        ScreenAssert.Contains(_app.Terminal, "Test CPU");

        // Press Escape to close
        await _app.SendKeysAsync(50, ConsoleKey.Escape);
        await _app.WaitForRenderAsync(300);

        // Detail-specific content should no longer be visible
        ScreenAssert.DoesNotContain(_app.Terminal, "Test CPU");
    }
}
