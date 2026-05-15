using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.WindowsIntegration.Updates;

namespace WarzoneEQ.Cli;

[SupportedOSPlatform("windows")]
public sealed class MainForm : Form
{
    private static readonly Color BgDark      = Color.FromArgb(28, 28, 32);
    private static readonly Color BgDarker    = Color.FromArgb(20, 20, 24);
    private static readonly Color BgDarkest   = Color.FromArgb(18, 18, 22);
    private static readonly Color FgText      = Color.FromArgb(230, 230, 230);
    private static readonly Color FgMuted     = Color.FromArgb(170, 170, 170);
    private static readonly Color AccentGold  = Color.FromArgb(240, 200, 80);

    private readonly TextBox _log;
    private readonly Button[] _easyButtons;
    private readonly AdvancedTab _advanced;
    private readonly Label _updateStatusLabel;
    private readonly Button _btnCheckUpdate;
    private CancellationTokenSource? _cts;
    private UpdateInfo? _pendingUpdate;

    public MainForm() : this(initialPreset: null) { }

    public MainForm(WorkflowOptions? initialPreset)
    {
        Text = "Hear It Loud — by MasterMind George";
        MinimumSize = new Size(820, 680);
        Size = new Size(900, 740);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgDark;
        ForeColor = FgText;
        Font = new Font("Segoe UI", 10F);

        var title = new Label
        {
            Text = "HEAR IT LOUD",
            Dock = DockStyle.Top,
            Height = 50,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = AccentGold,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = BgDarker,
        };
        var subtitle = new Label
        {
            Text = "by MasterMind George",
            Dock = DockStyle.Top,
            Height = 22,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            ForeColor = FgMuted,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = BgDarker,
        };

        var tabs = new TabControl
        {
            Dock = DockStyle.Top,
            Height = 360,
            Appearance = TabAppearance.FlatButtons,
            SizeMode = TabSizeMode.Fixed,
            ItemSize = new Size(140, 30),
            Padding = new Point(20, 6),
        };

        var easyTab = new TabPage("  Easy Mode  ") { BackColor = BgDark, Padding = new Padding(20, 10, 20, 10) };
        var advTab  = new TabPage("  Advanced  ") { BackColor = BgDark, Padding = new Padding(20, 10, 20, 10) };

        var easyButtons = BuildEasyTab(easyTab);
        _easyButtons = easyButtons;
        _advanced = new AdvancedTab(this);
        advTab.Controls.Add(_advanced);

        tabs.TabPages.Add(easyTab);
        tabs.TabPages.Add(advTab);

        _log = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            BackColor = BgDarkest,
            ForeColor = Color.FromArgb(210, 210, 210),
            Font = new Font("Consolas", 9F),
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = true,
            Text = WelcomeMessage(),
        };
        var logPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 4, 20, 16) };
        logPanel.Controls.Add(_log);

        var statusBar = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = BgDarker,
            Padding = new Padding(20, 4, 20, 4),
        };
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        statusBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var versionLabel = new Label
        {
            Text = "v" + UpdateChecker.CurrentVersion,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = FgMuted,
            BackColor = BgDarker,
            Font = new Font("Segoe UI", 9F),
        };
        _updateStatusLabel = new Label
        {
            Text = "Checking for updates…",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = FgMuted,
            BackColor = BgDarker,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
        };
        _btnCheckUpdate = new Button
        {
            Text = "Check for Updates",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(70, 90, 110),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F),
            UseVisualStyleBackColor = false,
        };
        _btnCheckUpdate.FlatAppearance.BorderSize = 0;
        _btnCheckUpdate.Click += async (_, _) =>
        {
            if (_pendingUpdate is not null) await InstallPendingUpdateAsync();
            else await CheckForUpdatesAsync();
        };

        statusBar.Controls.Add(versionLabel, 0, 0);
        statusBar.Controls.Add(_updateStatusLabel, 1, 0);
        statusBar.Controls.Add(_btnCheckUpdate, 2, 0);

        Controls.Add(logPanel);
        Controls.Add(tabs);
        Controls.Add(subtitle);
        Controls.Add(title);
        Controls.Add(statusBar);

        // Kick off a background update check as soon as the form is visible.
        // Silent when up-to-date or offline; shows a yellow banner if newer.
        Shown += async (_, _) => await CheckForUpdatesAsync();

        // Restore last-applied Advanced controls, or honor an explicit preset
        // passed in (e.g. from double-clicking a .warzeq file).
        var startupPreset = initialPreset ?? Settings.TryLoad();
        if (startupPreset is not null)
        {
            _advanced.LoadFrom(startupPreset);
            if (initialPreset is not null)
            {
                tabs.SelectedTab = advTab;
                Log("[preset] Loaded from .warzeq file. Click Apply (Install) to use it.");
            }
        }
    }

    internal void OnAdvancedApplied(WorkflowOptions options) => Settings.Save(options);

    private async Task CheckForUpdatesAsync()
    {
        if (IsDisposed) return;
        _btnCheckUpdate.Enabled = false;
        _updateStatusLabel.Text = "Checking for updates…";
        _updateStatusLabel.ForeColor = FgMuted;
        try
        {
            var info = await UpdateChecker.CheckAsync();
            if (IsDisposed) return;
            if (info is null)
            {
                _pendingUpdate = null;
                _updateStatusLabel.Text = "Up to date.";
                _updateStatusLabel.ForeColor = FgMuted;
                _btnCheckUpdate.Text = "Check for Updates";
                _btnCheckUpdate.BackColor = Color.FromArgb(70, 90, 110);
            }
            else
            {
                _pendingUpdate = info;
                _updateStatusLabel.Text = $"Update available: {info.LatestTag}";
                _updateStatusLabel.ForeColor = AccentGold;
                _btnCheckUpdate.Text = "Update Now";
                _btnCheckUpdate.BackColor = Color.FromArgb(50, 130, 60);
            }
        }
        catch (Exception ex)
        {
            if (IsDisposed) return;
            _updateStatusLabel.Text = "Update check failed (offline?)";
            _updateStatusLabel.ForeColor = FgMuted;
            Log($"[update] check failed: {ex.Message}");
        }
        finally
        {
            if (!IsDisposed) _btnCheckUpdate.Enabled = true;
        }
    }

    private async Task InstallPendingUpdateAsync()
    {
        if (_pendingUpdate is null) return;
        var info = _pendingUpdate;
        var sizeMb = info.InstallerSizeBytes / 1024 / 1024;
        var confirm = MessageBox.Show(
            $"Download and install {info.LatestTag}?\n\n" +
            $"Size: ~{sizeMb} MB\n\n" +
            "Your settings (Advanced tab values + any saved presets) and the" +
            " current Equalizer APO chain will be preserved.\n\n" +
            "The app will close, the installer will run silently in the background," +
            " and the app will reopen once the upgrade finishes.",
            "Hear It Loud — Update Available",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);
        if (confirm != DialogResult.Yes) return;

        _btnCheckUpdate.Enabled = false;
        ClearLog();
        Log($"=== Updating to {info.LatestTag} ===");
        Log("");
        try
        {
            var path = await UpdateChecker.DownloadAsync(info.InstallerUrl, Log);
            Log("");
            Log("Launching installer. You'll see a UAC prompt (one click — needed");
            Log("because EQ APO's config dir is under Program Files).");
            UpdateChecker.LaunchInstaller(path);
            // Installer will close us via CloseApplications=yes; we just wait briefly.
            await Task.Delay(2000);
            Application.Exit();
        }
        catch (Exception ex)
        {
            Log("");
            Log($"[update] failed: {ex.Message}");
            _btnCheckUpdate.Enabled = true;
        }
    }

    private Button[] BuildEasyTab(TabPage tab)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            BackColor = BgDark,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 4; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));

        var btnAuto      = MakeBigButton("Auto Setup\n(recommended for first run)", Color.FromArgb(50, 130, 60));
        var btnFootstep  = MakeBigButton("Footstep Priority\n(max competitive clarity)", Color.FromArgb(180, 110, 30));
        var btnDiagnose  = MakeBigButton("Diagnose && Auto-Fix\n(if anything sounds wrong)", Color.FromArgb(60, 100, 160));
        var btnDetect    = MakeBigButton("Detect My Hardware", Color.FromArgb(80, 80, 90));
        var btnSettings  = MakeBigButton("Open Windows Sound Settings", Color.FromArgb(80, 80, 90));
        var btnPlugins   = MakeBigButton("Get Optional Plugins\n(for the full-quality chain)", Color.FromArgb(80, 80, 90));
        var btnCheatSheet = MakeBigButton("Show Audio Cheat Sheet\n(in-game + Windows settings to use)", Color.FromArgb(120, 70, 140));

        grid.Controls.Add(btnAuto, 0, 0);
        grid.Controls.Add(btnFootstep, 1, 0);
        grid.Controls.Add(btnDiagnose, 0, 1);
        grid.Controls.Add(btnDetect, 1, 1);
        grid.Controls.Add(btnSettings, 0, 2);
        grid.Controls.Add(btnPlugins, 1, 2);
        grid.Controls.Add(btnCheatSheet, 0, 3);
        grid.SetColumnSpan(btnCheatSheet, 2);
        tab.Controls.Add(grid);

        btnAuto.Click       += (_, _) => Run("Auto Setup", w => Workflows.Auto(w, new WorkflowOptions()));
        btnFootstep.Click   += (_, _) => Run("Footstep Priority", w => Workflows.Auto(w, new WorkflowOptions(FootstepPriority: true)));
        btnDiagnose.Click   += (_, _) => Run("Diagnose & Fix", w => Workflows.Diagnose(w, applyFix: true));
        btnDetect.Click     += (_, _) => Run("Detect Hardware", w => Workflows.Detect(w, basic: false));
        btnSettings.Click   += (_, _) => OpenSoundSettings();
        btnPlugins.Click    += (_, _) => OpenPluginGuide();
        btnCheatSheet.Click += (_, _) => ShowCheatSheet();

        return new[] { btnAuto, btnFootstep, btnDiagnose, btnDetect, btnSettings, btnPlugins, btnCheatSheet };
    }

    private void ShowCheatSheet()
    {
        ClearLog();
        foreach (var line in AudioCheatSheet.Split('\n')) Log(line.TrimEnd('\r'));
    }

    private const string AudioCheatSheet = @"=== Warzone Audio Cheat Sheet ===

