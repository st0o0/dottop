using dottop.UI.Tests.Fixtures;
using dottop.UI.Tests.Helpers;
using Xunit;

namespace dottop.UI.Tests;

public class NavigationTests : IAsyncLifetime
{
    private DottopAppFixture _app = null!;

    public async Task InitializeAsync()
    {
        _app = new DottopAppFixture();
        await _app.StartAsync();
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task App_StartsOnProcessesPage()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "1:Processes", 5000);
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "PID", 3000);
        ScreenAssert.Contains(_app.Terminal, "Name");
    }

    [Fact]
    public async Task D2_NavigatesToPerformance()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "1:Processes");
        await _app.SendKeysAsync(50, ConsoleKey.D2);
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "CPU");
        ScreenAssert.Contains(_app.Terminal, "RAM");
    }

    [Fact]
    public async Task D3_NavigatesToServices()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "1:Processes");
        await _app.SendKeysAsync(50, ConsoleKey.D3);
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "3:Services");
    }

    [Fact]
    public async Task D4_NavigatesToNetwork()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "1:Processes");
        await _app.SendKeysAsync(50, ConsoleKey.D4);
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "4:Network");
    }

    [Fact]
    public async Task D5_NavigatesToSettings()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "1:Processes");
        await _app.SendKeysAsync(50, ConsoleKey.D5);
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "Theme");
        ScreenAssert.Contains(_app.Terminal, "Refresh");
    }

    [Fact]
    public async Task TabNavigation_RoundTrip()
    {
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "1:Processes");
        await _app.SendKeysAsync(50, ConsoleKey.D3);
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "3:Services");
        await _app.SendKeysAsync(50, ConsoleKey.D1);
        await ScreenAssert.WaitForTextAsync(_app.Terminal, "1:Processes");
        ScreenAssert.Contains(_app.Terminal, "PID");
    }
}
