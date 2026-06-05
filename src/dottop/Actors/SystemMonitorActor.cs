using Akka.Actor;

namespace dottop.Actors;

public sealed class SystemMonitorActor : ReceiveActor
{
    public static Props Props() => Akka.Actor.Props.Create<SystemMonitorActor>();

    public SystemMonitorActor()
    {
        Context.ActorOf(CpuMonitorActor.Props(), "cpu");
        Context.ActorOf(MemoryMonitorActor.Props(), "memory");
        Context.ActorOf(DiskMonitorActor.Props(), "disk");
        Context.ActorOf(NetworkMonitorActor.Props(), "network");
        Context.ActorOf(ProcessMonitorActor.Props(), "process");
    }

    protected override SupervisorStrategy SupervisorStrategy()
    {
        return new OneForOneStrategy(
            maxNrOfRetries: 3,
            withinTimeRange: TimeSpan.FromMinutes(1),
            decider: Decider.From(ex => Directive.Restart));
    }
}
