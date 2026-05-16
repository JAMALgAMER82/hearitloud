using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.WindowsIntegration.AbSwitcher;
using WarzoneEQ.WindowsIntegration.EqApo;
using WarzoneEQ.WindowsIntegration.Files;
using WarzoneEQ.WindowsIntegration.Plugins;
using WarzoneEQ.WindowsIntegration.ProcessWatch;
using WarzoneEQ.WindowsIntegration.Updates;

namespace WarzoneEQ.Cli;

[SupportedOSPlatform("windows")]
public sealed class MainForm : Form
{
    // Re-export Theme constants under the old names so existing field references
    // continue to compile. v1.5.0 switched the palette from slate-with-gold-accent
    // to deep-purple-with-lavender-accent; the field aliases preserve all the
    // existing layout code while shifting the colors.
    private static readonly Color BgDark      = Theme.BgRoot;
    private static readonly Color BgDarker    = Theme.BgChrome;
    private static readonly Color BgDarkest   = Theme.BgPanel;
    private static readonly Color FgText      = Theme.FgBody;
    private static readonly Color FgMuted     = Theme.FgMuted;
    private static readonly Color AccentGold  = Theme.Accent;

    private readonly TextBox _log;
    private readonly Button[] _easyButtons;
    private readonly AdvancedTab _advanced;
    private readonly Label _updateStatusLabel;
    private readonly Button _btnCheckUpdate;
    private CancellationTokenSource? _cts;
    private UpdateInfo? _pendingUpdate;
    private GlobalHotkey? _toggleHotkey;
    private ForegroundWatcher? _watcher;
    private NotifyIcon? _tray;
    private bool _autoSwitchEnabled = true;
    private ProcessProfileMap _profileMap = ProcessProfileMap.Default;

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

        var tabs = new UnderlinedTabControl
        {
            Dock = DockStyle.Top,
            Height = 360,
            Appearance = TabAppearance.FlatButtons,
            SizeMode = TabSizeMode.Fixed,
            ItemSize = new Size(160, 34),
            Padding = new Point(20, 8),
            Underline = AccentGold,
            TabBg = BgDarker,
            ActiveBg = BgDark,
            TabFg = FgText,
        };

        var easyTab = new TabPage("⚡  Easy Mode") { BackColor = BgDark, Padding = new Padding(20, 10, 20, 10) };
        var advTab  = new TabPage("⚙  Advanced")  { BackColor = BgDark, Padding = new Padding(20, 10, 20, 10) };
        var eqTab   = new TabPage("🎚  EQ Editor") { BackColor = BgDark, Padding = new Padding(20, 10, 20, 10) };

        var easyButtons = BuildEasyTab(easyTab);
        _easyButtons = easyButtons;
        _advanced = new AdvancedTab(this);
        advTab.Controls.Add(_advanced);
        var eqEditor = new EqEditorTab(this);
        eqTab.Controls.Add(eqEditor);

        tabs.TabPages.Add(easyTab);
        tabs.TabPages.Add(advTab);
        tabs.TabPages.Add(eqTab);

        _log = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            BackColor = BgDarkest,
            ForeColor = Theme.FgBody,
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
            BackColor = Theme.BtnInfo,
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

        // Register Ctrl+Shift+F8 as a global hotkey to toggle the A/B slot.
        // Silent fallback if registration fails (another app may own the combo).
        _toggleHotkey = new GlobalHotkey(GlobalHotkey.Mod.Ctrl | GlobalHotkey.Mod.Shift, Keys.F8, ToggleAbSlot);
        if (!_toggleHotkey.IsRegistered)
            Log("[hotkey] Ctrl+Shift+F8 already taken by another app — A/B toggle only via in-app button.");

        // Game auto-detection: when foreground process matches the game list,
        // switch to Slot A (footstep chain); otherwise Slot B (casual chain).
        _watcher = new ForegroundWatcher(TimeSpan.FromSeconds(1));
        _watcher.ForegroundChanged += OnForegroundChanged;

        BuildTrayIcon();

        FormClosed += (_, _) =>
        {
            _toggleHotkey?.Dispose();
            _watcher?.Dispose();
            _tray?.Dispose();
        };

