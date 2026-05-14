using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Filters;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Filters;

public class FilterTests
{
    [Fact]
    public void HighPass_at_120Hz_serializes_correctly()
    {
        var f = Filter.HighPass(120);
        f.ToConfigLine().Should().Be("Filter: ON HP Fc 120 Hz");
    }

    [Fact]
    public void LowPass_at_16000Hz_serializes_correctly()
    {
        var f = Filter.LowPass(16000);
        f.ToConfigLine().Should().Be("Filter: ON LP Fc 16000 Hz");
    }

    [Fact]
    public void Peaking_with_positive_gain_serializes_with_plus_sign()
    {
        var f = Filter.Peaking(freqHz: 3000, gainDb: 4, q: 1.2);
        f.ToConfigLine().Should().Be("Filter: ON PK Fc 3000 Hz Gain +4.0 dB Q 1.2");
    }

    [Fact]
    public void Peaking_with_negative_gain_serializes_with_minus_sign()
    {
        var f = Filter.Peaking(freqHz: 1200, gainDb: -3, q: 5);
        f.ToConfigLine().Should().Be("Filter: ON PK Fc 1200 Hz Gain -3.0 dB Q 5.0");
    }

    [Fact]
    public void LowShelf_serializes_without_Q()
    {
        var f = Filter.LowShelf(freqHz: 250, gainDb: -6);
        f.ToConfigLine().Should().Be("Filter: ON LS Fc 250 Hz Gain -6.0 dB");
    }

    [Fact]
    public void HighShelf_serializes_without_Q()
    {
        var f = Filter.HighShelf(freqHz: 10000, gainDb: -3);
        f.ToConfigLine().Should().Be("Filter: ON HS Fc 10000 Hz Gain -3.0 dB");
    }
}
