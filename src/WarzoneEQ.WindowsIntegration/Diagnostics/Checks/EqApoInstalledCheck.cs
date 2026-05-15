using WarzoneEQ.WindowsIntegration.EqApo;

namespace WarzoneEQ.WindowsIntegration.Diagnostics.Checks;

public sealed class EqApoInstalledCheck : IDiagnosticCheck
{
    private readonly IEqApoLocator _locator;
    public EqApoInstalledCheck(IEqApoLocator locator) => _locator = locator;

    public DiagnosticResult Run()
    {
        if (!_locator.IsInstalled)
        {
            return DiagnosticResult.Fail(
                "eqapo.installed",
                "Equalizer APO installed",
                "Equalizer APO is not installed (registry key HKLM\\SOFTWARE\\EqualizerAPO missing).",
                manualFix: "Re-run the Hear It Loud installer, or download Equalizer APO from https://sourceforge.net/projects/equalizerapo/ and reboot.");
        }
        return DiagnosticResult.Ok(
            "eqapo.installed",
            "Equalizer APO installed",
            $"Found at {_locator.ConfigDirectory}.");
    }
}
