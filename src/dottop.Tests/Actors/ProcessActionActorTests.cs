using System.Runtime.InteropServices;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using dottop.Actors;
using dottop.Platform.Windows;
using dottop.Tests.Platform;
using Xunit;

namespace dottop.Tests.Actors;

public class ProcessActionActorTests : TestKit
{
    [SkippableFact]
    public void ProcessActionActor_GetProcessTree_ReturnsTree()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        var actor = Sys.ActorOf(ProcessActionActor.Props(new WindowsProcessTree()));
        var currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
        actor.Tell(new GetProcessTree(currentPid));
        var result = ExpectMsg<ProcessTreeResult>(TimeSpan.FromSeconds(30));
        Assert.Equal(currentPid, result.Pid);
        Assert.NotEmpty(result.Name);
    }

    [Fact]
    public void ProcessActionActor_GetProcessTree_WithFake_ReturnsTree()
    {
        var actor = Sys.ActorOf(ProcessActionActor.Props(new FakeProcessTree()));
        actor.Tell(new GetProcessTree(100));
        var result = ExpectMsg<ProcessTreeResult>(TimeSpan.FromSeconds(30));
        Assert.Equal(100, result.Pid);
        Assert.Equal(2, result.Children.Count);
    }

    [Fact]
    public void ProcessActionActor_GetProcessEnvironment_ReturnsDictionary()
    {
        var actor = Sys.ActorOf(ProcessActionActor.Props(new FakeProcessTree()));
        var currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
        actor.Tell(new GetProcessEnvironment(currentPid));
        var result = ExpectMsg<ProcessEnvironmentResult>(TimeSpan.FromSeconds(30));
        Assert.NotEmpty(result.Variables);
    }

    [Fact]
    public void ProcessActionActor_KillInvalidPid_ReturnsFailure()
    {
        var actor = Sys.ActorOf(ProcessActionActor.Props(new FakeProcessTree()));
        actor.Tell(new KillProcess(-1));
        var result = ExpectMsg<ActionFailure>(TimeSpan.FromSeconds(30));
        Assert.NotEmpty(result.Error);
    }
}
