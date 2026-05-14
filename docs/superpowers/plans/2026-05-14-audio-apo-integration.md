# Audio APO + Windows Integration Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Take the config text our generator emits (sub-plan #1) and actually make EQ APO load it. Cover three integration points: (1) write the config to `C:\Program Files\EqualizerAPO\config\warzone\current.txt` and update its master `config.txt` Include line, (2) toggle Windows Loudness Equalization on a specific audio endpoint via registry property store (the ArtIsWar trick), (3) run a "chain health check" that verifies EQ APO is installed and our config is loaded. Plus extend the CLI with an `apply` command that does the end-to-end flow.

**Architecture:** A new `WarzoneEQ.AudioApo` class library, tested at the I/O boundary via a `IFileSystem` abstraction so unit tests don't write to the real EQ APO directory. A separate Windows-targeted adapter (`AudioApoWindowsAdapter`) implements the registry-touching parts. The CLI gains an `apply` verb.

**Tech Stack:** .NET 8, xUnit + FluentAssertions, `Microsoft.Win32.Registry` for LEQ toggle, `IFileSystem` from `System.IO.Abstractions` for file ops (testable).

---

## Repo layout deltas

```
src/WarzoneEQ.AudioApo/
├── WarzoneEQ.AudioApo.csproj
├── Paths/EqApoPaths.cs              ← well-known EQ APO config paths
├── Installer/
│   ├── ConfigInstaller.cs           ← writes current.txt + updates master config.txt Include line
│   └── HrirInstaller.cs             ← copies HeSuVi WAV to active slot
├── LoudnessEq/
│   └── LoudnessEqController.cs      ← reads/writes endpoint LEQ + release time
├── Health/
│   └── HealthChecker.cs             ← verifies EQ APO install + active config
└── AudioApoService.cs               ← public facade
src/WarzoneEQ.Cli/Program.cs         ← add `apply` subcommand
tests/WarzoneEQ.AudioApo.Tests/...   ← unit tests using in-memory IFileSystem
```

---

## Task 0: Project scaffolding

- [ ] **Create + wire projects, add deps**

```powershell
dotnet new classlib -n WarzoneEQ.AudioApo -o src/WarzoneEQ.AudioApo -f net8.0
dotnet new xunit -n WarzoneEQ.AudioApo.Tests -o tests/WarzoneEQ.AudioApo.Tests -f net8.0
dotnet sln add src/WarzoneEQ.AudioApo/WarzoneEQ.AudioApo.csproj
dotnet sln add tests/WarzoneEQ.AudioApo.Tests/WarzoneEQ.AudioApo.Tests.csproj
dotnet add tests/WarzoneEQ.AudioApo.Tests/WarzoneEQ.AudioApo.Tests.csproj reference src/WarzoneEQ.AudioApo/WarzoneEQ.AudioApo.csproj
dotnet add src/WarzoneEQ.AudioApo/WarzoneEQ.AudioApo.csproj package System.IO.Abstractions --version 21.0.29
dotnet add tests/WarzoneEQ.AudioApo.Tests/WarzoneEQ.AudioApo.Tests.csproj package System.IO.Abstractions.TestingHelpers --version 21.0.29
dotnet add tests/WarzoneEQ.AudioApo.Tests/WarzoneEQ.AudioApo.Tests.csproj package FluentAssertions --version 6.12.1
dotnet add tests/WarzoneEQ.AudioApo.Tests/WarzoneEQ.AudioApo.Tests.csproj package xunit --version 2.9.2
dotnet add tests/WarzoneEQ.AudioApo.Tests/WarzoneEQ.AudioApo.Tests.csproj package xunit.runner.visualstudio --version 2.8.2
rm src/WarzoneEQ.AudioApo/Class1.cs tests/WarzoneEQ.AudioApo.Tests/UnitTest1.cs
```

Build, commit.

---

## Task 1: `EqApoPaths` — well-known paths

Holds the constants the rest of the lib needs.

```csharp
// src/WarzoneEQ.AudioApo/Paths/EqApoPaths.cs
namespace WarzoneEQ.AudioApo.Paths;

public sealed record EqApoPaths(string RootDir)
{
    public static EqApoPaths Default => new(@"C:\Program Files\EqualizerAPO");

    public string ConfigDir          => Path.Combine(RootDir, "config");
    public string MasterConfigTxt    => Path.Combine(ConfigDir, "config.txt");
    public string WarzoneDir         => Path.Combine(ConfigDir, "warzone");
    public string CurrentTxt         => Path.Combine(WarzoneDir, "current.txt");
    public string ProfilesDir        => Path.Combine(WarzoneDir, "profiles");
    public string HeadphoneCorrDir   => Path.Combine(WarzoneDir, "headphone-correction");
    public string HrirDir            => Path.Combine(WarzoneDir, "hrir");
    public string JsfxDir            => Path.Combine(WarzoneDir, "jsfx");
    public string FpsCurvesDir       => Path.Combine(WarzoneDir, "fps-curves");
    public string ActiveHrirWav      => Path.Combine(HrirDir, "hesuvi-active.wav");
}
```

