using System.Reflection;
using Akka.Hosting;
using Akka.Logger.Serilog;
using dottop.App;
using dottop.App.Actors;
using dottop.App.Pages;
using dottop.App.Services;
using dottop.Core.Platform;
using dottop.Plugin.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Servus.Diagnostics;
using Termina.Hosting;
using Termina.Pages;

Console.InputEncoding = System.Text.Encoding.UTF8;
Console.OutputEncoding = System.Text.Encoding.UTF8;

var logPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "dottop", "logs", "dottop-.log");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddSerilog();

// 1. Platform — conditional registration via extension methods in dottop.Windows / dottop.Linux
PlatformRegistration.Register(builder.Services);

// 2. GPU (with fallback)
IGpuMetrics gpuMetrics;
try
{
    if (OperatingSystem.IsWindows())
    {
        var nvml = new dottop.Windows.NvmlGpuMetrics();
        gpuMetrics = nvml.IsAvailable ? nvml : NoGpuMetrics.Instance;
    }
    else
    {
        gpuMetrics = NoGpuMetrics.Instance;
    }
}
catch
{
    gpuMetrics = NoGpuMetrics.Instance;
}

builder.Services.AddSingleton(gpuMetrics);

// 3. Settings
var settingsService = new SettingsService();
settingsService.Load();
settingsService.ApplyAll();
builder.Services.AddSingleton(settingsService);
builder.Services.AddSingleton(new PinService());

var updateService = new UpdateService();
builder.Services.AddSingleton(updateService);
_ = updateService.CheckForUpdatesAsync();

var refreshInterval = TimeSpan.FromMilliseconds(settingsService.Settings.RefreshIntervalMs);

// 4. Plugin discovery
var plugins = PluginLoader.DiscoverPlugins().Where(p => p.IsAvailable).ToList();
foreach (var plugin in plugins)
    plugin.ConfigureServices(builder.Services);
var pluginRegistry = new PluginRegistry(plugins);
builder.Services.AddSingleton(pluginRegistry);
dottop.App.Nodes.TabBarNode.RegisterPluginTabs(pluginRegistry);

// 5. Senf.Tracing
builder.Services.AddServusLoggerTracing();

// 6. Build a temporary service provider to resolve platform services for actor construction
var tempSp = builder.Services.BuildServiceProvider();
var cpuMetrics = tempSp.GetRequiredService<ICpuMetrics>();
var memoryMetrics = tempSp.GetRequiredService<IMemoryMetrics>();
var diskMetrics = tempSp.GetRequiredService<IDiskMetrics>();
var networkMetrics = tempSp.GetRequiredService<INetworkMetrics>();
var processClassifier = tempSp.GetRequiredService<IProcessClassifier>();
var processTreeProvider = tempSp.GetRequiredService<IProcessTreeProvider>();
var serviceManager = tempSp.GetRequiredService<IServiceManager>();

// Initialize disk metrics in background
_ = Task.Run(() =>
{
    try
    {
        diskMetrics.Initialize();
    }
    catch
    {
    }
});

// 7. Akka — supervisor creates children, all ViewModel communication goes through it
builder.Services.AddAkka("dottop", configurationBuilder =>
{
    configurationBuilder.ConfigureLoggers(logging =>
    {
        logging.LogLevel = Akka.Event.LogLevel.WarningLevel;
        logging.AddSerilogLogging();
        logging.DeadLetterOptions = new DeadLetterOptions
        {
            ShouldLog = TriStateValue.None,
            LogDuringShutdown = false
        };
    });

    configurationBuilder.AddHocon("akka.stdout-loglevel = Off", HoconAddMode.Prepend);

    configurationBuilder.WithActors((system, registry) =>
    {
        var supervisor = system.ActorOf(
            MonitoringSupervisor.Props(
                cpuMetrics, memoryMetrics, diskMetrics, networkMetrics,
                gpuMetrics, processClassifier, processTreeProvider,
                serviceManager, refreshInterval),
            "monitoring-supervisor");
        registry.Register<MonitoringSupervisor>(supervisor);

        // Plugin actors
        foreach (var plugin in plugins)
            plugin.ConfigureActors(system, registry, tempSp, refreshInterval);
    });
});

// 8. Termina Pages
builder.Services.AddTermina("/", termina =>
{
    termina.RegisterRoute<ProcessesPage, ProcessesViewModel>("/", NavigationBehavior.PreserveState);
    termina.RegisterRoute<PerformancePage, PerformanceViewModel>("/performance", NavigationBehavior.PreserveState);
    termina.RegisterRoute<ServicesPage, ServicesViewModel>("/services", NavigationBehavior.PreserveState);
    termina.RegisterRoute<NetworkPage, NetworkViewModel>("/network", NavigationBehavior.PreserveState);

    // Plugin routes (registered via reflection to avoid compile-time dependency)
    foreach (var plugin in plugins)
    {
        if (plugin.TabInfo is { PageType: not null, ViewModelType: not null } tab)
        {
            var method = typeof(TerminaBuilder).GetMethods()
                .First(m => m.Name == "RegisterRoute" && m.GetParameters().Length == 2)
                .MakeGenericMethod(tab.PageType, tab.ViewModelType);
            method.Invoke(termina, [tab.Route, NavigationBehavior.PreserveState]);
        }
    }
});

await builder.Build().RunAsync();