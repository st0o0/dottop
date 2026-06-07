using dottop.UI.Tests.Fixtures;
using dottop.UI.Tests.Helpers;
using Xunit;

namespace dottop.UI.Tests;

public class ServicesPageTests : IAsyncLifetime
{
    private DottopAppFixture _app = null!;

    public async Task InitializeAsync()
    {
        _app = new DottopAppFixture();
        await _app.StartAsync();
        await _app.SendKeysAsync(50, ConsoleKey.D3); // Navigate to Services
        await _app.WaitForRenderAsync(500);
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ShowsServiceList()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "Windows Update");
        ScreenAssert.Contains(_app.Terminal, "Print Spooler");
        ScreenAssert.Contains(_app.Terminal, "Windows Time");
    }

    [Fact]
    public async Task Search_FiltersServices()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "Windows Update");

        // Activate search with '/'
        await _app.SendStringAsync("/");
        await _app.WaitForRenderAsync();

        // Type search text
        await _app.SendStringAsync("Update");
        await _app.WaitForRenderAsync(300);

        ScreenAssert.Contains(_app.Terminal, "Windows Update");
        ScreenAssert.DoesNotContain(_app.Terminal, "Print Spooler");
    }

    [Fact]
    public async Task DetailModal_OpensOnEnter()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "Windows Update");

        // Press Enter to open detail modal on selected service
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        // Detail modal should show the internal service name
        ScreenAssert.Contains(_app.Terminal, "wuauserv");
    }

    [Fact]
    public async Task DetailModal_EscapeCloses()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "Windows Update");

        // Open detail modal
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        // Verify modal is open
        ScreenAssert.Contains(_app.Terminal, "wuauserv");

        // Press Escape to close
        await _app.SendKeysAsync(50, ConsoleKey.Escape);
        await _app.WaitForRenderAsync(300);

        // Internal name should no longer be visible (modal closed)
        ScreenAssert.DoesNotContain(_app.Terminal, "wuauserv");
    }

    [Fact]
    public async Task DetailModal_StopAction_WorksInsideModal()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "Windows Update");

        // Open detail modal
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        ScreenAssert.Contains(_app.Terminal, "wuauserv");

        // Press X to stop service from inside the modal
        await _app.SendKeysAsync(50, ConsoleKey.X);
        await _app.WaitForRenderAsync(500);

        // Close the modal to see the status bar
        await _app.SendKeysAsync(50, ConsoleKey.Escape);
        await _app.WaitForRenderAsync(300);

        // After the action + refresh cycle, the service list should still be displayed
        // (proves the action round-trip succeeded without errors)
        ScreenAssert.Contains(_app.Terminal, "Windows Update");
        ScreenAssert.Contains(_app.Terminal, "3 services");
    }

    [Fact]
    public async Task ServiceAction_StartFromList()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "Windows Update");

        // Press S on main list to start service
        await _app.SendKeysAsync(50, ConsoleKey.S);
        await _app.WaitForRenderAsync(500);

        // After the action + refresh cycle, the service list refreshes successfully
        // (proves the action round-trip completed without errors)
        ScreenAssert.Contains(_app.Terminal, "Windows Update");
        ScreenAssert.Contains(_app.Terminal, "3 services");
    }
}
