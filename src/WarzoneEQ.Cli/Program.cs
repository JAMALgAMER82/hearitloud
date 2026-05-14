using System.CommandLine;
using WarzoneEQ.ConfigGenerator;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.DeviceDetection;
using WarzoneEQ.DeviceDetection.Detection;
using WarzoneEQ.DeviceDetection.Matching;
using WarzoneEQ.WindowsIntegration;
using WarzoneEQ.WindowsIntegration.EqApo;
using WarzoneEQ.WindowsIntegration.Files;

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

var root = new RootCommand("Warzone EQ — generate, detect, and install Equalizer APO configs for Call of Duty Warzone.")
{
    modeOption, curveOption, intensityOption, headphoneOption, dacOption,
    linearPhaseOption, adaptiveLoudnessOption, widerOption, noCompressorOption,
    detectOption, installOption, autoOption,
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

    if (detect)
    {
        var snapshot = DetectAudio();
        PrintDetection(snapshot);
        return;
    }

    if (auto)
    {
        var snapshot = DetectAudio();
        Console.WriteLine("Detected hardware:");
        PrintDetection(snapshot);
        Console.WriteLine();

        var autoHeadphone = snapshot.PrimaryHeadphone?.AutoeqSlug;
        var autoDac = snapshot.MultiEndpointDac?.GameEndpoint;
        if (autoHeadphone is null && autoDac is null)
        {
            Console.Error.WriteLine("No recognized headphones or DAC detected. Specify --headphone and/or --dac manually.");
            Environment.Exit(2);
            return;
        }

        var autoInput = BuildProfile(
            mode, curve, intensity,
            autoHeadphone ?? headphone, autoDac ?? dac,
            linearPhase, adaptiveLoudness, wider, !noCompressor);
        InstallProfile(autoInput);
        return;
    }

    var input = BuildProfile(
        mode, curve, intensity, headphone, dac,
        linearPhase, adaptiveLoudness, wider, !noCompressor);

    if (install) { InstallProfile(input); return; }

    Console.Write(ConfigGenerator.Generate(input));
});

return await root.InvokeAsync(args);

static ProfileInput BuildProfile(
    AudioMode mode, FpsCurveName curve, double intensity,
    string? headphone, string? dac,
    bool linearPhase, bool adaptiveLoudness, bool wider, bool footstepCompressor)
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
    };

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
    Console.WriteLine($"Installed Warzone EQ config to:");
    Console.WriteLine($"  {path}");
    Console.WriteLine();
    Console.WriteLine("Equalizer APO will hot-reload automatically (no restart needed).");
    Console.WriteLine("In Warzone: Settings -> Audio -> Audio Mix = Headphones Bass Cut,");
    Console.WriteLine("Surround Sound = 7.1, Music = 0, Enhanced Headphone Mode = OFF.");
}
