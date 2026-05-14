using WarzoneEQ.DeviceDetection.Detection;
using WarzoneEQ.DeviceDetection.Models;

namespace WarzoneEQ.DeviceDetection.Tests.Detection;

public sealed class FakeDeviceEnumerator : IDeviceEnumerator
{
    private readonly IReadOnlyList<AudioDevice> _devices;
    public FakeDeviceEnumerator(params AudioDevice[] devices) => _devices = devices;
    public IReadOnlyList<AudioDevice> EnumeratePlaybackDevices() => _devices;
}
