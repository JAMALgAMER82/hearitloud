using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Filters;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.ConfigGenerator.Profiles;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Profiles;

public class UserCustomProfileTests
{
    [Fact]
    public void Generates_user_filter_lines_in_pre_mix_stage()
    {
        var input = new ProfileInput(AudioMode.UserCustom)
        {
            UserFilters = new[]
            {
                Filter.Peaking(1000, gainDb: -3, q: 1),
                Filter.HighShelf(8000, gainDb: 4),
            },
        };
        var output = new UserCustomProfile().Generate(input);
        output.Should().Contain("Stage: pre-mix");
        output.Should().Contain("Filter: ON PK Fc 1000 Hz Gain -3.0 dB Q 1");
        output.Should().Contain("Filter: ON HS Fc 8000 Hz Gain +4.0 dB");
    }

    [Fact]
    public void Empty_user_filter_list_produces_a_valid_minimal_chain()
    {
        var output = new UserCustomProfile().Generate(new ProfileInput(AudioMode.UserCustom));
        output.Should().Contain("Stage: pre-mix");
        output.Should().Contain("Stage: post-mix");
        output.Should().NotContain("Filter:");
    }

    [Fact]
    public void ConfigGenerator_dispatches_to_UserCustom_profile()
    {
        var output = ConfigGenerator.Generate(new ProfileInput(AudioMode.UserCustom));
        output.Should().Contain("User custom chain");
    }

    [Fact]
    public void HRIR_and_headphone_correction_includes_still_apply()
    {
        var input = new ProfileInput(AudioMode.UserCustom)
        {
            HeadphoneCorrection = new HeadphoneCorrection("HD600"),
        };
        var output = new UserCustomProfile().Generate(input);
        output.Should().Contain(@"Include: warzone\hrir\hesuvi-active.wav");
        output.Should().Contain(@"Include: warzone\headphone-correction\HD600.txt");
    }

    [Fact]
    public void Wraps_user_chain_in_per_app_conditional()
    {
        var output = new UserCustomProfile().Generate(new ProfileInput(AudioMode.UserCustom));
        output.Should().Contain("If(app:cod.exe");
        output.TrimEnd().Should().EndWith("EndIf");
    }
}
