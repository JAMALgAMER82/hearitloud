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

        EmitStage(sb, new ChannelStage(
            new[] { Channel.L, Channel.R },
            Filters: new[] { Filter.HighPass(120) }));

        EmitStage(sb, new ChannelStage(
            new[] { Channel.FL, Channel.FR },
            PreampDb: -3));

        EmitStage(sb, new ChannelStage(
            new[] { Channel.FC },
            PreampDb: -10,
            Plugins: input.EnableVstPlugins
                ? new Plugin[] { TdrNova.SpectralDucker(thresholdDb: -32, ratio: 6, freqLow: 200, freqHigh: 5000) }
                : null));

        EmitStage(sb, new ChannelStage(
            new[] { Channel.BL, Channel.BR, Channel.SL, Channel.SR },
            PreampDb: 5,
            Plugins: input.EnableVstPlugins
                ? new Plugin[] { TdrNova.TransientShaper(freqHz: 3000, gainDb: 7, q: 2.0) }
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
