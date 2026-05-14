using System.CommandLine;
using WarzoneEQ.ConfigGenerator.Models;

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
    description: "Headphone slug (matches a file in warzone\\headphone-correction\\). Omit for none.");

var dacOption = new Option<string?>(
    name: "--dac",
    description: "DAC endpoint name to route to (e.g. 'Speakers Sound Blaster GC7 Game'). Omit for Windows default.");

var linearPhaseOption = new Option<bool>(name: "--linear-phase", description: "Enable linear-phase EQ (Cinematic only).");
var adaptiveLoudnessOption = new Option<bool>(name: "--adaptive-loudness", description: "Enable adaptive loudness JSFX.");
var widerOption = new Option<bool>(name: "--wider", description: "Enable Polyverse Wider on sides+rear.");
var noCompressorOption = new Option<bool>(name: "--no-compressor", description: "Disable the footstep-band upward compressor.");

var root = new RootCommand("Warzone EQ config generator — emits an Equalizer APO config to stdout.")
{
    modeOption, curveOption, intensityOption, headphoneOption, dacOption,
    linearPhaseOption, adaptiveLoudnessOption, widerOption, noCompressorOption,
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

    var input = new ProfileInput(mode)
    {
        FpsCurve = curve,
        Intensity = intensity,
        HeadphoneCorrection = headphone is null ? HeadphoneCorrection.None : new HeadphoneCorrection(headphone),
        DacEndpoint = dac is null ? DacEndpoint.WindowsDefault : new DacEndpoint(dac),
        EnableLinearPhase = linearPhase,
        EnableAdaptiveLoudness = adaptiveLoudness,
        EnablePolyverseWider = wider,
        EnableFootstepCompressor = !noCompressor,
    };

    Console.Write(WarzoneEQ.ConfigGenerator.ConfigGenerator.Generate(input));
});

return await root.InvokeAsync(args);
