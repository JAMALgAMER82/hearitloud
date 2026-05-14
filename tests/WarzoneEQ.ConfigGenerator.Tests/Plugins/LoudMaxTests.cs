using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Plugins;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Plugins;

public class LoudMaxTests
{
    [Fact]
    public void Limiter_with_default_ceiling_minus1dB_serializes_correctly()
    {
        var plugin = LoudMax.Limiter(ceilingDb: -1.0);
        plugin.ToConfigLine().Should().Be("Plugin: \"LoudMax\" -ceiling -1.0");
    }
}
