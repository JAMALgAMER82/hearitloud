using System.Runtime.Versioning;
using WarzoneEQ.WindowsIntegration.LoudnessEq;

namespace WarzoneEQ.WindowsIntegration.Diagnostics.Checks;

// Equalizer APO inserts itself into the audio chain via an APO driver that is
// only loaded at boot. If a pending reboot is detected, our config edits will
// have no audible effect until reboot.
public sealed class RebootPendingCheck : IDiagnosticCheck
{
    private readonly IRegistry _registry;

    public RebootPendingCheck(IRegistry registry) => _registry = registry;

    [SupportedOSPlatform("windows")]
    public DiagnosticResult Run()
    {
        var pending = _registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager",
            "PendingFileRenameOperations");

        if (pending is null)
        {
            return DiagnosticResult.Ok(
                "windows.reboot-pending",
                "Windows reboot not pending",
                "No PendingFileRenameOperations entries.");
        }

        return DiagnosticResult.Warn(
            "windows.reboot-pending",
            "Windows reboot not pending",
            "Windows is waiting on a reboot. Equalizer APO changes may not take effect until you restart.",
            manualFix: "Reboot Windows.");
    }
}
