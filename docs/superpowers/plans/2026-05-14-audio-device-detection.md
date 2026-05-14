# Audio Device Detection Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Detect what playback hardware the user has — headphones (USB VID/PID, Bluetooth name, or analog) and multi-endpoint DAC (GC7, G8, X3/4/5, MixAmp, etc.) — and map detections to AutoEQ slugs + DAC routing info. Pure detection + matching; no UI.

**Architecture:** Two layers. **Layer 1 (`Matching`):** pure-logic database that maps `(vid, pid)` → AutoEQ slug and identifies multi-endpoint DACs from the same lookup. Fully unit-testable on any OS. **Layer 2 (`Detection`):** thin Windows adapter that enumerates audio endpoints via WMI and gets Bluetooth device names via WinRT. Behind an `IDeviceEnumerator` interface so the matching tests don't need real hardware.

**Tech Stack:** C# 12, .NET 8 with Windows target framework `net8.0-windows10.0.22621.0` for WinRT projection. System.Management 10.0.x for WMI. CsWinRT for Bluetooth via Microsoft.Windows.SDK.NET.Ref.

---

## Repo layout at end of sub-plan

```
src/WarzoneEQ.DeviceDetection/
├── WarzoneEQ.DeviceDetection.csproj
├── Resources/
│   └── vidpid-overlay.json                (embedded resource)
├── Models/
│   ├── AudioDevice.cs
│   ├── DeviceKind.cs
│   ├── MultiEndpointDac.cs
│   └── HeadphoneMatch.cs
├── Matching/
│   ├── DeviceDatabase.cs                  (loads JSON, exposes lookups)
│   └── DeviceMatcher.cs                   (matches AudioDevice → HeadphoneMatch + MultiEndpointDac)
├── Detection/
│   ├── IDeviceEnumerator.cs               (abstraction for testing)
│   ├── WindowsDeviceEnumerator.cs         (WMI + WinRT impl)
│   └── BluetoothNameNormalizer.cs         (strips region suffixes, etc.)
└── DeviceDetectionService.cs              (public facade: detect current, return matches)

tests/WarzoneEQ.DeviceDetection.Tests/
├── WarzoneEQ.DeviceDetection.Tests.csproj
├── Matching/
│   ├── DeviceDatabaseTests.cs
│   └── DeviceMatcherTests.cs
├── Detection/
│   ├── FakeDeviceEnumerator.cs            (test double)
│   └── BluetoothNameNormalizerTests.cs
├── DeviceDetectionServiceTests.cs
└── TestData/
    └── sample-vidpid-overlay.json
```

---

## Task 0: Project skeleton

- [ ] **Step 1: Create projects**

```powershell
dotnet new classlib -n WarzoneEQ.DeviceDetection -o src/WarzoneEQ.DeviceDetection -f net8.0
dotnet new xunit -n WarzoneEQ.DeviceDetection.Tests -o tests/WarzoneEQ.DeviceDetection.Tests -f net8.0
dotnet sln add src/WarzoneEQ.DeviceDetection/WarzoneEQ.DeviceDetection.csproj
dotnet sln add tests/WarzoneEQ.DeviceDetection.Tests/WarzoneEQ.DeviceDetection.Tests.csproj
dotnet add tests/WarzoneEQ.DeviceDetection.Tests/WarzoneEQ.DeviceDetection.Tests.csproj reference src/WarzoneEQ.DeviceDetection/WarzoneEQ.DeviceDetection.csproj
```

Delete `Class1.cs` and `UnitTest1.cs` boilerplate.

- [ ] **Step 2: Add test deps**

```powershell
dotnet add tests/WarzoneEQ.DeviceDetection.Tests/WarzoneEQ.DeviceDetection.Tests.csproj package FluentAssertions --version 6.12.1
dotnet add tests/WarzoneEQ.DeviceDetection.Tests/WarzoneEQ.DeviceDetection.Tests.csproj package xunit --version 2.9.2
```

- [ ] **Step 3: Build + commit**

```powershell
dotnet build
git add src/WarzoneEQ.DeviceDetection/ tests/WarzoneEQ.DeviceDetection.Tests/ WarzoneEQ.sln
git commit -m "chore: scaffold WarzoneEQ.DeviceDetection projects"
```

---

## Task 1: `DeviceKind` enum + `AudioDevice` model

