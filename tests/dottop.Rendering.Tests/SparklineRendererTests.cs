using dottop.App.Rendering;
using dottop.Rendering;

namespace dottop.Rendering.Tests;

public class SparklineRendererTests
{
    [Fact]
    public void Empty_values_returns_spaces()
    {
        var result = SparklineRenderer.Render(Array.Empty<double>(), width: 8);
        Assert.Equal("        ", result);
        Assert.Equal(8, result.Length);
    }

    [Fact]
    public void All_zeros_returns_spaces()
    {
        var result = SparklineRenderer.Render(new double[] { 0, 0, 0, 0 }, width: 4);
        Assert.Equal("    ", result);
    }

    [Fact]
    public void All_100_returns_full_blocks()
    {
        var result = SparklineRenderer.Render(new double[] { 100, 100, 100 }, width: 3);
        Assert.Equal("███", result);
    }

    [Fact]
    public void Single_value_is_right_aligned()
    {
        var result = SparklineRenderer.Render(new double[] { 50 }, width: 4);
        Assert.Equal(4, result.Length);
        Assert.Equal("   ", result[..3]);
        Assert.NotEqual(' ', result[3]);
    }

    [Fact]
    public void Partial_fill_pads_left()
    {
        var values = new double[] { 20, 40, 60 };
        var result = SparklineRenderer.Render(values, width: 6);
        Assert.Equal(6, result.Length);
        Assert.Equal("   ", result[..3]);
    }

    [Fact]
    public void More_than_width_takes_last_N()
    {
        var values = new double[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
        var result = SparklineRenderer.Render(values, width: 4);
        Assert.Equal(4, result.Length);
        // Should use last 4 values: 70, 80, 90, 100
        // All should be non-space (all > 0)
        Assert.Equal(4, result.Trim().Length);
    }

    [Fact]
    public void Custom_width_is_respected()
    {
        var result = SparklineRenderer.Render(new double[] { 50, 50 }, width: 12);
        Assert.Equal(12, result.Length);
    }

    [Fact]
    public void Values_above_100_are_clamped()
    {
        var result = SparklineRenderer.Render(new double[] { 200 }, width: 1);
        Assert.Equal("█", result);
    }

    [Fact]
    public void Negative_values_are_clamped_to_zero()
    {
        var result = SparklineRenderer.Render(new double[] { -50 }, width: 1);
        Assert.Equal(" ", result);
    }

    [Fact]
    public void Gradient_values_increase_in_block_height()
    {
        var values = new double[] { 10, 30, 50, 70, 90 };
        var result = SparklineRenderer.Render(values, width: 5);
        Assert.Equal(5, result.Length);
        // Each subsequent character should be >= previous
        for (var i = 1; i < result.Length; i++)
        {
            Assert.True(result[i] >= result[i - 1]);
        }
    }
}
