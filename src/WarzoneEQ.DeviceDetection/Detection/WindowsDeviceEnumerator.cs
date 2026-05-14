using System.Management;
using System.Runtime.Versioning;
using WarzoneEQ.DeviceDetection.Models;

namespace WarzoneEQ.DeviceDetection.Detection;

[SupportedOSPlatform("windows")]
public sealed class WindowsDeviceEnumerator : IDeviceEnumerator
{
    public IReadOnlyList<AudioDevice> EnumeratePlaybackDevices()
    {
        var results = new List<AudioDevice>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, DeviceID, PNPDeviceID FROM Win32_SoundDevice WHERE Status = 'OK'");
        foreach (var obj in searcher.Get())
        {
            var name = obj["Name"]?.ToString() ?? "";
            var pnpId = obj["PNPDeviceID"]?.ToString() ?? "";

            if (PnpIdParser.IsBluetooth(pnpId))
            {
                results.Add(new AudioDevice(
                    EndpointName: name,
                    Kind: DeviceKind.Bluetooth,
                    BluetoothName: name));
                continue;
            }

            var (vid, pid) = PnpIdParser.ExtractVidPid(pnpId);
            if (vid is not null && pid is not null)
            {
                results.Add(new AudioDevice(
                    EndpointName: name,
                    Kind: DeviceKind.Usb,
                    UsbVid: vid,
                    UsbPid: pid));
                continue;
            }

            results.Add(new AudioDevice(EndpointName: name, Kind: DeviceKind.Analog));
        }
        return results;
    }
}
