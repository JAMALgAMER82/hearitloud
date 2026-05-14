namespace WarzoneEQ.ConfigGenerator.Models;

public sealed record HeadphoneCorrection
{
    public string? IncludePath { get; }

    public HeadphoneCorrection(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Slug must be non-empty.", nameof(slug));
        IncludePath = $@"warzone\headphone-correction\{slug}.txt";
    }

    private HeadphoneCorrection() => IncludePath = null;

    public static readonly HeadphoneCorrection None = new();
}