**Files:**
- Create: `src/WarzoneEQ.DeviceDetection/Models/DeviceKind.cs`
- Create: `src/WarzoneEQ.DeviceDetection/Models/AudioDevice.cs`
- Create: `tests/WarzoneEQ.DeviceDetection.Tests/Models/AudioDeviceTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using FluentAssertions;
using WarzoneEQ.DeviceDetection.Models;
using Xunit;

namespace WarzoneEQ.DeviceDetection.Tests.Models;

public class AudioDeviceTests
{
    [Fact]
    public void Usb_device_has_vid_pid()
    {
        var d = new AudioDevice(
            EndpointName: "Speakers (Sound Blaster GC7 Game)",
            Kind: DeviceKind.Usb,
            UsbVid: "041E",
            UsbPid: "3260");
        d.UsbVidPidKey.Should().Be("VID_041E&PID_3260");
    }

    [Fact]
    public void Bluetooth_device_has_name_only()
    {
        var d = new AudioDevice(
            EndpointName: "Sony WH-1000XM5",
            Kind: DeviceKind.Bluetooth,
            BluetoothName: "WH-1000XM5");
        d.UsbVidPidKey.Should().BeNull();
        d.BluetoothName.Should().Be("WH-1000XM5");
    }

    [Fact]
    public void Analog_device_has_no_identifier_beyond_endpoint_name()
    {
        var d = new AudioDevice(EndpointName: "Headphones (Realtek)", Kind: DeviceKind.Analog);
        d.UsbVidPidKey.Should().BeNull();
        d.BluetoothName.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run test, expect build failure**

`dotnet test --filter "FullyQualifiedName~AudioDeviceTests"` → build error.

- [ ] **Step 3: Implement**

`Models/DeviceKind.cs`:
```csharp
namespace WarzoneEQ.DeviceDetection.Models;

public enum DeviceKind { Usb, Bluetooth, Analog }
```

`Models/AudioDevice.cs`:
```csharp
namespace WarzoneEQ.DeviceDetection.Models;

public sealed record AudioDevice(
    string EndpointName,
    DeviceKind Kind,
    string? UsbVid = null,
    string? UsbPid = null,
    string? BluetoothName = null)
{
    public string? UsbVidPidKey =>
        UsbVid is not null && UsbPid is not null ? $"VID_{UsbVid}&PID_{UsbPid}" : null;
}
```

- [ ] **Step 4: Run test, expect pass**

`Passed: 3`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.DeviceDetection/Models/ tests/WarzoneEQ.DeviceDetection.Tests/Models/AudioDeviceTests.cs
git commit -m "feat: AudioDevice + DeviceKind models"
```

---

## Task 2: `HeadphoneMatch` + `MultiEndpointDac` models

**Files:**
- Create: `src/WarzoneEQ.DeviceDetection/Models/HeadphoneMatch.cs`
- Create: `src/WarzoneEQ.DeviceDetection/Models/MultiEndpointDac.cs`
- Create: `tests/WarzoneEQ.DeviceDetection.Tests/Models/HeadphoneMatchTests.cs`

- [ ] **Step 1: Failing test**

```csharp
using FluentAssertions;
using WarzoneEQ.DeviceDetection.Models;
using Xunit;

namespace WarzoneEQ.DeviceDetection.Tests.Models;

public class HeadphoneMatchTests
{
    [Fact]
    public void HeadphoneMatch_carries_model_and_autoeq_slug()
    {
        var m = new HeadphoneMatch(Model: "Sennheiser HD 600", AutoeqSlug: "sennheiser/HD_600");
        m.Model.Should().Be("Sennheiser HD 600");
        m.AutoeqSlug.Should().Be("sennheiser/HD_600");
    }

    [Fact]
    public void MultiEndpointDac_carries_game_and_voice_endpoint_names()
    {
        var dac = new MultiEndpointDac(
            Model: "Creative Sound Blaster GC7",
            GameEndpoint: "Speakers (Sound Blaster GC7 Game)",
            VoiceEndpoint: "Speakers (Sound Blaster GC7 Chat)");
        dac.GameEndpoint.Should().Be("Speakers (Sound Blaster GC7 Game)");
        dac.VoiceEndpoint.Should().Be("Speakers (Sound Blaster GC7 Chat)");
    }
}
```

- [ ] **Step 2: Run, expect build failure**

- [ ] **Step 3: Implement**

`Models/HeadphoneMatch.cs`:
```csharp
namespace WarzoneEQ.DeviceDetection.Models;

public sealed record HeadphoneMatch(string Model, string AutoeqSlug);
```

`Models/MultiEndpointDac.cs`:
```csharp
namespace WarzoneEQ.DeviceDetection.Models;

public sealed record MultiEndpointDac(string Model, string GameEndpoint, string VoiceEndpoint);
```

