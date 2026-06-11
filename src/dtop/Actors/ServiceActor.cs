using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.Actors;

public sealed class ServiceActor : ReceiveActor
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Service");

    public static Props Props(IServiceManager serviceManager) =>
        Akka.Actor.Props.Create(() => new ServiceActor(serviceManager));

    public ServiceActor(IServiceManager serviceManager)
    {
        Receive<GetServices>(_ =>
        {
            try
            {
                var services = serviceManager.GetServices();
                Sender.Tell(services);
            }
            catch (Exception ex)
            {
                Sender.Tell(new ActionFailure(ex.Message));
            }
        });

        Receive<StartService>(msg =>
        {
            try
            {
                var result = serviceManager.Start(msg.Name);
                Sender.Tell(new ActionSuccess(result));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ActionFailure(ex.Message));
            }
        });

        Receive<StopService>(msg =>
        {
            try
            {
                var result = serviceManager.Stop(msg.Name);
                Sender.Tell(new ActionSuccess(result));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ActionFailure(ex.Message));
            }
        });

        Receive<RestartService>(msg =>
        {
            try
            {
                var result = serviceManager.Restart(msg.Name);
                Sender.Tell(new ActionSuccess(result));
            }
            catch (Exception ex)
            {
                Sender.Tell(new ActionFailure(ex.Message));
            }
        });
    }

    protected override void PostStop()
    {
        Trace.Debug(this, "Stopped");
        base.PostStop();
    }
}