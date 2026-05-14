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

    public HeadphoneMatch? LookupHeadphoneByBluetoothName(string btName)
        => _headphonesByBtName.TryGetValue(btName, out var m) ? m : null;

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

        var headphones = new Dictionary<string, HeadphoneMatch>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in root.GetProperty("headphones").EnumerateObject())
        {
            var v = entry.Value;
            headphones[entry.Name] = new HeadphoneMatch(
                v.GetProperty("model").GetString()!,
                v.GetProperty("autoeq_slug").GetString()!);
        }

        var dacs = new Dictionary<string, MultiEndpointDac>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in root.GetProperty("multi_endpoint_dacs").EnumerateObject())
        {
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
                bt[entry.Name] = new HeadphoneMatch(entry.Name, entry.Value.GetString()!);
            }
        }

        return new DeviceDatabase(headphones, dacs, bt);
    }
}
