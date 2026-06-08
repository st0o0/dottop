using Akka.Actor;
using dottop.Actors;
using dottop.App.Actors;
using dottop.Core.Messages;
using dottop.Core.Models;
using dottop.Core.Platform;
using NSubstitute;

namespace dottop.Actors.Tests;

public class CpuMonitorActorTests : IAsyncLifetime
{
    private readonly ICpuMetrics _cpuMetrics = Substitute.For<ICpuMetrics>();
    private ActorSystem _sys = null!;

    public ValueTask InitializeAsync()
    {
        _sys = ActorSystem.Create("test");
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _sys.Terminate();
    }

    [Fact]
    public async Task StartMonitoring_ReturnsMonitoringStream()
    {
        _cpuMetrics.ProcessorName.Returns("Test CPU");
        _cpuMetrics.CoreCount.Returns(4);
        _cpuMetrics.Measure().Returns(new CpuMeasurement(42.0, [10, 20, 30, 40]));

        var actor = _sys.ActorOf(CpuMonitorActor.Props(_cpuMetrics, TimeSpan.FromMilliseconds(50)));

        var stream = await actor.Ask<MonitoringStream<CpuSnapshot>>(
            new StartMonitoring(), TimeSpan.FromSeconds(3));

        Assert.NotNull(stream);
        Assert.NotNull(stream.Data);

        await foreach (var snapshot in stream.Data)
        {
            Assert.Equal("Test CPU", snapshot.Name);
            Assert.Equal(42.0, snapshot.TotalPercent);
            Assert.Equal(4, snapshot.CorePercents.Count);
            break;
        }

        stream.Cancellation.Cancel();
    }

    [Fact]
    public async Task SecondStartMonitoring_CancelsPreviousStream()
    {
        _cpuMetrics.Measure().Returns(new CpuMeasurement(10, [10]));
        _cpuMetrics.ProcessorName.Returns("CPU");

        var actor = _sys.ActorOf(CpuMonitorActor.Props(_cpuMetrics, TimeSpan.FromMilliseconds(50)));

        var stream1 = await actor.Ask<MonitoringStream<CpuSnapshot>>(
            new StartMonitoring(), TimeSpan.FromSeconds(3));
        var stream2 = await actor.Ask<MonitoringStream<CpuSnapshot>>(
            new StartMonitoring(), TimeSpan.FromSeconds(3));

        Assert.True(stream1.Cancellation.IsCancellationRequested);
        Assert.False(stream2.Cancellation.IsCancellationRequested);

        stream2.Cancellation.Cancel();
    }
}