IN-GAME (Settings -> Audio)
  Audio Mix:                Headphones Bass Cut   (REQUIRED)
  Surround Sound:           7.1                   (REQUIRED)
  Music Volume:             0                     (REQUIRED)
  Enhanced Headphone Mode:  OFF                   (REQUIRED)
  Master Volume:            60-75% (not max — clips above 80%)
  Voice Chat Volume:        30-50% lower than master
  Voice Chat Effect:        None (no megaphone/helmet)
  Hit Marker Sound:         leave ON
  Mono Audio:               OFF

WINDOWS (Sound Control Panel — click ""Open Windows Sound Settings"" above)
  1. Set the GAME endpoint as the default playback device.
       (Sound Blaster GC7/G6 owners: pick the one named ""... Game"".)
  2. Communications tab -> ""Do nothing"" (disables 80% auto-ducking
       when Discord etc. detects voice activity — kills footsteps).
  3. Endpoint -> Properties -> Advanced -> 24-bit, 48000 Hz
       (Studio Quality). Matches Warzone's native rate, no resampling.
  4. Endpoint -> Properties -> Spatial sound -> Off.
       Hear It Loud's HRIR replaces it; doubling them smears the image.
  5. Endpoint -> Properties -> Advanced ->
       uncheck ""Allow applications to take exclusive control"".

