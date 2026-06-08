using FluentAssertions;
using Xunit;

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
        queue.Should().BeEmpty();
    }

    [Fact]
    public void Enqueue_adds_values_up_to_limit()
    {
        var queue = CreateHistory(10, 20, 30, 40, 50, 60, 70, 80);
        queue.Count.Should().Be(HistoryLength);
        queue.ToArray().Should().Equal(10, 20, 30, 40, 50, 60, 70, 80);
    }

    [Fact]
    public void Overflow_drops_oldest_values()
    {
        var queue = CreateHistory(10, 20, 30, 40, 50, 60, 70, 80, 90);
        queue.Count.Should().Be(HistoryLength);
        queue.Peek().Should().Be(20); // 10 was dropped
        queue.ToArray().Should().Equal(20, 30, 40, 50, 60, 70, 80, 90);
    }

    [Fact]
    public void Partial_fill_returns_fewer_than_limit()
    {
        var queue = CreateHistory(5, 10, 15);
        queue.Count.Should().Be(3);
        queue.ToArray().Should().Equal(5, 10, 15);
    }

    [Fact]
    public void ToArray_returns_snapshot_for_sparkline()
    {
        var queue = CreateHistory(1, 2, 3, 4, 5);
        IReadOnlyList<double> snapshot = queue.ToArray();
        snapshot.Should().HaveCount(5);
        snapshot[0].Should().Be(1);
        snapshot[4].Should().Be(5);
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

        history.Should().ContainKey(100);
        history.Should().NotContainKey(200);
        history.Should().ContainKey(300);
    }
}
