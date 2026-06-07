using Akka.Actor;
using dottop.Actors;
using dottop.Core.Messages;
using dottop.Core.Models;
using dottop.Core.Platform;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace dottop.Actors.Tests;

public class CpuMonitorActorTests : IAsyncLifetime
{
    private readonly ICpuMetrics _cpuMetrics = Substitute.For<ICpuMetrics>();
    private ActorSystem _sys = null!;

    public Task InitializeAsync()
    {
        _sys = ActorSystem.Create("test");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
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

        stream.Should().NotBeNull();
        stream.Data.Should().NotBeNull();

        await foreach (var snapshot in stream.Data)
        {
            snapshot.Name.Should().Be("Test CPU");
            snapshot.TotalPercent.Should().Be(42.0);
            snapshot.CorePercents.Should().HaveCount(4);
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

        stream1.Cancellation.IsCancellationRequested.Should().BeTrue();
        stream2.Cancellation.IsCancellationRequested.Should().BeFalse();

        stream2.Cancellation.Cancel();
    }
}
