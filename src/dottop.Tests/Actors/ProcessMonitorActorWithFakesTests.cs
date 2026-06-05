using Akka.Actor;
using Akka.TestKit.Xunit2;
using dottop.Actors;
using dottop.Models;
using dottop.Tests.Platform;
using Xunit;

namespace dottop.Tests.Actors;

public class ProcessMonitorActorWithFakesTests : TestKit
{
    [Fact]
    public async Task ProcessMonitorActor_UsesClassifier_ForGrouping()
    {
        var fakeClassifier = new FakeProcessClassifier { DefaultGroup = ProcessGroup.Apps };
        var actor = Sys.ActorOf(ProcessMonitorActor.Props(fakeClassifier));

        var response = await actor.Ask<MonitoringStream<List<ProcessSnapshot>>>(
            new StartMonitoring(), TimeSpan.FromSeconds(5));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var list in response.Data.WithCancellation(cts.Token))
        {
            Assert.NotEmpty(list);
            Assert.All(list, p => Assert.Equal(ProcessGroup.Apps, p.Group));
            break;
        }

        response.Cancellation.Cancel();
    }

    [Fact]
    public async Task ProcessMonitorActor_WithBackgroundClassifier_AllBackground()
    {
        var fakeClassifier = new FakeProcessClassifier { DefaultGroup = ProcessGroup.Background };
        var actor = Sys.ActorOf(ProcessMonitorActor.Props(fakeClassifier));

        var response = await actor.Ask<MonitoringStream<List<ProcessSnapshot>>>(
            new StartMonitoring(), TimeSpan.FromSeconds(5));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var list in response.Data.WithCancellation(cts.Token))
        {
            Assert.NotEmpty(list);
            Assert.All(list, p => Assert.Equal(ProcessGroup.Background, p.Group));
            break;
        }

        response.Cancellation.Cancel();
    }
}