        // Minimize-to-tray instead of taskbar so auto-switching keeps running.
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized) { Hide(); _tray?.ShowBalloonTip(800, "Hear It Loud", "Still running — auto-switching A/B by game.", ToolTipIcon.Info); }
        };
    }

    private void BuildTrayIcon()
    {
        _tray = new NotifyIcon
        {
            Icon = Icon, // form icon = exe icon (radiation-trefoil-with-ear)
            Text = "Hear It Loud",
            Visible = true,
        };
        var menu = new ContextMenuStrip();
        var showItem = menu.Items.Add("Show Hear It Loud", null, (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); });
        var toggleItem = menu.Items.Add("Toggle A/B (Ctrl+Shift+F8)", null, (_, _) => ToggleAbSlot());
        var autoItem = (ToolStripMenuItem)menu.Items.Add("Auto-switch by foreground game", null, (_, _) =>
        {
            _autoSwitchEnabled = !_autoSwitchEnabled;
            ((ToolStripMenuItem)menu.Items[2]).Checked = _autoSwitchEnabled;
            Log($"[auto-switch] {(_autoSwitchEnabled ? "ENABLED" : "disabled")}");
        });
        autoItem.Checked = _autoSwitchEnabled;
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => { _tray!.Visible = false; Application.Exit(); });
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); };
    }

    private void OnForegroundChanged(string? processName)
    {
        if (!_autoSwitchEnabled) return;
        if (InvokeRequired) { BeginInvoke(() => OnForegroundChanged(processName)); return; }
        try
        {
            var locator = new RegistryEqApoLocator();
            if (!locator.IsInstalled) return;
            var ab = new AbSwitcher(locator, new AtomicFileWriter());
            if (!File.Exists(ab.PathFor(AbSlot.A)) || !File.Exists(ab.PathFor(AbSlot.B))) return;
            var target = _profileMap.SlotFor(processName);
            if (ab.Active != target)
            {
                ab.SwitchTo(target);
                Log($"[auto-switch] foreground='{processName ?? "(none)"}' -> Slot {target}");
                if (_tray is not null) _tray.Text = $"Hear It Loud — Slot {target}";
            }
        }
        catch (Exception ex) { Log($"[auto-switch] failed: {ex.Message}"); }
    }

    internal void ToggleAbSlot()
    {
        if (InvokeRequired) { BeginInvoke(ToggleAbSlot); return; }
        try
        {
            var locator = new RegistryEqApoLocator();
            if (!locator.IsInstalled) { Log("[a/b] Equalizer APO not installed — can't toggle."); return; }
            var ab = new AbSwitcher(locator, new AtomicFileWriter());
            if (!File.Exists(ab.PathFor(AbSlot.A)) || !File.Exists(ab.PathFor(AbSlot.B)))
            {
                Log("[a/b] A/B slots not yet installed — click Auto Setup first.");
                return;
            }
            var newSlot = ab.Toggle();
            Log($"[a/b] switched to Slot {newSlot} (~60 ms hot-reload).");
        }
        catch (Exception ex) { Log($"[a/b] toggle failed: {ex.Message}"); }
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
                _btnCheckUpdate.BackColor = Theme.BtnInfo;
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
        // 4×2 grid of Cards. Each card has its own header (icon + title +
        // subtitle) painted by Card.cs, with the action button sitting in
        // the card's Body. The visual rhythm matches the reference design
        // — distinct surfaces with rounded corners, breathing room between.
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            BackColor = BgDark,
            Padding = new Padding(2),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 4; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));

        var cards = new[]
        {
            BuildEasyCard("⚡", "Auto Setup", "Recommended for first run.",          "Run Auto Setup", Theme.BtnSafe,       w => Workflows.Auto(w, new WorkflowOptions())),
            BuildEasyCard("👣", "Footstep Priority", "Max competitive clarity.",     "Run",            Theme.BtnAggressive, w => Workflows.Auto(w, new WorkflowOptions(FootstepPriority: true))),
            BuildEasyCard("🔧", "Diagnose && Auto-Fix", "If anything sounds wrong.", "Run",            Theme.BtnInfo,       w => Workflows.Diagnose(w, applyFix: true)),
            BuildEasyCard("🎧", "Detect My Hardware", "List headphones + DAC.",      "Detect",         Theme.BtnNeutral,    w => Workflows.Detect(w, basic: false)),
            BuildEasyCard("🔊", "Sound Settings",     "Open Windows audio panel.",   "Open",           Theme.BtnNeutral,    null, OpenSoundSettings),
            BuildEasyCard("🧩", "Optional Plugins",   "Full-quality chain helpers.", "Install / Info", Theme.BtnNeutral,    null, OpenPluginGuide),
            BuildEasyCard("📋", "Audio Cheat Sheet",  "In-game + Windows settings.", "Show",           Theme.BtnCheat,      null, ShowCheatSheet),
        };

        for (int i = 0; i < cards.Length; i++)
        {
            grid.Controls.Add(cards[i].Card, i % 2, i / 2);
        }
        tab.Controls.Add(grid);
        return cards.Select(c => c.Button).ToArray();
    }

    // Builds one card for the Easy Mode tab. `work` runs in the background
    // with live log streaming; `directAction` runs synchronously on the UI
    // thread (used for "open the Windows sound panel" type actions where
    // there's nothing to wait for).
    private (Card Card, Button Button) BuildEasyCard(
        string icon, string title, string subtitle, string buttonText, Color accent,
        Func<Action<string>, int>? work, Action? directAction = null)
    {
        var card = new Card
        {
            Title = title,
            Subtitle = subtitle,
            Icon = icon,
            Margin = new Padding(8),
            Dock = DockStyle.Fill,
        };
        var btn = MakeBigButton(buttonText, accent);
        btn.Dock = DockStyle.Fill;
        btn.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        card.Body.Controls.Add(btn);
        btn.Click += (_, _) =>
        {
            if (work is not null) Run(title, work);
            else directAction?.Invoke();
        };
        return (card, btn);
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
        Log("These three components unlock the transient shaper, spectral ducker,");
        Log("brick-wall limiter, and HRIR virtual surround that FootstepHunter and");
        Log("Cinematic modes use. Without them, the chain falls back to basic mode");
        Log("(filters and curves only — still useful, just less polished).");
        Log("");
        Log("Choose one of:");
        Log("  [A] Auto-install all three (recommended). UAC prompts will appear.");
        Log("  [B] Install just HeSuVi (most impactful — virtual surround).");
        Log("  [C] Open download pages manually.");
        Log("");
        Log("Type your choice in the next dialog box.");
        var dlg = new Form
        {
            Text = "Install Optional Plugins",
            Size = new Size(440, 220),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = BgDark,
            ForeColor = FgText,
            Font = new Font("Segoe UI", 10F),
        };
        var btnA = MakeBigButton("A: Install All Three", Color.FromArgb(50, 130, 60));
        var btnB = MakeBigButton("B: Just HeSuVi", Color.FromArgb(60, 100, 160));
        var btnC = MakeBigButton("C: Open Download Pages", Theme.BtnNeutral);
        btnA.Height = btnB.Height = btnC.Height = 44;
        btnA.Dock = btnB.Dock = btnC.Dock = DockStyle.Top;
        dlg.Controls.Add(btnC);
        dlg.Controls.Add(btnB);
        dlg.Controls.Add(btnA);
        btnA.Click += (_, _) => { dlg.Close(); InstallPluginsAsync(all: true, hesuviOnly: false); };
        btnB.Click += (_, _) => { dlg.Close(); InstallPluginsAsync(all: false, hesuviOnly: true); };
        btnC.Click += (_, _) => { dlg.Close(); OpenPluginPagesInBrowser(); };
        dlg.ShowDialog(this);
    }

    private void OpenPluginPagesInBrowser()
    {
        Log("");
        Log("Opening download pages in your browser:");
        foreach (var url in new[] {
            "https://www.tokyodawn.net/tdr-nova/",
            "https://loudmaxdownload.com",
            "https://sourceforge.net/projects/hesuvi/",
        })
        {
            Log($"  {url}");
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }
        Log("");
        Log("Drop the downloaded .dll files into:");
        Log("  C:\\Program Files\\EqualizerAPO\\VSTPlugins\\");
    }

    private void InstallPluginsAsync(bool all, bool hesuviOnly)
    {
        Run("Install Optional Plugins", w =>
        {
            var installer = new PluginInstaller(new RegistryEqApoLocator());
            int ok = 0, fail = 0;
            var queue = all
                ? new[] { OptionalPlugin.TdrNova, OptionalPlugin.LoudMax, OptionalPlugin.HeSuVi }
                : hesuviOnly
                    ? new[] { OptionalPlugin.HeSuVi }
                    : Array.Empty<OptionalPlugin>();
            foreach (var p in queue)
            {
                var task = installer.InstallAsync(p, w);
                task.GetAwaiter().GetResult();
                if (task.Result) ok++; else fail++;
            }
            w("");
            w($"Done: {ok} installed, {fail} failed.");
            w("Click \"Diagnose & Auto-Fix\" to verify and re-apply the chain.");
            return fail == 0 ? 0 : 1;
        });
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
    private readonly PurpleSlider _intensity;
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
        BackColor = Theme.BgRoot;

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

        _intensity = new PurpleSlider
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 100,
            Value = 100,
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
        _btnReset   = MainForm.MakeBigButton("Reset to Defaults", Theme.BtnNeutral);
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

        var checks1 = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.BgRoot };
        checks1.Controls.AddRange(new Control[] { _linearPhase, _adaptiveLoudness, _wider });
        layout.Controls.Add(checks1, 0, 3);
        layout.SetColumnSpan(checks1, 4);

        var checks2 = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Theme.BgRoot };
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
        ForeColor = Theme.FgBody,
        BackColor = Theme.BgRoot,
    };

    private static ComboBox MakeCombo(string[] items, string selected)
    {
        var c = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Theme.BgPanel,
            ForeColor = Theme.FgBody,
            FlatStyle = FlatStyle.Flat,
        };
        c.Items.AddRange(items);
        c.SelectedItem = selected;
        return c;
    }

    private static TextBox MakeTextBox(string placeholder) => new()
    {
        Dock = DockStyle.Fill,
        BackColor = Theme.BgPanel,
        ForeColor = Theme.FgBody,
        BorderStyle = BorderStyle.FixedSingle,
        PlaceholderText = placeholder,
    };

    private static CheckBox MakeCheck(string text, bool checkedState = false) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(6, 4, 16, 4),
        ForeColor = Theme.FgBody,
        BackColor = Theme.BgRoot,
        Checked = checkedState,
    };

    private static Button MakeSmallButton(string text)
    {
        var b = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.BtnNeutral,
            ForeColor = Color.White,
            Margin = new Padding(4, 0, 0, 0),
        };
        b.FlatAppearance.BorderSize = 0;
        return b;
    }
}

