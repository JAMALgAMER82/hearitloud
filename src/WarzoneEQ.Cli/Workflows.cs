using System.Runtime.Versioning;
using WarzoneEQ.ConfigGenerator;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.DeviceDetection;
using WarzoneEQ.DeviceDetection.Detection;
using WarzoneEQ.DeviceDetection.Matching;
using WarzoneEQ.WindowsIntegration;
using WarzoneEQ.WindowsIntegration.AbSwitcher;
using WarzoneEQ.WindowsIntegration.Diagnostics;
using WarzoneEQ.WindowsIntegration.EqApo;
using WarzoneEQ.WindowsIntegration.Files;
using WarzoneEQ.WindowsIntegration.LoudnessEq;
using WarzoneEQ.WindowsIntegration.Plugins;

namespace WarzoneEQ.Cli;

public sealed record WorkflowOptions(
    AudioMode Mode = AudioMode.Competitive,
    FpsCurveName Curve = FpsCurveName.Moderate,
    double Intensity = 1.0,
    string? Headphone = null,
    string? Dac = null,
    bool LinearPhase = false,
    bool AdaptiveLoudness = false,
    bool Wider = false,
    bool FootstepCompressor = true,
    bool Basic = false,
    bool FootstepPriority = false,
    PluginOverrides? PluginOverrides = null);

// All long-running operations behind buttons / CLI flags live here so the
// console entry point and the GUI form can call the same logic without a
// child-process round trip.
public static class Workflows
{
    [SupportedOSPlatform("windows")]
    public static DetectionSnapshot DetectHardware()
    {
        var db = DeviceDatabase.LoadEmbedded();
        var enumerator = new WindowsDeviceEnumerator();
        var matcher = new DeviceMatcher(db);
        return new DeviceDetectionService(enumerator, matcher).Snapshot();
    }

    public static void PrintDetection(Action<string> write, DetectionSnapshot snap, bool vstAvailable, bool hrirAvailable)
    {
        write($"  Headphone: {snap.PrimaryHeadphone?.Model ?? "(none recognized)"}");
        if (snap.PrimaryHeadphone is { } hp) write($"             AutoEQ slug: {hp.AutoeqSlug}");
        write($"  DAC:       {snap.MultiEndpointDac?.Model ?? "(none recognized)"}");
        if (snap.MultiEndpointDac is { } d)
        {
            write($"             Game endpoint:  {d.GameEndpoint}");
            write($"             Voice endpoint: {d.VoiceEndpoint}");
        }
        write($"  Devices scanned: {snap.Devices.Count}");
        write("");
        write($"  VST plugins available:   {(vstAvailable ? "yes" : "no")}");
        write($"  HeSuVi (HRIR) installed: {(hrirAvailable ? "yes" : "no")}");

        // Always dump the raw device list — turns "nothing recognized" from a
        // dead end into actionable info. The user can copy any EndpointName
        // into the Advanced tab's DAC field, or open an issue with the VID/PID
        // line so we can add their hardware to vidpid-overlay.json.
        write("");
        write("  Scanned playback devices:");
        if (snap.Devices.Count == 0)
        {
            write("    (none — Windows reports no playback endpoints. Is anything plugged in?)");
        }
        else
        {
            int n = 0;
            foreach (var dev in snap.Devices)
            {
                n++;
                var bits = new List<string> { dev.Kind.ToString() };
                if (dev.UsbVidPidKey is { } key) bits.Add(key);
                if (!string.IsNullOrEmpty(dev.BluetoothName)) bits.Add($"BT:{dev.BluetoothName}");
                write($"    {n}. \"{dev.EndpointName}\"  [{string.Join(" / ", bits)}]");
            }
            write("");
            if (snap.PrimaryHeadphone is null && snap.MultiEndpointDac is null)
            {
                write("  None of these matched the bundled hardware database.");
                write("  - To use one of them anyway: open the Advanced tab and paste the");
                write("    endpoint name into the \"DAC\" field, then click Apply.");
                write("  - To get full auto-detection: please open an issue at");
                write("    https://github.com/JAMALgAMER82/hearitloud/issues with the VID/PID");
                write("    or BT name above + your headphone model — I'll add it to the next release.");
            }
        }
    }