Tests: default points to the standard install dir, every accessor returns the right subpath.

## Task 2: `ConfigInstaller`

Writes the generated config string to `current.txt` and ensures `config.txt` has an `Include: warzone\current.txt` line. Pure file ops behind `IFileSystem`.

```csharp
// src/WarzoneEQ.AudioApo/Installer/ConfigInstaller.cs
using System.IO.Abstractions;
using WarzoneEQ.AudioApo.Paths;

namespace WarzoneEQ.AudioApo.Installer;

public sealed class ConfigInstaller
{
    private readonly IFileSystem _fs;
    private readonly EqApoPaths _paths;
    private const string IncludeLine = @"Include: warzone\current.txt";

    public ConfigInstaller(IFileSystem fs, EqApoPaths paths)
    {
        _fs = fs;
        _paths = paths;
    }

    public void Install(string configText)
    {
        EnsureDirectories();
        _fs.File.WriteAllText(_paths.CurrentTxt, configText);
        EnsureIncludeLine();
    }

    private void EnsureDirectories()
    {
        foreach (var dir in new[] { _paths.WarzoneDir, _paths.ProfilesDir,
                                    _paths.HeadphoneCorrDir, _paths.HrirDir,
                                    _paths.JsfxDir, _paths.FpsCurvesDir })
            _fs.Directory.CreateDirectory(dir);
    }

    private void EnsureIncludeLine()
    {
        var existing = _fs.File.Exists(_paths.MasterConfigTxt)
            ? _fs.File.ReadAllText(_paths.MasterConfigTxt)
            : "";
        if (existing.Contains(IncludeLine, StringComparison.OrdinalIgnoreCase)) return;
        var newContents = existing.TrimEnd() + Environment.NewLine + IncludeLine + Environment.NewLine;
        _fs.File.WriteAllText(_paths.MasterConfigTxt, newContents);
    }
}
```

Tests (using `MockFileSystem`):
- After `Install("test")`: `current.txt` content equals `"test"`.
- After `Install`: `config.txt` ends with `Include: warzone\current.txt`.
- If `config.txt` already has the Include line, it is not duplicated.
- All required subdirectories exist.

## Task 3: `HrirInstaller`

Copies a user-selected HRIR `.wav` (HeSuVi) into the active slot. Same pattern: `IFileSystem`-based.

```csharp
public sealed class HrirInstaller
{
    private readonly IFileSystem _fs;
    private readonly EqApoPaths _paths;
    public HrirInstaller(IFileSystem fs, EqApoPaths paths) { _fs = fs; _paths = paths; }

    public void Install(string sourceWavPath)
    {
        if (!_fs.File.Exists(sourceWavPath))
            throw new FileNotFoundException("HRIR source not found.", sourceWavPath);
        _fs.Directory.CreateDirectory(_paths.HrirDir);
        _fs.File.Copy(sourceWavPath, _paths.ActiveHrirWav, overwrite: true);
    }
}
```

Tests: copies file, overwrites existing, throws on missing source.

## Task 4: `LoudnessEqController` (Windows-only)

Reads + writes the FxProperties registry key for an audio endpoint. Per the spec, the keys are:
- `{fc52a749-4be9-4510-896e-966ba6525980},3` — LEQ enabled flag (DWORD)
- `{fc52a749-4be9-4510-896e-966ba6525980},9` — release time (DWORD)

Abstract the registry behind an `IRegistry` interface for testability.

