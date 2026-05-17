namespace WarzoneEQ.ConfigGenerator;

// Single source of truth for the Call of Duty executable names that should
// receive Hear It Loud's processing. Discord, Spotify, YouTube, browsers and
// anything else not on this list pass through EQ APO's identity chain.
//
// v1.10.7: profile generators now wrap their Stage/Channel/Plugin output in
// `If(<IfAppClause>) ... EndIf` so the chain self-protects at the per-file
// level — even if the master config.txt ever ends up with a non-conditional
// Include, the chain still only fires for COD. WarzoneMasterConfig already
// wraps the master Include for belt-and-suspenders defense in depth.
public static class WarzoneProcesses
{
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "cod.exe",
        "ModernWarfare.exe",
        "Warzone.exe",
        "cod_modernwarfare.exe",
        "BlackOps6.exe",
    };

    // Pre-built `app:cod.exe;app:ModernWarfare.exe;...` clause for EQ APO's
    // `If(...)` directive. Joined with `;` because EQ APO treats `;` as OR
    // between predicates inside a single If.
    public static readonly string IfAppClause =
        string.Join(";", Names.Select(n => $"app:{n}"));

    // Convenience: full `If(...)` line ready to drop into a config.
    public static readonly string IfLine = $"If({IfAppClause})";

    // Closing marker. EQ APO accepts both `EndIf` and `EndIf:` — we match the
    // existing WarzoneMasterConfig style (no colon) for consistency.
    public const string EndIfLine = "EndIf";
}
