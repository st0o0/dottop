using Akka.Actor;
using Akka.Hosting;
using Akka.Logger.Serilog;
using dtop.App;
using dtop.App.Actors;
using dtop.App.Docker;
using dtop.App.Pages;
using dtop.App.Services;
using dtop.Core.Platform;
using dtop.Plugin;
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
    "dtop", "logs", "dtop-.log");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddSerilog();

// 1. Platform — conditional registration via extension methods in dtop.Windows / dtop.Linux
PlatformRegistration.Register(builder.Services);

// 2. GPU (with fallback)
IGpuMetrics gpuMetrics;
try
{
    if (OperatingSystem.IsWindows())
    {
        var nvml = new dtop.Windows.NvmlGpuMetrics();
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

// 4. Docker (conditional — only if Docker is available)
var dockerProvider = new DockerProvider();
var dockerAvailable = false;
try { dockerAvailable = dockerProvider.IsAvailable; } catch { }

if (dockerAvailable)
{
    builder.Services.AddSingleton<IDockerProvider>(dockerProvider);
}

// 5. Plugin discovery
var tickSource = new AppTickSource(refreshInterval);
builder.Services.AddSingleton<ITickSource>(tickSource);
var pluginRegistry = PluginLoader.DiscoverAndConfigure(builder.Services, tickSource);

// Register Docker as a built-in tab when available
if (dockerAvailable)
{
    pluginRegistry.AddBuiltInTab(new PluginTabInfo("5:Docker", "/docker", ConsoleKey.D5));
}

builder.Services.AddSingleton(pluginRegistry);
dtop.App.Nodes.TabBarNode.RegisterPluginTabs(pluginRegistry);

// 6. Senf.Tracing
builder.Services.AddServusLoggerTracing();

// 7. Build a temporary service provider to resolve platform services for actor construction
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

// 8. Akka — supervisor creates children, all ViewModel communication goes through it
builder.Services.AddAkka("dtop", configurationBuilder =>
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

        // Docker actor (conditional)
        if (dockerAvailable)
        {
            var dockerInterval = TimeSpan.FromSeconds(Math.Max(3, refreshInterval.TotalSeconds));
            var dockerActor = system.ActorOf(
                DockerMonitorActor.Props(dockerProvider, dockerInterval),
                "docker-monitor");
            registry.Register<DockerMonitorActor>(dockerActor);
        }

        // Plugin actors
        var actorCtx = new PluginActorContextImpl(system, registry, tempSp, tickSource);
        foreach (var plugin in pluginRegistry.LoadedPlugins)
            plugin.ActorSetup?.Invoke(actorCtx);
    });
});

// 9. Termina Pages
builder.Services.AddTermina("/", termina =>
{
    termina.RegisterRoute<ProcessesPage, ProcessesViewModel>("/", NavigationBehavior.PreserveState);
    termina.RegisterRoute<PerformancePage, PerformanceViewModel>("/performance", NavigationBehavior.PreserveState);
    termina.RegisterRoute<ServicesPage, ServicesViewModel>("/services", NavigationBehavior.PreserveState);
    termina.RegisterRoute<NetworkPage, NetworkViewModel>("/network", NavigationBehavior.PreserveState);

    // Docker route (conditional)
    if (dockerAvailable)
    {
        termina.RegisterRoute<DockerPage, DockerViewModel>("/docker", NavigationBehavior.PreserveState);
    }

    // Plugin routes
    var routeCtx = new PluginRouteContext(termina);
    foreach (var plugin in pluginRegistry.LoadedPlugins)
        plugin.RouteSetup?.Invoke(routeCtx);
});

await builder.Build().RunAsync();