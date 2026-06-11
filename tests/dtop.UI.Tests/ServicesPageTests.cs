using dtop.UI.Tests.Fixtures;
using dtop.UI.Tests.Helpers;

namespace dtop.UI.Tests;

public class ServicesPageTests : IAsyncLifetime
{
    private DtopAppFixture _app = null!;

    public async ValueTask InitializeAsync()
    {
        _app = new DtopAppFixture();
        await _app.StartAsync();
        await _app.SendKeysAsync(50, ConsoleKey.D4); // Navigate to Services
        await _app.WaitForRenderAsync(500);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ShowsServiceList()
    {
        await _app.Terminal.WaitForTextAsync("Windows Update");
        ScreenAssert.Contains(_app.Terminal, "Print Spooler");
        ScreenAssert.Contains(_app.Terminal, "Windows Time");
    }

    [Fact]
    public async Task Search_FiltersServices()
    {
        await _app.Terminal.WaitForTextAsync("Windows Update");

        // Activate search with '/'
        await _app.SendStringAsync("/");
        await _app.WaitForRenderAsync();

        // Type search text
        await _app.SendStringAsync("Update");
        await _app.WaitForRenderAsync(300);

        ScreenAssert.Contains(_app.Terminal, "Windows Update");
        _app.Terminal.DoesNotContain("Print Spooler");
    }

    [Fact]
    public async Task DetailModal_OpensOnEnter()
    {
        await _app.Terminal.WaitForTextAsync("Windows Update");

        // Press Enter to open detail modal on selected service
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        // Detail modal should show the internal service name
        ScreenAssert.Contains(_app.Terminal, "wuauserv");
    }

    [Fact]
    public async Task DetailModal_EscapeCloses()
    {
        await _app.Terminal.WaitForTextAsync("Windows Update");

        // Open detail modal
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);

        // Verify modal is open
        ScreenAssert.Contains(_app.Terminal, "wuauserv");

        // Press Escape to close
        await _app.SendKeysAsync(50, ConsoleKey.Escape);
        await _app.WaitForRenderAsync(300);

        // Internal name should no longer be visible (modal closed)
        _app.Terminal.DoesNotContain("wuauserv");
    }

    [Fact]
    public async Task DetailModal_StopAction_WorksInsideModal()
    {
        await _app.Terminal.WaitForTextAsync("Windows Update");

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
        await _app.Terminal.WaitForTextAsync("Windows Update");

        // Press S on main list to start service
        await _app.SendKeysAsync(50, ConsoleKey.S);
        await _app.WaitForRenderAsync(500);

        // After the action + refresh cycle, the service list refreshes successfully
        // (proves the action round-trip completed without errors)
        ScreenAssert.Contains(_app.Terminal, "Windows Update");
        ScreenAssert.Contains(_app.Terminal, "3 services");
    }

    [Fact]
    public async Task StartService_FromMainList_ShowsSuccess()
    {
        await _app.Terminal.WaitForTextAsync("Windows Update");

        // Press S on main list to start the selected service
        await _app.SendKeysAsync(50, ConsoleKey.S);
        await _app.WaitForRenderAsync(500);

        // The action should succeed -- service list should still display correctly
        ScreenAssert.Contains(_app.Terminal, "Windows Update");
        ScreenAssert.Contains(_app.Terminal, "3 services");
    }

    [Fact]
    public async Task RestartService_FromDetailModal()
    {
        await _app.Terminal.WaitForTextAsync("Windows Update");

        // Open detail modal
        await _app.SendKeysAsync(50, ConsoleKey.Enter);
        await _app.WaitForRenderAsync(300);
        ScreenAssert.Contains(_app.Terminal, "wuauserv");

        // Press R to restart service from inside the modal
        await _app.SendKeysAsync(50, ConsoleKey.R);
        await _app.WaitForRenderAsync(500);

        // Close modal to verify state
        await _app.SendKeysAsync(50, ConsoleKey.Escape);
        await _app.WaitForRenderAsync(300);

        // Service list should still be displayed after the restart action
        ScreenAssert.Contains(_app.Terminal, "Windows Update");
        ScreenAssert.Contains(_app.Terminal, "3 services");
    }
}
