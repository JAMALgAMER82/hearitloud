using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.Versioning;
using WarzoneEQ.WindowsIntegration.EqApo;

namespace WarzoneEQ.WindowsIntegration.Plugins;

public enum OptionalPlugin { TdrNova, LoudMax, HeSuVi }

// Downloads and installs the three free optional VST/HRIR components our
// FootstepHunter and Cinematic chains use when present. Each is the
// publisher's official redistributable. We never re-host the binaries —
// we fetch directly from the upstream URL and cache to %TEMP%.
//
// TDR Nova and LoudMax ship as zip files of .dll plugins; we extract the
// .dll into EqualizerAPO\VSTPlugins\. HeSuVi ships as an Inno Setup
// installer that drops files into EqualizerAPO\config\HeSuVi\, including
// the HRIR WAVs our `Include:` line consumes — we run it silently.
[SupportedOSPlatform("windows")]
public sealed class PluginInstaller
{
    // Stable upstream URLs. If any of these break (publisher restructures),
    // diagnose will still flag the missing plugin so the user gets clear
    // manual fix instructions.
    public const string TdrNovaUrl = "https://www.tokyodawn.net/downloads/nova/TDR_Nova_2_x_x_Installer.zip";
    public const string LoudMaxUrl = "https://loudmaxdownload.com/LoudMax_v1_44_Win.zip";
    public const string HeSuViUrl  = "https://sourceforge.net/projects/hesuvi/files/latest/download";

    private readonly IEqApoLocator _locator;
    public PluginInstaller(IEqApoLocator locator) => _locator = locator;

    public string VstPluginsDir =>
        Path.Combine(Path.GetDirectoryName(_locator.ConfigDirectory.TrimEnd(Path.DirectorySeparatorChar))!, "VSTPlugins");

    public async Task<bool> InstallAsync(OptionalPlugin which, Action<string> log, CancellationToken ct = default)
    {
        if (!_locator.IsInstalled)
        {
            log("[error] Equalizer APO is not installed — install it first via the main installer.");
            return false;
        }

        return which switch
        {
            OptionalPlugin.TdrNova => await InstallZipPluginAsync(which, TdrNovaUrl, "TDR Nova.dll", log, ct),
            OptionalPlugin.LoudMax => await InstallZipPluginAsync(which, LoudMaxUrl, "LoudMax.dll", log, ct),
            OptionalPlugin.HeSuVi  => await InstallInnoBundleAsync(HeSuViUrl, log, ct),
            _ => false,
        };
    }

    private async Task<bool> InstallZipPluginAsync(
        OptionalPlugin which, string url, string dllSearchHint, Action<string> log, CancellationToken ct)
    {
        Directory.CreateDirectory(VstPluginsDir);
        var tempZip = Path.Combine(Path.GetTempPath(), $"hearitloud-{which}-{Guid.NewGuid():N}.zip");
        try
        {
            log($"[plugin] downloading {which} from {url}");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("HearItLoud-PluginInstaller");
            var bytes = await http.GetByteArrayAsync(url, ct);
            await File.WriteAllBytesAsync(tempZip, bytes, ct);
            log($"[plugin] downloaded {bytes.Length / 1024} KB");

            log($"[plugin] extracting {dllSearchHint} to {VstPluginsDir}");
            using var archive = ZipFile.OpenRead(tempZip);
            int extracted = 0;
            foreach (var entry in archive.Entries)
            {
                // Some publisher zips nest the DLL inside subfolders (x64/, win64/);
                // we want any DLL whose filename matches the hint, ignoring path.
                if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                var name = Path.GetFileName(entry.FullName);
                if (!name.Contains(Path.GetFileNameWithoutExtension(dllSearchHint), StringComparison.OrdinalIgnoreCase)) continue;
                var dest = Path.Combine(VstPluginsDir, name);
                entry.ExtractToFile(dest, overwrite: true);
                log($"[plugin] wrote {dest}");
                extracted++;
            }
            if (extracted == 0)
            {
                log($"[plugin] WARNING: zip didn't contain a DLL matching '{dllSearchHint}'. The publisher may have changed their layout — install manually.");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            log($"[plugin] {which} install failed: {ex.Message}");
            return false;
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { /* best-effort */ }
        }
    }

    private async Task<bool> InstallInnoBundleAsync(string url, Action<string> log, CancellationToken ct)
    {
        var tempExe = Path.Combine(Path.GetTempPath(), $"hearitloud-hesuvi-{Guid.NewGuid():N}.exe");
        try
        {
            log($"[hesuvi] downloading from {url}");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("HearItLoud-PluginInstaller");
            var bytes = await http.GetByteArrayAsync(url, ct);
            await File.WriteAllBytesAsync(tempExe, bytes, ct);
            log($"[hesuvi] downloaded {bytes.Length / 1024 / 1024} MB");

            log($"[hesuvi] running silent installer (UAC prompt expected)...");
            var psi = new ProcessStartInfo
            {
                FileName = tempExe,
                Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
                UseShellExecute = true,
                Verb = "runas",
            };
            using var p = Process.Start(psi);
            if (p is null) { log("[hesuvi] failed to launch installer."); return false; }
            await p.WaitForExitAsync(ct);
            log(p.ExitCode == 0 ? "[hesuvi] installed." : $"[hesuvi] installer exited with code {p.ExitCode}.");
            return p.ExitCode == 0;
        }
        catch (Exception ex)
        {
            log($"[hesuvi] install failed: {ex.Message}");
            return false;
        }
        finally
        {
            try { if (File.Exists(tempExe)) File.Delete(tempExe); } catch { /* best-effort */ }
        }
    }
}
