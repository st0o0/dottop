using dtop.UI.Tests.Fixtures;
using dtop.UI.Tests.Helpers;

namespace dtop.UI.Tests;

public class NavigationTests : IAsyncLifetime
{
    private DtopAppFixture _app = null!;

    public async ValueTask InitializeAsync()
    {
        _app = new DtopAppFixture();
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
    public async Task D3_NavigatesToPerformance()
    {
        await _app.Terminal.WaitForTextAsync("1:Processes");
        await _app.SendKeysAsync(50, ConsoleKey.D3);
        await _app.Terminal.WaitForTextAsync("CPU");
        ScreenAssert.Contains(_app.Terminal, "RAM");
    }

    [Fact]
    public async Task D4_NavigatesToServices()
    {
        await _app.Terminal.WaitForTextAsync("1:Processes");
        await _app.SendKeysAsync(50, ConsoleKey.D4);
        await _app.Terminal.WaitForTextAsync("4:Services");
    }

    [Fact]
    public async Task D5_NavigatesToNetwork()
    {
        await _app.Terminal.WaitForTextAsync("1:Processes");
        await _app.SendKeysAsync(50, ConsoleKey.D5);
        await _app.Terminal.WaitForTextAsync("5:Network");
    }

    [Fact]
    public async Task D6_NavigatesToDocker()
    {
        await _app.Terminal.WaitForTextAsync("1:Processes");
        await _app.SendKeysAsync(50, ConsoleKey.D6);
        await _app.Terminal.WaitForTextAsync("6:Docker", 3000);
    }

    [Fact]
    public async Task TabNavigation_RoundTrip()
    {
        await _app.Terminal.WaitForTextAsync("1:Processes");
        await _app.SendKeysAsync(50, ConsoleKey.D4);
        await _app.Terminal.WaitForTextAsync("4:Services");
        await _app.SendKeysAsync(50, ConsoleKey.D2);
        await _app.Terminal.WaitForTextAsync("1:Processes");
        ScreenAssert.Contains(_app.Terminal, "PID");
    }
}
