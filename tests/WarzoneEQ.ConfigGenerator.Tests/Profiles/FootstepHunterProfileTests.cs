using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.ConfigGenerator.Profiles;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Profiles;

public class FootstepHunterProfileTests
{
    [Fact]
    public void Generates_header_and_tighter_150_Hz_high_pass()
    {
        var output = new FootstepHunterProfile().Generate(new ProfileInput(AudioMode.FootstepHunter));
        output.Should().Contain("FootstepHunter mode");
        output.Should().Contain("Filter: ON HP Fc 150 Hz");
    }

    [Fact]
    public void Notches_both_low_mid_rumble_and_gunshot_band_on_LR()
    {
        var output = new FootstepHunterProfile().Generate(new ProfileInput(AudioMode.FootstepHunter));
        output.Should().MatchRegex(@"Channel: L R[\s\S]*?Filter: ON PK Fc 300 Hz Gain -4\.0 dB Q 5");
        output.Should().MatchRegex(@"Channel: L R[\s\S]*?Filter: ON PK Fc 1200 Hz Gain -5\.0 dB Q 5");
    }

    [Fact]
    public void Pushes_FL_FR_harder_at_minus_5_dB()
    {
        var output = new FootstepHunterProfile().Generate(new ProfileInput(AudioMode.FootstepHunter));
        output.Should().Contain("Channel: FL FR");
        output.Should().Contain("Preamp: -5.0 dB");
    }

    [Fact]
    public void Ducks_FC_at_minus_12_dB_with_22_dB_threshold_10_to_1_ratio()
    {
        var output = new FootstepHunterProfile().Generate(new ProfileInput(AudioMode.FootstepHunter));
        output.Should().Contain("Channel: FC");
        output.Should().Contain("Preamp: -12.0 dB");
        output.Should().Contain("Plugin: \"TDR Nova\" -bandA-thresh -22.0 -bandA-ratio 10.0");
    }

    [Fact]
    public void Boosts_rear_and_side_channels_with_high_shelf_above_2_kHz()
    {
        var output = new FootstepHunterProfile().Generate(new ProfileInput(AudioMode.FootstepHunter));
        output.Should().Contain("Channel: BL BR SL SR");
        output.Should().Contain("Preamp: 6.0 dB");
        output.Should().Contain("Filter: ON HS Fc 2000 Hz Gain +4.0 dB");
    }

    [Fact]
    public void Stacks_three_transient_shapers_for_grass_concrete_and_metal_footsteps()
    {
        var output = new FootstepHunterProfile().Generate(new ProfileInput(AudioMode.FootstepHunter));
        output.Should().Contain("Plugin: \"TDR Nova\" -bandB-freq 3000 -bandB-gain +8.0");
        output.Should().Contain("Plugin: \"TDR Nova\" -bandB-freq 5000 -bandB-gain +6.0");
        output.Should().Contain("Plugin: \"TDR Nova\" -bandB-freq 6500 -bandB-gain +4.0");
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
        output.Should().Contain("Filter: ON HP Fc 150 Hz");
        output.Should().Contain("Filter: ON HS Fc 2000 Hz Gain +4.0 dB");
        output.Should().Contain("Preamp: 6.0 dB");
        output.Should().Contain("Preamp: -12.0 dB");
    }

    [Fact]
    public void ConfigGenerator_dispatches_to_FootstepHunter_profile()
    {
        var output = ConfigGenerator.Generate(new ProfileInput(AudioMode.FootstepHunter));
        output.Should().Contain("FootstepHunter mode");
    }

    [Fact]
    public void Wraps_processing_in_per_app_conditional_so_other_apps_pass_through()
    {
        var output = new FootstepHunterProfile().Generate(new ProfileInput(AudioMode.FootstepHunter));
        output.Should().Contain("If(app:cod.exe");
        output.Should().Contain("app:BlackOps6.exe");
        var endIfIdx = output.LastIndexOf("EndIf", StringComparison.Ordinal);
        endIfIdx.Should().BeGreaterThan(0);
        output.IndexOf("Channel: LFE", StringComparison.Ordinal).Should().BeLessThan(endIfIdx);
        output.IndexOf("Plugin: \"LoudMax\"", StringComparison.Ordinal).Should().BeLessThan(endIfIdx);
    }
}
