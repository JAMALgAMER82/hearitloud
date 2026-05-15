using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.ConfigGenerator.Profiles;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Profiles;

public class FootstepHunterProfileTests
{
    [Fact]
    public void Generates_header_and_aggressive_high_pass()
    {
        var output = new FootstepHunterProfile().Generate(new ProfileInput(AudioMode.FootstepHunter));
        output.Should().Contain("FootstepHunter mode");
        output.Should().Contain("Filter: ON HP Fc 120 Hz");
    }

    [Fact]
    public void Ducks_front_center_more_aggressively_than_competitive()
    {
        var output = new FootstepHunterProfile().Generate(new ProfileInput(AudioMode.FootstepHunter));
        output.Should().Contain("Channel: FC");
        output.Should().Contain("Preamp: -10.0 dB");
        output.Should().Contain("Plugin: \"TDR Nova\" -bandA-thresh -32.0");
    }

    [Fact]
    public void Boosts_rear_and_side_channels_for_off_axis_footsteps()
    {
        var output = new FootstepHunterProfile().Generate(new ProfileInput(AudioMode.FootstepHunter));
        output.Should().Contain("Channel: BL BR SL SR");
        output.Should().Contain("Preamp: 5.0 dB");
        output.Should().Contain("Plugin: \"TDR Nova\" -bandB-freq 3000 -bandB-gain +7.0 -bandB-Q 2.0");
    }

    [Fact]
    public void Effectively_mutes_LFE_to_unmask_footstep_band()
    {
        var output = new FootstepHunterProfile().Generate(new ProfileInput(AudioMode.FootstepHunter));
        output.Should().Contain("Channel: LFE");
        output.Should().Contain("Preamp: -40.0 dB");
    }

    [Fact]
    public void Forces_aggressive_curve_regardless_of_input_curve()
    {
        var output = new FootstepHunterProfile().Generate(new ProfileInput(AudioMode.FootstepHunter)
        {
            FpsCurve = FpsCurveName.Moderate,
        });
        output.Should().Contain(@"Include: warzone\fps-curves\aggressive.txt");
        output.Should().NotContain(@"Include: warzone\fps-curves\moderate.txt");
    }

    [Fact]
    public void Tighter_LoudMax_ceiling_than_competitive()
    {
        var output = new FootstepHunterProfile().Generate(new ProfileInput(AudioMode.FootstepHunter));
        output.Should().Contain("Plugin: \"LoudMax\" -ceiling -0.5");
    }

    [Fact]
    public void Basic_mode_keeps_filters_and_levels_but_drops_VST_plugins()
    {
        var output = new FootstepHunterProfile().Generate(new ProfileInput(AudioMode.FootstepHunter)
        {
            EnableVstPlugins = false,
        });
        output.Should().NotContain("TDR Nova");
        output.Should().NotContain("LoudMax");
        output.Should().NotContain("ReaXcomp");
        output.Should().Contain("Filter: ON HP Fc 120 Hz");
        output.Should().Contain("Preamp: 5.0 dB");
        output.Should().Contain("Preamp: -10.0 dB");
    }

    [Fact]
    public void ConfigGenerator_dispatches_to_FootstepHunter_profile()
    {
        var output = ConfigGenerator.Generate(new ProfileInput(AudioMode.FootstepHunter));
        output.Should().Contain("FootstepHunter mode");
    }
}
