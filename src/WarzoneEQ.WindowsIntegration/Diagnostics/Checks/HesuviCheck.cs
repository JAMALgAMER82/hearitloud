using WarzoneEQ.WindowsIntegration.EqApo;

namespace WarzoneEQ.WindowsIntegration.Diagnostics.Checks;

public sealed class HesuviCheck : IDiagnosticCheck
{
    private readonly IEqApoLocator _locator;
    private readonly IFileSystem _fs;

    public HesuviCheck(IEqApoLocator locator, IFileSystem fs)
    {
        _locator = locator;
        _fs = fs;
    }

    public DiagnosticResult Run()
    {
        if (!_locator.IsInstalled)
        {
            return DiagnosticResult.Warn(
                "hesuvi.installed",
                "HeSuVi (HRIR virtual surround)",
                "Skipped — Equalizer APO is not installed.");
        }

        var hrirDir = Path.Combine(_locator.ConfigDirectory, "HeSuVi", "hrir");
        if (_fs.DirectoryExists(hrirDir))
        {
            return DiagnosticResult.Ok(
                "hesuvi.installed",
                "HeSuVi (HRIR virtual surround)",
                $"HeSuVi HRIR directory found at {hrirDir}.");
        }

        return DiagnosticResult.Warn(
            "hesuvi.installed",
            "HeSuVi (HRIR virtual surround)",
            "HeSuVi is not installed — HRIR Include line will be skipped.",
            manualFix: "Optional: install HeSuVi from https://sourceforge.net/projects/hesuvi/ for binaural virtual surround.");
    }
}
