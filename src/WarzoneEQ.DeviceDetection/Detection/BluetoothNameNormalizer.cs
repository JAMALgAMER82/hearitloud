using System.Text.RegularExpressions;

namespace WarzoneEQ.DeviceDetection.Detection;

public static class BluetoothNameNormalizer
{
    private static readonly string[] VendorPrefixes =
    {
        "Sony ", "Sennheiser ", "Bose ", "Apple ", "Microsoft ",
    };

    private static readonly Regex TrailingMarker = new(@"\s*[\(_].*$", RegexOptions.Compiled);

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var s = input.Trim();
        foreach (var prefix in VendorPrefixes)
            if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                s = s[prefix.Length..];
        s = TrailingMarker.Replace(s, "").Trim();
        return s;
    }
}