- [ ] **Step 4: Run, expect pass**

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.DeviceDetection/Models/HeadphoneMatch.cs src/WarzoneEQ.DeviceDetection/Models/MultiEndpointDac.cs tests/WarzoneEQ.DeviceDetection.Tests/Models/HeadphoneMatchTests.cs
git commit -m "feat: HeadphoneMatch + MultiEndpointDac value records"
```

---

## Task 3: Bundled `vidpid-overlay.json` (embedded resource)

**Files:**
- Create: `src/WarzoneEQ.DeviceDetection/Resources/vidpid-overlay.json`
- Modify: `src/WarzoneEQ.DeviceDetection/WarzoneEQ.DeviceDetection.csproj` (mark as embedded resource)

- [ ] **Step 1: Create the JSON**

`src/WarzoneEQ.DeviceDetection/Resources/vidpid-overlay.json`:
```json
{
  "headphones": {
    "VID_1532&PID_0517": { "model": "Razer BlackShark V2 Pro", "autoeq_slug": "razer/BlackShark_V2_Pro" },
    "VID_1038&PID_12AD": { "model": "SteelSeries Arctis Nova Pro Wireless", "autoeq_slug": "steelseries/Arctis_Nova_Pro_Wireless" },
    "VID_0BDA&PID_4014": { "model": "HyperX Cloud III Wireless", "autoeq_slug": "hyperx/Cloud_III_Wireless" },
    "VID_0D8C&PID_0014": { "model": "Sennheiser HD 600 (via USB DAC)", "autoeq_slug": "sennheiser/HD_600" }
  },
  "multi_endpoint_dacs": {
    "VID_041E&PID_3260": {
      "model": "Creative Sound Blaster GC7",
      "game_endpoint": "Speakers (Sound Blaster GC7 Game)",
      "voice_endpoint": "Speakers (Sound Blaster GC7 Chat)"
    },
    "VID_041E&PID_3270": {
      "model": "Creative Sound Blaster G8",
      "game_endpoint": "Speakers (Sound Blaster G8 Game)",
      "voice_endpoint": "Speakers (Sound Blaster G8 Chat)"
    },
    "VID_041E&PID_3251": {
      "model": "Creative Sound Blaster X3",
      "game_endpoint": "Speakers (Sound Blaster X3 Game)",
      "voice_endpoint": "Speakers (Sound Blaster X3 Chat)"
    },
    "VID_041E&PID_3253": {
      "model": "Creative Sound Blaster X4",
      "game_endpoint": "Speakers (Sound Blaster X4 Game)",
      "voice_endpoint": "Speakers (Sound Blaster X4 Chat)"
    },
    "VID_041E&PID_3265": {
      "model": "Creative Sound Blaster X5",
      "game_endpoint": "Speakers (Sound Blaster X5 Game)",
      "voice_endpoint": "Speakers (Sound Blaster X5 Chat)"
    },
    "VID_9886&PID_002C": {
      "model": "Astro MixAmp Pro TR",
      "game_endpoint": "Speakers (Astro MixAmp Pro Game)",
      "voice_endpoint": "Speakers (Astro MixAmp Pro Voice)"
    },
    "VID_1395&PID_011B": {
      "model": "Sennheiser GSX 1200 Pro",
      "game_endpoint": "Speakers (Sennheiser GSX 1200 Game)",
      "voice_endpoint": "Speakers (Sennheiser GSX 1200 Chat)"
    },
    "VID_1038&PID_12CB": {
      "model": "SteelSeries GameDAC Gen 2",
      "game_endpoint": "Speakers (SteelSeries GameDAC Gen 2 Game)",
      "voice_endpoint": "Speakers (SteelSeries GameDAC Gen 2 Chat)"
    }
  },
  "bluetooth_names": {
    "WH-1000XM5": "sony/WH-1000XM5",
    "WH-1000XM4": "sony/WH-1000XM4",
    "Arctis Nova Pro Wireless": "steelseries/Arctis_Nova_Pro_Wireless",
    "BlackShark V2 Pro": "razer/BlackShark_V2_Pro"
  }
}
```

- [ ] **Step 2: Mark as embedded resource**

Edit `src/WarzoneEQ.DeviceDetection/WarzoneEQ.DeviceDetection.csproj`, add inside `<Project>`:
```xml
  <ItemGroup>
    <EmbeddedResource Include="Resources\vidpid-overlay.json" />
  </ItemGroup>
```

- [ ] **Step 3: Commit**

```powershell
git add src/WarzoneEQ.DeviceDetection/Resources/ src/WarzoneEQ.DeviceDetection/WarzoneEQ.DeviceDetection.csproj
git commit -m "feat: bundled vidpid-overlay.json (9 headphones + 8 DACs + 4 BT)"
```

---

## Task 4: `DeviceDatabase` (loads embedded JSON, exposes lookups)

**Files:**
- Create: `src/WarzoneEQ.DeviceDetection/Matching/DeviceDatabase.cs`
- Create: `tests/WarzoneEQ.DeviceDetection.Tests/Matching/DeviceDatabaseTests.cs`

- [ ] **Step 1: Add System.Text.Json package (already in net8.0 BCL but use latest)**

```powershell
dotnet add src/WarzoneEQ.DeviceDetection/WarzoneEQ.DeviceDetection.csproj package System.Text.Json --version 8.0.5
```

- [ ] **Step 2: Failing tests**

```csharp
using FluentAssertions;
using WarzoneEQ.DeviceDetection.Matching;
using Xunit;

namespace WarzoneEQ.DeviceDetection.Tests.Matching;