HARDWARE / PRACTICAL
  - Wired > Bluetooth. BT adds 100-300ms latency and most codecs
    (SBC, AAC) roll off above 8 kHz — exactly where footsteps live.
  - Volume sweet spot: comfortable conversation level. Too loud and
    your ear's self-protective reflex compresses quiet detail.
  - Battle.net / Steam app volume = max. Let the in-game master be
    the only attenuator (less quantization noise).

If a setting in this list looks wrong on your friend's PC, click
""Diagnose & Auto-Fix"" — it catches several of the Windows-side
issues automatically and prints the click path for the rest.";

    internal static Button MakeBigButton(string text, Color accent)
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

    // Run a long-running operation off the UI thread. Disables all action
    // buttons (easy + advanced) while it runs.
    internal void Run(string actionName, Func<Action<string>, int> work)
    {
        if (_cts is not null) { Log("(busy — wait for the current action to finish)"); return; }
        _cts = new CancellationTokenSource();
        SetAllButtonsEnabled(false);
        ClearLog();
        Log($"=== {actionName} ===");
        Log("");

        Task.Run(() =>
        {
            int exitCode;
            try { exitCode = work(Log); }
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
                SetAllButtonsEnabled(true);
            });
        });
    }

    private void SetAllButtonsEnabled(bool enabled)
    {
        foreach (var b in _easyButtons) b.Enabled = enabled;
        _advanced.SetButtonsEnabled(enabled);
    }

    internal void Log(string line)
    {
        if (InvokeRequired) { BeginInvoke(() => Log(line)); return; }
        _log.AppendText(line + Environment.NewLine);
        _log.SelectionStart = _log.Text.Length;
        _log.ScrollToCaret();
    }

    internal void ClearLog() { if (InvokeRequired) { BeginInvoke(ClearLog); return; } _log.Clear(); }

    private static void OpenSoundSettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }); }
        catch { Process.Start(new ProcessStartInfo("control.exe", "mmsys.cpl") { UseShellExecute = true }); }
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
        try { Process.Start(new ProcessStartInfo("https://www.tokyodawn.net/tdr-nova/") { UseShellExecute = true }); }
        catch { /* user can copy-paste from the log */ }
    }

    private static string WelcomeMessage() =>
        "Welcome to Hear It Loud — by MasterMind George." + Environment.NewLine +
        Environment.NewLine +
        "EASY MODE: click \"Auto Setup\" if this is your first time." + Environment.NewLine +
        "The EQ only applies to Call of Duty — Discord, Spotify, browsers, etc." + Environment.NewLine +
        "all pass through with no processing." + Environment.NewLine +
        Environment.NewLine +
        "ADVANCED tab: pick mode + curve + intensity by hand, toggle linear phase /" + Environment.NewLine +
        "adaptive loudness / Wider, preview the generated config, then apply.";
}

