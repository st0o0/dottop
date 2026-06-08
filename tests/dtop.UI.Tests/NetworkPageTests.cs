using dtop.UI.Tests.Fixtures;
using dtop.UI.Tests.Helpers;

namespace dtop.UI.Tests;

public class NetworkPageTests : IAsyncLifetime
{
    private DtopAppFixture _app = null!;

    public async ValueTask InitializeAsync()
    {
        _app = new DtopAppFixture();
        await _app.StartAsync();
        await _app.SendKeysAsync(50, ConsoleKey.D4); // Navigate to Network
        await _app.WaitForRenderAsync(500);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ShowsConnectionList()
    {
        await _app.Terminal.WaitForTextAsync("chrome");
        ScreenAssert.Contains(_app.Terminal, "Established");
        ScreenAssert.Contains(_app.Terminal, "TCP");
    }

    [Fact]
    public async Task Search_FiltersConnections()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

        // Activate search with '/'
        await _app.SendStringAsync("/");
        await _app.WaitForRenderAsync();

        // Type search text
        await _app.SendStringAsync("chrome");
        await _app.WaitForRenderAsync(300);

        ScreenAssert.Contains(_app.Terminal, "chrome");
        _app.Terminal.DoesNotContain("svchost");
    }

    [Fact]
    public async Task DetailModal_OpensOnEnter()
    {
        await _app.Terminal.WaitForTextAsync("chrome");

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
        await _app.Terminal.WaitForTextAsync("chrome");

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
        await _app.Terminal.WaitForTextAsync("chrome");

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
        _app.Terminal.DoesNotContain("PID: 1001");
    }
}
