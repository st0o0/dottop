using dottop.UI.Tests.Fixtures;
using dottop.UI.Tests.Helpers;
using Xunit;

namespace dottop.UI.Tests;

public class NetworkPageTests : IAsyncLifetime
{
    private DottopAppFixture _app = null!;

    public async Task InitializeAsync()
    {
        _app = new DottopAppFixture();
        await _app.StartAsync();
        await _app.SendKeysAsync(50, ConsoleKey.D4); // Navigate to Network
        await _app.WaitForRenderAsync(500);
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ShowsConnectionList()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "chrome");
        ScreenAssert.Contains(_app.Terminal, "Established");
        ScreenAssert.Contains(_app.Terminal, "TCP");
    }

    [Fact]
    public async Task Search_FiltersConnections()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "chrome");

        // Activate search with '/'
        await _app.SendStringAsync("/");
        await _app.WaitForRenderAsync();

        // Type search text
        await _app.SendStringAsync("chrome");
        await _app.WaitForRenderAsync(300);

        ScreenAssert.Contains(_app.Terminal, "chrome");
        ScreenAssert.DoesNotContain(_app.Terminal, "svchost");
    }

    [Fact]
    public async Task DetailModal_OpensOnEnter()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "chrome");

        // Press Enter to open detail modal on selected connection
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        // Detail modal should show full endpoints (not truncated)
        ScreenAssert.Contains(_app.Terminal, "192.168.1.10:54321");
        ScreenAssert.Contains(_app.Terminal, "142.250.80.46:443");
    }

    [Fact]
    public async Task DetailModal_ShowsFullInfo()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "chrome");

        // Press Enter to open detail modal
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        // Detail modal should show all connection info
        ScreenAssert.Contains(_app.Terminal, "chrome");
        ScreenAssert.Contains(_app.Terminal, "PID: 1001");
        ScreenAssert.Contains(_app.Terminal, "TCP");
        ScreenAssert.Contains(_app.Terminal, "Established");
        ScreenAssert.Contains(_app.Terminal, "192.168.1.10:54321");
        ScreenAssert.Contains(_app.Terminal, "142.250.80.46:443");
    }

    [Fact]
    public async Task DetailModal_EscapeCloses()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "chrome");

        // Open detail modal
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        // Verify modal is open
        ScreenAssert.Contains(_app.Terminal, "PID: 1001");

        // Press Escape to close
        await _app.SendKeysAsync(50, ConsoleKey.Escape);
        await _app.WaitForRenderAsync(300);

        // Detail-specific content should no longer be visible (modal closed)
        // The full endpoint format "PID: 1001" only appears in the modal
        ScreenAssert.DoesNotContain(_app.Terminal, "PID: 1001");
    }
}