// Second tab — manual EQ controls for tech-savvy users.
[SupportedOSPlatform("windows")]
internal sealed class AdvancedTab : UserControl
{
    private readonly MainForm _owner;
    private readonly ComboBox _mode;
    private readonly ComboBox _curve;
    private readonly TrackBar _intensity;
    private readonly Label _intensityLabel;
    private readonly TextBox _headphone;
    private readonly TextBox _dac;
    private readonly CheckBox _linearPhase;
    private readonly CheckBox _adaptiveLoudness;
    private readonly CheckBox _wider;
    private readonly CheckBox _compressor;
    private readonly CheckBox _basic;
    private readonly Button _btnPreview;
    private readonly Button _btnApply;
    private readonly Button _btnReset;
    private readonly Button _btnAutofillHardware;
    private readonly Button _btnSavePreset;
    private readonly Button _btnLoadPreset;

    public AdvancedTab(MainForm owner)
    {
        _owner = owner;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(28, 28, 32);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 7,
            ColumnStyles =
            {
                new ColumnStyle(SizeType.Absolute, 110),
                new ColumnStyle(SizeType.Percent, 50),
                new ColumnStyle(SizeType.Absolute, 110),
                new ColumnStyle(SizeType.Percent, 50),
            },
            Padding = new Padding(8),
        };
        for (int i = 0; i < 7; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        _mode = MakeCombo(Enum.GetNames<AudioMode>(), nameof(AudioMode.Competitive));
        _curve = MakeCombo(Enum.GetNames<FpsCurveName>(), nameof(FpsCurveName.Moderate));

        _intensity = new TrackBar
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 100,
            Value = 100,
            TickFrequency = 25,
            BackColor = Color.FromArgb(28, 28, 32),
        };
        _intensityLabel = MakeLabel("Intensity: 100%");
        _intensity.ValueChanged += (_, _) => _intensityLabel.Text = $"Intensity: {_intensity.Value}%";

        _headphone = MakeTextBox(placeholder: "(auto-detect)");
        _dac       = MakeTextBox(placeholder: "(auto-detect)");

        _btnAutofillHardware = MakeSmallButton("Detect");
        _btnAutofillHardware.Click += (_, _) => AutofillHardware();

        _linearPhase      = MakeCheck("Linear phase EQ");
        _adaptiveLoudness = MakeCheck("Adaptive loudness");
        _wider            = MakeCheck("Polyverse Wider (sides+rear)");
        _compressor       = MakeCheck("Footstep upward compressor", checkedState: true);
        _basic            = MakeCheck("Basic mode (skip VST plugins)");

        _btnPreview = MainForm.MakeBigButton("Preview Config", Color.FromArgb(60, 100, 160));
        _btnApply   = MainForm.MakeBigButton("Apply (Install)", Color.FromArgb(50, 130, 60));
        _btnReset   = MainForm.MakeBigButton("Reset to Defaults", Color.FromArgb(80, 80, 90));
        _btnPreview.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _btnApply.Font   = new Font("Segoe UI", 10F, FontStyle.Bold);
        _btnReset.Font   = new Font("Segoe UI", 10F, FontStyle.Bold);

