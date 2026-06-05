using Akka.Actor;
using Akka.TestKit.Xunit2;
using dottop.Actors;
using dottop.Models;
using dottop.Platform.Windows;
using Xunit;

namespace dottop.Tests.Actors;

public class ProcessMonitorActorTests : TestKit
{
    [Fact]
    public async Task ProcessMonitorActor_OnStartMonitoring_StreamsProcessList()
    {
        var actor = Sys.ActorOf(ProcessMonitorActor.Props(new WindowsProcessClassifier()));

        var response = await actor.Ask<MonitoringStream<List<ProcessSnapshot>>>(
            new StartMonitoring(), TimeSpan.FromSeconds(5));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var list in response.Data.WithCancellation(cts.Token))
        {
            Assert.NotEmpty(list);
            Assert.All(list, p => Assert.True(p.Pid >= 0));
            break;
        }

        response.Cancellation.Cancel();
    }

    [Fact]
    public async Task ProcessMonitorActor_ProcessList_IsSortedByMemory()
    {
        var actor = Sys.ActorOf(ProcessMonitorActor.Props(new WindowsProcessClassifier()));

        var response = await actor.Ask<MonitoringStream<List<ProcessSnapshot>>>(
            new StartMonitoring(), TimeSpan.FromSeconds(5));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var list in response.Data.WithCancellation(cts.Token))
        {
            for (var i = 1; i < list.Count; i++)
                Assert.True(list[i - 1].WorkingSetBytes >= list[i].WorkingSetBytes);
            break;
        }

        response.Cancellation.Cancel();
    }
}
