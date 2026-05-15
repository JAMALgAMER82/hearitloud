using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace WarzoneEQ.Cli;

[SupportedOSPlatform("windows")]
public sealed class MainForm : Form
{
    private readonly TextBox _log;
    private readonly Button _btnAuto;
    private readonly Button _btnFootstep;
    private readonly Button _btnDiagnose;
    private readonly Button _btnDetect;
    private readonly Button _btnSoundSettings;
    private readonly Button _btnPlugins;
    private CancellationTokenSource? _cts;

    public MainForm()
    {
        Text = "Hear It Loud — by MasterMind George";
        MinimumSize = new Size(720, 560);
        Size = new Size(820, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(28, 28, 32);
        ForeColor = Color.FromArgb(230, 230, 230);
        Font = new Font("Segoe UI", 10F);

        var title = new Label
        {
            Text = "HEAR IT LOUD",
            Dock = DockStyle.Top,
            Height = 56,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = Color.FromArgb(240, 200, 80),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(20, 20, 24),
        };

        var subtitle = new Label
        {
            Text = "Pick what you want — the app does the rest.",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Segoe UI", 10F, FontStyle.Italic),
            ForeColor = Color.FromArgb(170, 170, 170),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.FromArgb(20, 20, 24),
        };

        var buttonPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 220,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(20, 12, 20, 8),
            BackColor = Color.FromArgb(28, 28, 32),
        };
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 3; i++)
            buttonPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3F));

        _btnAuto         = MakeButton("Auto Setup\n(recommended for first run)", Color.FromArgb(50, 130, 60));
        _btnFootstep     = MakeButton("Footstep Priority\n(max competitive clarity)", Color.FromArgb(180, 110, 30));
        _btnDiagnose     = MakeButton("Diagnose && Auto-Fix\n(if anything sounds wrong)", Color.FromArgb(60, 100, 160));
        _btnDetect       = MakeButton("Detect My Hardware", Color.FromArgb(80, 80, 90));
        _btnSoundSettings = MakeButton("Open Windows Sound Settings", Color.FromArgb(80, 80, 90));
        _btnPlugins      = MakeButton("Get Optional Plugins\n(for the full-quality chain)", Color.FromArgb(80, 80, 90));

        buttonPanel.Controls.Add(_btnAuto, 0, 0);
        buttonPanel.Controls.Add(_btnFootstep, 1, 0);
        buttonPanel.Controls.Add(_btnDiagnose, 0, 1);
        buttonPanel.Controls.Add(_btnDetect, 1, 1);
        buttonPanel.Controls.Add(_btnSoundSettings, 0, 2);
        buttonPanel.Controls.Add(_btnPlugins, 1, 2);

        _log = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(18, 18, 22),
            ForeColor = Color.FromArgb(210, 210, 210),
            Font = new Font("Consolas", 9F),
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = true,
            Text = WelcomeMessage(),
        };
        var logPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 4, 20, 16) };
        logPanel.Controls.Add(_log);

        var footer = new Label
        {
            Text = "Tip: 'Auto Setup' is the right answer 95% of the time. 'Footstep Priority' for ranked.",
            Dock = DockStyle.Bottom,
            Height = 32,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(150, 150, 150),
            BackColor = Color.FromArgb(20, 20, 24),
        };

        Controls.Add(logPanel);
        Controls.Add(buttonPanel);
        Controls.Add(subtitle);
        Controls.Add(title);
        Controls.Add(footer);

        _btnAuto.Click          += (_, _) => RunInBackground("Auto Setup", w => Workflows.Auto(w, new WorkflowOptions()));
        _btnFootstep.Click      += (_, _) => RunInBackground("Footstep Priority", w => Workflows.Auto(w, new WorkflowOptions(FootstepPriority: true)));
        _btnDiagnose.Click      += (_, _) => RunInBackground("Diagnose & Fix", w => Workflows.Diagnose(w, applyFix: true));
        _btnDetect.Click        += (_, _) => RunInBackground("Detect Hardware", w => Workflows.Detect(w, basic: false));
        _btnSoundSettings.Click += (_, _) => OpenSoundSettings();
        _btnPlugins.Click       += (_, _) => OpenPluginGuide();
    }

    private static Button MakeButton(string text, Color accent)
    {
        var b = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = accent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Margin = new Padding(6),
            UseVisualStyleBackColor = false,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent, 0.15f);
        b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(accent, 0.15f);
        return b;
    }

    private void RunInBackground(string actionName, Func<Action<string>, int> work)
    {
        if (_cts is not null) { Log("(busy — wait for the current action to finish)"); return; }
        _cts = new CancellationTokenSource();
        SetButtonsEnabled(false);
        ClearLog();
        Log($"=== {actionName} ===");
        Log("");

        Task.Run(() =>
        {
            int exitCode;
            try
            {
                exitCode = work(Log);
            }
            catch (Exception ex)
            {
                Log("");
                Log($"[error] {ex.GetType().Name}: {ex.Message}");
                exitCode = 1;
            }

            BeginInvoke(() =>
            {
                Log("");
                Log(exitCode == 0 ? $"[done] {actionName} completed." : $"[exit {exitCode}] {actionName} finished with issues.");
                _cts?.Dispose();
                _cts = null;
                SetButtonsEnabled(true);
            });
        });
    }

    private void SetButtonsEnabled(bool enabled)
    {
        foreach (var b in new[] { _btnAuto, _btnFootstep, _btnDiagnose, _btnDetect, _btnSoundSettings, _btnPlugins })
            b.Enabled = enabled;
    }

    // Marshals log writes back to the UI thread. Worker threads call this freely.
    private void Log(string line)
    {
        if (InvokeRequired) { BeginInvoke(() => Log(line)); return; }
        _log.AppendText(line + Environment.NewLine);
        _log.SelectionStart = _log.Text.Length;
        _log.ScrollToCaret();
    }

    private void ClearLog() { if (InvokeRequired) { BeginInvoke(ClearLog); return; } _log.Clear(); }

    private static void OpenSoundSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true });
        }
        catch
        {
            Process.Start(new ProcessStartInfo("control.exe", "mmsys.cpl") { UseShellExecute = true });
        }
    }

    private void OpenPluginGuide()
    {
        ClearLog();
        Log("=== Optional VST plugins for the full-quality chain ===");
        Log("");
        Log("Hear It Loud works without these — but installing them unlocks the");
        Log("transient shaper, spectral ducker, and brick-wall limiter.");
        Log("");
        Log("1. TDR Nova (free, by Tokyo Dawn Labs)");
        Log("     https://www.tokyodawn.net/tdr-nova/");
        Log("");
        Log("2. LoudMax (free, by Thomas Mundt)");
        Log("     https://loudmaxdownload.com");
        Log("");
        Log("3. HeSuVi (free, virtual surround)");
        Log("     https://sourceforge.net/projects/hesuvi/");
        Log("");
        Log("Drop the downloaded .dll files into:");
        Log("     C:\\Program Files\\EqualizerAPO\\VSTPlugins\\");
        Log("HeSuVi has its own installer — point it at the EqualizerAPO config dir.");
        Log("");
        Log("Then click \"Diagnose & Auto-Fix\" to verify they're picked up.");

        TryOpen("https://www.tokyodawn.net/tdr-nova/");
    }

    private static void TryOpen(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* user can copy-paste from the log */ }
    }

    private static string WelcomeMessage() =>
        "Welcome to Hear It Loud — by MasterMind George." + Environment.NewLine +
        Environment.NewLine +
        "Click \"Auto Setup\" to detect your headphones + DAC and install a tuned" + Environment.NewLine +
        "EQ chain for Call of Duty Warzone. The EQ only applies to the game — Discord," + Environment.NewLine +
        "Spotify, browsers, and everything else pass through untouched." + Environment.NewLine +
        Environment.NewLine +
        "If you've never used this app before, just click \"Auto Setup\".";
}