// Third tab — interactive visual EQ editor. Hosts the EqGraphControl plus
// Apply / Clear / Snap-to-AutoEQ buttons. The user's filter list is sent
// through Workflows.Install as an AudioMode.UserCustom profile.
[SupportedOSPlatform("windows")]
internal sealed class EqEditorTab : UserControl
{
    private readonly MainForm _owner;
    private readonly EqGraphControl _graph;
    private readonly Label _filterCountLabel;
    private readonly TextBox _headphoneBox;

    public EqEditorTab(MainForm owner)
    {
        _owner = owner;
        Dock = DockStyle.Fill;
        BackColor = Theme.BgRoot;

        _graph = new EqGraphControl { Dock = DockStyle.Fill };

        _filterCountLabel = new Label
        {
            Text = "0 filters",
            ForeColor = Theme.FgMuted,
            BackColor = Theme.BgRoot,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
        };
        _headphoneBox = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.BgPanel,
            ForeColor = Theme.FgBody,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Headphone slug for Snap (e.g. HD600)",
        };

        // Wire after the label exists so nullable analysis is satisfied.
        var label = _filterCountLabel;
        _graph.FiltersChanged += () =>
        {
            var c = _graph.Filters.Count;
            label.Text = $"{c} filter{(c == 1 ? "" : "s")}";
        };

