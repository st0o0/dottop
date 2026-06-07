using Akka.Actor;
using Akka.Hosting;
using dottop.Actors;
using dottop.Core.Platform;
using dottop.Pages;
using dottop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Termina.Hosting;
using Termina.Pages;

Console.InputEncoding = System.Text.Encoding.UTF8;
Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

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

var updateService = new UpdateService();
builder.Services.AddSingleton(updateService);
_ = updateService.CheckForUpdatesAsync();

var refreshInterval = TimeSpan.FromMilliseconds(settingsService.Settings.RefreshIntervalMs);

// 4. Akka — supervisor creates children, we register them individually for ViewModel access
builder.Services.AddAkka("dottop", configurationBuilder =>
{
    configurationBuilder.WithActors((system, registry) =>
    {
        // Resolve platform services from the service provider embedded in the ActorSystem
#pragma warning disable CS0618 // Type or member is obsolete
        var sp = Akka.DependencyInjection.ServiceProvider.For(system).Provider;
#pragma warning restore CS0618

        // Initialize disk metrics in background
        var diskMetrics = sp.GetRequiredService<IDiskMetrics>();
        _ = Task.Run(() => { try { diskMetrics.Initialize(); } catch { } });

        var supervisor = system.ActorOf(
            MonitoringSupervisor.Props(
                sp.GetRequiredService<ICpuMetrics>(),
                sp.GetRequiredService<IMemoryMetrics>(),
                diskMetrics,
                sp.GetRequiredService<INetworkMetrics>(),
                sp.GetRequiredService<IGpuMetrics>(),
                sp.GetRequiredService<IProcessClassifier>(),
                sp.GetRequiredService<IProcessTreeProvider>(),
                sp.GetRequiredService<IServiceManager>(),
                refreshInterval),
            "monitoring-supervisor");
        registry.Register<MonitoringSupervisor>(supervisor);

        // Resolve and register individual child actors for ViewModel access
        var timeout = TimeSpan.FromSeconds(5);

        var cpuRef = system.ActorSelection("/user/monitoring-supervisor/cpu-monitor")
            .ResolveOne(timeout).GetAwaiter().GetResult();
        registry.Register<CpuMonitorActor>(cpuRef);

        var memRef = system.ActorSelection("/user/monitoring-supervisor/memory-monitor")
            .ResolveOne(timeout).GetAwaiter().GetResult();
        registry.Register<MemoryMonitorActor>(memRef);

        var diskRef = system.ActorSelection("/user/monitoring-supervisor/disk-monitor")
            .ResolveOne(timeout).GetAwaiter().GetResult();
        registry.Register<DiskMonitorActor>(diskRef);

        var netRef = system.ActorSelection("/user/monitoring-supervisor/network-monitor")
            .ResolveOne(timeout).GetAwaiter().GetResult();
        registry.Register<NetworkMonitorActor>(netRef);

        var gpuRef = system.ActorSelection("/user/monitoring-supervisor/gpu-monitor")
            .ResolveOne(timeout).GetAwaiter().GetResult();
        registry.Register<GpuMonitorActor>(gpuRef);

        var procRef = system.ActorSelection("/user/monitoring-supervisor/process-supervisor/process-monitor")
            .ResolveOne(timeout).GetAwaiter().GetResult();
        registry.Register<ProcessMonitorActor>(procRef);

        var procActionRef = system.ActorSelection("/user/monitoring-supervisor/process-supervisor/process-action")
            .ResolveOne(timeout).GetAwaiter().GetResult();
        registry.Register<ProcessActionActor>(procActionRef);

        var svcRef = system.ActorSelection("/user/monitoring-supervisor/process-supervisor/service")
            .ResolveOne(timeout).GetAwaiter().GetResult();
        registry.Register<ServiceActor>(svcRef);
    });
});

// 5. Termina Pages
builder.Services.AddTermina("/", termina =>
{
    termina.RegisterRoute<ProcessesPage, ProcessesViewModel>("/", NavigationBehavior.PreserveState);
    termina.RegisterRoute<PerformancePage, PerformanceViewModel>("/performance", NavigationBehavior.PreserveState);
    termina.RegisterRoute<ServicesPage, ServicesViewModel>("/services", NavigationBehavior.PreserveState);
    termina.RegisterRoute<NetworkPage, NetworkViewModel>("/network", NavigationBehavior.PreserveState);
    termina.RegisterRoute<SettingsPage, SettingsViewModel>("/settings", NavigationBehavior.PreserveState);
});

await builder.Build().RunAsync();