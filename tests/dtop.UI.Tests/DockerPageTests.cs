using dtop.UI.Tests.Fixtures;
using dtop.UI.Tests.Helpers;

namespace dtop.UI.Tests;

public class DockerPageTests : IAsyncLifetime
{
    private DtopAppFixture _app = null!;

    public async ValueTask InitializeAsync()
    {
        _app = new DtopAppFixture();
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