        _btnPreview.Click += (_, _) => _owner.Run("Preview", w => Workflows.Print(w, BuildInputFromControls()));
        _btnApply.Click   += (_, _) =>
        {
            var opts = ReadOptionsFromControls();
            _owner.OnAdvancedApplied(opts);
            _owner.Run("Apply (Install)", w => Workflows.Install(w, Workflows.BuildInput(opts)));
        };
        _btnReset.Click   += (_, _) => ResetToDefaults();

        _btnSavePreset = MakeSmallButton("Save Preset…");
        _btnLoadPreset = MakeSmallButton("Load Preset…");
        _btnSavePreset.Click += (_, _) => SavePresetToFile();
        _btnLoadPreset.Click += (_, _) => LoadPresetFromFile();

        layout.Controls.Add(MakeLabel("Mode:"),      0, 0);
        layout.Controls.Add(_mode,                   1, 0);
        layout.Controls.Add(MakeLabel("Curve:"),     2, 0);
        layout.Controls.Add(_curve,                  3, 0);

        layout.Controls.Add(_intensityLabel,         0, 1);
        layout.SetColumnSpan(_intensityLabel, 1);
        layout.Controls.Add(_intensity,              1, 1);
        layout.SetColumnSpan(_intensity, 3);

        layout.Controls.Add(MakeLabel("Headphone:"), 0, 2);
        layout.Controls.Add(_headphone,              1, 2);
        layout.Controls.Add(MakeLabel("DAC:"),       2, 2);
        var dacRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        dacRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        dacRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        dacRow.Controls.Add(_dac, 0, 0);
        dacRow.Controls.Add(_btnAutofillHardware, 1, 0);
        layout.Controls.Add(dacRow, 3, 2);

