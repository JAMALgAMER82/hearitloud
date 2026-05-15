using FluentAssertions;
using VerifyXunit;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.ConfigGenerator.Profiles;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Profiles;

public class CinematicProfileTests
{
    [Fact]
    public void Includes_Polyverse_Wider_on_sides_and_rear()
    {
        var output = new CinematicProfile().Generate(new ProfileInput(AudioMode.Cinematic)
        {
            EnablePolyverseWider = true,
        });
        output.Should().MatchRegex(@"Channel: BL BR SL SR[\s\S]*?Plugin: ""Polyverse Wider""");
    }

    [Fact]
    public void Linear_phase_flag_appends_mode_to_transient_shaper()
    {
        var output = new CinematicProfile().Generate(new ProfileInput(AudioMode.Cinematic)
        {
            EnableLinearPhase = true,
        });
        output.Should().Contain("Plugin: \"TDR Nova\" -bandB-freq 3000 -bandB-gain +5.0 -bandB-Q 1.5 -mode linear-phase");
    }

    [Fact]
    public void Adaptive_loudness_JSFX_included_when_enabled()
    {
        var output = new CinematicProfile().Generate(new ProfileInput(AudioMode.Cinematic)
        {
            EnableAdaptiveLoudness = true,
        });
        output.Should().Contain(@"Plugin: 'warzone\jsfx\adaptive-loudness.jsfx'");
    }

    [Fact]
    public void Basic_mode_omits_all_VST_plugin_lines_but_keeps_filters_and_curves()
    {
        var output = new CinematicProfile().Generate(new ProfileInput(AudioMode.Cinematic)
        {
            EnableVstPlugins = false,
            EnableAdaptiveLoudness = true,
            EnablePolyverseWider = true,
        });
        output.Should().NotContain("TDR Nova");
        output.Should().NotContain("LoudMax");
        output.Should().NotContain("ReaXcomp");
        output.Should().NotContain("Polyverse Wider");
        output.Should().NotContain("adaptive-loudness.jsfx");
        output.Should().Contain("Filter: ON HP Fc 80 Hz");
        output.Should().Contain(@"Include: warzone\fps-curves");
    }

    [Fact]
    public void HrirInclude_disabled_omits_hesuvi_include_line()
    {
        var output = new CinematicProfile().Generate(new ProfileInput(AudioMode.Cinematic)
        {
            EnableHrirInclude = false,
        });
        output.Should().NotContain(@"Include: warzone\hrir\hesuvi-active.wav");
    }

    [Fact]
    public Task Snapshot_HD600_GC7_Aggressive_AllToggles()
    {
        var input = new ProfileInput(AudioMode.Cinematic)
        {
            FpsCurve = FpsCurveName.Aggressive,
            HeadphoneCorrection = new HeadphoneCorrection("HD600"),
            DacEndpoint = new DacEndpoint("Speakers Sound Blaster GC7 Game"),
            EnableLinearPhase = true,
            EnableAdaptiveLoudness = true,
            EnablePolyverseWider = true,
        };
        var output = new CinematicProfile().Generate(input);
        return Verifier.Verify(output)
            .UseDirectory("../Snapshots");
    }
}
