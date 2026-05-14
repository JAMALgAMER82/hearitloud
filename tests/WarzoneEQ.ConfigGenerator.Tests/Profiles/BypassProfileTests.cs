using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.ConfigGenerator.Profiles;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Profiles;

public class BypassProfileTests
{
    [Fact]
    public void Bypass_without_DAC_emits_only_a_comment_header()
    {
        var profile = new BypassProfile();
        var input = new ProfileInput(AudioMode.Bypass);
        var output = profile.Generate(input);
        output.Should().StartWith("# warzone\\current.txt — Bypass mode (chain disabled)");
        output.Should().NotContain("Device:");
        output.Should().NotContain("Filter:");
        output.Should().NotContain("Plugin:");
    }

    [Fact]
    public void Bypass_with_DAC_emits_Device_directive_only()
    {
        var profile = new BypassProfile();
        var input = new ProfileInput(AudioMode.Bypass)
        {
            DacEndpoint = new DacEndpoint("Speakers Sound Blaster GC7 Game"),
        };
        var output = profile.Generate(input);
        output.Should().Contain("Device: Speakers Sound Blaster GC7 Game");
    }
}
