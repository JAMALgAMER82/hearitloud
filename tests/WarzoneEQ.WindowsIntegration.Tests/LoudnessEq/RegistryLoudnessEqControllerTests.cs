using FluentAssertions;
using WarzoneEQ.WindowsIntegration.LoudnessEq;
using Xunit;

namespace WarzoneEQ.WindowsIntegration.Tests.LoudnessEq;

public class RegistryLoudnessEqControllerTests
{
    private const string TestEndpointGuid = "{abc12345-0000-0000-0000-000000000000}";

    [Fact]
    public void Read_absent_endpoint_returns_null()
    {
        var controller = new RegistryLoudnessEqController(new FakeRegistry());
        controller.Read(TestEndpointGuid).Should().BeNull();
    }

    [Fact]
    public void Round_trip_write_then_read_preserves_state()
    {
        var registry = new FakeRegistry();
        var controller = new RegistryLoudnessEqController(registry);
        controller.Write(TestEndpointGuid, new LoudnessEqState(Enabled: true, ReleaseTime: 2));
        controller.Read(TestEndpointGuid).Should().Be(new LoudnessEqState(true, 2));
    }

    [Fact]
    public void Write_clamps_release_time_to_valid_range()
    {
        var registry = new FakeRegistry();
        var controller = new RegistryLoudnessEqController(registry);
        controller.Write(TestEndpointGuid, new LoudnessEqState(Enabled: true, ReleaseTime: 99));
        controller.Read(TestEndpointGuid)!.ReleaseTime.Should().Be(LoudnessEqState.MaxReleaseTime);
    }

    [Fact]
    public void Write_clamps_release_time_below_minimum()
    {
        var registry = new FakeRegistry();
        var controller = new RegistryLoudnessEqController(registry);
        controller.Write(TestEndpointGuid, new LoudnessEqState(Enabled: false, ReleaseTime: 0));
        controller.Read(TestEndpointGuid)!.ReleaseTime.Should().Be(LoudnessEqState.MinReleaseTime);
    }

    [Fact]
    public void Enabled_flag_round_trips_false()
    {
        var registry = new FakeRegistry();
        var controller = new RegistryLoudnessEqController(registry);
        controller.Write(TestEndpointGuid, new LoudnessEqState(Enabled: false, ReleaseTime: 4));
        controller.Read(TestEndpointGuid).Should().Be(new LoudnessEqState(false, 4));
    }
}