        var checks1 = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(28, 28, 32) };
        checks1.Controls.AddRange(new Control[] { _linearPhase, _adaptiveLoudness, _wider });
        layout.Controls.Add(checks1, 0, 3);
        layout.SetColumnSpan(checks1, 4);

        var checks2 = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(28, 28, 32) };
        checks2.Controls.AddRange(new Control[] { _compressor, _basic });
        layout.Controls.Add(checks2, 0, 4);
        layout.SetColumnSpan(checks2, 4);

        var actionRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Height = 50 };
        for (int i = 0; i < 3; i++) actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3F));
        actionRow.Controls.Add(_btnPreview, 0, 0);
        actionRow.Controls.Add(_btnApply, 1, 0);
        actionRow.Controls.Add(_btnReset, 2, 0);
        layout.Controls.Add(actionRow, 0, 5);
        layout.SetColumnSpan(actionRow, 4);

        var presetRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Height = 32 };
        presetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        presetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        presetRow.Controls.Add(_btnSavePreset, 0, 0);
        presetRow.Controls.Add(_btnLoadPreset, 1, 0);
        layout.Controls.Add(presetRow, 0, 6);
        layout.SetColumnSpan(presetRow, 4);

        Controls.Add(layout);
    }

    public void LoadFrom(WorkflowOptions opts)
    {
        if (InvokeRequired) { BeginInvoke(() => LoadFrom(opts)); return; }
        _mode.SelectedItem = opts.Mode.ToString();
        _curve.SelectedItem = opts.Curve.ToString();
        _intensity.Value = Math.Clamp((int)Math.Round(opts.Intensity * 100), 0, 100);
        _headphone.Text = opts.Headphone ?? "";
        _dac.Text = opts.Dac ?? "";
        _linearPhase.Checked = opts.LinearPhase;
        _adaptiveLoudness.Checked = opts.AdaptiveLoudness;
        _wider.Checked = opts.Wider;
        _compressor.Checked = opts.FootstepCompressor;
        _basic.Checked = opts.Basic;
    }

    private WorkflowOptions ReadOptionsFromControls() => new(
        Mode: Enum.Parse<AudioMode>((string)_mode.SelectedItem!),
        Curve: Enum.Parse<FpsCurveName>((string)_curve.SelectedItem!),
        Intensity: _intensity.Value / 100.0,
        Headphone: string.IsNullOrWhiteSpace(_headphone.Text) ? null : _headphone.Text.Trim(),
        Dac: string.IsNullOrWhiteSpace(_dac.Text) ? null : _dac.Text.Trim(),
        LinearPhase: _linearPhase.Checked,
        AdaptiveLoudness: _adaptiveLoudness.Checked,
        Wider: _wider.Checked,
        FootstepCompressor: _compressor.Checked,
        Basic: _basic.Checked);

    private void SavePresetToFile()
    {
        using var dlg = new SaveFileDialog
        {
            Title = "Save Hear It Loud preset",
            Filter = $"{Presets.FileExtensionDescription} (*{Presets.FileExtension})|*{Presets.FileExtension}",
            DefaultExt = Presets.FileExtension.TrimStart('.'),
            FileName = "my-warzone-preset" + Presets.FileExtension,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            Presets.Save(dlg.FileName, ReadOptionsFromControls());
            _owner.Log($"[preset] Saved to {dlg.FileName}.");
            _owner.Log("Share this .warzeq file with a friend — they can double-click to load it.");
        }
        catch (Exception ex) { _owner.Log($"[error] Saving preset failed: {ex.Message}"); }
    }

    private void LoadPresetFromFile()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Load Hear It Loud preset",
            Filter = $"{Presets.FileExtensionDescription} (*{Presets.FileExtension})|*{Presets.FileExtension}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var opts = Presets.Load(dlg.FileName);
            LoadFrom(opts);
            _owner.Log($"[preset] Loaded from {dlg.FileName}. Click Apply (Install) to use it.");
        }
        catch (Exception ex) { _owner.Log($"[error] Loading preset failed: {ex.Message}"); }
    }

    public void SetButtonsEnabled(bool enabled)
    {
        _btnPreview.Enabled = enabled;
        _btnApply.Enabled = enabled;
        _btnReset.Enabled = enabled;
        _btnAutofillHardware.Enabled = enabled;
        _btnSavePreset.Enabled = enabled;
        _btnLoadPreset.Enabled = enabled;
    }

    private void AutofillHardware()
    {
        _owner.Run("Detect Hardware (for Advanced fields)", w =>
        {
            try
            {
                var snap = Workflows.DetectHardware();
                Workflows.PrintDetection(w, snap, Workflows.DetectVstAvailable(), Workflows.DetectHesuviInstalled());
                _owner.BeginInvoke(() =>
                {
                    if (snap.PrimaryHeadphone is { } hp) _headphone.Text = hp.AutoeqSlug;
                    if (snap.MultiEndpointDac is { } d) _dac.Text = d.GameEndpoint;
                });
                return 0;
            }
            catch (Exception ex)
            {
                w($"[error] {ex.Message}");
                return 1;
            }
        });
    }

    private void ResetToDefaults()
    {
        _mode.SelectedItem = nameof(AudioMode.Competitive);
        _curve.SelectedItem = nameof(FpsCurveName.Moderate);
        _intensity.Value = 100;
        _headphone.Text = "";
        _dac.Text = "";
        _linearPhase.Checked = false;
        _adaptiveLoudness.Checked = false;
        _wider.Checked = false;
        _compressor.Checked = true;
        _basic.Checked = false;
        _owner.Log("[reset] Advanced controls back to defaults.");
    }

    private ProfileInput BuildInputFromControls() => Workflows.BuildInput(ReadOptionsFromControls());

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(230, 230, 230),
        BackColor = Color.FromArgb(28, 28, 32),
    };

    private static ComboBox MakeCombo(string[] items, string selected)
    {
        var c = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(40, 40, 46),
            ForeColor = Color.FromArgb(230, 230, 230),
            FlatStyle = FlatStyle.Flat,
        };
        c.Items.AddRange(items);
        c.SelectedItem = selected;
        return c;
    }

    private static TextBox MakeTextBox(string placeholder) => new()
    {
        Dock = DockStyle.Fill,
        BackColor = Color.FromArgb(40, 40, 46),
        ForeColor = Color.FromArgb(230, 230, 230),
        BorderStyle = BorderStyle.FixedSingle,
        PlaceholderText = placeholder,
    };

    private static CheckBox MakeCheck(string text, bool checkedState = false) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(6, 4, 16, 4),
        ForeColor = Color.FromArgb(230, 230, 230),
        BackColor = Color.FromArgb(28, 28, 32),
        Checked = checkedState,
    };

    private static Button MakeSmallButton(string text)
    {
        var b = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(80, 80, 90),
            ForeColor = Color.White,
            Margin = new Padding(4, 0, 0, 0),
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }
}
