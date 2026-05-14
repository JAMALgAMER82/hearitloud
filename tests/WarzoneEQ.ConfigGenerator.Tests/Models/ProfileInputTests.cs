using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Models;

public class ProfileInputTests
{
    [Fact]
    public void Default_constructs_with_required_mode_only()
    {
        var p = new ProfileInput(AudioMode.Competitive);
        p.Mode.Should().Be(AudioMode.Competitive);
        p.FpsCurve.Should().Be(FpsCurveName.Moderate);
        p.Intensity.Should().Be(1.0);
        p.HeadphoneCorrection.Should().Be(HeadphoneCorrection.None);
        p.DacEndpoint.Should().Be(DacEndpoint.WindowsDefault);
        p.EnableFootstepCompressor.Should().BeTrue();
        p.EnableLinearPhase.Should().BeFalse();
        p.EnableAdaptiveLoudness.Should().BeFalse();
        p.EnablePolyverseWider.Should().BeFalse();
        p.HrirIncludePath.Should().Be(@"warzone\hrir\hesuvi-active.wav");
    }

    [Fact]
    public void Intensity_outside_0_to_1_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProfileInput(AudioMode.Competitive) { Intensity = 1.5 });
    }
}