    [SupportedOSPlatform("windows")]
    public static int Detect(Action<string> write, bool basic)
    {
        var snap = DetectHardware();
        bool vst = !basic && DetectVstAvailable();
        bool hrir = !basic && DetectHesuviInstalled();
        PrintDetection(write, snap, vst, hrir);
        return 0;
    }

    [SupportedOSPlatform("windows")]
    public static int Auto(Action<string> write, WorkflowOptions opts)
    {
        var mode = opts.Mode;
        var curve = opts.Curve;
        var intensity = opts.Intensity;
        if (opts.FootstepPriority)
        {
            mode = AudioMode.FootstepHunter;
            curve = FpsCurveName.Aggressive;
            intensity = 1.0;
        }

        bool vst = !opts.Basic && DetectVstAvailable();
        bool hrir = !opts.Basic && DetectHesuviInstalled();

        var snap = DetectHardware();
        write("Detected hardware:");
        PrintDetection(write, snap, vst, hrir);
        write("");
        if (!vst) write("(Falling back to basic chain — VST plugins not present.)");
        if (!hrir) write("(HRIR Include line will be skipped — HeSuVi not present.)");
        write("");

        var input = BuildInput(opts, mode, curve, intensity,
            snap.PrimaryHeadphone?.AutoeqSlug ?? opts.Headphone,
            snap.MultiEndpointDac?.GameEndpoint ?? opts.Dac,
            vst, hrir);
        var installCode = Install(write, input);
        if (installCode != 0) return installCode;

        TryEnableLoudnessEq(write, snap.MultiEndpointDac?.GameEndpoint);
        return 0;
    }

    [SupportedOSPlatform("windows")]
    public static int Install(Action<string> write, ProfileInput input)
    {
        var locator = new RegistryEqApoLocator();
        if (!locator.IsInstalled)
        {
            write("[error] Equalizer APO is not installed. Install it first from https://equalizerapo.com.");
            return 3;
        }

        // Always install in A/B mode: primary = user's choice, secondary =
        // the natural counterpart (FootstepHunter <-> Cinematic, fallback
        // Cinematic). Lets the user toggle mid-game via the global hotkey
        // without re-running --auto. The master config still includes
        // warzone\current.txt, which is now the A/B selector file.
        var writer = new AtomicFileWriter();
        var installer = new WarzoneConfigInstaller(locator, writer);
        installer.EnsureMasterIncludeOnly();

        var ab = new AbSwitcher(locator, writer);
        var secondary = ChooseSecondary(input);
        ab.Install(input, secondary);

        write("Installed Hear It Loud A/B config:");
        write($"  Slot A (active):  {input.Mode}");
        write($"  Slot B:           {secondary.Mode}");
        write($"  Selector:         {ab.SelectorPath}");
        write("");
        write("Press Ctrl+Shift+F8 in-game (or click \"Toggle A/B\" in the app)");
        write("to switch between slots. Switch takes ~60 ms — no audio dropout.");
        write("");
        write("Equalizer APO will hot-reload automatically (no restart needed).");
        write("In Warzone: Settings -> Audio -> Audio Mix = Headphones Bass Cut,");
        write("Surround Sound = 7.1, Music = 0, Enhanced Headphone Mode = OFF.");
        return 0;
    }

    private static ProfileInput ChooseSecondary(ProfileInput primary) => primary.Mode switch
    {
        AudioMode.FootstepHunter => primary with { Mode = AudioMode.Cinematic },
        AudioMode.Cinematic => primary with { Mode = AudioMode.FootstepHunter },
        AudioMode.Competitive => primary with { Mode = AudioMode.Cinematic },
        _ => primary with { Mode = AudioMode.Cinematic },
    };

