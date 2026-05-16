using System.Text.Json;
using WarzoneEQ.DeviceDetection.Models;

namespace WarzoneEQ.DeviceDetection.Matching;

public sealed class DeviceDatabase
{
    private readonly IReadOnlyDictionary<string, HeadphoneMatch> _headphonesByVidPid;
    private readonly IReadOnlyDictionary<string, MultiEndpointDac> _dacsByVidPid;
    private readonly IReadOnlyDictionary<string, HeadphoneMatch> _headphonesByBtName;

    public int HeadphoneCount => _headphonesByVidPid.Count + _headphonesByBtName.Count;
    public int MultiEndpointDacCount => _dacsByVidPid.Count;

    private DeviceDatabase(
        IReadOnlyDictionary<string, HeadphoneMatch> headphonesByVidPid,
        IReadOnlyDictionary<string, MultiEndpointDac> dacsByVidPid,
        IReadOnlyDictionary<string, HeadphoneMatch> headphonesByBtName)
    {
        _headphonesByVidPid = headphonesByVidPid;
        _dacsByVidPid = dacsByVidPid;
        _headphonesByBtName = headphonesByBtName;
    }

    public HeadphoneMatch? LookupHeadphoneByVidPid(string vidPidKey)
        => _headphonesByVidPid.TryGetValue(vidPidKey, out var m) ? m : null;

    public MultiEndpointDac? LookupDacByVidPid(string vidPidKey)
        => _dacsByVidPid.TryGetValue(vidPidKey, out var d) ? d : null;

    // BT device names in Windows arrive in many variants: bare model
    // ("WH-1000XM5"), with manufacturer prefix ("Sony WH-1000XM5"), or with
    // a connection-mode suffix ("WH-1000XM5 - Hands-Free AG"). Substring
    // matching against the registered patterns is dramatically more reliable
    // than the original exact match — one entry covers every variant.
    //
    // Longest pattern wins, so "Cloud III Wireless" beats "Cloud III" when
    // the device name contains both.
    public HeadphoneMatch? LookupHeadphoneByBluetoothName(string btName)
    {
        if (string.IsNullOrEmpty(btName)) return null;
        if (_headphonesByBtName.TryGetValue(btName, out var exact)) return exact;
        HeadphoneMatch? best = null;
        int bestLen = 0;
        foreach (var (pattern, match) in _headphonesByBtName)
        {
            if (pattern.Length <= bestLen) continue;
            if (btName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                best = match;
                bestLen = pattern.Length;
            }
        }
        return best;
    }

    private static bool IsCommentKey(string key) =>
        key.StartsWith('_') || key.StartsWith("//");

    public static DeviceDatabase LoadEmbedded()
    {
        var assembly = typeof(DeviceDatabase).Assembly;
        var resourceName = "WarzoneEQ.DeviceDetection.Resources.vidpid-overlay.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        return LoadFromStream(stream);
    }

    public static DeviceDatabase LoadFromStream(Stream stream)
    {
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        // JSON comment-keys (anything starting with "_" or "//") and any
        // non-object values are skipped so the human-readable section
        // headers in vidpid-overlay.json don't break the loader.
        var headphones = new Dictionary<string, HeadphoneMatch>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in root.GetProperty("headphones").EnumerateObject())
        {
            if (IsCommentKey(entry.Name) || entry.Value.ValueKind != JsonValueKind.Object) continue;
            var v = entry.Value;
            headphones[entry.Name] = new HeadphoneMatch(
                v.GetProperty("model").GetString()!,
                v.GetProperty("autoeq_slug").GetString()!);
        }

        var dacs = new Dictionary<string, MultiEndpointDac>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in root.GetProperty("multi_endpoint_dacs").EnumerateObject())
        {
            if (IsCommentKey(entry.Name) || entry.Value.ValueKind != JsonValueKind.Object) continue;
            var v = entry.Value;
            dacs[entry.Name] = new MultiEndpointDac(
                v.GetProperty("model").GetString()!,
                v.GetProperty("game_endpoint").GetString()!,
                v.GetProperty("voice_endpoint").GetString()!);
        }

        var bt = new Dictionary<string, HeadphoneMatch>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("bluetooth_names", out var btSection))
        {
            foreach (var entry in btSection.EnumerateObject())
            {
                if (IsCommentKey(entry.Name) || entry.Value.ValueKind != JsonValueKind.String) continue;
                bt[entry.Name] = new HeadphoneMatch(entry.Name, entry.Value.GetString()!);
            }
        }

        return new DeviceDatabase(headphones, dacs, bt);
    }
}
