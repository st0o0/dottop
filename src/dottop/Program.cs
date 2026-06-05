using Akka.Hosting;
using dottop.Actors;
using dottop.Pages;
using Microsoft.Extensions.Hosting;
using Termina.Hosting;
using Termina.Pages;

Console.InputEncoding = System.Text.Encoding.UTF8;
Console.OutputEncoding = System.Text.Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAkka("dottop", configurationBuilder =>
{
    configurationBuilder.WithActors((system, registry) =>
    {
        var cpu = system.ActorOf(CpuMonitorActor.Props(), "cpu-monitor");
        registry.Register<CpuMonitorActor>(cpu);

        var memory = system.ActorOf(MemoryMonitorActor.Props(), "memory-monitor");
        registry.Register<MemoryMonitorActor>(memory);

        var disk = system.ActorOf(DiskMonitorActor.Props(), "disk-monitor");
        registry.Register<DiskMonitorActor>(disk);

        var network = system.ActorOf(NetworkMonitorActor.Props(), "network-monitor");
        registry.Register<NetworkMonitorActor>(network);

        var process = system.ActorOf(ProcessMonitorActor.Props(), "process-monitor");
        registry.Register<ProcessMonitorActor>(process);

        var processAction = system.ActorOf(ProcessActionActor.Props(), "process-action");
        registry.Register<ProcessActionActor>(processAction);

        var serviceActor = system.ActorOf(WindowsServiceActor.Props(), "windows-service");
        registry.Register<WindowsServiceActor>(serviceActor);

        var startupActor = system.ActorOf(StartupActor.Props(), "startup");
        registry.Register<StartupActor>(startupActor);
    });
});

builder.Services.AddTermina("/", termina =>
{
    termina.RegisterRoute<ProcessesPage, ProcessesViewModel>("/", NavigationBehavior.PreserveState);
    termina.RegisterRoute<PerformancePage, PerformanceViewModel>("/performance", NavigationBehavior.PreserveState);
    termina.RegisterRoute<ServicesPage, ServicesViewModel>("/services", NavigationBehavior.PreserveState);
    termina.RegisterRoute<NetworkPage, NetworkViewModel>("/network", NavigationBehavior.PreserveState);
    termina.RegisterRoute<AutostartPage, AutostartViewModel>("/autostart", NavigationBehavior.PreserveState);
});

await builder.Build().RunAsync();
