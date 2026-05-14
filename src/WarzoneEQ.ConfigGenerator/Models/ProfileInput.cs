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

    public string HrirIncludePath { get; init; } = @"warzone\hrir\hesuvi-active.wav";
}
