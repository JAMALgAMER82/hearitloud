namespace WarzoneEQ.WindowsIntegration.LoudnessEq;

public sealed record LoudnessEqState(bool Enabled, int ReleaseTime)
{
    public const int MinReleaseTime = 2;
    public const int MaxReleaseTime = 7;

    public LoudnessEqState Clamp() => this with
    {
        ReleaseTime = Math.Clamp(ReleaseTime, MinReleaseTime, MaxReleaseTime),
    };
}
