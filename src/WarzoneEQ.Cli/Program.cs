using System.CommandLine;
using WarzoneEQ.ConfigGenerator;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.DeviceDetection;
using WarzoneEQ.DeviceDetection.Detection;
using WarzoneEQ.DeviceDetection.Matching;
using WarzoneEQ.WindowsIntegration;
using WarzoneEQ.WindowsIntegration.EqApo;
using WarzoneEQ.WindowsIntegration.Files;
using WarzoneEQ.WindowsIntegration.LoudnessEq;

var modeOption = new Option<AudioMode>(
    name: "--mode",
    description: "Audio mode: Competitive, Cinematic, or Bypass.",
    getDefaultValue: () => AudioMode.Competitive);

var curveOption = new Option<FpsCurveName>(
    name: "--curve",
    description: "FPS target curve: Minimalist, Moderate, Aggressive.",
    getDefaultValue: () => FpsCurveName.Moderate);

var intensityOption = new Option<double>(
    name: "--intensity",
    description: "FPS curve intensity, 0.0 to 1.0.",
    getDefaultValue: () => 1.0);

var headphoneOption = new Option<string?>(
    name: "--headphone",
    description: "Headphone slug. Omit to auto-detect.");

var dacOption = new Option<string?>(
    name: "--dac",
    description: "DAC endpoint name to route to. Omit for Windows default (or auto-detect with --auto).");

var linearPhaseOption = new Option<bool>("--linear-phase", description: "Enable linear-phase EQ (Cinematic only).");
var adaptiveLoudnessOption = new Option<bool>("--adaptive-loudness", description: "Enable adaptive loudness JSFX.");
var widerOption = new Option<bool>("--wider", description: "Enable Polyverse Wider on sides+rear.");
var noCompressorOption = new Option<bool>("--no-compressor", description: "Disable the footstep-band upward compressor.");

var detectOption = new Option<bool>("--detect", description: "Detect current headphones + DAC and exit.");
var installOption = new Option<bool>("--install", description: "Write the generated config into the Equalizer APO config directory.");
var autoOption = new Option<bool>("--auto", description: "One-shot: detect hardware, route to detected DAC's Game endpoint, install. Sensible defaults for everything.");
var basicOption = new Option<bool>("--basic", description: "Omit Plugin: and HRIR Include lines so the chain works on vanilla Equalizer APO (no TDR Nova/ReaXcomp/LoudMax/Wider/HeSuVi required).");

var root = new RootCommand("Hear It Loud — by MasterMind George. Generate, detect, and install Equalizer APO configs for Call of Duty Warzone.")
{
    modeOption, curveOption, intensityOption, headphoneOption, dacOption,
    linearPhaseOption, adaptiveLoudnessOption, widerOption, noCompressorOption,
    detectOption, installOption, autoOption, basicOption,
};

root.SetHandler(context =>
{
    var mode             = context.ParseResult.GetValueForOption(modeOption);
    var curve            = context.ParseResult.GetValueForOption(curveOption);
    var intensity        = context.ParseResult.GetValueForOption(intensityOption);
    var headphone        = context.ParseResult.GetValueForOption(headphoneOption);
    var dac              = context.ParseResult.GetValueForOption(dacOption);
    var linearPhase      = context.ParseResult.GetValueForOption(linearPhaseOption);
    var adaptiveLoudness = context.ParseResult.GetValueForOption(adaptiveLoudnessOption);
    var wider            = context.ParseResult.GetValueForOption(widerOption);
    var noCompressor     = context.ParseResult.GetValueForOption(noCompressorOption);
    var detect           = context.ParseResult.GetValueForOption(detectOption);
    var install          = context.ParseResult.GetValueForOption(installOption);
    var auto             = context.ParseResult.GetValueForOption(autoOption);
    var basic            = context.ParseResult.GetValueForOption(basicOption);

    // Determine optional-component availability so we can downgrade gracefully.
    bool vstAvailable = !basic && DetectVstPluginsAvailable();
    bool hrirAvailable = !basic && DetectHesuviInstalled();

    if (detect)
    {
        var snapshot = DetectAudio();
        PrintDetection(snapshot);
        Console.WriteLine();
        Console.WriteLine($"  VST plugins available: {(vstAvailable ? "yes" : "no")}");
        Console.WriteLine($"  HeSuVi (HRIR) installed: {(hrirAvailable ? "yes" : "no")}");
        return;
    }

    if (auto)
    {
        var snapshot = DetectAudio();
        Console.WriteLine("Detected hardware:");
        PrintDetection(snapshot);
        Console.WriteLine();
        Console.WriteLine($"VST plugins available: {(vstAvailable ? "yes" : "no — falling back to basic chain")}");
        Console.WriteLine($"HeSuVi installed:      {(hrirAvailable ? "yes" : "no — skipping HRIR Include")}");
        Console.WriteLine();

        var autoHeadphone = snapshot.PrimaryHeadphone?.AutoeqSlug;
        var autoDac = snapshot.MultiEndpointDac?.GameEndpoint;
        // Auto with no recognized hardware still produces a useful generic
        // chain — just leaves out headphone correction and DAC routing.

        var autoInput = BuildProfile(
            mode, curve, intensity,
            autoHeadphone ?? headphone, autoDac ?? dac,
            linearPhase, adaptiveLoudness, wider, !noCompressor,
            vstAvailable, hrirAvailable);
        InstallProfile(autoInput);
        TryEnableLoudnessEq(snapshot.MultiEndpointDac?.GameEndpoint);
        return;
    }

    var input = BuildProfile(
        mode, curve, intensity, headphone, dac,
        linearPhase, adaptiveLoudness, wider, !noCompressor,
        vstAvailable, hrirAvailable);

    if (install) { InstallProfile(input); return; }

    Console.Write(ConfigGenerator.Generate(input));
});

