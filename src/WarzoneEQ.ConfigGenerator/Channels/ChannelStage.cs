using System.Globalization;
using WarzoneEQ.ConfigGenerator.Filters;
using WarzoneEQ.ConfigGenerator.Plugins;

namespace WarzoneEQ.ConfigGenerator.Channels;

public sealed record ChannelStage(
    IReadOnlyList<Channel> Channels,
    double? PreampDb = null,
    IReadOnlyList<Filter>? Filters = null,
    IReadOnlyList<Plugin>? Plugins = null)
{
    public IEnumerable<string> ToConfigLines()
    {
        yield return "Channel: " + string.Join(' ', Channels);
        if (PreampDb.HasValue)
            yield return $"Preamp: {PreampDb.Value.ToString("0.0", CultureInfo.InvariantCulture)} dB";
        foreach (var f in Filters ?? Array.Empty<Filter>())
            yield return f.ToConfigLine();
        foreach (var p in Plugins ?? Array.Empty<Plugin>())
            yield return p.ToConfigLine();
    }
}
