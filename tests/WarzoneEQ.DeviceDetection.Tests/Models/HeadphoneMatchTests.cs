using FluentAssertions;
using WarzoneEQ.DeviceDetection.Models;
using Xunit;

namespace WarzoneEQ.DeviceDetection.Tests.Models;

public class HeadphoneMatchTests
{
    [Fact]
    public void HeadphoneMatch_carries_model_and_autoeq_slug()
    {
        var m = new HeadphoneMatch(Model: "Sennheiser HD 600", AutoeqSlug: "sennheiser/HD_600");
        m.Model.Should().Be("Sennheiser HD 600");
        m.AutoeqSlug.Should().Be("sennheiser/HD_600");
    }

    [Fact]
    public void MultiEndpointDac_carries_game_and_voice_endpoint_names()
    {
        var dac = new MultiEndpointDac(
            Model: "Creative Sound Blaster GC7",
            GameEndpoint: "Speakers (Sound Blaster GC7 Game)",
            VoiceEndpoint: "Speakers (Sound Blaster GC7 Chat)");
        dac.GameEndpoint.Should().Be("Speakers (Sound Blaster GC7 Game)");
        dac.VoiceEndpoint.Should().Be("Speakers (Sound Blaster GC7 Chat)");
    }
}
