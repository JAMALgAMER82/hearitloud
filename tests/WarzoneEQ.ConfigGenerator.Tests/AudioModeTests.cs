using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests;

public class AudioModeTests
{
    [Fact]
    public void Has_five_values_including_user_custom()
    {
        Enum.GetValues<AudioMode>()
            .Should().BeEquivalentTo(new[]
            {
                AudioMode.Competitive,
                AudioMode.Cinematic,
                AudioMode.Bypass,
                AudioMode.FootstepHunter,
                AudioMode.UserCustom,
            });
    }
}
