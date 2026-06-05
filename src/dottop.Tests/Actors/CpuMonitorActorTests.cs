using Akka.Actor;
using Akka.TestKit.Xunit2;
using dottop.Actors;
using dottop.Models;
using Xunit;

namespace dottop.Tests.Actors;

public class CpuMonitorActorTests : TestKit
{
    [Fact]
    public void CpuMonitorActor_OnTick_PublishesCpuSnapshot()
    {
        var actor = Sys.ActorOf(CpuMonitorActor.Props());
        Sys.EventStream.Subscribe(TestActor, typeof(CpuSnapshot));
        actor.Tell(new Tick());
        var snapshot = ExpectMsg<CpuSnapshot>(TimeSpan.FromSeconds(5));
        Assert.InRange(snapshot.TotalPercent, 0, 100);
        Assert.NotEmpty(snapshot.CorePercents);
    }
}
