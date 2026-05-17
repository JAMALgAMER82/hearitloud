using System.Text;
using WarzoneEQ.ConfigGenerator;

namespace WarzoneEQ.WindowsIntegration;

// Owns the block we inject into Equalizer APO's master config.txt. Centralized
// here because both the installer (which writes it) and the diagnostic check
// (which validates / repairs it) need the same definition.
public static class WarzoneMasterConfig
{
    public const string BlockStartMarker = "# >>> hear-it-loud-block-start";
    public const string BlockEndMarker   = "# <<< hear-it-loud-block-end";
    public const string IncludeLine      = @"Include: warzone\current.txt";

    // v1.10.7: process list moved to WarzoneEQ.ConfigGenerator.WarzoneProcesses
    // so the profile generators (which wrap their own output in If/EndIf for
    // belt-and-suspenders) share a single source of truth with the master
    // config block. Re-exposed here so existing tests keep working.
    public static IReadOnlyList<string> WarzoneProcesses => ConfigGenerator.WarzoneProcesses.Names;

    public static string BuildBlock()
    {
        var sb = new StringBuilder();
        sb.AppendLine(BlockStartMarker);
        sb.AppendLine("# Apply Hear It Loud EQ chain only to Call of Duty processes.");
        sb.AppendLine("# Discord, Spotify, YouTube etc. pass through untouched.");
        sb.AppendLine(ConfigGenerator.WarzoneProcesses.IfLine);
        sb.AppendLine(IncludeLine);
        sb.AppendLine(ConfigGenerator.WarzoneProcesses.EndIfLine);
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

    // Strips our managed block (and any pre-conditional bare Include line) so
    // the master config can be restored to its pre-Hear-It-Loud state during
    // uninstall. Leaves all other content untouched.
    public static string RemoveManagedBlock(string content)
    {
        var start = content.IndexOf(BlockStartMarker, StringComparison.Ordinal);
        var end = content.IndexOf(BlockEndMarker, StringComparison.Ordinal);
        if (start >= 0 && end > start)
        {
            var before = content.Substring(0, start).TrimEnd('\r', '\n', ' ', '\t');
            var after = content.Substring(end + BlockEndMarker.Length).TrimStart('\r', '\n');
            content = before
                + (before.Length > 0 && after.Length > 0 ? Environment.NewLine : "")
                + after;
        }
        if (content.Contains(IncludeLine, StringComparison.Ordinal))
        {
            content = string.Join(Environment.NewLine, content
                .Split('\n')
                .Where(line => !line.TrimEnd('\r').Equals(IncludeLine, StringComparison.Ordinal))
                .Select(line => line.TrimEnd('\r')));
        }
        return content;
    }

    public static bool HasManagedBlock(string content)
        => content.Contains(BlockStartMarker, StringComparison.Ordinal)
        && content.Contains(BlockEndMarker, StringComparison.Ordinal);

    public static bool HasLegacyBareInclude(string content)
        => !HasManagedBlock(content)
        && content.Contains(IncludeLine, StringComparison.Ordinal);
}
