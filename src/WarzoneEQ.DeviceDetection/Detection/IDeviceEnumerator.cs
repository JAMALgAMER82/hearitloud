using WarzoneEQ.DeviceDetection.Models;

namespace WarzoneEQ.DeviceDetection.Detection;

public interface IDeviceEnumerator
{
    IReadOnlyList<AudioDevice> EnumeratePlaybackDevices();
}
