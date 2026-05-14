using FluentAssertions;
using WarzoneEQ.DeviceDetection.Matching;
using WarzoneEQ.DeviceDetection.Models;
using Xunit;

namespace WarzoneEQ.DeviceDetection.Tests.Matching;

public class DeviceMatcherTests
{
    private readonly DeviceMatcher _matcher = new(DeviceDatabase.LoadEmbedded());

    [Fact]
    public void Usb_device_matches_known_headphone()
    {
        var device = new AudioDevice(
            EndpointName: "Razer BlackShark V2 Pro",
            Kind: DeviceKind.Usb,
            UsbVid: "1532",
            UsbPid: "0517");
        var result = _matcher.Match(device);
        result.Headphone.Should().NotBeNull();
        result.Headphone!.AutoeqSlug.Should().Be("razer/BlackShark_V2_Pro");
        result.Dac.Should().BeNull();
    }

    [Fact]
    public void Usb_device_matches_known_dac()
    {
        var device = new AudioDevice(
            EndpointName: "Speakers (Sound Blaster GC7 Game)",
            Kind: DeviceKind.Usb,
            UsbVid: "041E",
            UsbPid: "3260");
        var result = _matcher.Match(device);
        result.Dac.Should().NotBeNull();
        result.Dac!.Model.Should().Be("Creative Sound Blaster GC7");
        result.Headphone.Should().BeNull();
    }

    [Fact]
    public void Bluetooth_device_matches_via_normalized_name()
    {
        var device = new AudioDevice(
            EndpointName: "Sony WH-1000XM5",
            Kind: DeviceKind.Bluetooth,
            BluetoothName: "Sony WH-1000XM5");
        var result = _matcher.Match(device);
        result.Headphone.Should().NotBeNull();
        result.Headphone!.AutoeqSlug.Should().Be("sony/WH-1000XM5");
    }

    [Fact]
    public void Unknown_device_returns_empty_match()
    {
        var device = new AudioDevice(EndpointName: "Unknown DAC", Kind: DeviceKind.Analog);
        var result = _matcher.Match(device);
        result.Headphone.Should().BeNull();
        result.Dac.Should().BeNull();
    }
}