        var btnApply = MainForm.MakeBigButton("Apply (Install)", Color.FromArgb(46, 138, 64));
        var btnClear = MainForm.MakeBigButton("Clear", Color.FromArgb(72, 76, 90));
        var btnSnap  = MainForm.MakeBigButton("Snap to AutoEQ", Color.FromArgb(60, 108, 170));
        btnApply.Font = btnClear.Font = btnSnap.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

        btnApply.Click += (_, _) => _owner.Run("Apply (UserCustom)", w =>
            Workflows.Install(w, new ProfileInput(AudioMode.UserCustom)
            {
                UserFilters = _graph.Filters.ToList(),
            }));
        btnClear.Click += (_, _) => _graph.Clear();
        btnSnap.Click  += (_, _) => SnapToAutoEq();

        var actionRow = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 50, ColumnCount = 5 };
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        actionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        actionRow.Controls.Add(_filterCountLabel, 0, 0);
        actionRow.Controls.Add(_headphoneBox, 1, 0);
        actionRow.Controls.Add(btnSnap, 2, 0);
        actionRow.Controls.Add(btnClear, 3, 0);
        actionRow.Controls.Add(btnApply, 4, 0);

        Controls.Add(_graph);
        Controls.Add(actionRow);
    }

    private void SnapToAutoEq()
    {
        var slug = _headphoneBox.Text.Trim();
        if (string.IsNullOrEmpty(slug)) { _owner.Log("[snap] enter a headphone slug first (e.g. HD600, K712 Pro, DT 1990 Pro)."); return; }
        _owner.Run("Snap to AutoEQ", w =>
        {
            var fetcher = new WarzoneEQ.WindowsIntegration.AutoEq.AutoEqFetcher();
            var task = fetcher.FetchAsync(slug, w);
            task.GetAwaiter().GetResult();
            var path = task.Result;
            if (path is null) return 1;
            var filters = ParseAutoEqFile(path).ToList();
            _owner.BeginInvoke(() => _graph.SetFilters(filters));
            w($"[snap] loaded {filters.Count} filters from {slug}.");
            return 0;
        });
    }

    // AutoEQ ParametricEQ.txt format (lines):
    //   Preamp: -6.5 dB
    //   Filter 1: ON PK Fc 32 Hz Gain 4.0 dB Q 1.41
    //   Filter 2: ON PK Fc 64 Hz Gain -2.0 dB Q 1.41
    //   ...
    private static IEnumerable<WarzoneEQ.ConfigGenerator.Filters.Filter> ParseAutoEqFile(string path)
    {
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (!line.StartsWith("Filter", StringComparison.OrdinalIgnoreCase)) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // Expect: Filter N: ON PK Fc FFFF Hz Gain GG.G dB Q QQ
            string? type = null;
            double? fc = null, gain = null, q = null;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "PK" || parts[i] == "LP" || parts[i] == "HP" || parts[i] == "LS" || parts[i] == "HS") type = parts[i];
                if (parts[i] == "Fc" && i + 1 < parts.Length && double.TryParse(parts[i + 1], System.Globalization.CultureInfo.InvariantCulture, out var f)) fc = f;
                if (parts[i] == "Gain" && i + 1 < parts.Length && double.TryParse(parts[i + 1], System.Globalization.CultureInfo.InvariantCulture, out var g)) gain = g;
                if (parts[i] == "Q" && i + 1 < parts.Length && double.TryParse(parts[i + 1], System.Globalization.CultureInfo.InvariantCulture, out var qq)) q = qq;
            }
            if (type is null || fc is null) continue;
            yield return type switch
            {
                "PK" => WarzoneEQ.ConfigGenerator.Filters.Filter.Peaking(fc.Value, gain ?? 0, q ?? 1.0),
                "HP" => WarzoneEQ.ConfigGenerator.Filters.Filter.HighPass(fc.Value),
                "LP" => WarzoneEQ.ConfigGenerator.Filters.Filter.LowPass(fc.Value),
                "LS" => WarzoneEQ.ConfigGenerator.Filters.Filter.LowShelf(fc.Value, gain ?? 0),
                "HS" => WarzoneEQ.ConfigGenerator.Filters.Filter.HighShelf(fc.Value, gain ?? 0),
                _ => WarzoneEQ.ConfigGenerator.Filters.Filter.Peaking(fc.Value, gain ?? 0, q ?? 1.0),
            };
        }
    }
}

