namespace WarzoneEQ.WindowsIntegration.Diagnostics.Checks;

// Windows Spatial Sound (Sonic for Headphones, Dolby Atmos for Headphones) and
// per-app audio enhancements double up on top of our HRIR chain, causing comb
// filtering and a flabby, smeared image. We can't reliably disable these from
// the registry without touching per-endpoint GUIDs that vary by machine, so
// this is a warn-only check with explicit manual steps.
public sealed class WindowsAudioEnhancementsCheck : IDiagnosticCheck
{
    public DiagnosticResult Run()
    {
        return DiagnosticResult.Warn(
            "windows.spatial-sound",
            "Windows Spatial Sound / Enhancements (manual check)",
            "Cannot programmatically read per-endpoint Spatial Sound state across all Windows versions.",
            manualFix:
                "On the Game endpoint:\n" +
                "  1. Right-click the speaker icon in the system tray → Sound settings.\n" +
                "  2. Click the Game endpoint, then Device properties.\n" +
                "  3. Set Spatial Sound to OFF (we provide our own HRIR).\n" +
                "  4. In the Advanced tab, uncheck 'Allow applications to take exclusive control of this device'.\n" +
                "  5. In the Enhancements tab (if present), uncheck all enhancements.");
    }
}
