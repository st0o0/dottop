using System.Runtime.InteropServices;
using Akka.Hosting;
using dottop.Actors;
using dottop.Pages;
using dottop.Platform;
using dottop.Platform.Linux;
using dottop.Platform.Windows;
using dottop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Termina.Hosting;
using Termina.Pages;

Console.InputEncoding = System.Text.Encoding.UTF8;
Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

// Create platform-specific services
IDiskMetricsProvider diskMetrics = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? new WindowsDiskMetrics()
    : new LinuxDiskMetrics();
IProcessTreeProvider processTree = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? new WindowsProcessTree()
    : new LinuxProcessTree();
IServiceManager serviceManager = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? new WindowsServiceManager()
    : new LinuxServiceManager();
IProcessClassifier processClassifier = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? new WindowsProcessClassifier()
    : new LinuxProcessClassifier();
IConnectionProvider connectionProvider = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    ? new WindowsConnectionProvider()
    : new LinuxConnectionProvider();

IGpuMetricsProvider gpuMetrics;
try
{
    gpuMetrics = new NvmlGpuMetrics();
}
catch
{
    gpuMetrics = new NoGpuMetrics();
}

if (!gpuMetrics.IsAvailable)
{
    gpuMetrics = new NoGpuMetrics();
}

var settingsService = new SettingsService();
settingsService.Load();

builder.Services.AddSingleton(settingsService);
builder.Services.AddSingleton(connectionProvider);
builder.Services.AddSingleton(gpuMetrics);

builder.Services.AddAkka("dottop", configurationBuilder =>
{
    configurationBuilder.WithActors((system, registry) =>
    {
        var cpu = system.ActorOf(CpuMonitorActor.Props(), "cpu-monitor");
        registry.Register<CpuMonitorActor>(cpu);

        var memory = system.ActorOf(MemoryMonitorActor.Props(), "memory-monitor");
        registry.Register<MemoryMonitorActor>(memory);

        var disk = system.ActorOf(DiskMonitorActor.Props(diskMetrics), "disk-monitor");
        registry.Register<DiskMonitorActor>(disk);

        var network = system.ActorOf(NetworkMonitorActor.Props(), "network-monitor");
        registry.Register<NetworkMonitorActor>(network);

        var process = system.ActorOf(ProcessMonitorActor.Props(processClassifier), "process-monitor");
        registry.Register<ProcessMonitorActor>(process);

        var processAction = system.ActorOf(ProcessActionActor.Props(processTree), "process-action");
        registry.Register<ProcessActionActor>(processAction);

        var serviceActor = system.ActorOf(ServiceActor.Props(serviceManager), "service");
        registry.Register<ServiceActor>(serviceActor);

        var gpuActor = system.ActorOf(GpuMonitorActor.Props(gpuMetrics), "gpu-monitor");
        registry.Register<GpuMonitorActor>(gpuActor);
    });
});

builder.Services.AddTermina("/", termina =>
{
    termina.RegisterRoute<ProcessesPage, ProcessesViewModel>("/", NavigationBehavior.PreserveState);
    termina.RegisterRoute<PerformancePage, PerformanceViewModel>("/performance", NavigationBehavior.PreserveState);
    termina.RegisterRoute<ServicesPage, ServicesViewModel>("/services", NavigationBehavior.PreserveState);
    termina.RegisterRoute<NetworkPage, NetworkViewModel>("/network", NavigationBehavior.PreserveState);
    termina.RegisterRoute<SettingsPage, SettingsViewModel>("/settings", NavigationBehavior.PreserveState);
});

await builder.Build().RunAsync();