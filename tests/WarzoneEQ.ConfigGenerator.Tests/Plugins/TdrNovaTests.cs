using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Plugins;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Plugins;

public class TdrNovaTests
{
    [Fact]
    public void Spectral_ducker_serializes_with_band_A_params()
    {
        var plugin = TdrNova.SpectralDucker(thresholdDb: -28, ratio: 4, freqLow: 200, freqHigh: 5000);
        plugin.ToConfigLine().Should().Be(
            "Plugin: \"TDR Nova\" -bandA-thresh -28.0 -bandA-ratio 4.0 -bandA-fLow 200 -bandA-fHigh 5000");
    }

    [Fact]
    public void Transient_shaper_serializes_with_band_B_boost()
    {
        var plugin = TdrNova.TransientShaper(freqHz: 3000, gainDb: 5, q: 1.5);
        plugin.ToConfigLine().Should().Be(
            "Plugin: \"TDR Nova\" -bandB-freq 3000 -bandB-gain +5.0 -bandB-Q 1.5");
    }

    [Fact]
    public void Linear_phase_mode_appends_mode_flag()
    {
        var plugin = TdrNova.TransientShaper(freqHz: 3000, gainDb: 5, q: 1.5).WithLinearPhase();
        plugin.ToConfigLine().Should().EndWith(" -mode linear-phase");
    }
}
