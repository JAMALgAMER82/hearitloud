using System.CommandLine;
using WarzoneEQ.Cli;
using WarzoneEQ.ConfigGenerator.Models;

// Dual-mode entry point: launching with no args opens the WinForms GUI for
// non-technical users; any arg invokes the CLI for power users + the
// installer's post-install scripts.
if (args.Length == 0 && OperatingSystem.IsWindows())
{
    System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.SystemAware);
    System.Windows.Forms.Application.EnableVisualStyles();
    System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
    System.Windows.Forms.Application.Run(new MainForm());
    return 0;
}

var modeOption       = new Option<AudioMode>("--mode", () => AudioMode.Competitive, "Audio mode: Competitive, Cinematic, Bypass, FootstepHunter.");
var curveOption      = new Option<FpsCurveName>("--curve", () => FpsCurveName.Moderate, "FPS target curve.");
var intensityOption  = new Option<double>("--intensity", () => 1.0, "FPS curve intensity, 0.0 to 1.0.");
var headphoneOption  = new Option<string?>("--headphone", "Headphone slug. Omit to auto-detect.");
var dacOption        = new Option<string?>("--dac", "DAC endpoint name to route to.");
var linearPhaseOption     = new Option<bool>("--linear-phase", "Enable linear-phase EQ (Cinematic only).");
var adaptiveLoudnessOption = new Option<bool>("--adaptive-loudness", "Enable adaptive loudness JSFX.");
var widerOption           = new Option<bool>("--wider", "Enable Polyverse Wider on sides+rear.");
var noCompressorOption    = new Option<bool>("--no-compressor", "Disable the footstep-band upward compressor.");
var detectOption          = new Option<bool>("--detect", "Detect current headphones + DAC and exit.");
var installOption         = new Option<bool>("--install", "Write the generated config into the Equalizer APO config directory.");
var autoOption            = new Option<bool>("--auto", "One-shot: detect hardware, route to detected DAC's Game endpoint, install.");
var basicOption           = new Option<bool>("--basic", "Omit Plugin: and HRIR Include lines so the chain works on vanilla Equalizer APO.");
var footstepPriorityOption = new Option<bool>("--footstep-priority", "Use the FootstepHunter profile (max positional clarity).");
var diagnoseOption        = new Option<bool>("--diagnose", "Run system checks.");
var fixOption             = new Option<bool>("--fix", "With --diagnose: apply safe auto-fixes.");
var selfTestOption        = new Option<bool>("--self-test", "Generate every profile/curve/basic combination and verify.");
var uninstallCleanupOption = new Option<bool>("--uninstall-cleanup", "Remove the Hear It Loud block from EQ APO master config (called by the uninstaller).");
var guiOption             = new Option<bool>("--gui", "Force the GUI to open (same as launching with no args).");

var root = new RootCommand("Hear It Loud — by MasterMind George. Generate, detect, and install Equalizer APO configs for Call of Duty Warzone.")
{
    modeOption, curveOption, intensityOption, headphoneOption, dacOption,
    linearPhaseOption, adaptiveLoudnessOption, widerOption, noCompressorOption,
    detectOption, installOption, autoOption, basicOption,
    footstepPriorityOption, diagnoseOption, fixOption, selfTestOption,
    uninstallCleanupOption, guiOption,
};

root.SetHandler(context =>
{
    var p = context.ParseResult;
    Action<string> write = Console.WriteLine;

    if (p.GetValueForOption(guiOption) && OperatingSystem.IsWindows())
    {
        System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.SystemAware);
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        System.Windows.Forms.Application.Run(new MainForm());
        return;
    }

    if (!OperatingSystem.IsWindows())
    {
        Console.Error.WriteLine("Hear It Loud requires Windows.");
        Environment.Exit(2);
        return;
    }

    if (p.GetValueForOption(selfTestOption))           { Environment.Exit(Workflows.RunSelfTest(write)); return; }
    if (p.GetValueForOption(diagnoseOption))           { Environment.Exit(Workflows.Diagnose(write, p.GetValueForOption(fixOption))); return; }
    if (p.GetValueForOption(uninstallCleanupOption))   { Environment.Exit(Workflows.UninstallCleanup(write)); return; }

    var opts = new WorkflowOptions(
        Mode: p.GetValueForOption(modeOption),
        Curve: p.GetValueForOption(curveOption),
        Intensity: p.GetValueForOption(intensityOption),
        Headphone: p.GetValueForOption(headphoneOption),
        Dac: p.GetValueForOption(dacOption),
        LinearPhase: p.GetValueForOption(linearPhaseOption),
        AdaptiveLoudness: p.GetValueForOption(adaptiveLoudnessOption),
        Wider: p.GetValueForOption(widerOption),
        FootstepCompressor: !p.GetValueForOption(noCompressorOption),
        Basic: p.GetValueForOption(basicOption),
        FootstepPriority: p.GetValueForOption(footstepPriorityOption));

    if (p.GetValueForOption(detectOption)) { Environment.Exit(Workflows.Detect(write, opts.Basic)); return; }
    if (p.GetValueForOption(autoOption))   { Environment.Exit(Workflows.Auto(write, opts)); return; }

    var input = Workflows.BuildInput(opts);
    if (p.GetValueForOption(installOption)) { Environment.Exit(Workflows.Install(write, input)); return; }
    Environment.Exit(Workflows.Print(write, input));
});

return await root.InvokeAsync(args);
