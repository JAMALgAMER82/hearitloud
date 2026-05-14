using FluentAssertions;
using WarzoneEQ.DeviceDetection.Detection;
using Xunit;

namespace WarzoneEQ.DeviceDetection.Tests.Detection;

public class PnpIdParserTests
{
    [Theory]
    [InlineData(@"USB\VID_041E&PID_3260&MI_00\7&123ABC", "041E", "3260")]
    [InlineData(@"USB\VID_1532&PID_0517\6&XYZ",          "1532", "0517")]
    [InlineData(@"SWD\MMDEVAPI\{0.0.0.00000000}",        null,   null)]
    [InlineData(@"BTHENUM\Dev_001122334455\6&...",       null,   null)]
    public void Parses_VID_and_PID_from_USB_pnp_id(string input, string? expectedVid, string? expectedPid)
    {
        var (vid, pid) = PnpIdParser.ExtractVidPid(input);
        vid.Should().Be(expectedVid);
        pid.Should().Be(expectedPid);
    }

    [Theory]
    [InlineData(@"BTHENUM\Dev_AABBCCDDEEFF\6&xxxxx", true)]
    [InlineData(@"USB\VID_041E&PID_3260\xxxxx",      false)]
    public void Detects_bluetooth_devices_from_pnp_id(string input, bool isBluetooth)
    {
        PnpIdParser.IsBluetooth(input).Should().Be(isBluetooth);
    }
}
