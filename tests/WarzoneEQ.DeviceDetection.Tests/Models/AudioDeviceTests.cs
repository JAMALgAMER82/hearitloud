using FluentAssertions;
using WarzoneEQ.DeviceDetection.Models;
using Xunit;

namespace WarzoneEQ.DeviceDetection.Tests.Models;

public class AudioDeviceTests
{
    [Fact]
    public void Usb_device_has_vid_pid()
    {
        var d = new AudioDevice(
            EndpointName: "Speakers (Sound Blaster GC7 Game)",
            Kind: DeviceKind.Usb,
            UsbVid: "041E",
            UsbPid: "3260");
        d.UsbVidPidKey.Should().Be("VID_041E&PID_3260");
    }

    [Fact]
    public void Bluetooth_device_has_name_only()
    {
        var d = new AudioDevice(
            EndpointName: "Sony WH-1000XM5",
            Kind: DeviceKind.Bluetooth,
            BluetoothName: "WH-1000XM5");
        d.UsbVidPidKey.Should().BeNull();
        d.BluetoothName.Should().Be("WH-1000XM5");
    }

    [Fact]
    public void Analog_device_has_no_identifier_beyond_endpoint_name()
    {
        var d = new AudioDevice(EndpointName: "Headphones (Realtek)", Kind: DeviceKind.Analog);
        d.UsbVidPidKey.Should().BeNull();
        d.BluetoothName.Should().BeNull();
    }
}
