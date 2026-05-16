using WarzoneEQ.WindowsIntegration.AbSwitcher;

namespace WarzoneEQ.WindowsIntegration.ProcessWatch;

// Pure-data mapping: when the foreground process is in the gameProcesses
// set, the A/B slot should be A (the user's "in-game" profile); otherwise
// it should be B (the user's "desktop / casual listening" profile).
//
// Lives in WindowsIntegration (not Cli) so it's testable without WinForms
// and so a future v1.5 service / daemon could reuse it.
public sealed record ProcessProfileMap(IReadOnlySet<string> GameProcesses)
{
    public static ProcessProfileMap Default { get; } = new(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "cod.exe",
        "ModernWarfare.exe",
        "Warzone.exe",
        "cod_modernwarfare.exe",
        "BlackOps6.exe",
    });

    public AbSlot SlotFor(string? foregroundProcessName)
    {
        if (string.IsNullOrEmpty(foregroundProcessName)) return AbSlot.B;
        return GameProcesses.Contains(foregroundProcessName) ? AbSlot.A : AbSlot.B;
    }

    public bool IsGame(string? processName) =>
        !string.IsNullOrEmpty(processName) && GameProcesses.Contains(processName);
}
