# Control App GUI Implementation Plan (Sub-plan #4)

> **For agentic workers:** Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Build the WPF system-tray control app that orchestrates everything from sub-plans #1–3. Tray icon with mode-cycle menu, full window with five tabs (Status / Tuning / Headphones / Advanced / About), global hotkeys, foreground-window auto-switch, and JSON-backed app settings.

**Architecture:** MVVM. ViewModels are pure-logic (testable on any OS). Views are XAML. Side-effecting services (foreground-window reader, hotkey registration, file I/O) live behind interfaces so ViewModels stay test-friendly. Windows-specific code is gated by `[SupportedOSPlatform("windows")]` and lives in a separate `WarzoneEQ.App.Platform` namespace.

**Tech Stack:** C# 12, .NET 8 (`net8.0-windows`), WPF (`UseWPF=true`), H.NotifyIcon.Wpf for the tray icon.

---

## Repo layout

```
src/WarzoneEQ.App/
├── WarzoneEQ.App.csproj                 (WinExe, UseWPF=true)
├── App.xaml + App.xaml.cs                (startup, tray lifecycle)
├── MainWindow.xaml + MainWindow.xaml.cs  (5-tab shell)
├── ViewModels/
│   ├── ShellViewModel.cs                 (tab selection + mode toggle)
│   ├── StatusViewModel.cs
│   ├── TuningViewModel.cs
│   ├── HeadphonesViewModel.cs
│   ├── AdvancedViewModel.cs
│   └── AboutViewModel.cs
├── Services/
│   ├── IForegroundWindowReader.cs / Win32ForegroundWindowReader.cs
│   ├── ForegroundWindowMonitor.cs       (polls IForegroundWindowReader, fires events)
│   ├── IHotkeyRegistrar.cs / Win32HotkeyRegistrar.cs
│   ├── IAppSettingsStore.cs / JsonAppSettingsStore.cs
│   └── AppSettings.cs                   (DTO)
└── Views/
    └── (XAML tab UserControls)

tests/WarzoneEQ.App.Tests/
├── WarzoneEQ.App.Tests.csproj           (net8.0, no WPF)
├── ViewModels/...                       (pure-logic VM tests)
├── Services/
│   ├── ForegroundWindowMonitorTests.cs  (fake reader)
│   └── JsonAppSettingsStoreTests.cs     (temp dir)
```

The test project targets plain `net8.0` so the VMs must not directly depend on WPF types. ViewModels use `INotifyPropertyChanged` from `System.ComponentModel` (BCL, cross-platform).

---

## Task 1: Scaffold the app + test projects

```powershell
dotnet new wpf -n WarzoneEQ.App -o src/WarzoneEQ.App -f net8.0
dotnet new xunit -n WarzoneEQ.App.Tests -o tests/WarzoneEQ.App.Tests -f net8.0
dotnet sln add src/WarzoneEQ.App/WarzoneEQ.App.csproj
dotnet sln add tests/WarzoneEQ.App.Tests/WarzoneEQ.App.Tests.csproj
dotnet add tests/WarzoneEQ.App.Tests/WarzoneEQ.App.Tests.csproj reference src/WarzoneEQ.App/WarzoneEQ.App.csproj
dotnet add src/WarzoneEQ.App/WarzoneEQ.App.csproj reference src/WarzoneEQ.ConfigGenerator/WarzoneEQ.ConfigGenerator.csproj
dotnet add src/WarzoneEQ.App/WarzoneEQ.App.csproj reference src/WarzoneEQ.DeviceDetection/WarzoneEQ.DeviceDetection.csproj
dotnet add src/WarzoneEQ.App/WarzoneEQ.App.csproj reference src/WarzoneEQ.WindowsIntegration/WarzoneEQ.WindowsIntegration.csproj
dotnet add src/WarzoneEQ.App/WarzoneEQ.App.csproj package H.NotifyIcon.Wpf --version 2.1.4
dotnet add tests/WarzoneEQ.App.Tests/WarzoneEQ.App.Tests.csproj package FluentAssertions --version 6.12.1
dotnet add tests/WarzoneEQ.App.Tests/WarzoneEQ.App.Tests.csproj package xunit --version 2.9.2
dotnet add tests/WarzoneEQ.App.Tests/WarzoneEQ.App.Tests.csproj package xunit.runner.visualstudio --version 2.8.2
```

Build to verify. Commit.

---

## Task 2: `AppSettings` DTO + `JsonAppSettingsStore`

`AppSettings`:
- `AudioMode CurrentMode`
- `FpsCurveName CurrentCurve`
- `double Intensity`
- `bool AutoSwitchEnabled`
- `string? ManualHeadphoneOverrideSlug`
- `string? GameEndpoint`     (set by user during wizard, or detected)

Store reads/writes JSON at `%LOCALAPPDATA%\WarzoneEQ\settings.json`.

Tests:
- Round-trip serialize/deserialize.
- Missing file returns defaults.
- Invalid JSON returns defaults (no crash).

Commit `feat: AppSettings + JsonAppSettingsStore (JSON round-trip)`.

---

## Task 3: `ForegroundWindowMonitor` (auto-switch trigger)

