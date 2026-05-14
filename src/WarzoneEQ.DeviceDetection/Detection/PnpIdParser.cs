using System.Text.RegularExpressions;

namespace WarzoneEQ.DeviceDetection.Detection;

public static class PnpIdParser
{
    private static readonly Regex VidPidPattern =
        new(@"VID_([0-9A-Fa-f]{4})&PID_([0-9A-Fa-f]{4})", RegexOptions.Compiled);

    public static (string? Vid, string? Pid) ExtractVidPid(string? pnpId)
    {
        if (string.IsNullOrEmpty(pnpId)) return (null, null);
        var m = VidPidPattern.Match(pnpId);
        return m.Success
            ? (m.Groups[1].Value.ToUpperInvariant(), m.Groups[2].Value.ToUpperInvariant())
            : (null, null);
    }

    public static bool IsBluetooth(string? pnpId)
        => !string.IsNullOrEmpty(pnpId) && pnpId.StartsWith("BTHENUM", StringComparison.OrdinalIgnoreCase);
}
