using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Plugins;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Plugins;

public class ReaXcompTests
{
    [Fact]
    public void Upward_compressor_serializes_full_param_list()
    {
        var plugin = ReaXcomp.UpwardCompressor(
            bandIndex: 1,
            freqLowHz: 2000,
            freqHighHz: 4500,
            thresholdDb: -42,
            ratio: "1:2",
            attackMs: 5,
            releaseMs: 80);
        plugin.ToConfigLine().Should().Be(
            "Plugin: \"ReaXcomp\" -band 1 -freq-low 2000 -freq-high 4500 -threshold -42.0 -ratio 1:2 -attack 5 -release 80");
    }
}