// Tab control with an owner-drawn underline indicator under the active tab,
// gives the UI a Fluent-ish "selected" affordance instead of WinForms' default
// 3D button look. Owner-drawing means we also paint the tab background +
// foreground ourselves so the colors match the form's dark palette.
[SupportedOSPlatform("windows")]
internal sealed class UnderlinedTabControl : TabControl
{
    public Color Underline { get; set; } = Color.Gold;
    public Color TabBg     { get; set; } = Theme.BgChrome;
    public Color ActiveBg  { get; set; } = Theme.BgRoot;
    public Color TabFg     { get; set; } = Theme.FgBody;
    public int UnderlineHeight { get; set; } = 3;

    public UnderlinedTabControl()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        DrawItem += DrawTab;
    }

    private void DrawTab(object? sender, DrawItemEventArgs e)
    {
        var page = TabPages[e.Index];
        var rect = GetTabRect(e.Index);
        var active = e.Index == SelectedIndex;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using (var bg = new SolidBrush(active ? ActiveBg : TabBg))
            e.Graphics.FillRectangle(bg, rect);

        var fg = active ? TabFg : Color.FromArgb(160, TabFg.R, TabFg.G, TabFg.B);
        using (var fgBrush = new SolidBrush(fg))
        {
            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            e.Graphics.DrawString(page.Text, Font, fgBrush, rect, sf);
        }

        if (active)
        {
            using var ul = new SolidBrush(Underline);
            e.Graphics.FillRectangle(ul, rect.Left + 6, rect.Bottom - UnderlineHeight, rect.Width - 12, UnderlineHeight);
        }
    }
}
