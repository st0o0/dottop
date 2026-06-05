using System.ServiceProcess;
using Akka.Actor;
using dottop.Models;

namespace dottop.Actors;

public sealed class WindowsServiceActor : ReceiveActor
{
    public static Props Props() => Akka.Actor.Props.Create<WindowsServiceActor>();

    public WindowsServiceActor()
    {
        Receive<GetServices>(_ =>
        {
            try
            {
                var services = ServiceController.GetServices()
                    .Select(s => new WindowsServiceInfo(
                        s.ServiceName, s.DisplayName,
                        MapStatus(s.Status), ServiceStartType.Manual, null))
                    .OrderBy(s => s.DisplayName)
                    .ToList();
                Sender.Tell(services);
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        Receive<StartService>(msg =>
        {
            try
            {
                var sc = new ServiceController(msg.Name);
                sc.Start();
                Sender.Tell(new ActionSuccess($"Service {msg.Name} gestartet"));
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        Receive<StopService>(msg =>
        {
            try
            {
                var sc = new ServiceController(msg.Name);
                sc.Stop();
                Sender.Tell(new ActionSuccess($"Service {msg.Name} gestoppt"));
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });

        Receive<RestartService>(msg =>
        {
            try
            {
                var sc = new ServiceController(msg.Name);
                if (sc.Status == ServiceControllerStatus.Running) sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                sc.Start();
                Sender.Tell(new ActionSuccess($"Service {msg.Name} neugestartet"));
            }
            catch (Exception ex) { Sender.Tell(new ActionFailure(ex.Message)); }
        });
    }

    private static ServiceStatus MapStatus(ServiceControllerStatus status) => status switch
    {
        ServiceControllerStatus.Running => ServiceStatus.Running,
        ServiceControllerStatus.Stopped => ServiceStatus.Stopped,
        ServiceControllerStatus.StartPending => ServiceStatus.StartPending,
        ServiceControllerStatus.StopPending => ServiceStatus.StopPending,
        ServiceControllerStatus.Paused => ServiceStatus.Paused,
        _ => ServiceStatus.Stopped,
    };
}
