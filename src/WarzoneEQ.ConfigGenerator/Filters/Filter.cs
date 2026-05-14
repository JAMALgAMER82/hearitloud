using System.Globalization;
using System.Text;

namespace WarzoneEQ.ConfigGenerator.Filters;

public sealed record Filter(FilterType Type, double FrequencyHz, double? GainDb = null, double? Q = null)
{
    public static Filter HighPass(double freqHz) => new(FilterType.HP, freqHz);
    public static Filter LowPass(double freqHz) => new(FilterType.LP, freqHz);
    public static Filter Peaking(double freqHz, double gainDb, double q) => new(FilterType.PK, freqHz, gainDb, q);
    public static Filter LowShelf(double freqHz, double gainDb) => new(FilterType.LS, freqHz, gainDb);
    public static Filter HighShelf(double freqHz, double gainDb) => new(FilterType.HS, freqHz, gainDb);

    public Filter WithGain(double newGainDb) => this with { GainDb = newGainDb };

    public string ToConfigLine()
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append($"Filter: ON {Type} Fc {FrequencyHz.ToString("0.###", inv)} Hz");
        if (GainDb.HasValue)
            sb.Append($" Gain {(GainDb.Value >= 0 ? "+" : "")}{GainDb.Value.ToString("0.0", inv)} dB");
        if (Q.HasValue)
            sb.Append($" Q {Q.Value.ToString("0.0##", inv)}");
        return sb.ToString();
    }
}
