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
        var monitor = system.ActorOf(
            SystemMonitorActor.Props(TimeSpan.FromSeconds(1)), "system-monitor");
        registry.Register<SystemMonitorActor>(monitor);

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
