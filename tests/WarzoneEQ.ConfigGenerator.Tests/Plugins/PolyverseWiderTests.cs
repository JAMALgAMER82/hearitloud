using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Plugins;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Plugins;

public class PolyverseWiderTests
{
    [Fact]
    public void Wider_at_140_serializes_correctly()
    {
        var plugin = PolyverseWider.Width(140);
        plugin.ToConfigLine().Should().Be("Plugin: \"Polyverse Wider\" -width 140");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(201)]
    public void Wider_rejects_out_of_range_width(int badWidth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PolyverseWider.Width(badWidth));
    }
}