    // Auto-installs all three optional VST/HRIR plugins (TDR Nova, LoudMax,
    // HeSuVi) by downloading from the publishers' official URLs and running
    // their respective installers/zips silently. Called by the main installer
    // post-EQ-APO so end users never see a second installer UI.
    //
    // Best-effort: a publisher URL change or network failure logs the issue
    // but does NOT fail the parent install. The diagnose tool can recover
    // anything missing on next launch.
    [SupportedOSPlatform("windows")]
    public static int InstallOptionalPlugins(Action<string> write)
    {
        var locator = new RegistryEqApoLocator();
        if (!locator.IsInstalled)
        {
            write("[plugins] EQ APO not present — skipping plugin install.");
            return 0;
        }
        var installer = new PluginInstaller(locator);
        int ok = 0, fail = 0;
        foreach (var p in new[] { OptionalPlugin.LoudMax, OptionalPlugin.TdrNova, OptionalPlugin.HeSuVi })
        {
            try
            {
                var task = installer.InstallAsync(p, write);
                task.GetAwaiter().GetResult();
                if (task.Result) ok++; else fail++;
            }
            catch (Exception ex)
            {
                fail++;
                write($"[plugins] {p}: {ex.Message}");
            }
        }
        write("");
        write($"[plugins] {ok} installed, {fail} failed. The app falls back to basic mode for any that failed; Diagnose & Auto-Fix can re-attempt later.");
        // Always return 0 so the installer doesn't abort the whole setup on a
        // plugin-download failure — the core app works without them.
        return 0;
    }

    public static int Print(Action<string> write, ProfileInput input)
    {
        foreach (var line in ConfigGenerator.ConfigGenerator.Generate(input).Split('\n'))
            write(line.TrimEnd('\r'));
        return 0;
    }

    [SupportedOSPlatform("windows")]
    public static int Diagnose(Action<string> write, bool applyFix)
    {
        write("Hear It Loud — system diagnostics");
        write(new string('=', 50));
        var engine = StandardDiagnostics.ForCurrentMachine();
        var report = applyFix ? engine.RunAndAutoFix() : engine.Run();

        foreach (var r in report.Results)
        {
            var tag = r.Severity switch
            {
                DiagnosticSeverity.Ok      => "[ OK ]",
                DiagnosticSeverity.Warning => "[WARN]",
                DiagnosticSeverity.Error   => "[FAIL]",
                _ => "[????]",
            };
            write("");
            write($"{tag} {r.Title}");
            write($"       {r.Detail}");
            if (r.Severity != DiagnosticSeverity.Ok && r.ManualFix is { } mf)
                foreach (var line in mf.Split('\n')) write($"       fix: {line.TrimEnd()}");
        }
        write("");
        write(new string('-', 50));
        write($"Summary: {report.OkCount} ok, {report.WarnCount} warn, {report.ErrorCount} fail.");
        if (!applyFix && report.Results.Any(r => r.CanAutoFix))
            write("Re-run with --diagnose --fix (or click Diagnose & Fix in the app) to apply safe auto-fixes.");
        return report.AnyFailures ? 1 : 0;
    }

    public static int RunSelfTest(Action<string> write)
    {
        write("Hear It Loud — config generator self-test");
        write(new string('=', 50));
        var results = WarzoneEQ.WindowsIntegration.Diagnostics.SelfTest.Run();
        foreach (var r in results)
        {
            var tag = r.Passed ? "[PASS]" : "[FAIL]";
            write($"{tag} {r.Name}  ({r.Detail})");
        }
        var failed = results.Count(r => !r.Passed);
        write("");
        write(new string('-', 50));
        write($"Self-test: {results.Count - failed}/{results.Count} passed.");
        return failed > 0 ? 1 : 0;
    }

    // Removes Hear It Loud's managed block from EQ APO's master config.txt.
    // Called by the installer on uninstall so we don't leave a dangling
    // Include reference pointing at a deleted file.
    [SupportedOSPlatform("windows")]
    public static int UninstallCleanup(Action<string> write)
    {
        var locator = new RegistryEqApoLocator();
        if (!locator.IsInstalled)
        {
            write("Equalizer APO not present — nothing to clean up.");
            return 0;
        }
        var masterPath = Path.Combine(locator.ConfigDirectory, "config.txt");
        if (!File.Exists(masterPath))
        {
            write("Master config.txt not found — nothing to clean up.");
            return 0;
        }
        var content = File.ReadAllText(masterPath);
        if (!WarzoneMasterConfig.HasManagedBlock(content) && !WarzoneMasterConfig.HasLegacyBareInclude(content))
        {
            write("No Hear It Loud entries in master config — nothing to clean up.");
            return 0;
        }
        var cleaned = WarzoneMasterConfig.RemoveManagedBlock(content);
        File.WriteAllText(masterPath, cleaned);
        write($"Removed Hear It Loud block from {masterPath}.");
        return 0;
    }

