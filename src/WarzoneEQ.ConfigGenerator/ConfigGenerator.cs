using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.ConfigGenerator.Profiles;

namespace WarzoneEQ.ConfigGenerator;

public static class ConfigGenerator
{
    public static string Generate(ProfileInput input) => input.Mode switch
    {
        AudioMode.Competitive => new CompetitiveProfile().Generate(input),
        AudioMode.Cinematic   => new CinematicProfile().Generate(input),
        AudioMode.Bypass      => new BypassProfile().Generate(input),
        AudioMode.FootstepHunter => new FootstepHunterProfile().Generate(input),
        _ => throw new ArgumentOutOfRangeException(nameof(input.Mode)),
    };
}
