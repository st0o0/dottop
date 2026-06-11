using dtop.Nodes;

namespace dtop.Rendering.Tests;

public class GraphNodeHistoryTests
{
    [Fact]
    public void Graph_exposes_the_history_it_was_constructed_with()
    {
        var history = new MetricHistory();
        using var graph = new GraphNode(history);

        Assert.Same(history, graph.History);
    }

    [Fact]
    public void WithHistory_swaps_the_render_source()
    {
        using var graph = new GraphNode(new MetricHistory());
        var diskA = MetricHistory.From([1, 2, 3]);

        graph.WithHistory(diskA);

        Assert.Same(diskA, graph.History);
    }

    [Fact]
    public void Two_graphs_can_share_one_history_without_copying()
    {
        var shared = new MetricHistory();
        using var panelGraph = new GraphNode(shared);
        using var detailGraph = new GraphNode(shared);

        shared.Push(42);

        // Both graphs observe the same underlying samples — the detail graph shows
        // the full history immediately, with no separate per-graph buffer to drift.
        Assert.Same(shared, panelGraph.History);
        Assert.Same(shared, detailGraph.History);
        Assert.Equal(new double[] { 42 }, detailGraph.History.Snapshot());
    }
}
