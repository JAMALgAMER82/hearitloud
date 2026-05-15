using System.Text;

namespace WarzoneEQ.WindowsIntegration;

// Owns the block we inject into Equalizer APO's master config.txt. Centralized
// here because both the installer (which writes it) and the diagnostic check
// (which validates / repairs it) need the same definition.
public static class WarzoneMasterConfig
{
    public const string BlockStartMarker = "# >>> hear-it-loud-block-start";
    public const string BlockEndMarker   = "# <<< hear-it-loud-block-end";
    public const string IncludeLine      = @"Include: warzone\current.txt";

    // Every Warzone / Call of Duty executable we want our chain to apply to.
    // Discord, Spotify, YouTube, browsers — anything not on this list — get
    // EQ APO's identity passthrough.
    public static readonly IReadOnlyList<string> WarzoneProcesses = new[]
    {
        "cod.exe",
        "ModernWarfare.exe",
        "Warzone.exe",
        "cod_modernwarfare.exe",
        "BlackOps6.exe",
    };

    public static string BuildBlock()
    {
        var clause = string.Join(";", WarzoneProcesses.Select(p => $"app:{p}"));
        var sb = new StringBuilder();
        sb.AppendLine(BlockStartMarker);
        sb.AppendLine("# Apply Hear It Loud EQ chain only to Call of Duty processes.");
        sb.AppendLine("# Discord, Spotify, YouTube etc. pass through untouched.");
        sb.AppendLine($"If({clause})");
        sb.AppendLine(IncludeLine);
        sb.AppendLine("EndIf");
        sb.Append(BlockEndMarker);
        return sb.ToString();
    }

    // Returns the master config.txt content with our managed block present
    // exactly once and up-to-date. Three cases handled:
    //   1. Managed block markers found  -> replace block in-place (handles upgrades).
    //   2. Legacy bare Include line     -> upgrade to the conditional block.
    //   3. Neither present              -> append block to the end of the file.
    public static string Merge(string existing)
    {
        var block = BuildBlock();

        var start = existing.IndexOf(BlockStartMarker, StringComparison.Ordinal);
        var end = existing.IndexOf(BlockEndMarker, StringComparison.Ordinal);
        if (start >= 0 && end > start)
        {
            var before = existing.Substring(0, start);
            var after = existing.Substring(end + BlockEndMarker.Length);
            return before + block + after;
        }

        if (existing.Contains(IncludeLine, StringComparison.Ordinal))
            return existing.Replace(IncludeLine, block);

        var sep = existing.Length == 0 || existing.EndsWith("\n") ? "" : Environment.NewLine;
        return existing + sep + Environment.NewLine + block + Environment.NewLine;
    }

    public static bool HasManagedBlock(string content)
        => content.Contains(BlockStartMarker, StringComparison.Ordinal)
        && content.Contains(BlockEndMarker, StringComparison.Ordinal);

    public static bool HasLegacyBareInclude(string content)
        => !HasManagedBlock(content)
        && content.Contains(IncludeLine, StringComparison.Ordinal);
}
