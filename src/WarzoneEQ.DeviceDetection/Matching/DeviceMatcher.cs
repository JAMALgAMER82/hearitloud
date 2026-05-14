using WarzoneEQ.DeviceDetection.Detection;
using WarzoneEQ.DeviceDetection.Models;

namespace WarzoneEQ.DeviceDetection.Matching;

public sealed record MatchResult(HeadphoneMatch? Headphone, MultiEndpointDac? Dac);

public sealed class DeviceMatcher
{
    private readonly DeviceDatabase _db;
    public DeviceMatcher(DeviceDatabase db) => _db = db;

    public MatchResult Match(AudioDevice device)
    {
        if (device.Kind == DeviceKind.Usb && device.UsbVidPidKey is { } key)
        {
            var dac = _db.LookupDacByVidPid(key);
            if (dac is not null) return new MatchResult(null, dac);
            var hp = _db.LookupHeadphoneByVidPid(key);
            return new MatchResult(hp, null);
        }

        if (device.Kind == DeviceKind.Bluetooth && device.BluetoothName is { } btName)
        {
            var normalized = BluetoothNameNormalizer.Normalize(btName);
            var hp = _db.LookupHeadphoneByBluetoothName(normalized);
            return new MatchResult(hp, null);
        }

        return new MatchResult(null, null);
    }
}
