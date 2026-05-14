using WarzoneEQ.ConfigGenerator.Models;

namespace WarzoneEQ.ConfigGenerator.Profiles;

public interface IProfileGenerator
{
    string Generate(ProfileInput input);
}
