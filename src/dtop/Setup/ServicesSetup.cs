using dtop.Plugin;
using dtop.Services;
using dtop.Themes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Servus.Application.Startup;

namespace dtop.Setup;

public sealed class ServicesSetup : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        var themeService = new ThemeService();
        services.AddSingleton(themeService);

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
