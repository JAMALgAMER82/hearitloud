using System.Globalization;
using System.Text;

namespace WarzoneEQ.ConfigGenerator.Plugins;

public sealed record TdrNova : Plugin
{
    private readonly string _params;
    private readonly bool _linearPhase;

    private TdrNova(string @params, bool linearPhase = false)
    {
        _params = @params;
        _linearPhase = linearPhase;
    }

    public static TdrNova SpectralDucker(double thresholdDb, double ratio, double freqLow, double freqHigh)
    {
        var inv = CultureInfo.InvariantCulture;
        return new TdrNova(
            $"-bandA-thresh {thresholdDb.ToString("0.0", inv)} " +
            $"-bandA-ratio {ratio.ToString("0.0", inv)} " +
            $"-bandA-fLow {freqLow.ToString("0", inv)} " +
            $"-bandA-fHigh {freqHigh.ToString("0", inv)}");
    }

    public static TdrNova TransientShaper(double freqHz, double gainDb, double q)
    {
        var inv = CultureInfo.InvariantCulture;
        var sign = gainDb >= 0 ? "+" : "";
        return new TdrNova(
            $"-bandB-freq {freqHz.ToString("0", inv)} " +
            $"-bandB-gain {sign}{gainDb.ToString("0.0", inv)} " +
            $"-bandB-Q {q.ToString("0.0##", inv)}");
    }

    public TdrNova WithLinearPhase() => new(_params, linearPhase: true);

    public override string ToConfigLine()
    {
        var sb = new StringBuilder("Plugin: \"TDR Nova\" ").Append(_params);
        if (_linearPhase) sb.Append(" -mode linear-phase");
        return sb.ToString();
    }
}
