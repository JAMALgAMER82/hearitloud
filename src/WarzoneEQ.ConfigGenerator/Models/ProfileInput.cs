namespace WarzoneEQ.ConfigGenerator.Models;

public sealed record ProfileInput(AudioMode Mode)
{
    public FpsCurveName FpsCurve { get; init; } = FpsCurveName.Moderate;

    private double _intensity = 1.0;
    public double Intensity
    {
        get => _intensity;
        init
        {
            if (value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(nameof(value), "Intensity must be in [0, 1].");
            _intensity = value;
        }
    }

    public HeadphoneCorrection HeadphoneCorrection { get; init; } = HeadphoneCorrection.None;
    public DacEndpoint DacEndpoint { get; init; } = DacEndpoint.WindowsDefault;

    public bool EnableFootstepCompressor { get; init; } = true;
    public bool EnableLinearPhase { get; init; } = false;
    public bool EnableAdaptiveLoudness { get; init; } = false;
    public bool EnablePolyverseWider { get; init; } = false;

    /// <summary>
    /// When false, the generated config omits Plugin: lines (TDR Nova, ReaXcomp,
    /// LoudMax, Polyverse Wider). Use this on machines where the VST plugins
    /// aren't installed - EQ APO would log warnings about missing plugins
    /// otherwise. The Filter / Preamp / Include lines still work and provide
    /// the core EQ shaping.
    /// </summary>
    public bool EnableVstPlugins { get; init; } = true;

    /// <summary>
    /// When false, the HRIR Include line is omitted. Use this when HeSuVi
    /// isn't installed - EQ APO logs a warning for missing Include targets.
    /// </summary>
    public bool EnableHrirInclude { get; init; } = true;

    public string HrirIncludePath { get; init; } = @"warzone\hrir\hesuvi-active.wav";

    /// <summary>
    /// Filters from the visual EQ editor. Consumed by UserCustomProfile.
    /// Empty for all other modes.
    /// </summary>
    public IReadOnlyList<WarzoneEQ.ConfigGenerator.Filters.Filter> UserFilters { get; init; }
        = Array.Empty<WarzoneEQ.ConfigGenerator.Filters.Filter>();
}
