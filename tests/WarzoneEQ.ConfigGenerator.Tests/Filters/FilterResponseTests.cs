using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Filters;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Filters;

public class FilterResponseTests
{
    [Fact]
    public void Peaking_filter_returns_full_gain_at_center_frequency()
    {
        // A PK at 1 kHz with +6 dB and Q=1 should hit ~+6 dB at 1 kHz.
        var f = Filter.Peaking(1000, gainDb: 6, q: 1);
        var actual = FilterResponse.MagnitudeDb(f, 1000);
        actual.Should().BeApproximately(6.0, 0.5);
    }

    [Fact]
    public void Peaking_filter_returns_near_zero_dB_far_from_center()
    {
        // Two decades below center is well outside the bell.
        var f = Filter.Peaking(1000, gainDb: 12, q: 4);
        FilterResponse.MagnitudeDb(f, 10).Should().BeApproximately(0, 0.5);
        FilterResponse.MagnitudeDb(f, 18000).Should().BeApproximately(0, 1.5);
    }

    [Fact]
    public void HighPass_attenuates_below_corner()
    {
        var hp = Filter.HighPass(200);
        FilterResponse.MagnitudeDb(hp, 50).Should().BeLessThan(-10);
        FilterResponse.MagnitudeDb(hp, 2000).Should().BeApproximately(0, 1.0);
    }

    [Fact]
    public void LowPass_attenuates_above_corner()
    {
        var lp = Filter.LowPass(2000);
        FilterResponse.MagnitudeDb(lp, 200).Should().BeApproximately(0, 1.0);
        FilterResponse.MagnitudeDb(lp, 16000).Should().BeLessThan(-10);
    }

    [Fact]
    public void Chain_sums_individual_filter_responses_at_each_frequency()
    {
        var f1 = Filter.Peaking(1000, gainDb: 4, q: 1);
        var f2 = Filter.Peaking(1000, gainDb: 4, q: 1);
        var chain = new[] { f1, f2 };
        var single = FilterResponse.MagnitudeDb(f1, 1000);
        var summed = FilterResponse.ChainMagnitudeDb(chain, 1000);
        summed.Should().BeApproximately(2 * single, 0.1);
    }

    [Fact]
    public void HighShelf_lifts_treble_above_corner_without_affecting_bass()
    {
        var hs = Filter.HighShelf(4000, gainDb: 6);
        FilterResponse.MagnitudeDb(hs, 100).Should().BeApproximately(0, 1.0);
        FilterResponse.MagnitudeDb(hs, 16000).Should().BeApproximately(6, 1.5);
    }

    [Fact]
    public void LowShelf_lifts_bass_below_corner_without_affecting_treble()
    {
        var ls = Filter.LowShelf(200, gainDb: 6);
        FilterResponse.MagnitudeDb(ls, 16000).Should().BeApproximately(0, 1.0);
        FilterResponse.MagnitudeDb(ls, 20).Should().BeApproximately(6, 1.5);
    }

    [Fact]
    public void Notch_filter_has_deep_attenuation_at_center()
    {
        // PK with -24 dB and high Q acts as a notch.
        var notch = Filter.Peaking(1500, gainDb: -24, q: 8);
        FilterResponse.MagnitudeDb(notch, 1500).Should().BeLessThan(-15);
    }
}
