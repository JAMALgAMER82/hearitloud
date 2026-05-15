using FluentAssertions;
using VerifyXunit;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.ConfigGenerator.Profiles;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Profiles;

public class CompetitiveProfileTests
{
    [Fact]
    public void Generates_header_and_pre_mix_stage()
    {
        var output = new CompetitiveProfile().Generate(new ProfileInput(AudioMode.Competitive));
        output.Should().Contain("Stage: pre-mix");
        output.Should().Contain("Channel: L R");
        output.Should().Contain("Filter: ON HP Fc 80 Hz");
        output.Should().Contain("Channel: FC");
        output.Should().Contain("Plugin: \"TDR Nova\" -bandA-thresh -28.0");
        output.Should().Contain("Channel: BL BR SL SR");
        output.Should().Contain("Plugin: \"TDR Nova\" -bandB-freq 3000 -bandB-gain +5.0");
        output.Should().Contain("Channel: LFE");
    }

    [Fact]
    public void Generates_post_mix_stage_with_includes()
    {
        var input = new ProfileInput(AudioMode.Competitive)
        {
            HeadphoneCorrection = new HeadphoneCorrection("HD600"),
        };
        var output = new CompetitiveProfile().Generate(input);
        output.Should().Contain("Stage: post-mix");
        output.Should().Contain(@"Include: warzone\hrir\hesuvi-active.wav");
        output.Should().Contain(@"Include: warzone\headphone-correction\HD600.txt");
        output.Should().Contain(@"Include: warzone\fps-curves\moderate.txt");
    }

    [Fact]
    public void Includes_ReaXcomp_when_footstep_compressor_enabled()
    {
        var output = new CompetitiveProfile().Generate(new ProfileInput(AudioMode.Competitive));
        output.Should().Contain("Plugin: \"ReaXcomp\" -band 1 -freq-low 2000 -freq-high 4500 -threshold -42.0 -ratio 1:2");
    }

    [Fact]
    public void Excludes_ReaXcomp_when_footstep_compressor_disabled()
    {
        var output = new CompetitiveProfile().Generate(new ProfileInput(AudioMode.Competitive)
        {
            EnableFootstepCompressor = false,
        });
        output.Should().NotContain("ReaXcomp");
    }

    [Fact]
    public void Always_ends_with_LoudMax_limiter()
    {
        var output = new CompetitiveProfile().Generate(new ProfileInput(AudioMode.Competitive));
        var lines = output.TrimEnd().Split('\n');
        lines.Last().TrimEnd().Should().Be("Plugin: \"LoudMax\" -ceiling -1.0");
    }

    [Fact]
    public void Inlines_scaled_filters_when_intensity_below_one()
    {
        var output = new CompetitiveProfile().Generate(new ProfileInput(AudioMode.Competitive)
        {
            Intensity = 0.5,
        });
        output.Should().NotContain(@"Include: warzone\fps-curves\moderate.txt");
        output.Should().Contain("Filter: ON LS Fc 250 Hz Gain -3.0 dB");
    }

    [Fact]
    public void Basic_mode_omits_all_VST_plugin_lines()
    {
        var output = new CompetitiveProfile().Generate(new ProfileInput(AudioMode.Competitive)
        {
            EnableVstPlugins = false,
        });
        output.Should().NotContain("TDR Nova");
        output.Should().NotContain("LoudMax");
        output.Should().NotContain("ReaXcomp");
        output.Should().NotContain("Polyverse Wider");
        output.Should().Contain("Filter: ON HP Fc 80 Hz");
        output.Should().Contain(@"Include: warzone\fps-curves\moderate.txt");
    }

    [Fact]
    public void HrirInclude_disabled_omits_hesuvi_include_line()
    {
        var output = new CompetitiveProfile().Generate(new ProfileInput(AudioMode.Competitive)
        {
            EnableHrirInclude = false,
        });
        output.Should().NotContain(@"Include: warzone\hrir\hesuvi-active.wav");
    }

    [Fact]
    public Task Snapshot_HD600_GC7_Moderate_FullIntensity()
    {
        var input = new ProfileInput(AudioMode.Competitive)
        {
            HeadphoneCorrection = new HeadphoneCorrection("HD600"),
            DacEndpoint = new DacEndpoint("Speakers Sound Blaster GC7 Game"),
        };
        var output = new CompetitiveProfile().Generate(input);
        return Verifier.Verify(output)
            .UseDirectory("../Snapshots");
    }
}
