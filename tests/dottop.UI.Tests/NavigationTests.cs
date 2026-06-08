using dottop.UI.Tests.Fixtures;
using dottop.UI.Tests.Helpers;

namespace dottop.UI.Tests;

public class NavigationTests : IAsyncLifetime
{
    private DottopAppFixture _app = null!;

    public async ValueTask InitializeAsync()
    {
        _app = new DottopAppFixture();
        await _app.StartAsync();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task App_StartsOnProcessesPage()
    {
        await _app.Terminal.WaitForTextAsync("1:Processes");
        await _app.Terminal.WaitForTextAsync("PID", 3000);
        ScreenAssert.Contains(_app.Terminal, "Name");
    }

    [Fact]
    public async Task D2_NavigatesToPerformance()
    {
        await _app.Terminal.WaitForTextAsync("1:Processes");
        await _app.SendKeysAsync(50, ConsoleKey.D2);
        await _app.Terminal.WaitForTextAsync("CPU");
        ScreenAssert.Contains(_app.Terminal, "RAM");
    }

    [Fact]
    public async Task D3_NavigatesToServices()
    {
        await _app.Terminal.WaitForTextAsync("1:Processes");
        await _app.SendKeysAsync(50, ConsoleKey.D3);
        await _app.Terminal.WaitForTextAsync("3:Services");
    }

    [Fact]
    public async Task D4_NavigatesToNetwork()
    {
        await _app.Terminal.WaitForTextAsync("1:Processes");
        await _app.SendKeysAsync(50, ConsoleKey.D4);
        await _app.Terminal.WaitForTextAsync("4:Network");
    }

    [Fact]
    public async Task TabNavigation_RoundTrip()
    {
        await _app.Terminal.WaitForTextAsync("1:Processes");
        await _app.SendKeysAsync(50, ConsoleKey.D3);
        await _app.Terminal.WaitForTextAsync("3:Services");
        await _app.SendKeysAsync(50, ConsoleKey.D1);
        await _app.Terminal.WaitForTextAsync("1:Processes");
        ScreenAssert.Contains(_app.Terminal, "PID");
    }
}
