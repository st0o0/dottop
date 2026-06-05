using Akka.Actor;
using Akka.TestKit.Xunit2;
using dottop.Actors;
using dottop.Models;
using dottop.Tests.Platform;
using Xunit;

namespace dottop.Tests.Actors;

public class ServiceActorTests : TestKit
{
    [Fact]
    public void GetServices_ReturnsList()
    {
        var fakeManager = new FakeServiceManager();
        var actor = Sys.ActorOf(ServiceActor.Props(fakeManager));

        actor.Tell(new GetServices());

        var result = ExpectMsg<List<ServiceInfo>>(TimeSpan.FromSeconds(5));
        Assert.Equal(2, result.Count);
        Assert.Equal("TestSvc1", result[0].Name);
    }

    [Fact]
    public void StartService_ReturnsSuccess()
    {
        var fakeManager = new FakeServiceManager();
        var actor = Sys.ActorOf(ServiceActor.Props(fakeManager));

        actor.Tell(new StartService("TestSvc2"));

        var result = ExpectMsg<ActionSuccess>(TimeSpan.FromSeconds(5));
        Assert.Contains("TestSvc2", result.Message);
        Assert.Equal("start:TestSvc2", fakeManager.LastAction);
    }

    [Fact]
    public void StopService_ReturnsSuccess()
    {
        var fakeManager = new FakeServiceManager();
        var actor = Sys.ActorOf(ServiceActor.Props(fakeManager));

        actor.Tell(new StopService("TestSvc1"));

        var result = ExpectMsg<ActionSuccess>(TimeSpan.FromSeconds(5));
        Assert.Contains("TestSvc1", result.Message);
        Assert.Equal("stop:TestSvc1", fakeManager.LastAction);
    }

    [Fact]
    public void RestartService_ReturnsSuccess()
    {
        var fakeManager = new FakeServiceManager();
        var actor = Sys.ActorOf(ServiceActor.Props(fakeManager));

        actor.Tell(new RestartService("TestSvc1"));

        var result = ExpectMsg<ActionSuccess>(TimeSpan.FromSeconds(5));
        Assert.Contains("TestSvc1", result.Message);
        Assert.Equal("restart:TestSvc1", fakeManager.LastAction);
    }
}
