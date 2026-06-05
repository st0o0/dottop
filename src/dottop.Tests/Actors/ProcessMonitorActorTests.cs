using Akka.Actor;
using Akka.TestKit.Xunit2;
using dottop.Actors;
using dottop.Models;
using Xunit;

namespace dottop.Tests.Actors;

public class ProcessMonitorActorTests : TestKit
{
    [Fact]
    public void ProcessMonitorActor_OnTick_PublishesProcessList()
    {
        var actor = Sys.ActorOf(ProcessMonitorActor.Props());
        Sys.EventStream.Subscribe(TestActor, typeof(List<ProcessSnapshot>));
        actor.Tell(new Tick());
        var list = ExpectMsg<List<ProcessSnapshot>>(TimeSpan.FromSeconds(5));
        Assert.NotEmpty(list);
        Assert.All(list, p => Assert.True(p.Pid >= 0));
    }

    [Fact]
    public void ProcessMonitorActor_ProcessList_IsSortedByMemory()
    {
        var actor = Sys.ActorOf(ProcessMonitorActor.Props());
        Sys.EventStream.Subscribe(TestActor, typeof(List<ProcessSnapshot>));
        actor.Tell(new Tick());
        var list = ExpectMsg<List<ProcessSnapshot>>(TimeSpan.FromSeconds(5));
        for (var i = 1; i < list.Count; i++)
            Assert.True(list[i - 1].WorkingSetBytes >= list[i].WorkingSetBytes);
    }
}