public class DeviceDatabaseTests
{
    [Fact]
    public void Loads_default_embedded_database()
    {
        var db = DeviceDatabase.LoadEmbedded();
        db.HeadphoneCount.Should().BeGreaterThan(0);
        db.MultiEndpointDacCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Lookup_known_headphone_VID_PID_returns_match()
    {
        var db = DeviceDatabase.LoadEmbedded();
        var match = db.LookupHeadphoneByVidPid("VID_1532&PID_0517");
        match.Should().NotBeNull();
        match!.Model.Should().Be("Razer BlackShark V2 Pro");
        match.AutoeqSlug.Should().Be("razer/BlackShark_V2_Pro");
    }

    [Fact]
    public void Lookup_known_multi_endpoint_DAC_VID_PID_returns_match()
    {
        var db = DeviceDatabase.LoadEmbedded();
        var dac = db.LookupDacByVidPid("VID_041E&PID_3260");
        dac.Should().NotBeNull();
        dac!.Model.Should().Be("Creative Sound Blaster GC7");
        dac.GameEndpoint.Should().Contain("Game");
        dac.VoiceEndpoint.Should().Contain("Chat");
    }

    [Fact]
    public void Lookup_unknown_VID_PID_returns_null()
    {
        var db = DeviceDatabase.LoadEmbedded();
        db.LookupHeadphoneByVidPid("VID_FFFF&PID_FFFF").Should().BeNull();
        db.LookupDacByVidPid("VID_FFFF&PID_FFFF").Should().BeNull();
    }

    [Fact]
    public void Lookup_known_bluetooth_name_returns_autoeq_slug()
    {
        var db = DeviceDatabase.LoadEmbedded();
        db.LookupHeadphoneByBluetoothName("WH-1000XM5").Should().NotBeNull();
        db.LookupHeadphoneByBluetoothName("WH-1000XM5")!.AutoeqSlug.Should().Be("sony/WH-1000XM5");
    }
}
```

- [ ] **Step 3: Implement**

`src/WarzoneEQ.DeviceDetection/Matching/DeviceDatabase.cs`:
```csharp
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using WarzoneEQ.DeviceDetection.Models;

namespace WarzoneEQ.DeviceDetection.Matching;

public sealed class DeviceDatabase
{
    private readonly IReadOnlyDictionary<string, HeadphoneMatch> _headphonesByVidPid;
    private readonly IReadOnlyDictionary<string, MultiEndpointDac> _dacsByVidPid;
    private readonly IReadOnlyDictionary<string, HeadphoneMatch> _headphonesByBtName;

    public int HeadphoneCount => _headphonesByVidPid.Count + _headphonesByBtName.Count;
    public int MultiEndpointDacCount => _dacsByVidPid.Count;

    private DeviceDatabase(
        IReadOnlyDictionary<string, HeadphoneMatch> headphonesByVidPid,
        IReadOnlyDictionary<string, MultiEndpointDac> dacsByVidPid,
        IReadOnlyDictionary<string, HeadphoneMatch> headphonesByBtName)
    {
        _headphonesByVidPid = headphonesByVidPid;
        _dacsByVidPid = dacsByVidPid;
        _headphonesByBtName = headphonesByBtName;
    }

    public HeadphoneMatch? LookupHeadphoneByVidPid(string vidPidKey)
        => _headphonesByVidPid.TryGetValue(vidPidKey, out var m) ? m : null;

    public MultiEndpointDac? LookupDacByVidPid(string vidPidKey)
        => _dacsByVidPid.TryGetValue(vidPidKey, out var d) ? d : null;

    public HeadphoneMatch? LookupHeadphoneByBluetoothName(string btName)
        => _headphonesByBtName.TryGetValue(btName, out var m) ? m : null;

    public static DeviceDatabase LoadEmbedded()
    {
        var assembly = typeof(DeviceDatabase).Assembly;
        var resourceName = "WarzoneEQ.DeviceDetection.Resources.vidpid-overlay.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        return LoadFromStream(stream);
    }

    public static DeviceDatabase LoadFromStream(Stream stream)
    {
        var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var headphones = new Dictionary<string, HeadphoneMatch>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in root.GetProperty("headphones").EnumerateObject())
        {
            var v = entry.Value;
            headphones[entry.Name] = new HeadphoneMatch(
                v.GetProperty("model").GetString()!,
                v.GetProperty("autoeq_slug").GetString()!);
        }

        var dacs = new Dictionary<string, MultiEndpointDac>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in root.GetProperty("multi_endpoint_dacs").EnumerateObject())
        {
            var v = entry.Value;
            dacs[entry.Name] = new MultiEndpointDac(
                v.GetProperty("model").GetString()!,
                v.GetProperty("game_endpoint").GetString()!,
                v.GetProperty("voice_endpoint").GetString()!);
        }

        var bt = new Dictionary<string, HeadphoneMatch>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("bluetooth_names", out var btSection))
        {
            foreach (var entry in btSection.EnumerateObject())
            {
                bt[entry.Name] = new HeadphoneMatch(entry.Name, entry.Value.GetString()!);
            }
        }

        return new DeviceDatabase(headphones, dacs, bt);
    }
}
```

- [ ] **Step 4: Run, expect pass**

`Passed: 5`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.DeviceDetection/Matching/DeviceDatabase.cs tests/WarzoneEQ.DeviceDetection.Tests/Matching/DeviceDatabaseTests.cs src/WarzoneEQ.DeviceDetection/WarzoneEQ.DeviceDetection.csproj
git commit -m "feat: DeviceDatabase loads embedded vidpid-overlay.json"
```

---

## Task 5: `BluetoothNameNormalizer`

Strips region suffixes and variations from Bluetooth names so "Sony WH-1000XM5 (L)" matches "WH-1000XM5".

