using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests;

public class AudioModeTests
{
    [Fact]
    public void Has_four_values_competitive_cinematic_bypass_footstephunter()
    {
        Enum.GetValues<AudioMode>()
            .Should().BeEquivalentTo(new[]
            {
                AudioMode.Competitive,
                AudioMode.Cinematic,
                AudioMode.Bypass,
                AudioMode.FootstepHunter,
            });
    }
}
