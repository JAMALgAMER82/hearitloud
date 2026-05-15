using WarzoneEQ.WindowsIntegration.EqApo;

namespace WarzoneEQ.WindowsIntegration.Diagnostics.Checks;

// EQ APO's config.txt is the master entry point. If our `Include: warzone\current.txt`
// line is missing, our chain isn't loaded and the user hears no effect — a very
// common "I installed it but nothing changed" bug.
public sealed class MasterConfigIncludeCheck : IDiagnosticCheck
{
    private const string IncludeLine = @"Include: warzone\current.txt";

    private readonly IEqApoLocator _locator;
    private readonly IFileSystem _fs;

    public MasterConfigIncludeCheck(IEqApoLocator locator, IFileSystem fs)
    {
        _locator = locator;
        _fs = fs;
    }

    public DiagnosticResult Run()
    {
        if (!_locator.IsInstalled)
        {
            return DiagnosticResult.Warn(
                "eqapo.master-include",
                "config.txt includes warzone\\current.txt",
                "Skipped — Equalizer APO is not installed.");
        }

        var masterPath = Path.Combine(_locator.ConfigDirectory, "config.txt");
        if (!_fs.FileExists(masterPath))
        {
            return DiagnosticResult.Fail(
                "eqapo.master-include",
                "config.txt includes warzone\\current.txt",
                $"Master file {masterPath} is missing. Equalizer APO is broken.",
                manualFix: "Reinstall Equalizer APO.");
        }

        var content = _fs.ReadAllText(masterPath);
        if (content.Contains(IncludeLine, StringComparison.Ordinal))
        {
            return DiagnosticResult.Ok(
                "eqapo.master-include",
                "config.txt includes warzone\\current.txt",
                "Master config references our chain.");
        }

        return DiagnosticResult.Fail(
            "eqapo.master-include",
            "config.txt includes warzone\\current.txt",
            "Master config.txt is missing the Include line — Hear It Loud's chain isn't loaded.",
            autoFix: () => _fs.AppendAllText(masterPath, Environment.NewLine + IncludeLine + Environment.NewLine),
            manualFix: $"Append this line to {masterPath}:\n  {IncludeLine}");
    }
}