**Files:**
- Create: `src/WarzoneEQ.DeviceDetection/Detection/BluetoothNameNormalizer.cs`
- Create: `tests/WarzoneEQ.DeviceDetection.Tests/Detection/BluetoothNameNormalizerTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using FluentAssertions;
using WarzoneEQ.DeviceDetection.Detection;
using Xunit;

namespace WarzoneEQ.DeviceDetection.Tests.Detection;

public class BluetoothNameNormalizerTests
{
    [Theory]
    [InlineData("WH-1000XM5", "WH-1000XM5")]
    [InlineData("Sony WH-1000XM5", "WH-1000XM5")]
    [InlineData("WH-1000XM5 (L)", "WH-1000XM5")]
    [InlineData("WH-1000XM5_LE", "WH-1000XM5")]
    [InlineData("  Sony WH-1000XM5  ", "WH-1000XM5")]
    [InlineData("Arctis Nova Pro Wireless (Wired)", "Arctis Nova Pro Wireless")]
    public void Normalizes_known_variations(string input, string expected)
    {
        BluetoothNameNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Empty_or_null_returns_empty()
    {
        BluetoothNameNormalizer.Normalize(null).Should().BeEmpty();
        BluetoothNameNormalizer.Normalize("").Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run, expect failure**

- [ ] **Step 3: Implement**

`src/WarzoneEQ.DeviceDetection/Detection/BluetoothNameNormalizer.cs`:
```csharp
using System.Text.RegularExpressions;

namespace WarzoneEQ.DeviceDetection.Detection;

public static class BluetoothNameNormalizer
{
    // Strip leading vendor prefixes that aren't part of the model
    private static readonly string[] VendorPrefixes =
    {
        "Sony ", "Sennheiser ", "Bose ", "Apple ", "Microsoft ",
    };

    // Strip trailing parenthetical or underscore-suffix markers
    private static readonly Regex TrailingMarker = new(@"\s*[\(_].*$", RegexOptions.Compiled);

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var s = input.Trim();
        foreach (var prefix in VendorPrefixes)
            if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                s = s[prefix.Length..];
        s = TrailingMarker.Replace(s, "").Trim();
        return s;
    }
}
```

- [ ] **Step 4: Run, expect pass**

`Passed: 8`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.DeviceDetection/Detection/BluetoothNameNormalizer.cs tests/WarzoneEQ.DeviceDetection.Tests/Detection/BluetoothNameNormalizerTests.cs
git commit -m "feat: BluetoothNameNormalizer (strip vendor prefix + trailing markers)"
```

---

## Task 6: `DeviceMatcher` (input AudioDevice → output HeadphoneMatch + MultiEndpointDac)

**Files:**
- Create: `src/WarzoneEQ.DeviceDetection/Matching/DeviceMatcher.cs`
- Create: `tests/WarzoneEQ.DeviceDetection.Tests/Matching/DeviceMatcherTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using FluentAssertions;
using WarzoneEQ.DeviceDetection.Matching;
using WarzoneEQ.DeviceDetection.Models;
using Xunit;

namespace WarzoneEQ.DeviceDetection.Tests.Matching;

public class DeviceMatcherTests
{
    private readonly DeviceMatcher _matcher = new(DeviceDatabase.LoadEmbedded());

    [Fact]
    public void Usb_device_matches_known_headphone()
    {
        var device = new AudioDevice(
            EndpointName: "Razer BlackShark V2 Pro",
            Kind: DeviceKind.Usb,
            UsbVid: "1532",
            UsbPid: "0517");
        var result = _matcher.Match(device);
        result.Headphone.Should().NotBeNull();
        result.Headphone!.AutoeqSlug.Should().Be("razer/BlackShark_V2_Pro");
        result.Dac.Should().BeNull();
    }

    [Fact]
    public void Usb_device_matches_known_dac()
    {
        var device = new AudioDevice(
            EndpointName: "Speakers (Sound Blaster GC7 Game)",
            Kind: DeviceKind.Usb,
            UsbVid: "041E",
            UsbPid: "3260");
        var result = _matcher.Match(device);
        result.Dac.Should().NotBeNull();
        result.Dac!.Model.Should().Be("Creative Sound Blaster GC7");
        result.Headphone.Should().BeNull();
    }

    [Fact]
    public void Bluetooth_device_matches_via_normalized_name()
    {
        var device = new AudioDevice(
            EndpointName: "Sony WH-1000XM5",
            Kind: DeviceKind.Bluetooth,
            BluetoothName: "Sony WH-1000XM5");
        var result = _matcher.Match(device);
        result.Headphone.Should().NotBeNull();
        result.Headphone!.AutoeqSlug.Should().Be("sony/WH-1000XM5");
    }

    [Fact]
    public void Unknown_device_returns_empty_match()
    {
        var device = new AudioDevice(EndpointName: "Unknown DAC", Kind: DeviceKind.Analog);
        var result = _matcher.Match(device);
        result.Headphone.Should().BeNull();
        result.Dac.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run, expect build failure**

- [ ] **Step 3: Implement**

`src/WarzoneEQ.DeviceDetection/Matching/DeviceMatcher.cs`:
```csharp
using WarzoneEQ.DeviceDetection.Detection;
using WarzoneEQ.DeviceDetection.Models;

