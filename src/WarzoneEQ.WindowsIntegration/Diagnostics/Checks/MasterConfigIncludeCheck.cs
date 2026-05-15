using WarzoneEQ.WindowsIntegration.EqApo;

namespace WarzoneEQ.WindowsIntegration.Diagnostics.Checks;

// EQ APO's config.txt is the master entry point. Our chain is loaded via a
// conditional block: `If(app:cod.exe;app:Warzone.exe;...) Include EndIf`.
// That block routes the EQ only to Call of Duty processes, leaving Discord,
// Spotify, browsers, etc. as identity passthrough.
//
// Three states this check distinguishes:
//   - Managed block present       -> OK
//   - Legacy bare Include present -> WARN + auto-fix (upgrade to conditional)
//   - Neither present             -> FAIL + auto-fix (write fresh conditional)
public sealed class MasterConfigIncludeCheck : IDiagnosticCheck
{
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
                "config.txt loads Hear It Loud only for Warzone",
                "Skipped — Equalizer APO is not installed.");
        }

        var masterPath = Path.Combine(_locator.ConfigDirectory, "config.txt");
        if (!_fs.FileExists(masterPath))
        {
            return DiagnosticResult.Fail(
                "eqapo.master-include",
                "config.txt loads Hear It Loud only for Warzone",
                $"Master file {masterPath} is missing. Equalizer APO is broken.",
                manualFix: "Reinstall Equalizer APO.");
        }

        var content = _fs.ReadAllText(masterPath);

        if (WarzoneMasterConfig.HasManagedBlock(content))
        {
            return DiagnosticResult.Ok(
                "eqapo.master-include",
                "config.txt loads Hear It Loud only for Warzone",
                "Conditional block is present — Warzone gets the EQ chain; Discord/Spotify/etc. pass through untouched.");
        }

        if (WarzoneMasterConfig.HasLegacyBareInclude(content))
        {
            return DiagnosticResult.Warn(
                "eqapo.master-include",
                "config.txt loads Hear It Loud only for Warzone",
                "Found legacy unconditional Include — the chain is being applied to every app (including Discord). Upgrade available.",
                autoFix: () => _fs.WriteAllText(masterPath, WarzoneMasterConfig.Merge(content)),
                manualFix: $"Replace the bare 'Include: warzone\\current.txt' line in {masterPath} with the conditional block from WarzoneMasterConfig.BuildBlock().");
        }

        return DiagnosticResult.Fail(
            "eqapo.master-include",
            "config.txt loads Hear It Loud only for Warzone",
            "Master config does not reference Hear It Loud's chain — the EQ is not loaded.",
            autoFix: () => _fs.WriteAllText(masterPath, WarzoneMasterConfig.Merge(content)),
            manualFix: $"Append the conditional block to {masterPath} (or re-run --auto).");
    }
}
