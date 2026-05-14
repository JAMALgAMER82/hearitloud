using System.Globalization;

namespace WarzoneEQ.ConfigGenerator.Plugins;

public sealed record ReaXcomp : Plugin
{
    private readonly string _params;
    private ReaXcomp(string @params) => _params = @params;

    public static ReaXcomp UpwardCompressor(
        int bandIndex, double freqLowHz, double freqHighHz,
        double thresholdDb, string ratio, int attackMs, int releaseMs)
    {
        var inv = CultureInfo.InvariantCulture;
        return new ReaXcomp(
            $"-band {bandIndex} " +
            $"-freq-low {freqLowHz.ToString("0", inv)} " +
            $"-freq-high {freqHighHz.ToString("0", inv)} " +
            $"-threshold {thresholdDb.ToString("0.0", inv)} " +
            $"-ratio {ratio} " +
            $"-attack {attackMs.ToString(inv)} " +
            $"-release {releaseMs.ToString(inv)}");
    }

    public override string ToConfigLine() => $"Plugin: \"ReaXcomp\" {_params}";
}
