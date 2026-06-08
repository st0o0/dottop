using Akka.Actor;
using dtop.Core.Messages;
using dtop.Core.Platform;
using Servus;
using Servus.Diagnostics;

namespace dtop.App.Actors;

public sealed class ProcessSupervisor : ReceiveActor
{
    private static readonly TraceChannel Trace = Senf.Tracing.For("Process.Supervisor");

    private readonly IActorRef _processMonitor;
    private readonly IActorRef _processAction;
    private readonly IActorRef _service;

    public static Props Props(
        IProcessClassifier classifier,
        IProcessTreeProvider treeProvider,
        IServiceManager serviceManager,
        TimeSpan interval) =>
        Akka.Actor.Props.Create(() => new ProcessSupervisor(
            classifier, treeProvider, serviceManager, interval));

    public ProcessSupervisor(
        IProcessClassifier classifier,
        IProcessTreeProvider treeProvider,
        IServiceManager serviceManager,
        TimeSpan interval)
    {
        _processMonitor = Context.ActorOf(ProcessMonitorActor.Props(classifier, interval), "process-monitor");
        _processAction = Context.ActorOf(ProcessActionActor.Props(treeProvider), "process-action");
        _service = Context.ActorOf(ServiceActor.Props(serviceManager), "service");

        // Process monitoring
        Receive<StartProcessMonitoring>(msg => _processMonitor.Forward(msg));

        // Process actions
        Receive<KillProcess>(msg => _processAction.Forward(msg));
        Receive<SetProcessPriority>(msg => _processAction.Forward(msg));
        Receive<SetProcessAffinity>(msg => _processAction.Forward(msg));
        Receive<GetProcessTree>(msg => _processAction.Forward(msg));
        Receive<GetProcessEnvironment>(msg => _processAction.Forward(msg));
        Receive<GetProcessHandles>(msg => _processAction.Forward(msg));

        // Service commands
        Receive<GetServices>(msg => _service.Forward(msg));
        Receive<StartService>(msg => _service.Forward(msg));
        Receive<StopService>(msg => _service.Forward(msg));
        Receive<RestartService>(msg => _service.Forward(msg));
    }

    protected override SupervisorStrategy SupervisorStrategy() =>
        new OneForOneStrategy(
            maxNrOfRetries: 5,
            withinTimeRange: TimeSpan.FromSeconds(10),
            localOnlyDecider: ex =>
            {
                var directive = ex switch
                {
                    InvalidOperationException => Directive.Resume,
                    System.ComponentModel.Win32Exception => Directive.Resume,
                    _ => Directive.Restart
                };
                Trace.Warning(this, "Supervision decision: {0} for {1}", directive, ex.GetType().Name);
                return directive;
            });

    protected override void PostStop()
    {
        Trace.Debug(this, "Stopped");
        base.PostStop();
    }
}