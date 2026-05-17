using System.Text;
using WarzoneEQ.ConfigGenerator.Models;

namespace WarzoneEQ.ConfigGenerator.Profiles;

// Generated when the user designs a chain in the visual EQ editor. We don't
// inject any of the FootstepHunter / Cinematic chain shaping — the user's
// filter list is the entire spec. Optional HRIR Include + headphone correction
// Include still apply so the binaural virtualization works, since those are
// post-mix steps the user wouldn't normally draw by hand.
public sealed class UserCustomProfile : IProfileGenerator
{
    public string Generate(ProfileInput input)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# warzone\\current.txt — User custom chain (visual EQ editor)");

        if (input.DacEndpoint.DeviceDirective is { } directive)
            sb.AppendLine(directive);

        // v1.10.7: see CompetitiveProfile for the rationale.
        sb.AppendLine(WarzoneProcesses.IfLine);

        sb.AppendLine();
        sb.AppendLine("Stage: pre-mix");
        sb.AppendLine();

        foreach (var f in input.UserFilters)
            sb.AppendLine(f.ToConfigLine());

        sb.AppendLine();
        sb.AppendLine("Stage: post-mix");
        sb.AppendLine();

        if (input.EnableHrirInclude)
            sb.AppendLine($"Include: {input.HrirIncludePath}");
        if (input.HeadphoneCorrection.IncludePath is { } hcPath)
            sb.AppendLine($"Include: {hcPath}");

        sb.AppendLine(WarzoneProcesses.EndIfLine);
        return sb.ToString();
    }
}
