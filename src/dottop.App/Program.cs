using Akka.Hosting;
using Akka.Logger.Serilog;
using dottop.App.Actors;
using dottop.App.Docker;
using dottop.App.Pages;
using dottop.App.Services;
using dottop.Core.Platform;
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
builder.Services.AddSingleton<IDockerProvider, DockerProvider>();

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

// 4. Senf.Tracing
builder.Services.AddServusLoggerTracing();

// 5. Build a temporary service provider to resolve platform services for actor construction
var tempSp = builder.Services.BuildServiceProvider();
var cpuMetrics = tempSp.GetRequiredService<ICpuMetrics>();
var memoryMetrics = tempSp.GetRequiredService<IMemoryMetrics>();
var diskMetrics = tempSp.GetRequiredService<IDiskMetrics>();
var networkMetrics = tempSp.GetRequiredService<INetworkMetrics>();
var processClassifier = tempSp.GetRequiredService<IProcessClassifier>();
var processTreeProvider = tempSp.GetRequiredService<IProcessTreeProvider>();
var serviceManager = tempSp.GetRequiredService<IServiceManager>();
var dockerProvider = tempSp.GetRequiredService<IDockerProvider>();

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

// 6. Akka — supervisor creates children, all ViewModel communication goes through it
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
                serviceManager, dockerProvider, refreshInterval),
            "monitoring-supervisor");
        registry.Register<MonitoringSupervisor>(supervisor);
    });
});

// 7. Termina Pages
builder.Services.AddTermina("/", termina =>
{
    termina.RegisterRoute<ProcessesPage, ProcessesViewModel>("/", NavigationBehavior.PreserveState);
    termina.RegisterRoute<PerformancePage, PerformanceViewModel>("/performance", NavigationBehavior.PreserveState);
    termina.RegisterRoute<ServicesPage, ServicesViewModel>("/services", NavigationBehavior.PreserveState);
    termina.RegisterRoute<NetworkPage, NetworkViewModel>("/network", NavigationBehavior.PreserveState);
    termina.RegisterRoute<DockerPage, DockerViewModel>("/docker", NavigationBehavior.PreserveState);
});

await builder.Build().RunAsync();