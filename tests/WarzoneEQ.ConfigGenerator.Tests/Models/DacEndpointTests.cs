using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Models;

public class DacEndpointTests
{
    [Fact]
    public void Constructs_with_endpoint_name()
    {
        var dac = new DacEndpoint("Speakers Sound Blaster GC7 Game");
        dac.DeviceDirective.Should().Be("Device: Speakers Sound Blaster GC7 Game");
    }

    [Fact]
    public void Default_endpoint_omits_Device_directive()
    {
        DacEndpoint.WindowsDefault.DeviceDirective.Should().BeNull();
    }
}
