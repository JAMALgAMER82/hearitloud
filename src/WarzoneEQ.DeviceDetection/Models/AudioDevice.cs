namespace WarzoneEQ.DeviceDetection.Models;

public sealed record AudioDevice(
    string EndpointName,
    DeviceKind Kind,
    string? UsbVid = null,
    string? UsbPid = null,
    string? BluetoothName = null)
{
    public string? UsbVidPidKey =>
        UsbVid is not null && UsbPid is not null ? $"VID_{UsbVid}&PID_{UsbPid}" : null;
}
