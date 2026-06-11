using dtop.Core.Messages;
using dtop.Services;
using Microsoft.Extensions.Time.Testing;
using R3;

namespace dtop.UI.Tests.Services;

public class RefreshServiceTests
{
    [Fact]
    public void Ticks_EmitOnInterval_WithMonotonicSeq()
    {
        var time = new FakeTimeProvider();
        using var svc = new RefreshService(TimeSpan.FromMilliseconds(500), time);
        var ticks = new List<Tick>();
        svc.Ticks.Subscribe(ticks.Add);

        time.Advance(TimeSpan.FromMilliseconds(500));
        time.Advance(TimeSpan.FromMilliseconds(500));

        Assert.Equal(2, ticks.Count);
        Assert.Equal(0, ticks[0].Seq);
        Assert.Equal(1, ticks[1].Seq);
        Assert.Equal(TimeSpan.FromMilliseconds(500), ticks[0].BaseInterval);
    }

    [Fact]
    public void Pause_StopsTicks_ResumeContinuesSeq()
    {
        var time = new FakeTimeProvider();
        using var svc = new RefreshService(TimeSpan.FromMilliseconds(500), time);
        var ticks = new List<Tick>();
        svc.Ticks.Subscribe(ticks.Add);

        time.Advance(TimeSpan.FromMilliseconds(500));   // seq 0
        svc.IsPaused.Value = true;
        time.Advance(TimeSpan.FromMilliseconds(2000));  // nothing
        svc.IsPaused.Value = false;
        time.Advance(TimeSpan.FromMilliseconds(500));   // seq 1

        Assert.Equal(2, ticks.Count);
        Assert.Equal(1, ticks[1].Seq);
    }

    [Fact]
    public void SpeedUp_StepsDown_AndClampsAtFastest()
    {
        var time = new FakeTimeProvider();
        using var svc = new RefreshService(TimeSpan.FromMilliseconds(500), time);

        svc.SpeedUp();
        Assert.Equal(TimeSpan.FromMilliseconds(250), svc.Interval.Value);
        svc.SpeedUp();
        Assert.Equal(TimeSpan.FromMilliseconds(250), svc.Interval.Value); // clamped
    }

    [Fact]
    public void SlowDown_StepsUp_AndClampsAtSlowest()
    {
        var time = new FakeTimeProvider();
        using var svc = new RefreshService(TimeSpan.FromMilliseconds(2000), time);

        svc.SlowDown();
        Assert.Equal(TimeSpan.FromMilliseconds(4000), svc.Interval.Value);
        svc.SlowDown();
        Assert.Equal(TimeSpan.FromMilliseconds(4000), svc.Interval.Value); // clamped
    }

    [Fact]
    public void IntervalChange_TakesEffect_NextTickCarriesNewInterval()
    {
        var time = new FakeTimeProvider();
        using var svc = new RefreshService(TimeSpan.FromMilliseconds(500), time);
        var ticks = new List<Tick>();
        svc.Ticks.Subscribe(ticks.Add);

        svc.SlowDown(); // 1000ms
        time.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Empty(ticks);                       // old cadence gone
        time.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Single(ticks);                      // fired at 1000ms
        Assert.Equal(TimeSpan.FromMilliseconds(1000), ticks[0].BaseInterval);
    }

    [Fact]
    public void NonStepInitialInterval_SnapsToNearestStep()
    {
        var time = new FakeTimeProvider();
        using var svc = new RefreshService(TimeSpan.FromMilliseconds(5000), time);
        Assert.Equal(TimeSpan.FromMilliseconds(4000), svc.Interval.Value);
    }

    [Fact]
    public void TickSource_Subscribe_ReceivesCallbacks()
    {
        var time = new FakeTimeProvider();
        using var svc = new RefreshService(TimeSpan.FromMilliseconds(500), time);
        var called = 0;
        using var sub = ((dtop.Plugin.ITickSource)svc).Subscribe(() => called++);

        time.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(1, called);
    }
}
