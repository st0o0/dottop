using dtop.App.Services;
using dtop.Plugin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Servus.Application.Startup;

namespace dtop.App.Setup;

public sealed class ServicesSetup : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        var settingsService = new SettingsService();
        settingsService.Load();
        settingsService.ApplyAll();
        services.AddSingleton(settingsService);

        services.AddSingleton(new PinService());

        var updateService = new UpdateService();
        services.AddSingleton(updateService);
        _ = updateService.CheckForUpdatesAsync();

        var refreshInterval = TimeSpan.FromMilliseconds(settingsService.Settings.RefreshIntervalMs);
        var tickSource = new AppTickSource(refreshInterval);
        services.AddSingleton<ITickSource>(tickSource);
        services.AddSingleton(tickSource);
    }
}
