using System.Text;
using WarzoneEQ.ConfigGenerator.Channels;
using WarzoneEQ.ConfigGenerator.Filters;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.ConfigGenerator.Plugins;

namespace WarzoneEQ.ConfigGenerator.Profiles;

// Maximum-priority footstep-hunting chain. Trades cinematic balance for
// raw positional clarity: heavier HP to kill rumble, FC ducked harder so
// gunfire/dialog don't mask the 2-5 kHz footstep band, rears/sides
// boosted because most footstep cues arrive off-axis, LFE killed, sharper
// transient shaper.
public sealed class FootstepHunterProfile : IProfileGenerator
{
    public string Generate(ProfileInput input)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# warzone\\current.txt — FootstepHunter mode (max positional clarity)");

        // Resolve plugin parameter overrides: null fields fall through to the
        // profile's tuned-for-Warzone defaults.
        var o = input.PluginOverrides;
        double fcDuckThresh   = o?.FcDuckerThresholdDb     ?? -22;
        double fcDuckRatio    = o?.FcDuckerRatio           ?? 10;
        double shaper3kGain   = o?.RearShaper3kHzGainDb    ?? 8;
        double shaper5kGain   = o?.RearShaper5kHzGainDb    ?? 6;
        double shaper65kGain  = o?.RearShaper6_5kHzGainDb  ?? 4;
        double compThresh     = o?.FootstepCompThresholdDb ?? -38;
        string compRatio      = o?.FootstepCompRatio       ?? "1:3";
        double limCeiling     = o?.LimiterCeilingDb        ?? -0.5;
        bool fcDuckEnabled    = (o?.FcDuckerEnabled       ?? true) && input.EnableVstPlugins;
        bool shapersEnabled   = (o?.RearShapersEnabled    ?? true) && input.EnableVstPlugins;
        bool compEnabled      = (o?.FootstepCompEnabled   ?? input.EnableFootstepCompressor) && input.EnableVstPlugins;
        bool limEnabled       = (o?.LimiterEnabled        ?? true) && input.EnableVstPlugins;

        if (input.DacEndpoint.DeviceDirective is { } directive)
            sb.AppendLine(directive);

        sb.AppendLine();
        sb.AppendLine("Stage: pre-mix");

        // L+R: tighter HP (150 Hz kills more low rumble that masks footsteps),
        // two surgical notches at the gunshot fundamental (1.2 kHz) and the
        // explosion/wind low-mid mush (300 Hz). Both narrow Q=5 so voice
        // formants (700 Hz vowels, 2 kHz sibilants) are untouched.
        EmitStage(sb, new ChannelStage(
            new[] { Channel.L, Channel.R },
            Filters: new[]
            {
                Filter.HighPass(150),
                Filter.Peaking(300, gainDb: -4, q: 5),
                Filter.Peaking(1200, gainDb: -5, q: 5),
            }));

        // FL/FR pushed harder (-5 dB instead of -3): in 7.1 mixes the front
        // pair carries most of the gunfire-direction cues. We want the user's
        // attention pulled toward the lateral channels where footsteps live.
        EmitStage(sb, new ChannelStage(
            new[] { Channel.FL, Channel.FR },
            PreampDb: -5));

        // FC gets the heaviest ducking — dialog and gunshots both live dead-
        // center in 7.1 mixes. Threshold -22 dB / ratio 10:1 is intentionally
        // brutal: it costs some dialog intelligibility to make footsteps
        // pop through cleanly. Plus the gunshot notch on the FC channel too.
        EmitStage(sb, new ChannelStage(
            new[] { Channel.FC },
            PreampDb: -12,
            Filters: new[] { Filter.Peaking(1200, gainDb: -5, q: 5) },
            Plugins: fcDuckEnabled
                ? new Plugin[] { TdrNova.SpectralDucker(thresholdDb: fcDuckThresh, ratio: fcDuckRatio, freqLow: 200, freqHigh: 5000) }
                : null));

        // Rears/sides: every footstep cue we can extract lives here. Stack
        // THREE transient shapers across the footstep frequency range so
        // every surface type gets sharpened — grass/dirt (3 kHz), hard-surface
        // scuffs (5 kHz), metallic snap (6.5 kHz). Plus a broad high-shelf
        // boost above 2 kHz to push the whole footstep band forward on the
        // lateral channels.
        EmitStage(sb, new ChannelStage(
            new[] { Channel.BL, Channel.BR, Channel.SL, Channel.SR },
            PreampDb: 6,
            Filters: new[] { Filter.HighShelf(2000, gainDb: 4) },
            Plugins: shapersEnabled
                ? new Plugin[]
                {
                    TdrNova.TransientShaper(freqHz: 3000, gainDb: shaper3kGain, q: 2.0),
                    TdrNova.TransientShaper(freqHz: 5000, gainDb: shaper5kGain, q: 2.0),
                    TdrNova.TransientShaper(freqHz: 6500, gainDb: shaper65kGain, q: 2.5),
                }
                : null));

        // LFE practically muted — sub-bass masks footsteps and isn't directional anyway.
        EmitStage(sb, new ChannelStage(
            new[] { Channel.LFE },
            PreampDb: -40));

        sb.AppendLine();
        sb.AppendLine("Stage: post-mix");
        sb.AppendLine();

        if (input.EnableHrirInclude)
            sb.AppendLine($"Include: {input.HrirIncludePath}");
        if (input.HeadphoneCorrection.IncludePath is { } hcPath)
            sb.AppendLine($"Include: {hcPath}");

        EmitFpsCurve(sb, input);

        if (compEnabled)
            sb.AppendLine(ReaXcomp.UpwardCompressor(
                bandIndex: 1, freqLowHz: 2000, freqHighHz: 4500,
                thresholdDb: compThresh, ratio: compRatio, attackMs: 3, releaseMs: 60).ToConfigLine());

        if (limEnabled)
            sb.AppendLine(LoudMax.Limiter(limCeiling).ToConfigLine());

        return sb.ToString();
    }

    private static void EmitStage(StringBuilder sb, ChannelStage stage)
    {
        sb.AppendLine();
        foreach (var line in stage.ToConfigLines()) sb.AppendLine(line);
    }

    private static void EmitFpsCurve(StringBuilder sb, ProfileInput input)
    {
        // FootstepHunter forces Aggressive curve at full intensity regardless of input.
        var curve = FpsCurveName.Aggressive;
        if (Math.Abs(input.Intensity - 1.0) < 0.001)
        {
            sb.AppendLine($@"Include: warzone\fps-curves\{curve.ToString().ToLowerInvariant()}.txt");
            return;
        }
        sb.AppendLine($"# FPS curve {curve} at {input.Intensity:P0} intensity (inlined)");
        foreach (var f in Intensity.Scale(FpsCurves.Get(curve), input.Intensity))
            sb.AppendLine(f.ToConfigLine());
    }
}