    [SupportedOSPlatform("windows")]
    public static bool DetectVstAvailable()
    {
        var eqApoPath = Microsoft.Win32.Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\EqualizerAPO", "InstallPath", null) as string;
        if (string.IsNullOrEmpty(eqApoPath)) return false;
        var vstDir = Path.Combine(eqApoPath, "VSTPlugins");
        if (!Directory.Exists(vstDir)) return false;
        var files = Directory.GetFiles(vstDir, "*.dll", SearchOption.TopDirectoryOnly);
        bool hasTdrNova = files.Any(f => Path.GetFileName(f).Contains("TDR Nova", StringComparison.OrdinalIgnoreCase));
        bool hasLoudMax = files.Any(f => Path.GetFileName(f).Contains("LoudMax", StringComparison.OrdinalIgnoreCase));
        return hasTdrNova && hasLoudMax;
    }

    [SupportedOSPlatform("windows")]
    public static bool DetectHesuviInstalled()
    {
        var eqApoPath = Microsoft.Win32.Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\EqualizerAPO", "InstallPath", null) as string;
        if (string.IsNullOrEmpty(eqApoPath)) return false;
        return Directory.Exists(Path.Combine(eqApoPath, "config", "HeSuVi", "hrir"));
    }

    public static ProfileInput BuildInput(WorkflowOptions opts) =>
        BuildInput(opts, opts.Mode, opts.Curve, opts.Intensity, opts.Headphone, opts.Dac,
            !opts.Basic, !opts.Basic);

    private static ProfileInput BuildInput(
        WorkflowOptions opts, AudioMode mode, FpsCurveName curve, double intensity,
        string? headphone, string? dac, bool enableVst, bool enableHrir)
        => new(mode)
        {
            FpsCurve = curve,
            Intensity = intensity,
            HeadphoneCorrection = headphone is null ? HeadphoneCorrection.None : new HeadphoneCorrection(headphone),
            DacEndpoint = dac is null ? DacEndpoint.WindowsDefault : new DacEndpoint(dac),
            EnableLinearPhase = opts.LinearPhase,
            EnableAdaptiveLoudness = opts.AdaptiveLoudness,
            EnablePolyverseWider = opts.Wider,
            EnableFootstepCompressor = opts.FootstepCompressor,
            EnableVstPlugins = enableVst,
            EnableHrirInclude = enableHrir,
            PluginOverrides = opts.PluginOverrides,
        };

    [SupportedOSPlatform("windows")]
    private static void TryEnableLoudnessEq(Action<string> write, string? gameEndpointFriendlyName)
    {
        if (gameEndpointFriendlyName is null) return;
        try
        {
            var guid = FindRenderEndpointGuid(gameEndpointFriendlyName);
            if (guid is null)
            {
                write($"(Skipped Windows Loudness EQ: endpoint '{gameEndpointFriendlyName}' not found in registry.)");
                return;
            }
            new RegistryLoudnessEqController()
                .Write(guid, new LoudnessEqState(Enabled: true, ReleaseTime: LoudnessEqState.MinReleaseTime));
            write($"Windows Loudness EQ enabled (SHORT release) on {gameEndpointFriendlyName}.");
        }
        catch (Exception ex)
        {
            write($"(Windows Loudness EQ step skipped — {ex.Message})");
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? FindRenderEndpointGuid(string friendlyName)
    {
        using var renderKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render");
        if (renderKey is null) return null;
        foreach (var sub in renderKey.GetSubKeyNames())
        {
            using var props = renderKey.OpenSubKey(sub + @"\Properties");
            if (props is null) continue;
            var name = props.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},14")?.ToString();
            if (name is null) continue;
            if (string.Equals(name, friendlyName, StringComparison.OrdinalIgnoreCase)
                || name.Contains(friendlyName, StringComparison.OrdinalIgnoreCase))
                return sub;
        }
        return null;
    }
}
