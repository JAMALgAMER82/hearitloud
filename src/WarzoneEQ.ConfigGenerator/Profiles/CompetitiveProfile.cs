using System.Text;
using WarzoneEQ.ConfigGenerator.Channels;
using WarzoneEQ.ConfigGenerator.Filters;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.ConfigGenerator.Plugins;

namespace WarzoneEQ.ConfigGenerator.Profiles;

public sealed class CompetitiveProfile : IProfileGenerator
{
    public string Generate(ProfileInput input)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# warzone\\current.txt — Competitive mode");

        if (input.DacEndpoint.DeviceDirective is { } directive)
            sb.AppendLine(directive);

        sb.AppendLine();
        sb.AppendLine("Stage: pre-mix");

        EmitStage(sb, new ChannelStage(
            new[] { Channel.L, Channel.R },
            Filters: new[] { Filter.HighPass(80) }));

        EmitStage(sb, new ChannelStage(
            new[] { Channel.FL, Channel.FR },
            PreampDb: -3));

        EmitStage(sb, new ChannelStage(
            new[] { Channel.FC },
            PreampDb: -6,
            Plugins: input.EnableVstPlugins
                ? new Plugin[] { TdrNova.SpectralDucker(thresholdDb: -28, ratio: 4, freqLow: 200, freqHigh: 5000) }
                : null));

        EmitStage(sb, new ChannelStage(
            new[] { Channel.BL, Channel.BR, Channel.SL, Channel.SR },
            PreampDb: 2,
            Plugins: input.EnableVstPlugins
                ? new Plugin[] { TdrNova.TransientShaper(freqHz: 3000, gainDb: 5, q: 1.5) }
                : null));

        EmitStage(sb, new ChannelStage(
            new[] { Channel.LFE },
            PreampDb: -12));

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
                thresholdDb: -42, ratio: "1:2", attackMs: 5, releaseMs: 80).ToConfigLine());

        if (input.EnableVstPlugins)
            sb.AppendLine(LoudMax.Limiter(-1.0).ToConfigLine());

        return sb.ToString();
    }

    private static void EmitStage(StringBuilder sb, ChannelStage stage)
    {
        sb.AppendLine();
        foreach (var line in stage.ToConfigLines()) sb.AppendLine(line);
    }

    private static void EmitFpsCurve(StringBuilder sb, ProfileInput input)
    {
        if (Math.Abs(input.Intensity - 1.0) < 0.001)
        {
            var slug = input.FpsCurve.ToString().ToLowerInvariant();
            sb.AppendLine($@"Include: warzone\fps-curves\{slug}.txt");
            return;
        }
        sb.AppendLine($"# FPS curve {input.FpsCurve} at {input.Intensity:P0} intensity (inlined)");
        foreach (var f in Intensity.Scale(FpsCurves.Get(input.FpsCurve), input.Intensity))
            sb.AppendLine(f.ToConfigLine());
    }
}
