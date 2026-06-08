using FluentAssertions;
using Xunit;
using dottop.Rendering;

namespace dottop.Rendering.Tests;

public class SparklineRendererTests
{
    [Fact]
    public void Empty_values_returns_spaces()
    {
        var result = SparklineRenderer.Render(Array.Empty<double>(), width: 8);
        result.Should().Be("        ");
        result.Length.Should().Be(8);
    }

    [Fact]
    public void All_zeros_returns_spaces()
    {
        var result = SparklineRenderer.Render(new double[] { 0, 0, 0, 0 }, width: 4);
        result.Should().Be("    ");
    }

    [Fact]
    public void All_100_returns_full_blocks()
    {
        var result = SparklineRenderer.Render(new double[] { 100, 100, 100 }, width: 3);
        result.Should().Be("███");
    }

    [Fact]
    public void Single_value_is_right_aligned()
    {
        var result = SparklineRenderer.Render(new double[] { 50 }, width: 4);
        result.Length.Should().Be(4);
        result[..3].Should().Be("   ");
        result[3].Should().NotBe(' ');
    }

    [Fact]
    public void Partial_fill_pads_left()
    {
        var values = new double[] { 20, 40, 60 };
        var result = SparklineRenderer.Render(values, width: 6);
        result.Length.Should().Be(6);
        result[..3].Should().Be("   ");
    }

    [Fact]
    public void More_than_width_takes_last_N()
    {
        var values = new double[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
        var result = SparklineRenderer.Render(values, width: 4);
        result.Length.Should().Be(4);
        // Should use last 4 values: 70, 80, 90, 100
        // All should be non-space (all > 0)
        result.Trim().Length.Should().Be(4);
    }

    [Fact]
    public void Custom_width_is_respected()
    {
        var result = SparklineRenderer.Render(new double[] { 50, 50 }, width: 12);
        result.Length.Should().Be(12);
    }

    [Fact]
    public void Values_above_100_are_clamped()
    {
        var result = SparklineRenderer.Render(new double[] { 200 }, width: 1);
        result.Should().Be("█");
    }

    [Fact]
    public void Negative_values_are_clamped_to_zero()
    {
        var result = SparklineRenderer.Render(new double[] { -50 }, width: 1);
        result.Should().Be(" ");
    }

    [Fact]
    public void Gradient_values_increase_in_block_height()
    {
        var values = new double[] { 10, 30, 50, 70, 90 };
        var result = SparklineRenderer.Render(values, width: 5);
        result.Length.Should().Be(5);
        // Each subsequent character should be >= previous
        for (var i = 1; i < result.Length; i++)
        {
            result[i].Should().BeGreaterThanOrEqualTo(result[i - 1]);
        }
    }
}
