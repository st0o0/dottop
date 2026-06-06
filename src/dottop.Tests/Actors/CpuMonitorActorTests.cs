using System.Runtime.InteropServices;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using dottop.Actors;
using dottop.Models;
using dottop.Platform;
using dottop.Platform.Windows;
using Xunit;

namespace dottop.Tests.Actors;

public class CpuMonitorActorTests : TestKit
{
    [SkippableFact]
    public async Task CpuMonitorActor_OnStartMonitoring_StreamsCpuSnapshots()
    {
        Skip.IfNot(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));

        using var cpuMetrics = new WindowsCpuMetrics();
        var actor = Sys.ActorOf(CpuMonitorActor.Props(cpuMetrics, TimeSpan.FromSeconds(1)));

        var response = await actor.Ask<MonitoringStream<CpuSnapshot>>(
            new StartMonitoring(), TimeSpan.FromSeconds(10));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var snapshot in response.Data.WithCancellation(cts.Token))
        {
            Assert.InRange(snapshot.TotalPercent, 0, 100);
            Assert.NotEmpty(snapshot.CorePercents);
            break;
        }

        response.Cancellation.Cancel();
    }
}
