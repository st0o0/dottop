using dtop.Plugin;
using dtop.Services;
using dtop.Themes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using R3;
using Servus.Application.Startup;

namespace dtop.Setup;

public sealed class ServicesSetup : IServiceSetupContainer
{
    public void SetupServices(IServiceCollection services, IConfiguration configuration)
    {
        var themeService = new ThemeService();
        themeService.LoadFromDirectory(Path.Combine(AppContext.BaseDirectory, "themes"));
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
        var refreshService = new RefreshService(refreshInterval);
        services.AddSingleton<IRefreshService>(refreshService);
        services.AddSingleton<ITickSource>(refreshService);

        refreshService.Interval.Subscribe(iv =>
        {
            settingsService.Settings.RefreshIntervalMs = (int)iv.TotalMilliseconds;
            settingsService.Save();
        });

        var metricStore = new MetricStore();
        services.AddSingleton(metricStore);
        services.AddSingleton<IMetricSink>(metricStore);

        services.AddSingleton<IMonitorDemand, MonitorDemandService>();
    }
}