namespace WarzoneEQ.DeviceDetection.Matching;

public sealed record MatchResult(HeadphoneMatch? Headphone, MultiEndpointDac? Dac);

public sealed class DeviceMatcher
{
    private readonly DeviceDatabase _db;
    public DeviceMatcher(DeviceDatabase db) => _db = db;

    public MatchResult Match(AudioDevice device)
    {
        if (device.Kind == DeviceKind.Usb && device.UsbVidPidKey is { } key)
        {
            var dac = _db.LookupDacByVidPid(key);
            if (dac is not null) return new MatchResult(null, dac);
            var hp = _db.LookupHeadphoneByVidPid(key);
            return new MatchResult(hp, null);
        }

        if (device.Kind == DeviceKind.Bluetooth && device.BluetoothName is { } btName)
        {
            var normalized = BluetoothNameNormalizer.Normalize(btName);
            var hp = _db.LookupHeadphoneByBluetoothName(normalized);
            return new MatchResult(hp, null);
        }

        return new MatchResult(null, null);
    }
}
```

- [ ] **Step 4: Run, expect pass**

`Passed: 4`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.DeviceDetection/Matching/DeviceMatcher.cs tests/WarzoneEQ.DeviceDetection.Tests/Matching/DeviceMatcherTests.cs
git commit -m "feat: DeviceMatcher (USB VID/PID + Bluetooth name -> match)"
```

---

## Task 7: `IDeviceEnumerator` + fake for testing

**Files:**
- Create: `src/WarzoneEQ.DeviceDetection/Detection/IDeviceEnumerator.cs`
- Create: `tests/WarzoneEQ.DeviceDetection.Tests/Detection/FakeDeviceEnumerator.cs`

- [ ] **Step 1: Create interface**

`src/WarzoneEQ.DeviceDetection/Detection/IDeviceEnumerator.cs`:
```csharp
using WarzoneEQ.DeviceDetection.Models;

namespace WarzoneEQ.DeviceDetection.Detection;

public interface IDeviceEnumerator
{
    IReadOnlyList<AudioDevice> EnumeratePlaybackDevices();
}
```

- [ ] **Step 2: Create fake**

`tests/WarzoneEQ.DeviceDetection.Tests/Detection/FakeDeviceEnumerator.cs`:
```csharp
using WarzoneEQ.DeviceDetection.Detection;
using WarzoneEQ.DeviceDetection.Models;

namespace WarzoneEQ.DeviceDetection.Tests.Detection;

public sealed class FakeDeviceEnumerator : IDeviceEnumerator
{
    private readonly IReadOnlyList<AudioDevice> _devices;
    public FakeDeviceEnumerator(params AudioDevice[] devices) => _devices = devices;
    public IReadOnlyList<AudioDevice> EnumeratePlaybackDevices() => _devices;
}
```

- [ ] **Step 3: Commit**

```powershell
git add src/WarzoneEQ.DeviceDetection/Detection/IDeviceEnumerator.cs tests/WarzoneEQ.DeviceDetection.Tests/Detection/FakeDeviceEnumerator.cs
git commit -m "feat: IDeviceEnumerator interface + FakeDeviceEnumerator test double"
```

---

## Task 8: `DeviceDetectionService` (the public facade)

**Files:**
- Create: `src/WarzoneEQ.DeviceDetection/DeviceDetectionService.cs`
- Create: `tests/WarzoneEQ.DeviceDetection.Tests/DeviceDetectionServiceTests.cs`

- [ ] **Step 1: Failing tests**

```csharp
using FluentAssertions;
using WarzoneEQ.DeviceDetection;
using WarzoneEQ.DeviceDetection.Detection;
using WarzoneEQ.DeviceDetection.Matching;
using WarzoneEQ.DeviceDetection.Models;
using WarzoneEQ.DeviceDetection.Tests.Detection;
using Xunit;

namespace WarzoneEQ.DeviceDetection.Tests;

public class DeviceDetectionServiceTests
{
    private static DeviceDetectionService BuildService(params AudioDevice[] devices)
        => new(new FakeDeviceEnumerator(devices), new DeviceMatcher(DeviceDatabase.LoadEmbedded()));

    [Fact]
    public void Detects_no_devices_returns_empty_result()
    {
        var svc = BuildService();
        var snapshot = svc.Snapshot();
        snapshot.Devices.Should().BeEmpty();
        snapshot.PrimaryHeadphone.Should().BeNull();
        snapshot.MultiEndpointDac.Should().BeNull();
    }

    [Fact]
    public void Detects_GC7_and_picks_it_as_DAC()
    {
        var svc = BuildService(
            new AudioDevice(
                EndpointName: "Speakers (Sound Blaster GC7 Game)",
                Kind: DeviceKind.Usb,
                UsbVid: "041E",
                UsbPid: "3260"),
            new AudioDevice(
                EndpointName: "Speakers (Sound Blaster GC7 Chat)",
                Kind: DeviceKind.Usb,
                UsbVid: "041E",
                UsbPid: "3260"));
        var snap = svc.Snapshot();
        snap.MultiEndpointDac.Should().NotBeNull();
        snap.MultiEndpointDac!.Model.Should().Be("Creative Sound Blaster GC7");
    }

    [Fact]
    public void Detects_known_headphone_via_VID_PID()
    {
        var svc = BuildService(new AudioDevice(
            EndpointName: "Razer BlackShark V2 Pro",
            Kind: DeviceKind.Usb,
            UsbVid: "1532",
            UsbPid: "0517"));
        var snap = svc.Snapshot();
        snap.PrimaryHeadphone.Should().NotBeNull();
        snap.PrimaryHeadphone!.AutoeqSlug.Should().Be("razer/BlackShark_V2_Pro");
    }

    [Fact]
    public void Detects_known_bluetooth_headphone()
    {
        var svc = BuildService(new AudioDevice(
            EndpointName: "Sony WH-1000XM5",
            Kind: DeviceKind.Bluetooth,
            BluetoothName: "Sony WH-1000XM5"));
        var snap = svc.Snapshot();
        snap.PrimaryHeadphone.Should().NotBeNull();
        snap.PrimaryHeadphone!.AutoeqSlug.Should().Be("sony/WH-1000XM5");
    }
}
```

