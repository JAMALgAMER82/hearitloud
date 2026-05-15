using System.Diagnostics;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;

namespace WarzoneEQ.WindowsIntegration.Updates;

public sealed record UpdateInfo(
    string LatestTag,
    string CurrentVersion,
    string InstallerUrl,
    string ReleasePageUrl,
    string ReleaseNotes,
    long InstallerSizeBytes);

// Self-update via GitHub Releases. The /releases/latest endpoint returns the
// most recent non-draft, non-prerelease tag. We compare against the running
// assembly version, and if newer we offer the user a one-click upgrade that
// preserves their settings (which live in %APPDATA% — outside the installer's
// install dir, so they survive any in-place upgrade).
public static class UpdateChecker
{
    public const string ReleasesApi = "https://api.github.com/repos/JAMALgAMER82/hearitloud/releases/latest";
    private const string UserAgent = "HearItLoud-UpdateCheck";
    private const string InstallerAssetName = "HearItLoud-Setup.exe";

    // Read from the entry assembly (the CLI exe) so this still returns the
    // right number when invoked from the form, the CLI, or the installer.
    public static string CurrentVersion =>
        (Assembly.GetEntryAssembly() ?? typeof(UpdateChecker).Assembly)
            .GetName().Version?.ToString(3) ?? "0.0.0";

    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        var json = await http.GetStringAsync(ReleasesApi, ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var pageUrl = root.GetProperty("html_url").GetString() ?? "";
        var notes = root.TryGetProperty("body", out var b) ? (b.GetString() ?? "") : "";

        string? installerUrl = null;
        long size = 0;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (string.Equals(name, InstallerAssetName, StringComparison.OrdinalIgnoreCase))
            {
                installerUrl = asset.GetProperty("browser_download_url").GetString();
                size = asset.GetProperty("size").GetInt64();
                break;
            }
        }
        if (installerUrl is null) return null;

        var current = CurrentVersion;
        return IsNewer(tag, current)
            ? new UpdateInfo(tag, current, installerUrl, pageUrl, notes, size)
            : null;
    }

    // Public for testing. Accepts "v1.2.3" or "1.2.3"; strips a leading "v"/"V".
    // Returns true iff `tagVersion` parses as a strictly greater version than
    // `currentVersion`. Non-parseable inputs return false (safe default — we
    // never offer an "update" we can't confidently identify as newer).
    public static bool IsNewer(string tagVersion, string currentVersion)
    {
        if (!Version.TryParse(Normalize(tagVersion), out var t)) return false;
        if (!Version.TryParse(Normalize(currentVersion), out var c)) return false;
        return t > c;
    }

    private static string Normalize(string v) =>
        string.IsNullOrEmpty(v) ? v : (v[0] is 'v' or 'V' ? v[1..] : v);

    public static async Task<string> DownloadAsync(
        string url, Action<string>? progress = null, CancellationToken ct = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"HearItLoud-Update-{Guid.NewGuid():N}.exe");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        progress?.Invoke($"Downloading from {url} ...");
        var bytes = await http.GetByteArrayAsync(url, ct);
        await File.WriteAllBytesAsync(tempPath, bytes, ct);
        progress?.Invoke($"Downloaded {bytes.Length / 1024 / 1024} MB to:");
        progress?.Invoke($"  {tempPath}");
        return tempPath;
    }

    // Launches the downloaded installer in silent mode with admin elevation.
    // Inno Setup flags:
    //   /SILENT             — progress shown, no wizard pages
    //   /SUPPRESSMSGBOXES   — auto-OK any MsgBox (skips the reboot popup)
    //   /NORESTART          — overrides AlwaysRestart=yes (we don't reboot for upgrades)
    //   /TASKS=""           — skip the post-install runautotune task (user already has a config)
    //
    // The installer itself has CloseApplications=yes + RestartApplications=yes,
    // so it will close the running app, overwrite the exe, and re-launch.
    [SupportedOSPlatform("windows")]
    public static void LaunchInstaller(string installerPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART /TASKS=\"\"",
            UseShellExecute = true,
            Verb = "runas",
        });
    }
}
