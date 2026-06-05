using Akka.Actor;
using Akka.TestKit.Xunit2;
using dottop.Actors;
using dottop.Tests.Platform;
using Xunit;

namespace dottop.Tests.Actors;

public class ProcessActionActorWithFakesTests : TestKit
{
    [Fact]
    public void GetProcessTree_UsesFakeProvider()
    {
        var fakeTree = new FakeProcessTree();
        var actor = Sys.ActorOf(ProcessActionActor.Props(fakeTree));

        actor.Tell(new GetProcessTree(100));

        var result = ExpectMsg<ProcessTreeResult>(TimeSpan.FromSeconds(5));
        Assert.Equal(100, result.Pid);
        Assert.Equal("FakeProcess-100", result.Name);
        Assert.Equal(2, result.Children.Count);
        Assert.Equal("child1", result.Children[0].Name);
        Assert.Equal("child2", result.Children[1].Name);
    }

    [Fact]
    public void GetProcessTree_ReturnsCorrectChildPids()
    {
        var fakeTree = new FakeProcessTree();
        var actor = Sys.ActorOf(ProcessActionActor.Props(fakeTree));

        actor.Tell(new GetProcessTree(500));

        var result = ExpectMsg<ProcessTreeResult>(TimeSpan.FromSeconds(5));
        Assert.Equal(501, result.Children[0].Pid);
        Assert.Equal(502, result.Children[1].Pid);
    }
}