```csharp
public interface IForegroundWindowReader { string? CurrentProcessName(); }

public sealed class ForegroundWindowMonitor
{
    public event EventHandler<string?>? ProcessChanged;
    private readonly IForegroundWindowReader _reader;
    private readonly TimeSpan _interval;
    private string? _last;
    public ForegroundWindowMonitor(IForegroundWindowReader reader, TimeSpan? interval = null)
    {
        _reader = reader;
        _interval = interval ?? TimeSpan.FromSeconds(1);
    }
    public void Tick()
    {
        var name = _reader.CurrentProcessName();
        if (name == _last) return;
        _last = name;
        ProcessChanged?.Invoke(this, name);
    }
}
```

Tests use a fake `IForegroundWindowReader` that returns scripted values and assert `ProcessChanged` fires on transitions only.

Production impl `Win32ForegroundWindowReader` calls `GetForegroundWindow` + `GetWindowThreadProcessId` + `Process.GetProcessById`. Marked `[SupportedOSPlatform("windows")]`.

Commit `feat: ForegroundWindowMonitor (process-change events on poll)`.

---

## Task 4: `ShellViewModel` (mode toggle + auto-switch wiring)

```csharp
public sealed class ShellViewModel : INotifyPropertyChanged
{
    public AudioMode CurrentMode { get; private set; }
    public bool AutoSwitchEnabled { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<AudioMode>? ModeApplied;

    public void SetMode(AudioMode mode, bool fromAutoSwitch = false)
    {
        if (mode == CurrentMode) return;
        CurrentMode = mode;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentMode)));
        ModeApplied?.Invoke(this, mode);
    }

    public void OnForegroundProcessChanged(string? processName)
    {
        if (!AutoSwitchEnabled) return;
        var target = string.Equals(processName, "WARZONE", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(processName, "Warzone", StringComparison.OrdinalIgnoreCase)
            ? AudioMode.Competitive
            : AudioMode.Cinematic;
        SetMode(target, fromAutoSwitch: true);
    }
}
```

Tests:
- Manual `SetMode` raises `PropertyChanged` and `ModeApplied`.
- `OnForegroundProcessChanged("WARZONE")` switches to Competitive when auto-switch is on.
- `OnForegroundProcessChanged("Discord")` switches to Cinematic.
- Auto-switch disabled: no mode change.

Commit `feat: ShellViewModel (mode toggle + foreground-driven auto-switch)`.

---

## Task 5: `TuningViewModel` (curve + intensity + toggles)

Properties:
- `FpsCurveName CurveName { get; set; }`
- `double Intensity { get; set; }` (clamped 0..1)
- `bool FootstepCompressor { get; set; }`
- `bool LinearPhase { get; set; }`
- `bool AdaptiveLoudness { get; set; }`
- `bool PolyverseWider { get; set; }`

Each setter raises `PropertyChanged`.

Method: `ProfileInput BuildProfileInput(AudioMode mode, HeadphoneCorrection hp, DacEndpoint dac)` that constructs the immutable `ProfileInput` for the config generator.

Tests:
- Setting curve/intensity raises PropertyChanged.
- Intensity > 1 throws ArgumentOutOfRangeException.
- BuildProfileInput packages all toggles into the right ProfileInput fields.

Commit `feat: TuningViewModel (curve + intensity + toggles -> ProfileInput)`.

---

## Task 6: `HeadphonesViewModel`

Properties:
- `string? AutoDetectedModel`
- `string? AutoDetectedSlug`
- `string? ManualOverrideSlug`
- `string? EffectiveSlug` (override wins if non-null, else auto)

Method `AcceptAutoDetection(DetectionSnapshot)` — copies fields from a snapshot.
Method `SetManualOverride(string slug)`.
Method `ClearOverride()`.

Tests: snapshot input populates auto fields; override takes precedence; clear restores auto.

Commit `feat: HeadphonesViewModel (auto-detected vs manual override)`.

---

## Task 7: Tray + MainWindow XAML wiring

This is XAML + code-behind. NOT unit-testable from xUnit (requires WPF runtime + a display). Implementation:

- `App.xaml` references `MainWindow` (StartupUri).
- App startup creates `H.NotifyIcon.Wpf.TaskbarIcon` from XAML resources, binds click → toggle window.
- Five `TabItem` controls in `MainWindow.xaml` bind to the five ViewModels.
- Tray icon color binds to `ShellViewModel.CurrentMode` (Green=Competitive, Blue=Cinematic, Grey=Bypass) via a value converter.

Per spec §9: keep all visualizers inside the control app window, never as game overlays.

No unit tests for this task — manually verify by running `dotnet run --project src/WarzoneEQ.App`.

Commit `feat: tray icon + 5-tab MainWindow XAML (manual verification only)`.

---

## Task 8: Hotkey registration

```csharp
public interface IHotkeyRegistrar
{
    int Register(string name, ModifierKeys mods, Key key, Action onPressed);
    void Unregister(int id);
}
```

Implementation calls Win32 `RegisterHotKey` + listens to WM_HOTKEY messages on a hidden window. Hotkeys per spec §9.2:
- Ctrl+Alt+1 → Competitive
- Ctrl+Alt+2 → Cinematic
- Ctrl+Alt+0 → Bypass
- Ctrl+Alt+B → momentary A/B (last mode ↔ bypass)

Tests use a fake registrar.

Commit `feat: hotkey registration (Ctrl+Alt+1/2/0/B)`.

---

## Task 9: README + final pass

- Mark sub-plan #4 done, update test count.
- Run `dotnet test` — all tests pass.
- Manual verification note: launch the app, check tray icon appears, all five tabs render.

Commit + merge to master.
