using WarzoneEQ.DeviceDetection.Detection;
using WarzoneEQ.DeviceDetection.Matching;
using WarzoneEQ.DeviceDetection.Models;

namespace WarzoneEQ.DeviceDetection;

public sealed record DetectionSnapshot(
    IReadOnlyList<AudioDevice> Devices,
    HeadphoneMatch? PrimaryHeadphone,
    MultiEndpointDac? MultiEndpointDac);

public sealed class DeviceDetectionService
{
    private readonly IDeviceEnumerator _enumerator;
    private readonly DeviceMatcher _matcher;

    public DeviceDetectionService(IDeviceEnumerator enumerator, DeviceMatcher matcher)
    {
        _enumerator = enumerator;
        _matcher = matcher;
    }

    public DetectionSnapshot Snapshot()
    {
        var devices = _enumerator.EnumeratePlaybackDevices();
        HeadphoneMatch? headphone = null;
        MultiEndpointDac? dac = null;

        foreach (var d in devices)
        {
            var match = _matcher.Match(d);
            headphone ??= match.Headphone;
            dac ??= match.Dac;
        }

        return new DetectionSnapshot(devices, headphone, dac);
    }
}
