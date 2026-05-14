using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Filters;
using WarzoneEQ.ConfigGenerator.Models;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Filters;

public class FpsCurvesTests
{
    [Fact]
    public void Minimalist_has_3_filters()
    {
        FpsCurves.Get(FpsCurveName.Minimalist).Should().HaveCount(3);
    }

    [Fact]
    public void Moderate_has_6_filters()
    {
        FpsCurves.Get(FpsCurveName.Moderate).Should().HaveCount(6);
    }

    [Fact]
    public void Aggressive_has_10_filters()
    {
        FpsCurves.Get(FpsCurveName.Aggressive).Should().HaveCount(10);
    }

    [Fact]
    public void Moderate_third_filter_is_2000Hz_peaking_plus3dB()
    {
        var filters = FpsCurves.Get(FpsCurveName.Moderate);
        filters[2].Type.Should().Be(FilterType.PK);
        filters[2].FrequencyHz.Should().Be(2000);
        filters[2].GainDb.Should().Be(3);
        filters[2].Q.Should().Be(1.4);
    }

    [Fact]
    public void Aggressive_includes_suppressed_gunfire_scoop_at_1200Hz_minus3dB()
    {
        var filters = FpsCurves.Get(FpsCurveName.Aggressive);
        filters.Should().Contain(f =>
            f.FrequencyHz == 1200 && f.GainDb == -3 && f.Q == 5);
    }
}
