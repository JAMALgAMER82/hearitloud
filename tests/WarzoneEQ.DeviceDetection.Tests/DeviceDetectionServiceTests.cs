using FluentAssertions;
using WarzoneEQ.DeviceDetection.Matching;
using WarzoneEQ.DeviceDetection.Models;
using WarzoneEQ.DeviceDetection.Tests.Detection;
using Xunit;

namespace WarzoneEQ.DeviceDetection.Tests;

public class DeviceDetectionServiceTests
{
    private static DeviceDetectionService BuildService(params AudioDevice[] devices)
        => new(new FakeDeviceEnumerator(devices), new DeviceMatcher(DeviceDatabase.LoadEmbedded()));

    [Fact]
    public void Detects_no_devices_returns_empty_result()
    {
        var svc = BuildService();
        var snapshot = svc.Snapshot();
        snapshot.Devices.Should().BeEmpty();
        snapshot.PrimaryHeadphone.Should().BeNull();
        snapshot.MultiEndpointDac.Should().BeNull();
    }

    [Fact]
    public void Detects_GC7_and_picks_it_as_DAC()
    {
        var svc = BuildService(
            new AudioDevice(
                EndpointName: "Speakers (Sound Blaster GC7 Game)",
                Kind: DeviceKind.Usb,
                UsbVid: "041E",
                UsbPid: "3260"),
            new AudioDevice(
                EndpointName: "Speakers (Sound Blaster GC7 Chat)",
                Kind: DeviceKind.Usb,
                UsbVid: "041E",
                UsbPid: "3260"));
        var snap = svc.Snapshot();
        snap.MultiEndpointDac.Should().NotBeNull();
        snap.MultiEndpointDac!.Model.Should().Be("Creative Sound Blaster GC7");
    }

    [Fact]
    public void Detects_known_headphone_via_VID_PID()
    {
        var svc = BuildService(new AudioDevice(
            EndpointName: "Razer BlackShark V2 Pro",
            Kind: DeviceKind.Usb,
            UsbVid: "1532",
            UsbPid: "0517"));
        var snap = svc.Snapshot();
        snap.PrimaryHeadphone.Should().NotBeNull();
        snap.PrimaryHeadphone!.AutoeqSlug.Should().Be("razer/BlackShark_V2_Pro");
    }

    [Fact]
    public void Detects_known_bluetooth_headphone()
    {
        var svc = BuildService(new AudioDevice(
            EndpointName: "Sony WH-1000XM5",
            Kind: DeviceKind.Bluetooth,
            BluetoothName: "Sony WH-1000XM5"));
        var snap = svc.Snapshot();
        snap.PrimaryHeadphone.Should().NotBeNull();
        snap.PrimaryHeadphone!.AutoeqSlug.Should().Be("sony/WH-1000XM5");
    }
}
