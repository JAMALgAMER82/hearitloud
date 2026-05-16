namespace WarzoneEQ.ConfigGenerator.Models;

// Per-plugin parameter overrides supplied by the GUI's Plugin Control card.
// When attached to a ProfileInput, the relevant profile generator (currently
// FootstepHunter) reads these values instead of its hardcoded defaults.
//
// All properties are nullable: null = "use the profile's built-in default",
// non-null = "use this exact value". This lets the user override only the
// knobs they care about without re-typing the whole chain.
public sealed record PluginOverrides
{
    // TDR Nova spectral ducker on the FC channel.
    public double? FcDuckerThresholdDb { get; init; }   // e.g. -22 (default)
    public double? FcDuckerRatio       { get; init; }   // e.g. 10 (default)

    // The three stacked transient shapers on rears/sides (each modulates the
    // 3 / 5 / 6.5 kHz bands respectively). Only the gain is exposed — freq
    // and Q are fixed by the profile to keep the UI tractable.
    public double? RearShaper3kHzGainDb  { get; init; }  // e.g. +8 (default)
    public double? RearShaper5kHzGainDb  { get; init; }  // e.g. +6 (default)
    public double? RearShaper6_5kHzGainDb { get; init; } // e.g. +4 (default)

    // ReaXcomp upward compressor in the footstep band.
    public double? FootstepCompThresholdDb { get; init; } // e.g. -38 (default)
    public string? FootstepCompRatio       { get; init; } // e.g. "1:3" (default)

    // LoudMax brick-wall limiter ceiling.
    public double? LimiterCeilingDb { get; init; }       // e.g. -0.5 (default)

    // Per-plugin enable toggles. Null = use the profile default
    // (which depends on input.EnableVstPlugins / EnableFootstepCompressor).
    public bool? FcDuckerEnabled       { get; init; }
    public bool? RearShapersEnabled    { get; init; }
    public bool? FootstepCompEnabled   { get; init; }
    public bool? LimiterEnabled        { get; init; }
}
