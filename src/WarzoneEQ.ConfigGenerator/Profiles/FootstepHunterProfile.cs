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

        if (input.DacEndpoint.DeviceDirective is { } directive)
            sb.AppendLine(directive);

        sb.AppendLine();
        sb.AppendLine("Stage: pre-mix");

        // L+R get HP @ 120 plus a narrow notch at 1.2 kHz where Warzone's
        // gunshots peak — reduces gun bleed into the 2-5 kHz footstep band
        // without touching voice intelligibility (vowels live around 700 Hz).
        EmitStage(sb, new ChannelStage(
            new[] { Channel.L, Channel.R },
            Filters: new[]
            {
                Filter.HighPass(120),
                Filter.Peaking(1200, gainDb: -3, q: 4),
            }));

        EmitStage(sb, new ChannelStage(
            new[] { Channel.FL, Channel.FR },
            PreampDb: -3));

        // FC gets aggressive ducking — gunshots come dead-center in 7.1 mixes,
        // and we'd rather lose a few dB of dialog than have rifles mask the
        // footstep band. Threshold -24 dB / ratio 8:1 is intentionally harsh.
        EmitStage(sb, new ChannelStage(
            new[] { Channel.FC },
            PreampDb: -10,
            Filters: new[] { Filter.Peaking(1200, gainDb: -3, q: 4) },
            Plugins: input.EnableVstPlugins
                ? new Plugin[] { TdrNova.SpectralDucker(thresholdDb: -24, ratio: 8, freqLow: 200, freqHigh: 5000) }
                : null));

        // Rears/sides: where most footstep cues actually live. Two transient
        // shapers stacked — 3 kHz catches grass/dirt steps, 5 kHz catches
        // hard-surface scuffs (concrete, metal grates) which have more HF
        // content. Both fire only when the input is transient (TDR Nova's
        // bandwise dynamics handle the gating automatically).
        EmitStage(sb, new ChannelStage(
            new[] { Channel.BL, Channel.BR, Channel.SL, Channel.SR },
            PreampDb: 5,
            Plugins: input.EnableVstPlugins
                ? new Plugin[]
                {
                    TdrNova.TransientShaper(freqHz: 3000, gainDb: 7, q: 2.0),
                    TdrNova.TransientShaper(freqHz: 5000, gainDb: 5, q: 2.0),
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

        if (input.EnableVstPlugins && input.EnableFootstepCompressor)
            sb.AppendLine(ReaXcomp.UpwardCompressor(
                bandIndex: 1, freqLowHz: 2000, freqHighHz: 4500,
                thresholdDb: -38, ratio: "1:3", attackMs: 3, releaseMs: 60).ToConfigLine());

        if (input.EnableVstPlugins)
            sb.AppendLine(LoudMax.Limiter(-0.5).ToConfigLine());

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
