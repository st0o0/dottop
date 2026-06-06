using System.Runtime.InteropServices;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using dottop.Actors;
using dottop.Models;
using dottop.Platform.Windows;
using Xunit;

namespace dottop.Tests.Actors;

public class ProcessMonitorActorTests : TestKit
{
    [SkippableFact]
    public async Task ProcessMonitorActor_OnStartMonitoring_StreamsProcessList()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        var actor = Sys.ActorOf(ProcessMonitorActor.Props(new WindowsProcessClassifier(), TimeSpan.FromSeconds(1)));

        var response = await actor.Ask<MonitoringStream<List<ProcessSnapshot>>>(
            new StartMonitoring(), TimeSpan.FromSeconds(10));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var list in response.Data.WithCancellation(cts.Token))
        {
            Assert.NotEmpty(list);
            Assert.All(list, p => Assert.True(p.Pid >= 0));
            break;
        }

        response.Cancellation.Cancel();
    }

    [SkippableFact]
    public async Task ProcessMonitorActor_ProcessList_IsSortedByMemory()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        var actor = Sys.ActorOf(ProcessMonitorActor.Props(new WindowsProcessClassifier(), TimeSpan.FromSeconds(1)));

        var response = await actor.Ask<MonitoringStream<List<ProcessSnapshot>>>(
            new StartMonitoring(), TimeSpan.FromSeconds(10));

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
