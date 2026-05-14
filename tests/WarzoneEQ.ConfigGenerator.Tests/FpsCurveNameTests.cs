using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests;

public class FpsCurveNameTests
{
    [Fact]
    public void Has_three_values_minimalist_moderate_aggressive()
    {
        Enum.GetValues<FpsCurveName>()
            .Should().BeEquivalentTo(new[] { FpsCurveName.Minimalist, FpsCurveName.Moderate, FpsCurveName.Aggressive });
    }
}