return await root.InvokeAsync(args);

static ProfileInput BuildProfile(
    AudioMode mode, FpsCurveName curve, double intensity,
    string? headphone, string? dac,
    bool linearPhase, bool adaptiveLoudness, bool wider, bool footstepCompressor,
    bool enableVstPlugins, bool enableHrirInclude)
    => new(mode)
    {
        FpsCurve = curve,
        Intensity = intensity,
        HeadphoneCorrection = headphone is null ? HeadphoneCorrection.None : new HeadphoneCorrection(headphone),
        DacEndpoint = dac is null ? DacEndpoint.WindowsDefault : new DacEndpoint(dac),
        EnableLinearPhase = linearPhase,
        EnableAdaptiveLoudness = adaptiveLoudness,
        EnablePolyverseWider = wider,
        EnableFootstepCompressor = footstepCompressor,
        EnableVstPlugins = enableVstPlugins,
        EnableHrirInclude = enableHrirInclude,
    };

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
static bool DetectVstPluginsAvailable()
{
    var eqApoPath = Microsoft.Win32.Registry.GetValue(
        @"HKEY_LOCAL_MACHINE\SOFTWARE\EqualizerAPO", "InstallPath", null) as string;
    if (string.IsNullOrEmpty(eqApoPath)) return false;
    var vstDir = Path.Combine(eqApoPath, "VSTPlugins");
    if (!Directory.Exists(vstDir)) return false;
    // Check for at least TDR Nova and LoudMax — the two most-used plugins in our chain.
    var files = Directory.GetFiles(vstDir, "*.dll", SearchOption.TopDirectoryOnly);
    bool hasTdrNova = files.Any(f => Path.GetFileName(f).Contains("TDR Nova", StringComparison.OrdinalIgnoreCase));
    bool hasLoudMax = files.Any(f => Path.GetFileName(f).Contains("LoudMax", StringComparison.OrdinalIgnoreCase));
    return hasTdrNova && hasLoudMax;
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
static bool DetectHesuviInstalled()
{
    var eqApoPath = Microsoft.Win32.Registry.GetValue(
        @"HKEY_LOCAL_MACHINE\SOFTWARE\EqualizerAPO", "InstallPath", null) as string;
    if (string.IsNullOrEmpty(eqApoPath)) return false;
    // HeSuVi ships HRIR WAVs into EqualizerAPO\config\HeSuVi\hrir\
    return Directory.Exists(Path.Combine(eqApoPath, "config", "HeSuVi", "hrir"));
}

static DetectionSnapshot DetectAudio()
{
    var db = DeviceDatabase.LoadEmbedded();
    var enumerator = new WindowsDeviceEnumerator();
    var matcher = new DeviceMatcher(db);
    var service = new DeviceDetectionService(enumerator, matcher);
    return service.Snapshot();
}

static void PrintDetection(DetectionSnapshot snapshot)
{
    Console.WriteLine($"  Headphone: {snapshot.PrimaryHeadphone?.Model ?? "(none recognized)"}");
    if (snapshot.PrimaryHeadphone is { } hp)
        Console.WriteLine($"             AutoEQ slug: {hp.AutoeqSlug}");
    Console.WriteLine($"  DAC:       {snapshot.MultiEndpointDac?.Model ?? "(none recognized)"}");
    if (snapshot.MultiEndpointDac is { } d)
    {
        Console.WriteLine($"             Game endpoint:  {d.GameEndpoint}");
        Console.WriteLine($"             Voice endpoint: {d.VoiceEndpoint}");
    }
    Console.WriteLine($"  Devices scanned: {snapshot.Devices.Count}");
}

static void InstallProfile(ProfileInput input)
{
    var locator = new RegistryEqApoLocator();
    if (!locator.IsInstalled)
    {
        Console.Error.WriteLine("Equalizer APO is not installed. Install it first from https://equalizerapo.com.");
        Environment.Exit(3);
        return;
    }
    var installer = new WarzoneConfigInstaller(locator, new AtomicFileWriter());
    var path = installer.Install(input);
    Console.WriteLine($"Installed Hear It Loud config to:");
    Console.WriteLine($"  {path}");
    Console.WriteLine();
    Console.WriteLine("Equalizer APO will hot-reload automatically (no restart needed).");
    Console.WriteLine("In Warzone: Settings -> Audio -> Audio Mix = Headphones Bass Cut,");
    Console.WriteLine("Surround Sound = 7.1, Music = 0, Enhanced Headphone Mode = OFF.");
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
static void TryEnableLoudnessEq(string? gameEndpointFriendlyName)
{
    if (gameEndpointFriendlyName is null) return;
    try
    {
        var endpointGuid = FindRenderEndpointGuid(gameEndpointFriendlyName);
        if (endpointGuid is null)
        {
            Console.WriteLine($"(Skipped Windows Loudness EQ: endpoint '{gameEndpointFriendlyName}' not found in registry.)");
            return;
        }
        var ctrl = new RegistryLoudnessEqController();
        ctrl.Write(endpointGuid, new LoudnessEqState(Enabled: true, ReleaseTime: LoudnessEqState.MinReleaseTime));
        Console.WriteLine($"Windows Loudness EQ enabled (SHORT release) on {gameEndpointFriendlyName}.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"(Windows Loudness EQ step skipped — {ex.Message})");
    }
}

[System.Runtime.Versioning.SupportedOSPlatform("windows")]
static string? FindRenderEndpointGuid(string friendlyName)
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
        {
            return sub; // already in {guid} form from registry
        }
    }
    return null;
}
