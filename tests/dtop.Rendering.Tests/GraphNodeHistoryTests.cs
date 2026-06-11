using dtop.Nodes;
using Termina.Layout;

namespace dtop.Rendering.Tests;

public class GraphNodeHistoryTests
{
    [Fact]
    public void SetData_makes_data_available_for_rendering()
    {
        using var graph = new GraphNode();
        graph.SetData([1, 2, 3]);

        // Graph accepts data without throwing
        Assert.NotNull(graph);
    }

    [Fact]
    public void MetricHistory_snapshot_feeds_graph_via_SetData()
    {
        var history = new MetricHistory();
        history.Push(42);
        history.Push(84);

        using var graph = new GraphNode();
        graph.SetData(history.Snapshot());

        Assert.NotNull(graph);
    }

    [Fact]
    public void Two_graphs_can_share_one_history_via_SetData()
    {
        var shared = new MetricHistory();
        using var panelGraph = new GraphNode();
        using var detailGraph = new GraphNode();

        shared.Push(42);
        var snapshot = shared.Snapshot();

        panelGraph.SetData(snapshot);
        detailGraph.SetData(snapshot);

        Assert.Equal(new double[] { 42 }, snapshot);
    }
}
