using WarzoneEQ.ConfigGenerator.Models;

namespace WarzoneEQ.WindowsIntegration.Diagnostics;

public sealed record SelfTestResult(string Name, bool Passed, string Detail);

// In-process integration check that validates the config generator can produce
// well-formed output for every AudioMode + curve + basic-mode combination.
// Run by --self-test before --auto so a bad build is caught before any disk write.
public static class SelfTest
{
    public static IReadOnlyList<SelfTestResult> Run()
    {
        var results = new List<SelfTestResult>();

        foreach (var mode in Enum.GetValues<AudioMode>())
        {
            foreach (var curve in Enum.GetValues<FpsCurveName>())
            {
                foreach (var basic in new[] { false, true })
                {
                    var name = $"Generate {mode}/{curve}/basic={basic}";
                    try
                    {
                        var input = new ProfileInput(mode)
                        {
                            FpsCurve = curve,
                            EnableVstPlugins = !basic,
                            EnableHrirInclude = !basic,
                        };
                        var output = ConfigGenerator.ConfigGenerator.Generate(input);
                        if (string.IsNullOrWhiteSpace(output))
                        {
                            results.Add(new SelfTestResult(name, false, "Output was empty."));
                            continue;
                        }
                        if (basic && (output.Contains("Plugin: \"TDR Nova\"") || output.Contains("Plugin: \"LoudMax\"")))
                        {
                            results.Add(new SelfTestResult(name, false, "Basic mode leaked a VST Plugin: line."));
                            continue;
                        }
                        // Bypass mode doesn't emit Filter or Channel lines, just a passthrough comment.
                        if (mode != AudioMode.Bypass && !output.Contains("Stage:"))
                        {
                            results.Add(new SelfTestResult(name, false, "Output is missing Stage: directives."));
                            continue;
                        }
                        results.Add(new SelfTestResult(name, true, $"{output.Length} chars"));
                    }
                    catch (Exception ex)
                    {
                        results.Add(new SelfTestResult(name, false, $"{ex.GetType().Name}: {ex.Message}"));
                    }
                }
            }
        }

        return results;
    }
}
