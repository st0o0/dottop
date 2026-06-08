namespace dottop.Rendering.Tests;

/// <summary>
/// Tests the Queue-based CPU history pattern used by ProcessesViewModel.
/// Tests the pattern in isolation without ViewModel dependency.
/// </summary>
public class CpuHistoryTests
{
    private const int HistoryLength = 8;

    private static Queue<double> CreateHistory(params double[] values)
    {
        var queue = new Queue<double>(HistoryLength);
        foreach (var v in values)
        {
            queue.Enqueue(v);
            if (queue.Count > HistoryLength)
                queue.Dequeue();
        }
        return queue;
    }

    [Fact]
    public void New_queue_is_empty()
    {
        var queue = new Queue<double>(HistoryLength);
        Assert.Empty(queue);
    }

    [Fact]
    public void Enqueue_adds_values_up_to_limit()
    {
        var queue = CreateHistory(10, 20, 30, 40, 50, 60, 70, 80);
        Assert.Equal(HistoryLength, queue.Count);
        Assert.Equal(new double[] { 10, 20, 30, 40, 50, 60, 70, 80 }, queue.ToArray());
    }

    [Fact]
    public void Overflow_drops_oldest_values()
    {
        var queue = CreateHistory(10, 20, 30, 40, 50, 60, 70, 80, 90);
        Assert.Equal(HistoryLength, queue.Count);
        Assert.Equal(20, queue.Peek()); // 10 was dropped
        Assert.Equal(new double[] { 20, 30, 40, 50, 60, 70, 80, 90 }, queue.ToArray());
    }

    [Fact]
    public void Partial_fill_returns_fewer_than_limit()
    {
        var queue = CreateHistory(5, 10, 15);
        Assert.Equal(3, queue.Count);
        Assert.Equal(new double[] { 5, 10, 15 }, queue.ToArray());
    }

    [Fact]
    public void ToArray_returns_snapshot_for_sparkline()
    {
        var queue = CreateHistory(1, 2, 3, 4, 5);
        IReadOnlyList<double> snapshot = queue.ToArray();
        Assert.Equal(5, snapshot.Count);
        Assert.Equal(1, snapshot[0]);
        Assert.Equal(5, snapshot[4]);
    }

    [Fact]
    public void Stale_pid_removal_pattern_works()
    {
        var history = new Dictionary<int, Queue<double>>
        {
            [100] = CreateHistory(10, 20),
            [200] = CreateHistory(30, 40),
            [300] = CreateHistory(50, 60),
        };

        var activePids = new HashSet<int> { 100, 300 };
        var stale = history.Keys.Where(pid => !activePids.Contains(pid)).ToList();
        foreach (var pid in stale)
            history.Remove(pid);

        Assert.True(history.ContainsKey(100));
        Assert.False(history.ContainsKey(200));
        Assert.True(history.ContainsKey(300));
    }
}
