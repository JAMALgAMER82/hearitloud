using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests;

public class ConfigGeneratorTests
{
    [Fact]
    public void Routes_Competitive_to_CompetitiveProfile()
    {
        var output = WarzoneEQ.ConfigGenerator.ConfigGenerator.Generate(new ProfileInput(AudioMode.Competitive));
        output.Should().Contain("Competitive mode");
    }

    [Fact]
    public void Routes_Cinematic_to_CinematicProfile()
    {
        var output = WarzoneEQ.ConfigGenerator.ConfigGenerator.Generate(new ProfileInput(AudioMode.Cinematic));
        output.Should().Contain("Cinematic mode");
    }

    [Fact]
    public void Routes_Bypass_to_BypassProfile()
    {
        var output = WarzoneEQ.ConfigGenerator.ConfigGenerator.Generate(new ProfileInput(AudioMode.Bypass));
        output.Should().Contain("Bypass mode");
    }
}