- [ ] **Step 2: Run, expect failure**

- [ ] **Step 3: Implement**

`src/WarzoneEQ.DeviceDetection/DeviceDetectionService.cs`:
```csharp
using WarzoneEQ.DeviceDetection.Detection;
using WarzoneEQ.DeviceDetection.Matching;
using WarzoneEQ.DeviceDetection.Models;

namespace WarzoneEQ.DeviceDetection;

public sealed record DetectionSnapshot(
    IReadOnlyList<AudioDevice> Devices,
    HeadphoneMatch? PrimaryHeadphone,
    MultiEndpointDac? MultiEndpointDac);

public sealed class DeviceDetectionService
{
    private readonly IDeviceEnumerator _enumerator;
    private readonly DeviceMatcher _matcher;

    public DeviceDetectionService(IDeviceEnumerator enumerator, DeviceMatcher matcher)
    {
        _enumerator = enumerator;
        _matcher = matcher;
    }

    public DetectionSnapshot Snapshot()
    {
        var devices = _enumerator.EnumeratePlaybackDevices();
        HeadphoneMatch? headphone = null;
        MultiEndpointDac? dac = null;

        foreach (var d in devices)
        {
            var match = _matcher.Match(d);
            headphone ??= match.Headphone;
            dac ??= match.Dac;
        }

        return new DetectionSnapshot(devices, headphone, dac);
    }
}
```

- [ ] **Step 4: Run, expect pass**

`Passed: 4`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.DeviceDetection/DeviceDetectionService.cs tests/WarzoneEQ.DeviceDetection.Tests/DeviceDetectionServiceTests.cs
git commit -m "feat: DeviceDetectionService facade (enumerate + match -> snapshot)"
```

---

## Task 9: `WindowsDeviceEnumerator` (real WMI + WinRT impl)

The Windows-only adapter. Cannot unit-test the actual WMI/WinRT calls; we test the parsing logic in isolation.

**Files:**
- Modify: `src/WarzoneEQ.DeviceDetection/WarzoneEQ.DeviceDetection.csproj` (multi-target)
- Create: `src/WarzoneEQ.DeviceDetection/Detection/WindowsDeviceEnumerator.cs`
- Create: `src/WarzoneEQ.DeviceDetection/Detection/PnpIdParser.cs` (extracts VID/PID from PnP ID strings — testable)
- Create: `tests/WarzoneEQ.DeviceDetection.Tests/Detection/PnpIdParserTests.cs`

- [ ] **Step 1: Multi-target the csproj**

Edit `src/WarzoneEQ.DeviceDetection/WarzoneEQ.DeviceDetection.csproj` `<TargetFramework>` → `<TargetFrameworks>net8.0;net8.0-windows10.0.22621.0</TargetFrameworks>` and conditional package refs:
```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0-windows10.0.22621.0'">
  <PackageReference Include="System.Management" Version="8.0.0" />
</ItemGroup>
```

(WinRT projection ships in the SDK so no separate package required for net8.0-windows.)

- [ ] **Step 2: PnpIdParser failing tests**

```csharp
using FluentAssertions;
using WarzoneEQ.DeviceDetection.Detection;
using Xunit;

namespace WarzoneEQ.DeviceDetection.Tests.Detection;

public class PnpIdParserTests
{
    [Theory]
    [InlineData(@"USB\VID_041E&PID_3260&MI_00\7&123ABC", "041E", "3260")]
    [InlineData(@"USB\VID_1532&PID_0517\6&XYZ",          "1532", "0517")]
    [InlineData(@"SWD\MMDEVAPI\{0.0.0.00000000}",        null,   null)]
    [InlineData(@"BTHENUM\Dev_001122334455\6&...",       null,   null)]
    public void Parses_VID_and_PID_from_USB_pnp_id(string input, string? expectedVid, string? expectedPid)
    {
        var (vid, pid) = PnpIdParser.ExtractVidPid(input);
        vid.Should().Be(expectedVid);
        pid.Should().Be(expectedPid);
    }