```csharp
public interface IRegistry
{
    object? GetValue(string keyPath, string valueName);
    void SetValue(string keyPath, string valueName, int value);
}

public sealed class LoudnessEqController
{
    private const string FxRoot =
        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render";
    private const string LeqEnabledValue = "{fc52a749-4be9-4510-896e-966ba6525980},3";
    private const string LeqReleaseValue = "{fc52a749-4be9-4510-896e-966ba6525980},9";

    private readonly IRegistry _registry;
    public LoudnessEqController(IRegistry registry) => _registry = registry;

    public void Enable(string endpointGuid, int releaseTime = 2)
    {
        if (releaseTime is < 2 or > 7) throw new ArgumentOutOfRangeException(nameof(releaseTime));
        var key = $@"{FxRoot}\{{{endpointGuid}}}\FxProperties";
        _registry.SetValue(key, LeqEnabledValue, 1);
        _registry.SetValue(key, LeqReleaseValue, releaseTime);
    }

    public void Disable(string endpointGuid)
    {
        var key = $@"{FxRoot}\{{{endpointGuid}}}\FxProperties";
        _registry.SetValue(key, LeqEnabledValue, 0);
    }

    public (bool Enabled, int ReleaseTime) Read(string endpointGuid)
    {
        var key = $@"{FxRoot}\{{{endpointGuid}}}\FxProperties";
        var enabled = (_registry.GetValue(key, LeqEnabledValue) as int?) ?? 0;
        var release = (_registry.GetValue(key, LeqReleaseValue) as int?) ?? 0;
        return (enabled == 1, release);
    }
}
```

Tests via in-memory `FakeRegistry`:
- `Enable("GUID")` writes 1 + release time 2 to correct paths.
- `Enable(..., 5)` writes release time 5.
- `Enable(..., 8)` throws.
- `Disable("GUID")` writes 0.
- `Read` returns previously written values.

Real `WindowsRegistry : IRegistry` wraps `Microsoft.Win32.Registry.SetValue/GetValue`. Not unit-tested (just delegates).

## Task 5: `HealthChecker`

Verifies the chain is properly installed and would actually run.

```csharp
public sealed record HealthReport(
    bool EqApoInstalled,
    bool MasterConfigHasInclude,
    bool CurrentTxtExists,
    string? CurrentTxtPath,
    long? CurrentTxtSizeBytes);

public sealed class HealthChecker
{
    private readonly IFileSystem _fs;
    private readonly EqApoPaths _paths;
    public HealthChecker(IFileSystem fs, EqApoPaths paths) { _fs = fs; _paths = paths; }

    public HealthReport Check()
    {
        var eqApoInstalled = _fs.Directory.Exists(_paths.RootDir);
        var masterIncludes = _fs.File.Exists(_paths.MasterConfigTxt)
            && _fs.File.ReadAllText(_paths.MasterConfigTxt).Contains(@"warzone\current.txt", StringComparison.OrdinalIgnoreCase);
        var currentExists = _fs.File.Exists(_paths.CurrentTxt);
        long? size = currentExists ? _fs.FileInfo.New(_paths.CurrentTxt).Length : null;
        return new HealthReport(eqApoInstalled, masterIncludes, currentExists,
            currentExists ? _paths.CurrentTxt : null, size);
    }
}
```

Tests: all-false report when nothing exists; all-true when fully installed.

## Task 6: `AudioApoService` facade

Stitches together ConfigInstaller, HrirInstaller, HealthChecker, optionally LoudnessEqController.

```csharp
public sealed class AudioApoService
{
    private readonly ConfigInstaller _config;
    private readonly HrirInstaller _hrir;
    private readonly HealthChecker _health;

    public AudioApoService(ConfigInstaller config, HrirInstaller hrir, HealthChecker health)
    {
        _config = config;
        _hrir = hrir;
        _health = health;
    }

    public void ApplyConfig(string configText) => _config.Install(configText);
    public void InstallHrir(string sourceWavPath) => _hrir.Install(sourceWavPath);
    public HealthReport Health() => _health.Check();

    public static AudioApoService CreateDefault()
    {
        var fs = new System.IO.Abstractions.FileSystem();
        var paths = EqApoPaths.Default;
        return new AudioApoService(
            new ConfigInstaller(fs, paths),
            new HrirInstaller(fs, paths),
            new HealthChecker(fs, paths));
    }
}
```

Tests: end-to-end with a `MockFileSystem` — `CreateDefault` swapped for an injected fake.

## Task 7: CLI `apply` command

Extend `WarzoneEQ.Cli/Program.cs` with an `apply` subcommand:

```
WarzoneEQ.Cli apply --mode Competitive --headphone HD600 --dac "Speakers Sound Blaster GC7 Game"
```

Generates the config, calls `AudioApoService.CreateDefault().ApplyConfig(...)`. Reports the resulting file path. Exits 0 on success, 1 on missing EQ APO with a clear error message.

Add CLI test (against the real filesystem, in a temp dir, with `EqApoPaths` overridden).

## Task 8: README + final test pass

Mark sub-plan #3 done in README, run all tests, commit.

## Not covered

- `IPolicyConfig` for setting the Windows default playback device (sub-plan #4 / wizard).
- Actual EQ APO installation detection on the user's machine (sub-plan #7 installer).
- Driver-signed Loudness EQ flag write (writing under HKLM may need elevation; flag this in v1.1 documentation).
