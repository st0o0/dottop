using dottop.UI.Tests.Fixtures;
using dottop.UI.Tests.Helpers;

namespace dottop.UI.Tests;

public class DockerPageTests : IAsyncLifetime
{
    private DottopAppFixture _app = null!;

    public async ValueTask InitializeAsync()
    {
        _app = new DottopAppFixture();
        await _app.StartAsync();
        await _app.SendKeysAsync(50, ConsoleKey.D5); // Navigate to Docker
        await _app.WaitForRenderAsync(500);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task ShowsDockerTab()
    {
        await _app.Terminal.WaitForTextAsync("Docker", 3000);
    }
}
