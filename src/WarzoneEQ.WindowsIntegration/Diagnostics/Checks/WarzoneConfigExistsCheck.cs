using WarzoneEQ.WindowsIntegration.EqApo;

namespace WarzoneEQ.WindowsIntegration.Diagnostics.Checks;

public sealed class WarzoneConfigExistsCheck : IDiagnosticCheck
{
    private readonly IEqApoLocator _locator;
    private readonly IFileSystem _fs;

    public WarzoneConfigExistsCheck(IEqApoLocator locator, IFileSystem fs)
    {
        _locator = locator;
        _fs = fs;
    }

    public DiagnosticResult Run()
    {
        if (!_locator.IsInstalled)
        {
            return DiagnosticResult.Warn(
                "eqapo.warzone-config",
                "warzone\\current.txt present",
                "Skipped — Equalizer APO is not installed.");
        }

        var path = Path.Combine(_locator.ConfigDirectory, "warzone", "current.txt");
        if (_fs.FileExists(path))
        {
            return DiagnosticResult.Ok(
                "eqapo.warzone-config",
                "warzone\\current.txt present",
                $"Config file present at {path}.");
        }

        return DiagnosticResult.Fail(
            "eqapo.warzone-config",
            "warzone\\current.txt present",
            "Hear It Loud config has not been installed yet.",
            manualFix: "Run: HearItLoud.exe --auto");
    }
}
