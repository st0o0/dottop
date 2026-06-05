using Akka.Actor;
using Akka.TestKit.Xunit2;
using dottop.Actors;
using dottop.Platform.Windows;
using Xunit;

namespace dottop.Tests.Actors;

public class ProcessActionActorTests : TestKit
{
    [Fact]
    public void ProcessActionActor_GetProcessTree_ReturnsTree()
    {
        var actor = Sys.ActorOf(ProcessActionActor.Props(new WindowsProcessTree()));
        var currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
        actor.Tell(new GetProcessTree(currentPid));
        var result = ExpectMsg<ProcessTreeResult>(TimeSpan.FromSeconds(5));
        Assert.Equal(currentPid, result.Pid);
        Assert.NotEmpty(result.Name);
    }

    [Fact]
    public void ProcessActionActor_GetProcessEnvironment_ReturnsDictionary()
    {
        var actor = Sys.ActorOf(ProcessActionActor.Props(new WindowsProcessTree()));
        var currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
        actor.Tell(new GetProcessEnvironment(currentPid));
        var result = ExpectMsg<ProcessEnvironmentResult>(TimeSpan.FromSeconds(5));
        Assert.NotEmpty(result.Variables);
    }

    [Fact]
    public void ProcessActionActor_KillInvalidPid_ReturnsFailure()
    {
        var actor = Sys.ActorOf(ProcessActionActor.Props(new WindowsProcessTree()));
        actor.Tell(new KillProcess(-1));
        var result = ExpectMsg<ActionFailure>(TimeSpan.FromSeconds(5));
        Assert.NotEmpty(result.Error);
    }
}
