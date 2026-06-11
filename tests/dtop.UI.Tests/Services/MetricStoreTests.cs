using dtop.Core.Models;
using dtop.Services;

namespace dtop.UI.Tests.Services;

public class MetricStoreTests
{
    [Fact]
    public void PublishCpu_SetsLatest_AndPushesHistory()
    {
        var store = new MetricStore();

        store.Publish(new CpuSnapshot("cpu", 42.0, [40, 44]));

        Assert.Equal(42.0, store.Cpu.Value!.TotalPercent);
        Assert.Equal([42.0], store.CpuHistory.Snapshot());
    }

    [Fact]
    public void PublishMemory_PushesUsedPercentHistory()
    {
        var store = new MetricStore();

        store.Publish(new MemorySnapshot(100, 25));

        Assert.Equal([25.0], store.MemHistory.Snapshot());
    }

    [Fact]
    public void KeyedHistory_SameKey_SameInstance()
    {
        var store = new MetricStore();

        var a = store.History("disk:C:active");
        var b = store.History("disk:C:active");

        Assert.Same(a, b);
    }

    [Fact]
    public void KeyedHistory_EvictsLeastRecentlyUsed_BeyondLimit()
    {
        var store = new MetricStore(keyedHistoryLimit: 2);

        var first = store.History("a");
        store.History("b");
        store.History("c");           // evicts "a"
        var again = store.History("a");

        Assert.NotSame(first, again);
    }
}
