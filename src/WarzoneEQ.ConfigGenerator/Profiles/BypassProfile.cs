using System.Text;
using WarzoneEQ.ConfigGenerator.Models;

namespace WarzoneEQ.ConfigGenerator.Profiles;

public sealed class BypassProfile : IProfileGenerator
{
    public string Generate(ProfileInput input)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# warzone\\current.txt — Bypass mode (chain disabled)");
        if (input.DacEndpoint.DeviceDirective is { } directive)
            sb.AppendLine(directive);
        return sb.ToString();
    }
}
