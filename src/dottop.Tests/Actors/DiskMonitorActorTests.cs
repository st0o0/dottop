using Akka.Actor;
using Akka.TestKit.Xunit2;
using dottop.Actors;
using dottop.Models;
using dottop.Tests.Platform;
using Xunit;

namespace dottop.Tests.Actors;

public class DiskMonitorActorTests : TestKit
{
    [Fact]
    public async Task DiskMonitorActor_UsesProvider_ForMetrics()
    {
        var fakeDisk = new FakeDiskMetrics();
        fakeDisk.Data["C:"] = (1024 * 1024, 512 * 1024, 42.5);
        fakeDisk.Initialize();

        var actor = Sys.ActorOf(DiskMonitorActor.Props(fakeDisk, TimeSpan.FromSeconds(1)));

        var response = await actor.Ask<MonitoringStream<List<DiskSnapshot>>>(
            new StartMonitoring(), TimeSpan.FromSeconds(5));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await foreach (var disks in response.Data.WithCancellation(cts.Token))
        {
            Assert.True(fakeDisk.Initialized);
            break;
        }

        response.Cancellation.Cancel();
    }
}
