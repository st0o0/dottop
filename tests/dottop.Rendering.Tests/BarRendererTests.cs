using FluentAssertions;
using Xunit;
using dottop.Rendering;

namespace dottop.Rendering.Tests;

public class BarRendererTests
{
    [Fact]
    public void Zero_percent_returns_empty_bar()
    {
        var result = BarRenderer.Render(0, width: 6);
        result.Should().Be("[      ]");
    }

    [Fact]
    public void Hundred_percent_returns_full_bar()
    {
        var result = BarRenderer.Render(100, width: 6);
        result.Should().Be("[██████]");
    }

    [Fact]
    public void Fifty_percent_fills_half()
    {
        var result = BarRenderer.Render(50, width: 6);
        result.Should().Be("[███   ]");
    }

    [Fact]
    public void Custom_width_is_respected()
    {
        var result = BarRenderer.Render(50, width: 10);
        result.Length.Should().Be(12); // width + 2 brackets
        result.Should().Be("[█████     ]");
    }

    [Fact]
    public void Negative_value_is_clamped_to_zero()
    {
        var result = BarRenderer.Render(-20, width: 6);
        result.Should().Be("[      ]");
    }

    [Fact]
    public void Over_100_is_clamped_to_full()
    {
        var result = BarRenderer.Render(150, width: 6);
        result.Should().Be("[██████]");
    }

    [Fact]
    public void Small_percent_rounds_down()
    {
        // 10% of 6 = 0.6, rounds down to 0
        var result = BarRenderer.Render(10, width: 6);
        result.Should().Be("[      ]");
    }
}
