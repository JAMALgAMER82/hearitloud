using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Channels;
using WarzoneEQ.ConfigGenerator.Filters;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Channels;

public class ChannelStageTests
{
    [Fact]
    public void Single_channel_preamp_serializes_correctly()
    {
        var stage = new ChannelStage(new[] { Channel.FC }, PreampDb: -6);
        var lines = stage.ToConfigLines();
        lines.Should().Equal(
            "Channel: FC",
            "Preamp: -6.0 dB"
        );
    }

    [Fact]
    public void Multiple_channels_listed_space_separated()
    {
        var stage = new ChannelStage(new[] { Channel.BL, Channel.BR, Channel.SL, Channel.SR }, PreampDb: 2);
        var lines = stage.ToConfigLines();
        lines.First().Should().Be("Channel: BL BR SL SR");
    }

    [Fact]
    public void Stage_with_filters_emits_each_filter_line()
    {
        var stage = new ChannelStage(
            new[] { Channel.L, Channel.R },
            PreampDb: null,
            Filters: new[] { Filter.HighPass(80) });
        stage.ToConfigLines().Should().Equal(
            "Channel: L R",
            "Filter: ON HP Fc 80 Hz"
        );
    }

    [Fact]
    public void Stage_without_preamp_omits_preamp_line()
    {
        var stage = new ChannelStage(new[] { Channel.L, Channel.R }, PreampDb: null);
        stage.ToConfigLines().Should().Equal("Channel: L R");
    }

    [Fact]
    public void Stage_with_zero_preamp_still_emits_line()
    {
        var stage = new ChannelStage(new[] { Channel.L }, PreampDb: 0);
        stage.ToConfigLines().Should().Equal(
            "Channel: L",
            "Preamp: 0.0 dB"
        );
    }
}