    [Theory]
    [InlineData(@"BTHENUM\Dev_AABBCCDDEEFF\6&xxxxx", true)]
    [InlineData(@"USB\VID_041E&PID_3260\xxxxx",      false)]
    public void Detects_bluetooth_devices_from_pnp_id(string input, bool isBluetooth)
    {
        PnpIdParser.IsBluetooth(input).Should().Be(isBluetooth);
    }
}
```

- [ ] **Step 3: Implement PnpIdParser (target net8.0 so it's testable)**

`src/WarzoneEQ.DeviceDetection/Detection/PnpIdParser.cs`:
```csharp
using System.Text.RegularExpressions;

namespace WarzoneEQ.DeviceDetection.Detection;

public static class PnpIdParser
{
    private static readonly Regex VidPidPattern =
        new(@"VID_([0-9A-Fa-f]{4})&PID_([0-9A-Fa-f]{4})", RegexOptions.Compiled);

    public static (string? Vid, string? Pid) ExtractVidPid(string? pnpId)
    {
        if (string.IsNullOrEmpty(pnpId)) return (null, null);
        var m = VidPidPattern.Match(pnpId);
        return m.Success
            ? (m.Groups[1].Value.ToUpperInvariant(), m.Groups[2].Value.ToUpperInvariant())
            : (null, null);
    }

    public static bool IsBluetooth(string? pnpId)
        => !string.IsNullOrEmpty(pnpId) && pnpId.StartsWith("BTHENUM", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: Run PnpIdParserTests, expect pass**

- [ ] **Step 5: Implement `WindowsDeviceEnumerator` (Windows-only target)**

`src/WarzoneEQ.DeviceDetection/Detection/WindowsDeviceEnumerator.cs`:
```csharp
#if WINDOWS
using System.Management;
using WarzoneEQ.DeviceDetection.Models;

namespace WarzoneEQ.DeviceDetection.Detection;

public sealed class WindowsDeviceEnumerator : IDeviceEnumerator
{
    public IReadOnlyList<AudioDevice> EnumeratePlaybackDevices()
    {
        var results = new List<AudioDevice>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, DeviceID, PNPDeviceID FROM Win32_SoundDevice WHERE Status = 'OK'");
        foreach (var obj in searcher.Get())
        {
            var name = obj["Name"]?.ToString() ?? "";
            var pnpId = obj["PNPDeviceID"]?.ToString() ?? "";

            if (PnpIdParser.IsBluetooth(pnpId))
            {
                results.Add(new AudioDevice(
                    EndpointName: name,
                    Kind: DeviceKind.Bluetooth,
                    BluetoothName: name));
                continue;
            }

            var (vid, pid) = PnpIdParser.ExtractVidPid(pnpId);
            if (vid is not null && pid is not null)
            {
                results.Add(new AudioDevice(
                    EndpointName: name,
                    Kind: DeviceKind.Usb,
                    UsbVid: vid,
                    UsbPid: pid));
                continue;
            }

            results.Add(new AudioDevice(EndpointName: name, Kind: DeviceKind.Analog));
        }
        return results;
    }
}
#endif
```

Add `<DefineConstants>$(DefineConstants);WINDOWS</DefineConstants>` to the windows-target ItemGroup in the csproj.

- [ ] **Step 6: Commit**

```powershell
git add src/WarzoneEQ.DeviceDetection/Detection/ tests/WarzoneEQ.DeviceDetection.Tests/Detection/PnpIdParserTests.cs src/WarzoneEQ.DeviceDetection/WarzoneEQ.DeviceDetection.csproj
git commit -m "feat: WindowsDeviceEnumerator (WMI Win32_SoundDevice -> AudioDevice list)"
```

---

## Task 10: Final test pass + README update

- [ ] **Step 1: Run full test suite**

```powershell
dotnet test
```

Expected: all device-detection tests pass plus the 56 from sub-plan #1.

- [ ] **Step 2: Update README sub-plan progress**

Mark sub-plan #2 as `[x]`.

- [ ] **Step 3: Commit**

```powershell
git add README.md
git commit -m "docs: mark sub-plan #2 complete in README"
```

---

## Self-review checklist

- [ ] Every detection path (USB VID/PID, Bluetooth name, analog) has tests.
- [ ] Multi-endpoint DAC detection returns the right Game/Voice endpoint names.
- [ ] Unknown devices return null match (no crash, no exception).
- [ ] `WindowsDeviceEnumerator` is behind `IDeviceEnumerator` so all logic is unit-testable.
- [ ] No placeholders. All code blocks runnable.
- [ ] Every task ends with a commit.

## Not covered (deferred)

- Live device-change events (the GUI in sub-plan #4 will poll on a timer instead).
- AutoEQ full DB extraction — vidpid-overlay covers the most common models; full DB is part of sub-plan #6.
- Fuzzy Levenshtein matching for unknown Bluetooth names — exact match for v1.
