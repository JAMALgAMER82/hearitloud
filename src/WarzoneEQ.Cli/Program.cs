using System.CommandLine;
using System.Runtime.InteropServices;
using WarzoneEQ.Cli;
using WarzoneEQ.ConfigGenerator.Models;

// Subsystem is Windows (no console window in GUI mode). When CLI args are
// passed, attach to the parent terminal's console so stdout/stderr still flow
// to the shell that invoked us. ATTACH_PARENT_PROCESS = -1.
if (args.Length > 0 && OperatingSystem.IsWindows())
{
    _ = AttachConsole(-1);
}

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool AttachConsole(int processId);

// Dual-mode entry point:
//   - no args                  -> open WinForms GUI
//   - single .warzeq file arg  -> open GUI with that preset preloaded
//                                 (handles file-association double-click)
//   - any other args           -> CLI mode
if (OperatingSystem.IsWindows() && args.Length == 0)
{
    LaunchGui(initialPreset: null);
    return 0;
}
if (OperatingSystem.IsWindows() && args.Length == 1 && Presets.LooksLikePresetFile(args[0]))
{
    try { LaunchGui(initialPreset: Presets.Load(args[0])); }
    catch (Exception ex)
    {
        System.Windows.Forms.MessageBox.Show(
            $"Could not load preset:\n\n{ex.Message}",
            "Hear It Loud",
            System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Error);
    }
    return 0;
}

static void LaunchGui(WorkflowOptions? initialPreset)
{
    System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.SystemAware);
    System.Windows.Forms.Application.EnableVisualStyles();
    System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

    // v1.10.1: WinForms only routes UI-thread exceptions through
    // Application.ThreadException if the unhandled-exception mode is
    // explicitly set to CatchException. Without this, the default .NET
    // "Continue / Quit" dialog appears instead of our MessageBox + crash
    // log. Must be called BEFORE Application.Run.
    System.Windows.Forms.Application.SetUnhandledExceptionMode(
        System.Windows.Forms.UnhandledExceptionMode.CatchException);

    // v1.10: catch every unhandled exception during form construction OR
    // runtime, write a full stack trace to %APPDATA%\HearItLoud\crash.log,
    // and pop up a MessageBox so the user can see (and report) the error
    // instead of the app dying silently. "Fully automatic" debugging path.
    System.Windows.Forms.Application.ThreadException += (_, e) => HandleGuiCrash(e.Exception);
    AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    {
        if (e.ExceptionObject is Exception ex) HandleGuiCrash(ex);
    };

    // If the previous run crashed, show the user the crash log on startup
    // so they don't lose the report (useful when they didn't see the popup).
    ShowPreviousCrashLogIfAny();

    try { System.Windows.Forms.Application.Run(new MainForm(initialPreset)); }
    catch (Exception ex) { HandleGuiCrash(ex); }
}

static string CrashLogPath() => Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "HearItLoud", "crash.log");

static void HandleGuiCrash(Exception ex)
{
    try
    {
        var path = CrashLogPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var body =
            "=== Hear It Loud crashed at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===" + Environment.NewLine +
            "Version: " + (typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "?") + Environment.NewLine +
            "OS:      " + Environment.OSVersion + Environment.NewLine +
            Environment.NewLine + ex + Environment.NewLine;
        File.WriteAllText(path, body);

        System.Windows.Forms.MessageBox.Show(
            "Hear It Loud crashed:" + Environment.NewLine + Environment.NewLine +
            ex.GetType().Name + ": " + ex.Message + Environment.NewLine + Environment.NewLine +
            "Full crash log saved to:" + Environment.NewLine + path + Environment.NewLine + Environment.NewLine +
            "Please open an issue at https://github.com/JAMALgAMER82/hearitloud/issues with this file attached.",
            "Hear It Loud — Crash",
            System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Error);
    }
    catch { /* swallow — if even the crash handler crashes, all we can do is exit */ }
    Environment.Exit(1);
}

static void ShowPreviousCrashLogIfAny()
{
    try
    {
        var path = CrashLogPath();
        if (!File.Exists(path)) return;
        var content = File.ReadAllText(path);
        // Move the log aside so we don't show it on every subsequent launch.
        var archived = path + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Move(path, archived);
        System.Windows.Forms.MessageBox.Show(
            "The previous run of Hear It Loud crashed. Here's the report:" + Environment.NewLine + Environment.NewLine +
            (content.Length > 1200 ? content.Substring(0, 1200) + "..." : content) + Environment.NewLine + Environment.NewLine +
            "Archived to: " + archived,
            "Hear It Loud — Previous crash",
            System.Windows.Forms.MessageBoxButtons.OK,
            System.Windows.Forms.MessageBoxIcon.Warning);
    }
    catch { /* swallow */ }
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
var installPluginsOption = new Option<bool>("--install-plugins", "Download + install the optional VST/HRIR plugins (TDR Nova, LoudMax, HeSuVi). Called by the main installer post-setup.");
var guiOption             = new Option<bool>("--gui", "Force the GUI to open (same as launching with no args).");

var root = new RootCommand("Hear It Loud — by MasterMind George. Generate, detect, and install Equalizer APO configs for Call of Duty Warzone.")
{
    modeOption, curveOption, intensityOption, headphoneOption, dacOption,
    linearPhaseOption, adaptiveLoudnessOption, widerOption, noCompressorOption,
    detectOption, installOption, autoOption, basicOption,
    footstepPriorityOption, diagnoseOption, fixOption, selfTestOption,
    uninstallCleanupOption, installPluginsOption, guiOption,
};

root.SetHandler(context =>
{
    var p = context.ParseResult;
    Action<string> write = Console.WriteLine;

    if (p.GetValueForOption(guiOption) && OperatingSystem.IsWindows())
    {
        LaunchGui(initialPreset: null);
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
    if (p.GetValueForOption(installPluginsOption))     { Environment.Exit(Workflows.InstallOptionalPlugins(write)); return; }

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
