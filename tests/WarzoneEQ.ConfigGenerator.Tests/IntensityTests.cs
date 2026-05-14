using FluentAssertions;
using WarzoneEQ.ConfigGenerator;
using WarzoneEQ.ConfigGenerator.Filters;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests;

public class IntensityTests
{
    [Fact]
    public void Scaling_at_1_returns_filters_unchanged()
    {
        var f = Filter.Peaking(3000, 5, 1.5);
        Intensity.Scale(f, 1.0).Should().BeEquivalentTo(f);
    }

    [Fact]
    public void Scaling_at_0_5_halves_gain_keeps_freq_and_q()
    {
        var f = Filter.Peaking(3000, 5, 1.5);
        var scaled = Intensity.Scale(f, 0.5);
        scaled.GainDb.Should().Be(2.5);
        scaled.FrequencyHz.Should().Be(3000);
        scaled.Q.Should().Be(1.5);
    }

    [Fact]
    public void Scaling_at_0_zeros_gain()
    {
        var f = Filter.Peaking(3000, 5, 1.5);
        Intensity.Scale(f, 0).GainDb.Should().Be(0);
    }

    [Fact]
    public void Scaling_filter_without_gain_is_no_op()
    {
        var hp = Filter.HighPass(80);
        Intensity.Scale(hp, 0.5).Should().BeEquivalentTo(hp);
    }

    [Fact]
    public void Scaling_a_list_scales_each_filter()
    {
        var curve = FpsCurves.Get(WarzoneEQ.ConfigGenerator.Models.FpsCurveName.Moderate);
        var scaled = Intensity.Scale(curve, 0.5);
        scaled[0].GainDb.Should().Be(-3);
        scaled[3].GainDb.Should().Be(2.5);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Scaling_throws_on_out_of_range_intensity(double bad)
    {
        var f = Filter.Peaking(3000, 5, 1.5);
        Assert.Throws<ArgumentOutOfRangeException>(() => Intensity.Scale(f, bad));
    }
}
