# EQ APO + Windows Audio Integration Implementation Plan (Sub-plan #3)

> **For agentic workers:** Use superpowers:executing-plans.

**Goal:** Take the strings produced by sub-plan #1 and the detections from sub-plan #2 and actually make them affect Windows audio: write `current.txt` to EQ APO's config dir, toggle Windows Loudness Equalization on the right endpoint, and provide a small `WarzoneConfigInstaller` orchestrator the GUI (sub-plan #4) will drive.

**Architecture:** A new `WarzoneEQ.WindowsIntegration` library. All side-effecting classes sit behind interfaces so the orchestrator can be unit-tested with fakes. The actual Windows registry / file I/O is exercised by integration tests against a sandboxed location.

**Tech Stack:** Same .NET 8 + xUnit + FluentAssertions. Uses `Microsoft.Win32.Registry` (BCL on net8.0-windows). No new package dependencies.

---

## Task 1: Project skeleton

- [ ] `dotnet new classlib -n WarzoneEQ.WindowsIntegration -o src/WarzoneEQ.WindowsIntegration -f net8.0`
- [ ] `dotnet sln add src/WarzoneEQ.WindowsIntegration/WarzoneEQ.WindowsIntegration.csproj`
- [ ] Add references to `WarzoneEQ.ConfigGenerator` and `WarzoneEQ.DeviceDetection`
- [ ] `dotnet new xunit -n WarzoneEQ.WindowsIntegration.Tests -o tests/WarzoneEQ.WindowsIntegration.Tests -f net8.0`
- [ ] Add to solution + reference + FluentAssertions
- [ ] Delete boilerplate `Class1.cs` / `UnitTest1.cs`
- [ ] Commit `chore: scaffold WarzoneEQ.WindowsIntegration`

## Task 2: `IConfigFileWriter` + `AtomicFileWriter`

Writes a string to a target path via temp file + rename for atomicity.

`src/WarzoneEQ.WindowsIntegration/Files/IConfigFileWriter.cs`:
```csharp
namespace WarzoneEQ.WindowsIntegration.Files;
public interface IConfigFileWriter { void Write(string targetPath, string contents); }
```

`src/WarzoneEQ.WindowsIntegration/Files/AtomicFileWriter.cs`:
```csharp
namespace WarzoneEQ.WindowsIntegration.Files;
public sealed class AtomicFileWriter : IConfigFileWriter
{
    public void Write(string targetPath, string contents)
    {
        var dir = Path.GetDirectoryName(targetPath)
                  ?? throw new ArgumentException("targetPath has no directory.", nameof(targetPath));
        Directory.CreateDirectory(dir);
        var temp = targetPath + ".tmp";
        File.WriteAllText(temp, contents, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (File.Exists(targetPath)) File.Replace(temp, targetPath, destinationBackupFileName: null);
        else File.Move(temp, targetPath);
    }
}
```

Tests in `tests/WarzoneEQ.WindowsIntegration.Tests/Files/AtomicFileWriterTests.cs`:
- Writes new file with given contents.
- Overwrites existing file (replace).
- Creates directory if it doesn't exist.
- Each test uses a unique temp directory under `Path.GetTempPath()`.

Commit `feat: AtomicFileWriter (atomic temp+rename for EQ APO config writes)`.

## Task 3: `IEqApoLocator` + `RegistryEqApoLocator`

Finds where EQ APO is installed.

EQ APO 1.4.2 writes its install path to `HKLM\SOFTWARE\EqualizerAPO\InstallPath` (string value). Config dir is `{InstallPath}\config`.

```csharp
namespace WarzoneEQ.WindowsIntegration.EqApo;
public interface IEqApoLocator
{
    bool IsInstalled { get; }
    string ConfigDirectory { get; }   // throws InvalidOperationException if !IsInstalled
}

public sealed class RegistryEqApoLocator : IEqApoLocator
{
    private readonly string? _installPath;
    public RegistryEqApoLocator()
    {
        _installPath = Microsoft.Win32.Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\EqualizerAPO", "InstallPath", null) as string;
    }
    public bool IsInstalled => !string.IsNullOrWhiteSpace(_installPath);
    public string ConfigDirectory => IsInstalled
        ? Path.Combine(_installPath!, "config")
        : throw new InvalidOperationException("Equalizer APO not installed.");
}
```

Plus a `FakeEqApoLocator(string configDirectory)` for tests.

Tests:
- `FakeEqApoLocator("C:/foo")` returns `"C:/foo"`.
- `RegistryEqApoLocator` smoke test (skip if not installed on the test machine).

Commit `feat: IEqApoLocator + RegistryEqApoLocator (HKLM\\SOFTWARE\\EqualizerAPO\\InstallPath)`.

