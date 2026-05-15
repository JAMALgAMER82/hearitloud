using WarzoneEQ.WindowsIntegration.EqApo;

namespace WarzoneEQ.WindowsIntegration.Diagnostics.Checks;

// VST plugins (TDR Nova, LoudMax) are optional. Their absence is a warning,
// not a failure — the --basic chain works without them.
public sealed class VstPluginsCheck : IDiagnosticCheck
{
    private readonly IEqApoLocator _locator;
    private readonly IFileSystem _fs;

    public VstPluginsCheck(IEqApoLocator locator, IFileSystem fs)
    {
        _locator = locator;
        _fs = fs;
    }

    public DiagnosticResult Run()
    {
        if (!_locator.IsInstalled)
        {
            return DiagnosticResult.Warn(
                "vst.plugins",
                "VST plugins (TDR Nova + LoudMax)",
                "Skipped — Equalizer APO is not installed.");
        }

        var vstDir = Path.Combine(Path.GetDirectoryName(_locator.ConfigDirectory.TrimEnd(Path.DirectorySeparatorChar))!, "VSTPlugins");
        if (!_fs.DirectoryExists(vstDir))
        {
            return DiagnosticResult.Warn(
                "vst.plugins",
                "VST plugins (TDR Nova + LoudMax)",
                "No VSTPlugins directory in the Equalizer APO install — running on the basic chain.",
                manualFix: "Optional: install TDR Nova (https://www.tokyodawn.net) and LoudMax (https://loudmax.blogspot.com) into the EqualizerAPO\\VSTPlugins folder for the full chain.");
        }

        var files = _fs.EnumerateFiles(vstDir, "*.dll").Select(Path.GetFileName).ToList();
        bool hasTdrNova = files.Any(f => f!.Contains("TDR Nova", StringComparison.OrdinalIgnoreCase));
        bool hasLoudMax = files.Any(f => f!.Contains("LoudMax", StringComparison.OrdinalIgnoreCase));

        if (hasTdrNova && hasLoudMax)
        {
            return DiagnosticResult.Ok(
                "vst.plugins",
                "VST plugins (TDR Nova + LoudMax)",
                "Both TDR Nova and LoudMax are present.");
        }

        var missing = new List<string>();
        if (!hasTdrNova) missing.Add("TDR Nova");
        if (!hasLoudMax) missing.Add("LoudMax");

        return DiagnosticResult.Warn(
            "vst.plugins",
            "VST plugins (TDR Nova + LoudMax)",
            $"Missing: {string.Join(", ", missing)}. The chain will fall back to basic mode.",
            manualFix: $"Optional: install {string.Join(" and ", missing)} into {vstDir} for the full chain.");
    }
}
