using Akka.Actor;
using dottop.Core.Platform;

namespace dottop.Actors;

public sealed class ProcessSupervisor : ReceiveActor
{
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
        Context.ActorOf(ProcessMonitorActor.Props(classifier, interval), "process-monitor");
        Context.ActorOf(ProcessActionActor.Props(treeProvider), "process-action");
        Context.ActorOf(ServiceActor.Props(serviceManager), "service");
    }

    protected override SupervisorStrategy SupervisorStrategy() =>
        new OneForOneStrategy(
            maxNrOfRetries: 5,
            withinTimeRange: TimeSpan.FromSeconds(10),
            localOnlyDecider: ex => ex switch
            {
                InvalidOperationException => Directive.Resume,
                System.ComponentModel.Win32Exception => Directive.Resume,
                _ => Directive.Restart
            });
}