## Task 4: `ILoudnessEqController` + `RegistryLoudnessEqController`

Toggles Windows Loudness Equalization on an audio endpoint and sets release time.

Per spec §5.5, the registry path is:
```
HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\{endpoint-guid}\FxProperties
  {fc52a749-4be9-4510-896e-966ba6525980},3   ← enabled flag (DWORD 0/1)
  {fc52a749-4be9-4510-896e-966ba6525980},9   ← release time (DWORD 2..7)
```

Interface:
```csharp
namespace WarzoneEQ.WindowsIntegration.LoudnessEq;

public sealed record LoudnessEqState(bool Enabled, int ReleaseTime);

public interface ILoudnessEqController
{
    LoudnessEqState? Read(string endpointGuid);
    void Write(string endpointGuid, LoudnessEqState state);
}
```

Production impl reads/writes the registry. We DO NOT want tests to actually touch HKLM. Use an `IRegistry` abstraction:
```csharp
namespace WarzoneEQ.WindowsIntegration.LoudnessEq;

internal interface IRegistry
{
    object? GetValue(string keyName, string valueName);
    void SetValue(string keyName, string valueName, object value, Microsoft.Win32.RegistryValueKind kind);
}

internal sealed class SystemRegistry : IRegistry
{
    public object? GetValue(string keyName, string valueName)
        => Microsoft.Win32.Registry.GetValue(keyName, valueName, null);
    public void SetValue(string keyName, string valueName, object value, Microsoft.Win32.RegistryValueKind kind)
        => Microsoft.Win32.Registry.SetValue(keyName, valueName, value, kind);
}
```

`RegistryLoudnessEqController` takes an `IRegistry` (defaults to `SystemRegistry`) and uses it. Tests inject a `FakeRegistry` (Dictionary-backed).

Tests:
- Reading an absent endpoint returns null.
- Writing then reading returns the same state.
- Release time clamped to [2, 7].

Commit `feat: LoudnessEqController (Windows Loudness EQ via FxProperties registry)`.

## Task 5: `WarzoneConfigInstaller` orchestrator

Takes a `ProfileInput` (from sub-plan #1) and a `DetectionSnapshot` (from sub-plan #2), generates the config, and installs it.

```csharp
namespace WarzoneEQ.WindowsIntegration;

public sealed class WarzoneConfigInstaller
{
    private readonly IEqApoLocator _locator;
    private readonly IConfigFileWriter _writer;
    public WarzoneConfigInstaller(IEqApoLocator locator, IConfigFileWriter writer)
    {
        _locator = locator;
        _writer = writer;
    }

    public string Install(WarzoneEQ.ConfigGenerator.Models.ProfileInput input)
    {
        var configText = WarzoneEQ.ConfigGenerator.ConfigGenerator.Generate(input);
        var path = Path.Combine(_locator.ConfigDirectory, "warzone", "current.txt");
        _writer.Write(path, configText);
        return path;
    }
}
```

Also ensure `config.txt` includes `Include: warzone\current.txt` (idempotent — only append if missing).

```csharp
public string Install(ProfileInput input)
{
    EnsureMasterConfigIncludes();
    // ... (same as above)
}

private void EnsureMasterConfigIncludes()
{
    var masterPath = Path.Combine(_locator.ConfigDirectory, "config.txt");
    if (!File.Exists(masterPath)) return;
    var existing = File.ReadAllText(masterPath);
    if (existing.Contains("Include: warzone\\current.txt", StringComparison.Ordinal)) return;
    File.AppendAllText(masterPath, Environment.NewLine + "Include: warzone\\current.txt" + Environment.NewLine);
}
```

Tests:
- With a `FakeEqApoLocator` pointing to a temp dir, install a Competitive profile → file appears at `{temp}/warzone/current.txt` with expected contents.
- If `config.txt` exists without the Include, install appends it. If it has the Include, install leaves it alone.

Commit `feat: WarzoneConfigInstaller (generates + writes current.txt + ensures master include)`.

## Task 6: README + integration into CLI

- Update CLI to gain `--install` flag that runs `WarzoneConfigInstaller` against the real registry.
- Update README with sub-plan #3 ticked + new test count.

Manually run `dotnet run --project src/WarzoneEQ.Cli -- --install --mode Competitive --headphone HD600` (only if EQ APO is installed on the test machine; otherwise expect a clear error message).

Commit `feat: --install flag on CLI` and `docs: sub-plan #3 done`.

## Out of scope (deferred to #4)

- IPolicyConfig (set Windows default playback device) — only useful when the GUI prompts for it.
- Hot-reload event watching — EQ APO already watches its own config file; we don't need to trigger anything.
- Audio chain health check — depends on GUI surfacing it.
