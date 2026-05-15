using WarzoneEQ.WindowsIntegration.EqApo;

namespace WarzoneEQ.WindowsIntegration.Diagnostics.Checks;

// Equalizer APO's config dir lives under Program Files by default, which means
// writes require elevation or filesystem ACLs the installer is meant to set up.
// If we can't write a probe file, --install will silently fail or throw.
public sealed class ConfigDirWritableCheck : IDiagnosticCheck
{
    private readonly IEqApoLocator _locator;
    private readonly IFileSystem _fs;

    public ConfigDirWritableCheck(IEqApoLocator locator, IFileSystem fs)
    {
        _locator = locator;
        _fs = fs;
    }

    public DiagnosticResult Run()
    {
        if (!_locator.IsInstalled)
        {
            return DiagnosticResult.Warn(
                "eqapo.writable",
                "Config directory is writable",
                "Skipped — Equalizer APO is not installed.");
        }

        var probe = Path.Combine(_locator.ConfigDirectory, ".hearitloud-probe");
        try
        {
            _fs.WriteAllText(probe, "probe");
            _fs.DeleteFile(probe);
            return DiagnosticResult.Ok(
                "eqapo.writable",
                "Config directory is writable",
                $"{_locator.ConfigDirectory} accepts writes.");
        }
        catch (Exception ex)
        {
            return DiagnosticResult.Fail(
                "eqapo.writable",
                "Config directory is writable",
                $"Cannot write to {_locator.ConfigDirectory}: {ex.Message}",
                manualFix: "Run HearItLoud.exe from an elevated (Administrator) shell, or grant your user write access to the EqualizerAPO config directory.");
        }
    }
}
