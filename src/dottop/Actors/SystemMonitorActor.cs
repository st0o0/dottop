using Akka.Actor;

namespace dottop.Actors;

public sealed class SystemMonitorActor : ReceiveActor
{
    public static Props Props(TimeSpan interval)
        => Akka.Actor.Props.Create(() => new SystemMonitorActor(interval));

    public SystemMonitorActor(TimeSpan interval)
    {
        var cpu = Context.ActorOf(CpuMonitorActor.Props(), "cpu");
        var memory = Context.ActorOf(MemoryMonitorActor.Props(), "memory");
        var disk = Context.ActorOf(DiskMonitorActor.Props(), "disk");
        var network = Context.ActorOf(NetworkMonitorActor.Props(), "network");
        var process = Context.ActorOf(ProcessMonitorActor.Props(), "process");
        var children = new[] { cpu, memory, disk, network, process };

        Context.System.Scheduler.ScheduleTellRepeatedly(
            TimeSpan.Zero, interval, Self, new Tick(), Self);

        Receive<Tick>(_ =>
        {
            foreach (var child in children) child.Tell(new Tick());
        });
    }

    protected override SupervisorStrategy SupervisorStrategy()
    {
        return new OneForOneStrategy(
            maxNrOfRetries: 3,
            withinTimeRange: TimeSpan.FromMinutes(1),
            decider: Decider.From(ex => Directive.Restart));
    }
}
