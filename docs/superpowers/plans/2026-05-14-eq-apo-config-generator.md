# EQ APO Config Generator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a tested, pure-logic .NET 8 library that takes a `ProfileInput` (mode, headphones, DAC, FPS curve, intensity, toggles) and emits a valid Equalizer APO `config.txt` string. Plus a small CLI wrapper for manual verification.

**Architecture:** Functional core (no I/O, no Windows APIs) so it's fully unit-testable on any machine. The library produces a string; a future sub-plan handles writing it to disk. Two layers: low-level filter/plugin serializers (`Filter`, `Plugin`), and high-level profile generators (`CompetitiveProfile`, `CinematicProfile`, `BypassProfile`). All curve and plugin parameters are constants in code, derived from `docs/superpowers/specs/2026-05-14-warzone-eq-design.md`.

**Tech Stack:** C# 12, .NET 8 (LTS), xUnit 2.9+, FluentAssertions 6.12+, Verify.Xunit 24+ (snapshot tests for full configs), System.CommandLine for the CLI.

---

## Repo layout at end of sub-plan

```
radio ap/
├── docs/
│   └── superpowers/
│       ├── specs/2026-05-14-warzone-eq-design.md   (already exists)
│       └── plans/2026-05-14-eq-apo-config-generator.md   (this file)
├── WarzoneEQ.sln
├── .gitignore
├── Directory.Build.props
├── src/
│   ├── WarzoneEQ.ConfigGenerator/
│   │   ├── WarzoneEQ.ConfigGenerator.csproj
│   │   ├── Models/
│   │   │   ├── AudioMode.cs
│   │   │   ├── FpsCurveName.cs
│   │   │   ├── HeadphoneCorrection.cs
│   │   │   ├── DacEndpoint.cs
│   │   │   └── ProfileInput.cs
│   │   ├── Filters/
│   │   │   ├── Filter.cs
│   │   │   ├── FilterType.cs
│   │   │   └── FpsCurves.cs
│   │   ├── Plugins/
│   │   │   ├── Plugin.cs
│   │   │   ├── TdrNova.cs
│   │   │   ├── ReaXcomp.cs
│   │   │   ├── LoudMax.cs
│   │   │   └── PolyverseWider.cs
│   │   ├── Channels/
│   │   │   ├── Channel.cs
│   │   │   └── ChannelStage.cs
│   │   ├── Intensity.cs
│   │   ├── Profiles/
│   │   │   ├── IProfileGenerator.cs
│   │   │   ├── CompetitiveProfile.cs
│   │   │   ├── CinematicProfile.cs
│   │   │   └── BypassProfile.cs
│   │   └── ConfigGenerator.cs
│   └── WarzoneEQ.Cli/
│       ├── WarzoneEQ.Cli.csproj
│       └── Program.cs
└── tests/
    └── WarzoneEQ.ConfigGenerator.Tests/
        ├── WarzoneEQ.ConfigGenerator.Tests.csproj
        ├── Filters/
        │   ├── FilterTests.cs
        │   └── FpsCurvesTests.cs
        ├── Plugins/
        │   ├── TdrNovaTests.cs
        │   ├── ReaXcompTests.cs
        │   ├── LoudMaxTests.cs
        │   └── PolyverseWiderTests.cs
        ├── Channels/
        │   └── ChannelStageTests.cs
        ├── IntensityTests.cs
        ├── Profiles/
        │   ├── CompetitiveProfileTests.cs
        │   ├── CinematicProfileTests.cs
        │   └── BypassProfileTests.cs
        ├── ConfigGeneratorTests.cs
        └── Snapshots/
            (Verify-generated *.verified.txt files)
```

---

## Task 0: Solution skeleton + tooling

**Files:**
- Create: `WarzoneEQ.sln`
- Create: `.gitignore`
- Create: `Directory.Build.props`
- Create: `src/WarzoneEQ.ConfigGenerator/WarzoneEQ.ConfigGenerator.csproj`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/WarzoneEQ.ConfigGenerator.Tests.csproj`

- [ ] **Step 1: Create the solution and projects**

Run (from repo root `c:\Users\Administrator\Desktop\claude\radio ap`):
```powershell
dotnet new sln -n WarzoneEQ
dotnet new classlib -n WarzoneEQ.ConfigGenerator -o src/WarzoneEQ.ConfigGenerator -f net8.0
dotnet new xunit -n WarzoneEQ.ConfigGenerator.Tests -o tests/WarzoneEQ.ConfigGenerator.Tests -f net8.0
dotnet sln add src/WarzoneEQ.ConfigGenerator/WarzoneEQ.ConfigGenerator.csproj
dotnet sln add tests/WarzoneEQ.ConfigGenerator.Tests/WarzoneEQ.ConfigGenerator.Tests.csproj
dotnet add tests/WarzoneEQ.ConfigGenerator.Tests/WarzoneEQ.ConfigGenerator.Tests.csproj reference src/WarzoneEQ.ConfigGenerator/WarzoneEQ.ConfigGenerator.csproj
```

- [ ] **Step 2: Delete `Class1.cs` and `UnitTest1.cs` boilerplate**

Remove `src/WarzoneEQ.ConfigGenerator/Class1.cs` and `tests/WarzoneEQ.ConfigGenerator.Tests/UnitTest1.cs`.

- [ ] **Step 3: Add `Directory.Build.props`**

Create `Directory.Build.props` at repo root:
```xml
<Project>
  <PropertyGroup>
    <LangVersion>12</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <ImplicitUsings>enable</ImplicitUsings>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: Add test dependencies**

```powershell
dotnet add tests/WarzoneEQ.ConfigGenerator.Tests/WarzoneEQ.ConfigGenerator.Tests.csproj package FluentAssertions --version 6.12.1
dotnet add tests/WarzoneEQ.ConfigGenerator.Tests/WarzoneEQ.ConfigGenerator.Tests.csproj package Verify.Xunit --version 24.2.0
```

- [ ] **Step 5: Create `.gitignore`**

Create `.gitignore` at repo root:
```
bin/
obj/
*.user
.vs/
*.received.txt
TestResults/
```

- [ ] **Step 6: Build to verify the solution is wired correctly**

Run: `dotnet build`
Expected output ends with `Build succeeded.` and `0 Error(s)`.

- [ ] **Step 7: Run tests to verify xUnit works**

Run: `dotnet test`
Expected: `Passed!  - Failed: 0, Passed: 0, Skipped: 0` (or similar — no tests yet).

- [ ] **Step 8: Commit**

```powershell
git add WarzoneEQ.sln Directory.Build.props .gitignore src/ tests/
git commit -m "chore: scaffold .NET 8 solution + xUnit + FluentAssertions + Verify"
```

---

## Task 1: `AudioMode` enum

**Files:**
- Create: `src/WarzoneEQ.ConfigGenerator/Models/AudioMode.cs`
- Test: `tests/WarzoneEQ.ConfigGenerator.Tests/Filters/FilterTests.cs` (will assert that AudioMode is referenced; we'll write a placeholder test here)

- [ ] **Step 1: Write the failing test**

Create `tests/WarzoneEQ.ConfigGenerator.Tests/AudioModeTests.cs`:
```csharp
using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests;

public class AudioModeTests
{
    [Fact]
    public void Has_three_values_competitive_cinematic_bypass()
    {
        Enum.GetValues<AudioMode>()
            .Should().BeEquivalentTo(new[] { AudioMode.Competitive, AudioMode.Cinematic, AudioMode.Bypass });
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~AudioMode"`
Expected: build error `AudioMode could not be found`.

- [ ] **Step 3: Implement `AudioMode`**

Create `src/WarzoneEQ.ConfigGenerator/Models/AudioMode.cs`:
```csharp
namespace WarzoneEQ.ConfigGenerator.Models;

public enum AudioMode
{
    Competitive,
    Cinematic,
    Bypass,
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~AudioMode"`
Expected: `Passed: 1`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.ConfigGenerator/Models/AudioMode.cs tests/WarzoneEQ.ConfigGenerator.Tests/AudioModeTests.cs
git commit -m "feat: AudioMode enum (Competitive, Cinematic, Bypass)"
```

---

## Task 2: `FpsCurveName` enum

**Files:**
- Create: `src/WarzoneEQ.ConfigGenerator/Models/FpsCurveName.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/FpsCurveNameTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests;

public class FpsCurveNameTests
{
    [Fact]
    public void Has_three_values_minimalist_moderate_aggressive()
    {
        Enum.GetValues<FpsCurveName>()
            .Should().BeEquivalentTo(new[] { FpsCurveName.Minimalist, FpsCurveName.Moderate, FpsCurveName.Aggressive });
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~FpsCurveName"`
Expected: build error.

- [ ] **Step 3: Implement**

```csharp
namespace WarzoneEQ.ConfigGenerator.Models;

public enum FpsCurveName
{
    Minimalist,
    Moderate,
    Aggressive,
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~FpsCurveName"`
Expected: `Passed: 1`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.ConfigGenerator/Models/FpsCurveName.cs tests/WarzoneEQ.ConfigGenerator.Tests/FpsCurveNameTests.cs
git commit -m "feat: FpsCurveName enum (Minimalist, Moderate, Aggressive)"
```

---

## Task 3: `FilterType` enum + `Filter` record

EQ APO has these filter types: `HP` (high-pass), `LP` (low-pass), `PK` (peaking/bell), `LS` (low-shelf), `HS` (high-shelf). Each line is `Filter: ON <type> Fc <freq> Hz [Gain <gain> dB] [Q <q>]`.

**Files:**
- Create: `src/WarzoneEQ.ConfigGenerator/Filters/FilterType.cs`
- Create: `src/WarzoneEQ.ConfigGenerator/Filters/Filter.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/Filters/FilterTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Filters;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Filters;

public class FilterTests
{
    [Fact]
    public void HighPass_at_120Hz_serializes_correctly()
    {
        var f = Filter.HighPass(120);
        f.ToConfigLine().Should().Be("Filter: ON HP Fc 120 Hz");
    }

    [Fact]
    public void LowPass_at_16000Hz_serializes_correctly()
    {
        var f = Filter.LowPass(16000);
        f.ToConfigLine().Should().Be("Filter: ON LP Fc 16000 Hz");
    }

    [Fact]
    public void Peaking_with_positive_gain_serializes_with_plus_sign()
    {
        var f = Filter.Peaking(freqHz: 3000, gainDb: 4, q: 1.2);
        f.ToConfigLine().Should().Be("Filter: ON PK Fc 3000 Hz Gain +4.0 dB Q 1.2");
    }

    [Fact]
    public void Peaking_with_negative_gain_serializes_with_minus_sign()
    {
        var f = Filter.Peaking(freqHz: 1200, gainDb: -3, q: 5);
        f.ToConfigLine().Should().Be("Filter: ON PK Fc 1200 Hz Gain -3.0 dB Q 5.0");
    }

    [Fact]
    public void LowShelf_serializes_without_Q()
    {
        var f = Filter.LowShelf(freqHz: 250, gainDb: -6);
        f.ToConfigLine().Should().Be("Filter: ON LS Fc 250 Hz Gain -6.0 dB");
    }

    [Fact]
    public void HighShelf_serializes_without_Q()
    {
        var f = Filter.HighShelf(freqHz: 10000, gainDb: -3);
        f.ToConfigLine().Should().Be("Filter: ON HS Fc 10000 Hz Gain -3.0 dB");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~FilterTests"`
Expected: build errors `Filter could not be found`.

- [ ] **Step 3: Implement `FilterType` and `Filter`**

`src/WarzoneEQ.ConfigGenerator/Filters/FilterType.cs`:
```csharp
namespace WarzoneEQ.ConfigGenerator.Filters;

public enum FilterType
{
    HP,
    LP,
    PK,
    LS,
    HS,
}
```

`src/WarzoneEQ.ConfigGenerator/Filters/Filter.cs`:
```csharp
using System.Globalization;

namespace WarzoneEQ.ConfigGenerator.Filters;

public sealed record Filter(FilterType Type, double FrequencyHz, double? GainDb = null, double? Q = null)
{
    public static Filter HighPass(double freqHz) => new(FilterType.HP, freqHz);
    public static Filter LowPass(double freqHz) => new(FilterType.LP, freqHz);
    public static Filter Peaking(double freqHz, double gainDb, double q) => new(FilterType.PK, freqHz, gainDb, q);
    public static Filter LowShelf(double freqHz, double gainDb) => new(FilterType.LS, freqHz, gainDb);
    public static Filter HighShelf(double freqHz, double gainDb) => new(FilterType.HS, freqHz, gainDb);

    public Filter WithGain(double newGainDb) => this with { GainDb = newGainDb };

    public string ToConfigLine()
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.Append($"Filter: ON {Type} Fc {FrequencyHz.ToString("0.###", inv)} Hz");
        if (GainDb.HasValue)
            sb.Append($" Gain {(GainDb.Value >= 0 ? "+" : "")}{GainDb.Value.ToString("0.0", inv)} dB");
        if (Q.HasValue)
            sb.Append($" Q {Q.Value.ToString("0.0##", inv)}");
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~FilterTests"`
Expected: `Passed: 6`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.ConfigGenerator/Filters/ tests/WarzoneEQ.ConfigGenerator.Tests/Filters/
git commit -m "feat: Filter record with EQ APO serialization (HP/LP/PK/LS/HS)"
```

---

## Task 4: Shipped FPS curves (the three constants)

Per spec §7.2.

**Files:**
- Create: `src/WarzoneEQ.ConfigGenerator/Filters/FpsCurves.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/Filters/FpsCurvesTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Filters;
using WarzoneEQ.ConfigGenerator.Models;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Filters;

public class FpsCurvesTests
{
    [Fact]
    public void Minimalist_has_3_filters()
    {
        FpsCurves.Get(FpsCurveName.Minimalist).Should().HaveCount(3);
    }

    [Fact]
    public void Moderate_has_6_filters()
    {
        FpsCurves.Get(FpsCurveName.Moderate).Should().HaveCount(6);
    }

    [Fact]
    public void Aggressive_has_10_filters()
    {
        FpsCurves.Get(FpsCurveName.Aggressive).Should().HaveCount(10);
    }

    [Fact]
    public void Moderate_third_filter_is_2000Hz_peaking_plus3dB()
    {
        var filters = FpsCurves.Get(FpsCurveName.Moderate);
        filters[2].Type.Should().Be(FilterType.PK);
        filters[2].FrequencyHz.Should().Be(2000);
        filters[2].GainDb.Should().Be(3);
        filters[2].Q.Should().Be(1.4);
    }

    [Fact]
    public void Aggressive_includes_suppressed_gunfire_scoop_at_1200Hz_minus3dB()
    {
        var filters = FpsCurves.Get(FpsCurveName.Aggressive);
        filters.Should().Contain(f =>
            f.FrequencyHz == 1200 && f.GainDb == -3 && f.Q == 5);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~FpsCurvesTests"`
Expected: build error.

- [ ] **Step 3: Implement `FpsCurves`**

```csharp
using WarzoneEQ.ConfigGenerator.Models;

namespace WarzoneEQ.ConfigGenerator.Filters;

public static class FpsCurves
{
    public static IReadOnlyList<Filter> Get(FpsCurveName name) => name switch
    {
        FpsCurveName.Minimalist => Minimalist,
        FpsCurveName.Moderate   => Moderate,
        FpsCurveName.Aggressive => Aggressive,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static readonly IReadOnlyList<Filter> Minimalist = new[]
    {
        Filter.HighPass(120),
        Filter.Peaking(3000, 4, 1.2),
        Filter.HighShelf(8000, -2),
    };

    private static readonly IReadOnlyList<Filter> Moderate = new[]
    {
        Filter.LowShelf(250, -6),
        Filter.Peaking(800, -3, 4),
        Filter.Peaking(2000, 3, 1.4),
        Filter.Peaking(3500, 5, 1.8),
        Filter.Peaking(5000, 2, 1.2),
        Filter.HighShelf(10000, -3),
    };

    private static readonly IReadOnlyList<Filter> Aggressive = new[]
    {
        Filter.HighPass(80),
        Filter.Peaking(180, -4, 2),
        Filter.Peaking(500, -2, 3),
        Filter.Peaking(1200, -3, 5),
        Filter.Peaking(2800, 6, 2),
        Filter.Peaking(4000, 4, 2),
        Filter.Peaking(6000, -5, 4),
        Filter.HighShelf(7000, 1),
        Filter.Peaking(12000, -2, 1),
        Filter.LowPass(16000),
    };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~FpsCurvesTests"`
Expected: `Passed: 5`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.ConfigGenerator/Filters/FpsCurves.cs tests/WarzoneEQ.ConfigGenerator.Tests/Filters/FpsCurvesTests.cs
git commit -m "feat: shipped FPS curves (Minimalist 3-band, Moderate 6-band, Aggressive 10-band)"
```

---

## Task 5: Intensity scaling

Per spec §7.3: `effective_gain = nominal_gain × (slider / 100)`. Scales only gains, not Fc or Q. Filters without a gain (HP, LP) are unchanged.

**Files:**
- Create: `src/WarzoneEQ.ConfigGenerator/Intensity.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/IntensityTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using WarzoneEQ.ConfigGenerator;
using WarzoneEQ.ConfigGenerator.Filters;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests;

public class IntensityTests
{
    [Fact]
    public void Scaling_at_1_returns_filters_unchanged()
    {
        var f = Filter.Peaking(3000, 5, 1.5);
        Intensity.Scale(f, 1.0).Should().BeEquivalentTo(f);
    }

    [Fact]
    public void Scaling_at_0_5_halves_gain_keeps_freq_and_q()
    {
        var f = Filter.Peaking(3000, 5, 1.5);
        var scaled = Intensity.Scale(f, 0.5);
        scaled.GainDb.Should().Be(2.5);
        scaled.FrequencyHz.Should().Be(3000);
        scaled.Q.Should().Be(1.5);
    }

    [Fact]
    public void Scaling_at_0_zeros_gain()
    {
        var f = Filter.Peaking(3000, 5, 1.5);
        Intensity.Scale(f, 0).GainDb.Should().Be(0);
    }

    [Fact]
    public void Scaling_filter_without_gain_is_no_op()
    {
        var hp = Filter.HighPass(80);
        Intensity.Scale(hp, 0.5).Should().BeEquivalentTo(hp);
    }

    [Fact]
    public void Scaling_a_list_scales_each_filter()
    {
        var curve = FpsCurves.Get(WarzoneEQ.ConfigGenerator.Models.FpsCurveName.Moderate);
        var scaled = Intensity.Scale(curve, 0.5);
        scaled[0].GainDb.Should().Be(-3);   // was -6
        scaled[3].GainDb.Should().Be(2.5);  // was 5
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Scaling_throws_on_out_of_range_intensity(double bad)
    {
        var f = Filter.Peaking(3000, 5, 1.5);
        Assert.Throws<ArgumentOutOfRangeException>(() => Intensity.Scale(f, bad));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~IntensityTests"`
Expected: build error.

- [ ] **Step 3: Implement `Intensity`**

```csharp
using WarzoneEQ.ConfigGenerator.Filters;

namespace WarzoneEQ.ConfigGenerator;

public static class Intensity
{
    public static Filter Scale(Filter filter, double intensity)
    {
        if (intensity < 0 || intensity > 1)
            throw new ArgumentOutOfRangeException(nameof(intensity), "Must be in [0, 1].");
        if (!filter.GainDb.HasValue) return filter;
        return filter.WithGain(filter.GainDb.Value * intensity);
    }

    public static IReadOnlyList<Filter> Scale(IReadOnlyList<Filter> filters, double intensity)
        => filters.Select(f => Scale(f, intensity)).ToList();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~IntensityTests"`
Expected: `Passed: 7`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.ConfigGenerator/Intensity.cs tests/WarzoneEQ.ConfigGenerator.Tests/IntensityTests.cs
git commit -m "feat: intensity scaling (multiplies gains only, leaves Fc/Q untouched)"
```

---

## Task 6: `Channel` enum + `ChannelStage`

EQ APO uses channel names `L R FL FR FC LFE BL BR SL SR`. A "channel stage" is a `Channel:` directive followed by zero or more `Preamp:`/`Filter:`/`Plugin:` lines.

**Files:**
- Create: `src/WarzoneEQ.ConfigGenerator/Channels/Channel.cs`
- Create: `src/WarzoneEQ.ConfigGenerator/Channels/ChannelStage.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/Channels/ChannelStageTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Channels;
using WarzoneEQ.ConfigGenerator.Filters;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Channels;

public class ChannelStageTests
{
    [Fact]
    public void Single_channel_preamp_serializes_correctly()
    {
        var stage = new ChannelStage(new[] { Channel.FC }, preampDb: -6);
        var lines = stage.ToConfigLines();
        lines.Should().Equal(
            "Channel: FC",
            "Preamp: -6.0 dB"
        );
    }

    [Fact]
    public void Multiple_channels_listed_space_separated()
    {
        var stage = new ChannelStage(new[] { Channel.BL, Channel.BR, Channel.SL, Channel.SR }, preampDb: 2);
        var lines = stage.ToConfigLines();
        lines.First().Should().Be("Channel: BL BR SL SR");
    }

    [Fact]
    public void Stage_with_filters_emits_each_filter_line()
    {
        var stage = new ChannelStage(
            new[] { Channel.L, Channel.R },
            preampDb: null,
            filters: new[] { Filter.HighPass(80) });
        stage.ToConfigLines().Should().Equal(
            "Channel: L R",
            "Filter: ON HP Fc 80 Hz"
        );
    }

    [Fact]
    public void Stage_without_preamp_omits_preamp_line()
    {
        var stage = new ChannelStage(new[] { Channel.L, Channel.R }, preampDb: null);
        stage.ToConfigLines().Should().Equal("Channel: L R");
    }

    [Fact]
    public void Stage_with_zero_preamp_still_emits_line()
    {
        var stage = new ChannelStage(new[] { Channel.L }, preampDb: 0);
        stage.ToConfigLines().Should().Equal(
            "Channel: L",
            "Preamp: 0.0 dB"
        );
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ChannelStageTests"`
Expected: build error.

- [ ] **Step 3: Implement `Channel` and `ChannelStage`**

`src/WarzoneEQ.ConfigGenerator/Channels/Channel.cs`:
```csharp
namespace WarzoneEQ.ConfigGenerator.Channels;

public enum Channel
{
    L, R, FL, FR, FC, LFE, BL, BR, SL, SR,
}
```

`src/WarzoneEQ.ConfigGenerator/Channels/ChannelStage.cs`:
```csharp
using System.Globalization;
using WarzoneEQ.ConfigGenerator.Filters;
using WarzoneEQ.ConfigGenerator.Plugins;

namespace WarzoneEQ.ConfigGenerator.Channels;

public sealed record ChannelStage(
    IReadOnlyList<Channel> Channels,
    double? PreampDb = null,
    IReadOnlyList<Filter>? Filters = null,
    IReadOnlyList<Plugin>? Plugins = null)
{
    public IEnumerable<string> ToConfigLines()
    {
        yield return "Channel: " + string.Join(' ', Channels);
        if (PreampDb.HasValue)
            yield return $"Preamp: {PreampDb.Value.ToString("0.0", CultureInfo.InvariantCulture)} dB";
        foreach (var f in Filters ?? Array.Empty<Filter>())
            yield return f.ToConfigLine();
        foreach (var p in Plugins ?? Array.Empty<Plugin>())
            yield return p.ToConfigLine();
    }
}
```

(`Plugin` doesn't exist yet — Task 7 creates it. The Filters/Plugins constructor params will compile because they're optional with default `null`. We use `Array.Empty<Plugin>()` in the iteration which compiles once Plugin exists. Until then, temporarily make the Plugins property `IReadOnlyList<object>?` and remove the iteration. **Alternative simpler path:** put a placeholder `public abstract record Plugin(string Name);` empty type in `src/WarzoneEQ.ConfigGenerator/Plugins/Plugin.cs` first.)

**Cleaner approach: do this before Step 3:**

Create `src/WarzoneEQ.ConfigGenerator/Plugins/Plugin.cs`:
```csharp
namespace WarzoneEQ.ConfigGenerator.Plugins;

public abstract record Plugin
{
    public abstract string ToConfigLine();
}
```

Then Step 3 above compiles. We'll fill in concrete plugins in later tasks.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ChannelStageTests"`
Expected: `Passed: 5`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.ConfigGenerator/Channels/ src/WarzoneEQ.ConfigGenerator/Plugins/Plugin.cs tests/WarzoneEQ.ConfigGenerator.Tests/Channels/
git commit -m "feat: Channel enum + ChannelStage serialization (preamp + filters + plugins)"
```

---

## Task 7: `TdrNova` plugin (transient shaper + spectral ducker)

TDR Nova's EQ APO loader treats it as a VST. Parameters get passed via the `Plugin:` line. Per spec §5.2, our two usages are:

- **Transient shaper on sides+rear:** band-B boost +5 dB at 3 kHz, Q 1.5
- **Spectral ducker on FC:** band-A threshold −28 dB, ratio 4:1, band 200–5000 Hz

The exact EQ APO syntax for VSTs is:
```
Plugin: "TDR Nova" -bandA-thresh -28 -bandA-ratio 4 -bandA-fLow 200 -bandA-fHigh 5000
```

**Files:**
- Create: `src/WarzoneEQ.ConfigGenerator/Plugins/TdrNova.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/Plugins/TdrNovaTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Plugins;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Plugins;

public class TdrNovaTests
{
    [Fact]
    public void Spectral_ducker_serializes_with_band_A_params()
    {
        var plugin = TdrNova.SpectralDucker(thresholdDb: -28, ratio: 4, freqLow: 200, freqHigh: 5000);
        plugin.ToConfigLine().Should().Be(
            "Plugin: \"TDR Nova\" -bandA-thresh -28.0 -bandA-ratio 4.0 -bandA-fLow 200 -bandA-fHigh 5000");
    }

    [Fact]
    public void Transient_shaper_serializes_with_band_B_boost()
    {
        var plugin = TdrNova.TransientShaper(freqHz: 3000, gainDb: 5, q: 1.5);
        plugin.ToConfigLine().Should().Be(
            "Plugin: \"TDR Nova\" -bandB-freq 3000 -bandB-gain +5.0 -bandB-Q 1.5");
    }

    [Fact]
    public void Linear_phase_mode_appends_mode_flag()
    {
        var plugin = TdrNova.TransientShaper(freqHz: 3000, gainDb: 5, q: 1.5).WithLinearPhase();
        plugin.ToConfigLine().Should().EndWith(" -mode linear-phase");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~TdrNovaTests"`
Expected: build error.

- [ ] **Step 3: Implement `TdrNova`**

```csharp
using System.Globalization;
using System.Text;

namespace WarzoneEQ.ConfigGenerator.Plugins;

public sealed record TdrNova : Plugin
{
    private readonly string _params;
    private readonly bool _linearPhase;

    private TdrNova(string @params, bool linearPhase = false)
    {
        _params = @params;
        _linearPhase = linearPhase;
    }

    public static TdrNova SpectralDucker(double thresholdDb, double ratio, double freqLow, double freqHigh)
    {
        var inv = CultureInfo.InvariantCulture;
        return new TdrNova(
            $"-bandA-thresh {thresholdDb.ToString("0.0", inv)} " +
            $"-bandA-ratio {ratio.ToString("0.0", inv)} " +
            $"-bandA-fLow {freqLow.ToString("0", inv)} " +
            $"-bandA-fHigh {freqHigh.ToString("0", inv)}");
    }

    public static TdrNova TransientShaper(double freqHz, double gainDb, double q)
    {
        var inv = CultureInfo.InvariantCulture;
        var sign = gainDb >= 0 ? "+" : "";
        return new TdrNova(
            $"-bandB-freq {freqHz.ToString("0", inv)} " +
            $"-bandB-gain {sign}{gainDb.ToString("0.0", inv)} " +
            $"-bandB-Q {q.ToString("0.0##", inv)}");
    }

    public TdrNova WithLinearPhase() => new(_params, linearPhase: true);

    public override string ToConfigLine()
    {
        var sb = new StringBuilder("Plugin: \"TDR Nova\" ").Append(_params);
        if (_linearPhase) sb.Append(" -mode linear-phase");
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~TdrNovaTests"`
Expected: `Passed: 3`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.ConfigGenerator/Plugins/TdrNova.cs tests/WarzoneEQ.ConfigGenerator.Tests/Plugins/TdrNovaTests.cs
git commit -m "feat: TDR Nova plugin (spectral ducker + transient shaper + linear-phase mode)"
```

---

## Task 8: `ReaXcomp` plugin (footstep-band upward compressor)

Per spec §5.2: `Plugin: "ReaXcomp" -band 1 -freq-low 2000 -freq-high 4500 -threshold -42 -ratio 1:2 -attack 5 -release 80`.

**Files:**
- Create: `src/WarzoneEQ.ConfigGenerator/Plugins/ReaXcomp.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/Plugins/ReaXcompTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Plugins;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Plugins;

public class ReaXcompTests
{
    [Fact]
    public void Upward_compressor_serializes_full_param_list()
    {
        var plugin = ReaXcomp.UpwardCompressor(
            bandIndex: 1,
            freqLowHz: 2000,
            freqHighHz: 4500,
            thresholdDb: -42,
            ratio: "1:2",
            attackMs: 5,
            releaseMs: 80);
        plugin.ToConfigLine().Should().Be(
            "Plugin: \"ReaXcomp\" -band 1 -freq-low 2000 -freq-high 4500 -threshold -42.0 -ratio 1:2 -attack 5 -release 80");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~ReaXcompTests"`
Expected: build error.

- [ ] **Step 3: Implement `ReaXcomp`**

```csharp
using System.Globalization;

namespace WarzoneEQ.ConfigGenerator.Plugins;

public sealed record ReaXcomp : Plugin
{
    private readonly string _params;
    private ReaXcomp(string @params) => _params = @params;

    public static ReaXcomp UpwardCompressor(
        int bandIndex, double freqLowHz, double freqHighHz,
        double thresholdDb, string ratio, int attackMs, int releaseMs)
    {
        var inv = CultureInfo.InvariantCulture;
        return new ReaXcomp(
            $"-band {bandIndex} " +
            $"-freq-low {freqLowHz.ToString("0", inv)} " +
            $"-freq-high {freqHighHz.ToString("0", inv)} " +
            $"-threshold {thresholdDb.ToString("0.0", inv)} " +
            $"-ratio {ratio} " +
            $"-attack {attackMs.ToString(inv)} " +
            $"-release {releaseMs.ToString(inv)}");
    }

    public override string ToConfigLine() => $"Plugin: \"ReaXcomp\" {_params}";
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~ReaXcompTests"`
Expected: `Passed: 1`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.ConfigGenerator/Plugins/ReaXcomp.cs tests/WarzoneEQ.ConfigGenerator.Tests/Plugins/ReaXcompTests.cs
git commit -m "feat: ReaXcomp plugin (footstep-band upward compressor)"
```

---

## Task 9: `LoudMax` plugin (limiter)

Per spec §5.2: `Plugin: "LoudMax" -ceiling -1.0`.

**Files:**
- Create: `src/WarzoneEQ.ConfigGenerator/Plugins/LoudMax.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/Plugins/LoudMaxTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Plugins;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Plugins;

public class LoudMaxTests
{
    [Fact]
    public void Limiter_with_default_ceiling_minus1dB_serializes_correctly()
    {
        var plugin = LoudMax.Limiter(ceilingDb: -1.0);
        plugin.ToConfigLine().Should().Be("Plugin: \"LoudMax\" -ceiling -1.0");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~LoudMaxTests"`
Expected: build error.

- [ ] **Step 3: Implement `LoudMax`**

```csharp
using System.Globalization;

namespace WarzoneEQ.ConfigGenerator.Plugins;

public sealed record LoudMax : Plugin
{
    private readonly double _ceilingDb;
    private LoudMax(double ceilingDb) => _ceilingDb = ceilingDb;
    public static LoudMax Limiter(double ceilingDb) => new(ceilingDb);
    public override string ToConfigLine()
        => $"Plugin: \"LoudMax\" -ceiling {_ceilingDb.ToString("0.0", CultureInfo.InvariantCulture)}";
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~LoudMaxTests"`
Expected: `Passed: 1`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.ConfigGenerator/Plugins/LoudMax.cs tests/WarzoneEQ.ConfigGenerator.Tests/Plugins/LoudMaxTests.cs
git commit -m "feat: LoudMax plugin (brick-wall limiter)"
```

---

## Task 10: `PolyverseWider` plugin (stereo widener for Cinematic mode)

Polyverse Wider has one main param: width (0–200, 100 = unity). Mono-safe by design.

**Files:**
- Create: `src/WarzoneEQ.ConfigGenerator/Plugins/PolyverseWider.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/Plugins/PolyverseWiderTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Plugins;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Plugins;

public class PolyverseWiderTests
{
    [Fact]
    public void Wider_at_140_serializes_correctly()
    {
        var plugin = PolyverseWider.Width(140);
        plugin.ToConfigLine().Should().Be("Plugin: \"Polyverse Wider\" -width 140");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(201)]
    public void Wider_rejects_out_of_range_width(int badWidth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PolyverseWider.Width(badWidth));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~PolyverseWiderTests"`
Expected: build error.

- [ ] **Step 3: Implement `PolyverseWider`**

```csharp
namespace WarzoneEQ.ConfigGenerator.Plugins;

public sealed record PolyverseWider : Plugin
{
    private readonly int _width;
    private PolyverseWider(int width) => _width = width;
    public static PolyverseWider Width(int width)
    {
        if (width < 0 || width > 200)
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be in [0, 200].");
        return new PolyverseWider(width);
    }
    public override string ToConfigLine() => $"Plugin: \"Polyverse Wider\" -width {_width}";
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~PolyverseWiderTests"`
Expected: `Passed: 3`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.ConfigGenerator/Plugins/PolyverseWider.cs tests/WarzoneEQ.ConfigGenerator.Tests/Plugins/PolyverseWiderTests.cs
git commit -m "feat: Polyverse Wider plugin (mono-safe stereo widener)"
```

---

## Task 11: `HeadphoneCorrection` + `DacEndpoint` value types

`HeadphoneCorrection` is the slug we put in `Include: warzone\headphone-correction\<slug>.txt`. `DacEndpoint` carries the Windows endpoint name we put in the `Device:` directive (e.g., `Speakers Sound Blaster GC7 Game`).

**Files:**
- Create: `src/WarzoneEQ.ConfigGenerator/Models/HeadphoneCorrection.cs`
- Create: `src/WarzoneEQ.ConfigGenerator/Models/DacEndpoint.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/Models/HeadphoneCorrectionTests.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/Models/DacEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/WarzoneEQ.ConfigGenerator.Tests/Models/HeadphoneCorrectionTests.cs`:
```csharp
using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Models;

public class HeadphoneCorrectionTests
{
    [Fact]
    public void Constructs_with_a_slug()
    {
        var hc = new HeadphoneCorrection("HD600");
        hc.IncludePath.Should().Be(@"warzone\headphone-correction\HD600.txt");
    }

    [Fact]
    public void Rejects_null_or_whitespace_slug()
    {
        Assert.Throws<ArgumentException>(() => new HeadphoneCorrection(""));
        Assert.Throws<ArgumentException>(() => new HeadphoneCorrection("   "));
    }

    [Fact]
    public void None_constant_represents_no_correction()
    {
        HeadphoneCorrection.None.IncludePath.Should().BeNull();
    }
}
```

`tests/WarzoneEQ.ConfigGenerator.Tests/Models/DacEndpointTests.cs`:
```csharp
using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Models;

public class DacEndpointTests
{
    [Fact]
    public void Constructs_with_endpoint_name()
    {
        var dac = new DacEndpoint("Speakers Sound Blaster GC7 Game");
        dac.DeviceDirective.Should().Be("Device: Speakers Sound Blaster GC7 Game");
    }

    [Fact]
    public void Default_endpoint_omits_Device_directive()
    {
        DacEndpoint.WindowsDefault.DeviceDirective.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~Models"`
Expected: build error.

- [ ] **Step 3: Implement both types**

`src/WarzoneEQ.ConfigGenerator/Models/HeadphoneCorrection.cs`:
```csharp
namespace WarzoneEQ.ConfigGenerator.Models;

public sealed record HeadphoneCorrection
{
    public string? IncludePath { get; }
    private HeadphoneCorrection(string? path) => IncludePath = path;
    public HeadphoneCorrection(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Slug must be non-empty.", nameof(slug));
        IncludePath = $@"warzone\headphone-correction\{slug}.txt";
    }
    public static readonly HeadphoneCorrection None = new((string?)null);
}
```

`src/WarzoneEQ.ConfigGenerator/Models/DacEndpoint.cs`:
```csharp
namespace WarzoneEQ.ConfigGenerator.Models;

public sealed record DacEndpoint
{
    public string? DeviceDirective { get; }
    private DacEndpoint(string? directive) => DeviceDirective = directive;
    public DacEndpoint(string endpointName)
    {
        if (string.IsNullOrWhiteSpace(endpointName))
            throw new ArgumentException("Endpoint name must be non-empty.", nameof(endpointName));
        DeviceDirective = $"Device: {endpointName}";
    }
    public static readonly DacEndpoint WindowsDefault = new((string?)null);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~Models"`
Expected: `Passed: 5`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.ConfigGenerator/Models/HeadphoneCorrection.cs src/WarzoneEQ.ConfigGenerator/Models/DacEndpoint.cs tests/WarzoneEQ.ConfigGenerator.Tests/Models/
git commit -m "feat: HeadphoneCorrection + DacEndpoint value types"
```

---

## Task 12: `ProfileInput` record (the public input DTO)

The single record an outside caller passes in.

**Files:**
- Create: `src/WarzoneEQ.ConfigGenerator/Models/ProfileInput.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/Models/ProfileInputTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Models;

public class ProfileInputTests
{
    [Fact]
    public void Default_constructs_with_required_mode_only()
    {
        var p = new ProfileInput(AudioMode.Competitive);
        p.Mode.Should().Be(AudioMode.Competitive);
        p.FpsCurve.Should().Be(FpsCurveName.Moderate);
        p.Intensity.Should().Be(1.0);
        p.HeadphoneCorrection.Should().Be(HeadphoneCorrection.None);
        p.DacEndpoint.Should().Be(DacEndpoint.WindowsDefault);
        p.EnableFootstepCompressor.Should().BeTrue();
        p.EnableLinearPhase.Should().BeFalse();
        p.EnableAdaptiveLoudness.Should().BeFalse();
        p.EnablePolyverseWider.Should().BeFalse();
        p.HrirIncludePath.Should().Be(@"warzone\hrir\hesuvi-active.wav");
    }

    [Fact]
    public void Intensity_outside_0_to_1_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProfileInput(AudioMode.Competitive) { Intensity = 1.5 });
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ProfileInputTests"`
Expected: build error.

- [ ] **Step 3: Implement `ProfileInput`**

```csharp
namespace WarzoneEQ.ConfigGenerator.Models;

public sealed record ProfileInput(AudioMode Mode)
{
    public FpsCurveName FpsCurve { get; init; } = FpsCurveName.Moderate;

    private double _intensity = 1.0;
    public double Intensity
    {
        get => _intensity;
        init
        {
            if (value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(nameof(value), "Intensity must be in [0, 1].");
            _intensity = value;
        }
    }

    public HeadphoneCorrection HeadphoneCorrection { get; init; } = HeadphoneCorrection.None;
    public DacEndpoint DacEndpoint { get; init; } = DacEndpoint.WindowsDefault;

    public bool EnableFootstepCompressor { get; init; } = true;
    public bool EnableLinearPhase { get; init; } = false;
    public bool EnableAdaptiveLoudness { get; init; } = false;
    public bool EnablePolyverseWider { get; init; } = false;

    public string HrirIncludePath { get; init; } = @"warzone\hrir\hesuvi-active.wav";
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ProfileInputTests"`
Expected: `Passed: 2`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.ConfigGenerator/Models/ProfileInput.cs tests/WarzoneEQ.ConfigGenerator.Tests/Models/ProfileInputTests.cs
git commit -m "feat: ProfileInput record (mode + curve + intensity + toggles + headphone/DAC)"
```

---

## Task 13: `IProfileGenerator` interface + `BypassProfile`

The simplest profile — emits a near-empty config that still respects `Device:` if present. Lets us validate the generator scaffold end-to-end before tackling the complex Competitive/Cinematic chains.

**Files:**
- Create: `src/WarzoneEQ.ConfigGenerator/Profiles/IProfileGenerator.cs`
- Create: `src/WarzoneEQ.ConfigGenerator/Profiles/BypassProfile.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/Profiles/BypassProfileTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.ConfigGenerator.Profiles;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Profiles;

public class BypassProfileTests
{
    [Fact]
    public void Bypass_without_DAC_emits_only_a_comment_header()
    {
        var profile = new BypassProfile();
        var input = new ProfileInput(AudioMode.Bypass);
        var output = profile.Generate(input);
        output.Should().StartWith("# warzone\\current.txt — Bypass mode (chain disabled)");
        output.Should().NotContain("Device:");
        output.Should().NotContain("Filter:");
        output.Should().NotContain("Plugin:");
    }

    [Fact]
    public void Bypass_with_DAC_emits_Device_directive_only()
    {
        var profile = new BypassProfile();
        var input = new ProfileInput(AudioMode.Bypass)
        {
            DacEndpoint = new DacEndpoint("Speakers Sound Blaster GC7 Game"),
        };
        var output = profile.Generate(input);
        output.Should().Contain("Device: Speakers Sound Blaster GC7 Game");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~BypassProfileTests"`
Expected: build error.

- [ ] **Step 3: Implement**

`src/WarzoneEQ.ConfigGenerator/Profiles/IProfileGenerator.cs`:
```csharp
using WarzoneEQ.ConfigGenerator.Models;

namespace WarzoneEQ.ConfigGenerator.Profiles;

public interface IProfileGenerator
{
    string Generate(ProfileInput input);
}
```

`src/WarzoneEQ.ConfigGenerator/Profiles/BypassProfile.cs`:
```csharp
using System.Text;
using WarzoneEQ.ConfigGenerator.Models;

namespace WarzoneEQ.ConfigGenerator.Profiles;

public sealed class BypassProfile : IProfileGenerator
{
    public string Generate(ProfileInput input)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# warzone\\current.txt — Bypass mode (chain disabled)");
        if (input.DacEndpoint.DeviceDirective is { } directive)
            sb.AppendLine(directive);
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~BypassProfileTests"`
Expected: `Passed: 2`.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.ConfigGenerator/Profiles/IProfileGenerator.cs src/WarzoneEQ.ConfigGenerator/Profiles/BypassProfile.cs tests/WarzoneEQ.ConfigGenerator.Tests/Profiles/BypassProfileTests.cs
git commit -m "feat: IProfileGenerator + BypassProfile (minimal chain)"
```

---

## Task 14: `CompetitiveProfile` (the workhorse)

Per spec §5.2 Competitive mode. Order of operations:

1. Header comment
2. `Device:` if DAC endpoint set
3. Pre-mix stage:
   - `L R` channels: safety HP at 80 Hz
   - `FL FR` channels: −3 dB preamp
   - `FC` channel: −6 dB preamp + TDR Nova spectral ducker (band A)
   - `BL BR SL SR` channels: +2 dB preamp + TDR Nova transient shaper (band B)
   - `LFE` channel: −12 dB preamp
4. Post-mix stage:
   - `Include:` HRIR
   - `Include:` headphone correction (if present)
   - `Include:` `warzone\fps-curves\<mode-lowercased>.txt` BUT only if intensity == 1.0; otherwise inline scaled filters
   - ReaXcomp footstep-band upward compressor (if enabled)
   - LoudMax limiter

**Files:**
- Create: `src/WarzoneEQ.ConfigGenerator/Profiles/CompetitiveProfile.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/Profiles/CompetitiveProfileTests.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/Snapshots/CompetitiveProfileTests.Hd600_Gc7_Moderate_FullIntensity.verified.txt`

- [ ] **Step 1: Write the failing tests (unit + snapshot)**

```csharp
using FluentAssertions;
using VerifyXunit;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.ConfigGenerator.Profiles;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Profiles;

[UsesVerify]
public class CompetitiveProfileTests
{
    [Fact]
    public void Generates_header_and_pre_mix_stage()
    {
        var output = new CompetitiveProfile().Generate(new ProfileInput(AudioMode.Competitive));
        output.Should().Contain("Stage: pre-mix");
        output.Should().Contain("Channel: L R");
        output.Should().Contain("Filter: ON HP Fc 80 Hz");
        output.Should().Contain("Channel: FC");
        output.Should().Contain("Plugin: \"TDR Nova\" -bandA-thresh -28.0");
        output.Should().Contain("Channel: BL BR SL SR");
        output.Should().Contain("Plugin: \"TDR Nova\" -bandB-freq 3000 -bandB-gain +5.0");
        output.Should().Contain("Channel: LFE");
    }

    [Fact]
    public void Generates_post_mix_stage_with_includes()
    {
        var input = new ProfileInput(AudioMode.Competitive)
        {
            HeadphoneCorrection = new HeadphoneCorrection("HD600"),
        };
        var output = new CompetitiveProfile().Generate(input);
        output.Should().Contain("Stage: post-mix");
        output.Should().Contain(@"Include: warzone\hrir\hesuvi-active.wav");
        output.Should().Contain(@"Include: warzone\headphone-correction\HD600.txt");
        output.Should().Contain(@"Include: warzone\fps-curves\moderate.txt");
    }

    [Fact]
    public void Includes_ReaXcomp_when_footstep_compressor_enabled()
    {
        var output = new CompetitiveProfile().Generate(new ProfileInput(AudioMode.Competitive));
        output.Should().Contain("Plugin: \"ReaXcomp\" -band 1 -freq-low 2000 -freq-high 4500 -threshold -42.0 -ratio 1:2");
    }

    [Fact]
    public void Excludes_ReaXcomp_when_footstep_compressor_disabled()
    {
        var output = new CompetitiveProfile().Generate(new ProfileInput(AudioMode.Competitive)
        {
            EnableFootstepCompressor = false,
        });
        output.Should().NotContain("ReaXcomp");
    }

    [Fact]
    public void Always_ends_with_LoudMax_limiter()
    {
        var output = new CompetitiveProfile().Generate(new ProfileInput(AudioMode.Competitive));
        var lines = output.TrimEnd().Split('\n');
        lines.Last().TrimEnd().Should().Be("Plugin: \"LoudMax\" -ceiling -1.0");
    }

    [Fact]
    public void Inlines_scaled_filters_when_intensity_below_one()
    {
        var output = new CompetitiveProfile().Generate(new ProfileInput(AudioMode.Competitive)
        {
            Intensity = 0.5,
        });
        output.Should().NotContain(@"Include: warzone\fps-curves\moderate.txt");
        output.Should().Contain("Filter: ON LS Fc 250 Hz Gain -3.0 dB"); // half of -6
    }

    [Fact]
    public Task Snapshot_HD600_GC7_Moderate_FullIntensity()
    {
        var input = new ProfileInput(AudioMode.Competitive)
        {
            HeadphoneCorrection = new HeadphoneCorrection("HD600"),
            DacEndpoint = new DacEndpoint("Speakers Sound Blaster GC7 Game"),
        };
        var output = new CompetitiveProfile().Generate(input);
        return Verifier.Verify(output);
    }
}
```

The verified-snapshot file will be auto-created on first run (named `Snapshot_HD600_GC7_Moderate_FullIntensity.received.txt`). After eyeballing the file, rename it from `.received.txt` to `.verified.txt` to lock it in.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~CompetitiveProfileTests"`
Expected: build error.

- [ ] **Step 3: Implement `CompetitiveProfile`**

```csharp
using System.Globalization;
using System.Text;
using WarzoneEQ.ConfigGenerator.Channels;
using WarzoneEQ.ConfigGenerator.Filters;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.ConfigGenerator.Plugins;

namespace WarzoneEQ.ConfigGenerator.Profiles;

public sealed class CompetitiveProfile : IProfileGenerator
{
    public string Generate(ProfileInput input)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# warzone\\current.txt — Competitive mode");

        if (input.DacEndpoint.DeviceDirective is { } directive)
            sb.AppendLine(directive);

        sb.AppendLine();
        sb.AppendLine("Stage: pre-mix");

        EmitStage(sb, new ChannelStage(
            new[] { Channel.L, Channel.R },
            filters: new[] { Filter.HighPass(80) }));

        EmitStage(sb, new ChannelStage(
            new[] { Channel.FL, Channel.FR },
            preampDb: -3));

        EmitStage(sb, new ChannelStage(
            new[] { Channel.FC },
            preampDb: -6,
            plugins: new Plugin[]
            {
                TdrNova.SpectralDucker(thresholdDb: -28, ratio: 4, freqLow: 200, freqHigh: 5000),
            }));

        EmitStage(sb, new ChannelStage(
            new[] { Channel.BL, Channel.BR, Channel.SL, Channel.SR },
            preampDb: 2,
            plugins: new Plugin[]
            {
                TdrNova.TransientShaper(freqHz: 3000, gainDb: 5, q: 1.5),
            }));

        EmitStage(sb, new ChannelStage(
            new[] { Channel.LFE },
            preampDb: -12));

        sb.AppendLine();
        sb.AppendLine("Stage: post-mix");
        sb.AppendLine();

        sb.AppendLine($"Include: {input.HrirIncludePath}");
        if (input.HeadphoneCorrection.IncludePath is { } hcPath)
            sb.AppendLine($"Include: {hcPath}");

        EmitFpsCurve(sb, input);

        if (input.EnableFootstepCompressor)
            sb.AppendLine(ReaXcomp.UpwardCompressor(
                bandIndex: 1, freqLowHz: 2000, freqHighHz: 4500,
                thresholdDb: -42, ratio: "1:2", attackMs: 5, releaseMs: 80).ToConfigLine());

        sb.AppendLine(LoudMax.Limiter(-1.0).ToConfigLine());

        return sb.ToString();
    }

    private static void EmitStage(StringBuilder sb, ChannelStage stage)
    {
        sb.AppendLine();
        foreach (var line in stage.ToConfigLines()) sb.AppendLine(line);
    }

    private static void EmitFpsCurve(StringBuilder sb, ProfileInput input)
    {
        if (Math.Abs(input.Intensity - 1.0) < 0.001)
        {
            var slug = input.FpsCurve.ToString().ToLowerInvariant();
            sb.AppendLine($@"Include: warzone\fps-curves\{slug}.txt");
            return;
        }
        sb.AppendLine($"# FPS curve {input.FpsCurve} at {input.Intensity:P0} intensity (inlined)");
        foreach (var f in Intensity.Scale(FpsCurves.Get(input.FpsCurve), input.Intensity))
            sb.AppendLine(f.ToConfigLine());
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~CompetitiveProfileTests"`
Expected: 6 unit tests pass. Snapshot test will FAIL with a `.received.txt` file written next to the test.

- [ ] **Step 5: Inspect and accept the snapshot**

Open `tests/WarzoneEQ.ConfigGenerator.Tests/Snapshots/CompetitiveProfileTests.Snapshot_HD600_GC7_Moderate_FullIntensity.received.txt`. Verify the output looks like the example in spec §5.2. If correct, rename to `.verified.txt`:

```powershell
Rename-Item "tests/WarzoneEQ.ConfigGenerator.Tests/Snapshots/CompetitiveProfileTests.Snapshot_HD600_GC7_Moderate_FullIntensity.received.txt" "CompetitiveProfileTests.Snapshot_HD600_GC7_Moderate_FullIntensity.verified.txt"
```

Re-run: `dotnet test --filter "FullyQualifiedName~Snapshot_HD600"`
Expected: `Passed: 1`.

- [ ] **Step 6: Commit**

```powershell
git add src/WarzoneEQ.ConfigGenerator/Profiles/CompetitiveProfile.cs tests/WarzoneEQ.ConfigGenerator.Tests/Profiles/CompetitiveProfileTests.cs tests/WarzoneEQ.ConfigGenerator.Tests/Snapshots/CompetitiveProfileTests.Snapshot_HD600_GC7_Moderate_FullIntensity.verified.txt
git commit -m "feat: CompetitiveProfile (pre-mix per-channel + post-mix HRIR/correction/curve/comp/limiter)"
```

---

## Task 15: `CinematicProfile` (full chain)

Same chain as Competitive plus:
1. Polyverse Wider on `BL BR SL SR` (mono-safe widener, width 140)
2. Linear-phase mode on the transient shaper if `input.EnableLinearPhase` is true
3. Adaptive-loudness JSFX line before the limiter if `input.EnableAdaptiveLoudness` is true

The adaptive-loudness JSFX is a custom file; from the spec it lives at `warzone\jsfx\adaptive-loudness.jsfx`. EQ APO's syntax for JSFX is `Plugin: 'warzone\jsfx\adaptive-loudness.jsfx'` (single-quoted file path).

**Files:**
- Create: `src/WarzoneEQ.ConfigGenerator/Profiles/CinematicProfile.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/Profiles/CinematicProfileTests.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/Snapshots/CinematicProfileTests.Snapshot_HD600_GC7_Aggressive_AllToggles.verified.txt`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using VerifyXunit;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.ConfigGenerator.Profiles;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests.Profiles;

[UsesVerify]
public class CinematicProfileTests
{
    [Fact]
    public void Includes_Polyverse_Wider_on_sides_and_rear()
    {
        var output = new CinematicProfile().Generate(new ProfileInput(AudioMode.Cinematic)
        {
            EnablePolyverseWider = true,
        });
        // Find the BL BR SL SR stage and verify the plugin line appears within it
        output.Should().MatchRegex(@"Channel: BL BR SL SR[\s\S]*?Plugin: ""Polyverse Wider""");
    }

    [Fact]
    public void Linear_phase_flag_appends_mode_to_transient_shaper()
    {
        var output = new CinematicProfile().Generate(new ProfileInput(AudioMode.Cinematic)
        {
            EnableLinearPhase = true,
        });
        output.Should().Contain("Plugin: \"TDR Nova\" -bandB-freq 3000 -bandB-gain +5.0 -bandB-Q 1.5 -mode linear-phase");
    }

    [Fact]
    public void Adaptive_loudness_JSFX_included_when_enabled()
    {
        var output = new CinematicProfile().Generate(new ProfileInput(AudioMode.Cinematic)
        {
            EnableAdaptiveLoudness = true,
        });
        output.Should().Contain(@"Plugin: 'warzone\jsfx\adaptive-loudness.jsfx'");
    }

    [Fact]
    public Task Snapshot_HD600_GC7_Aggressive_AllToggles()
    {
        var input = new ProfileInput(AudioMode.Cinematic)
        {
            FpsCurve = FpsCurveName.Aggressive,
            HeadphoneCorrection = new HeadphoneCorrection("HD600"),
            DacEndpoint = new DacEndpoint("Speakers Sound Blaster GC7 Game"),
            EnableLinearPhase = true,
            EnableAdaptiveLoudness = true,
            EnablePolyverseWider = true,
        };
        var output = new CinematicProfile().Generate(input);
        return Verifier.Verify(output);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~CinematicProfileTests"`
Expected: build error.

- [ ] **Step 3: Implement `CinematicProfile`**

```csharp
using System.Globalization;
using System.Text;
using WarzoneEQ.ConfigGenerator.Channels;
using WarzoneEQ.ConfigGenerator.Filters;
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.ConfigGenerator.Plugins;

namespace WarzoneEQ.ConfigGenerator.Profiles;

public sealed class CinematicProfile : IProfileGenerator
{
    public string Generate(ProfileInput input)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# warzone\\current.txt — Cinematic mode (full chain)");

        if (input.DacEndpoint.DeviceDirective is { } directive)
            sb.AppendLine(directive);

        sb.AppendLine();
        sb.AppendLine("Stage: pre-mix");

        EmitStage(sb, new ChannelStage(
            new[] { Channel.L, Channel.R },
            filters: new[] { Filter.HighPass(80) }));

        EmitStage(sb, new ChannelStage(
            new[] { Channel.FL, Channel.FR },
            preampDb: -3));

        EmitStage(sb, new ChannelStage(
            new[] { Channel.FC },
            preampDb: -6,
            plugins: new Plugin[]
            {
                TdrNova.SpectralDucker(thresholdDb: -28, ratio: 4, freqLow: 200, freqHigh: 5000),
            }));

        var rearPlugins = new List<Plugin>
        {
            BuildTransientShaper(input),
        };
        if (input.EnablePolyverseWider)
            rearPlugins.Add(PolyverseWider.Width(140));

        EmitStage(sb, new ChannelStage(
            new[] { Channel.BL, Channel.BR, Channel.SL, Channel.SR },
            preampDb: 2,
            plugins: rearPlugins));

        EmitStage(sb, new ChannelStage(
            new[] { Channel.LFE },
            preampDb: -12));

        sb.AppendLine();
        sb.AppendLine("Stage: post-mix");
        sb.AppendLine();

        sb.AppendLine($"Include: {input.HrirIncludePath}");
        if (input.HeadphoneCorrection.IncludePath is { } hcPath)
            sb.AppendLine($"Include: {hcPath}");

        EmitFpsCurve(sb, input);

        if (input.EnableAdaptiveLoudness)
            sb.AppendLine(@"Plugin: 'warzone\jsfx\adaptive-loudness.jsfx'");

        if (input.EnableFootstepCompressor)
            sb.AppendLine(ReaXcomp.UpwardCompressor(
                bandIndex: 1, freqLowHz: 2000, freqHighHz: 4500,
                thresholdDb: -42, ratio: "1:2", attackMs: 5, releaseMs: 80).ToConfigLine());

        sb.AppendLine(LoudMax.Limiter(-1.0).ToConfigLine());

        return sb.ToString();
    }

    private static Plugin BuildTransientShaper(ProfileInput input)
    {
        var shaper = TdrNova.TransientShaper(freqHz: 3000, gainDb: 5, q: 1.5);
        return input.EnableLinearPhase ? shaper.WithLinearPhase() : shaper;
    }

    private static void EmitStage(StringBuilder sb, ChannelStage stage)
    {
        sb.AppendLine();
        foreach (var line in stage.ToConfigLines()) sb.AppendLine(line);
    }

    private static void EmitFpsCurve(StringBuilder sb, ProfileInput input)
    {
        if (Math.Abs(input.Intensity - 1.0) < 0.001)
        {
            var slug = input.FpsCurve.ToString().ToLowerInvariant();
            sb.AppendLine($@"Include: warzone\fps-curves\{slug}.txt");
            return;
        }
        sb.AppendLine($"# FPS curve {input.FpsCurve} at {input.Intensity:P0} intensity (inlined)");
        foreach (var f in Intensity.Scale(FpsCurves.Get(input.FpsCurve), input.Intensity))
            sb.AppendLine(f.ToConfigLine());
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~CinematicProfileTests"`
Expected: 3 unit tests pass; snapshot test fails with `.received.txt`.

- [ ] **Step 5: Accept the snapshot**

Inspect `tests/WarzoneEQ.ConfigGenerator.Tests/Snapshots/CinematicProfileTests.Snapshot_HD600_GC7_Aggressive_AllToggles.received.txt`. If correct:
```powershell
Rename-Item "tests/WarzoneEQ.ConfigGenerator.Tests/Snapshots/CinematicProfileTests.Snapshot_HD600_GC7_Aggressive_AllToggles.received.txt" "CinematicProfileTests.Snapshot_HD600_GC7_Aggressive_AllToggles.verified.txt"
```

Re-run: `dotnet test --filter "FullyQualifiedName~Snapshot_HD600_GC7_Aggressive"`
Expected: `Passed: 1`.

- [ ] **Step 6: Commit**

```powershell
git add src/WarzoneEQ.ConfigGenerator/Profiles/CinematicProfile.cs tests/WarzoneEQ.ConfigGenerator.Tests/Profiles/CinematicProfileTests.cs tests/WarzoneEQ.ConfigGenerator.Tests/Snapshots/CinematicProfileTests.Snapshot_HD600_GC7_Aggressive_AllToggles.verified.txt
git commit -m "feat: CinematicProfile (full chain with widener + linear-phase + adaptive loudness)"
```

---

## Task 16: `ConfigGenerator` public facade

The single entry point users of the library call.

**Files:**
- Create: `src/WarzoneEQ.ConfigGenerator/ConfigGenerator.cs`
- Create: `tests/WarzoneEQ.ConfigGenerator.Tests/ConfigGeneratorTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using FluentAssertions;
using WarzoneEQ.ConfigGenerator.Models;
using Xunit;

namespace WarzoneEQ.ConfigGenerator.Tests;

public class ConfigGeneratorTests
{
    [Fact]
    public void Routes_Competitive_to_CompetitiveProfile()
    {
        var output = ConfigGenerator.Generate(new ProfileInput(AudioMode.Competitive));
        output.Should().Contain("Competitive mode");
    }

    [Fact]
    public void Routes_Cinematic_to_CinematicProfile()
    {
        var output = ConfigGenerator.Generate(new ProfileInput(AudioMode.Cinematic));
        output.Should().Contain("Cinematic mode");
    }

    [Fact]
    public void Routes_Bypass_to_BypassProfile()
    {
        var output = ConfigGenerator.Generate(new ProfileInput(AudioMode.Bypass));
        output.Should().Contain("Bypass mode");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ConfigGeneratorTests"`
Expected: build error.

- [ ] **Step 3: Implement `ConfigGenerator`**

```csharp
using WarzoneEQ.ConfigGenerator.Models;
using WarzoneEQ.ConfigGenerator.Profiles;

namespace WarzoneEQ.ConfigGenerator;

public static class ConfigGenerator
{
    public static string Generate(ProfileInput input) => input.Mode switch
    {
        AudioMode.Competitive => new CompetitiveProfile().Generate(input),
        AudioMode.Cinematic   => new CinematicProfile().Generate(input),
        AudioMode.Bypass      => new BypassProfile().Generate(input),
        _ => throw new ArgumentOutOfRangeException(nameof(input.Mode)),
    };
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ConfigGeneratorTests"`
Expected: `Passed: 3`.

- [ ] **Step 5: Run the full test suite to verify nothing else broke**

Run: `dotnet test`
Expected: all tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/WarzoneEQ.ConfigGenerator/ConfigGenerator.cs tests/WarzoneEQ.ConfigGenerator.Tests/ConfigGeneratorTests.cs
git commit -m "feat: ConfigGenerator public facade (routes by AudioMode)"
```

---

## Task 17: CLI demo project

`WarzoneEQ.Cli` so we can eyeball generated configs from the command line. Uses `System.CommandLine` (Microsoft's modern CLI library).

**Files:**
- Create: `src/WarzoneEQ.Cli/WarzoneEQ.Cli.csproj`
- Create: `src/WarzoneEQ.Cli/Program.cs`

- [ ] **Step 1: Create the project and reference**

```powershell
dotnet new console -n WarzoneEQ.Cli -o src/WarzoneEQ.Cli -f net8.0
dotnet sln add src/WarzoneEQ.Cli/WarzoneEQ.Cli.csproj
dotnet add src/WarzoneEQ.Cli/WarzoneEQ.Cli.csproj reference src/WarzoneEQ.ConfigGenerator/WarzoneEQ.ConfigGenerator.csproj
dotnet add src/WarzoneEQ.Cli/WarzoneEQ.Cli.csproj package System.CommandLine --version 2.0.0-beta4.22272.1
```

(Note: System.CommandLine is still beta but mature; this is the version Microsoft itself ships with `dotnet` tooling.)

- [ ] **Step 2: Delete the `Program.cs` boilerplate**

Remove the default `src/WarzoneEQ.Cli/Program.cs` content.

- [ ] **Step 3: Implement the CLI**

`src/WarzoneEQ.Cli/Program.cs`:
```csharp
using System.CommandLine;
using WarzoneEQ.ConfigGenerator;
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

    Console.Write(ConfigGenerator.Generate(input));
});

return await root.InvokeAsync(args);
```

- [ ] **Step 4: Build and run the CLI manually**

```powershell
dotnet build src/WarzoneEQ.Cli/WarzoneEQ.Cli.csproj
dotnet run --project src/WarzoneEQ.Cli -- --mode Competitive --headphone HD600 --dac "Speakers Sound Blaster GC7 Game" --curve Moderate
```

Expected: a multi-line EQ APO config printed to stdout that matches the snapshot from Task 14.

- [ ] **Step 5: Commit**

```powershell
git add src/WarzoneEQ.Cli/ WarzoneEQ.sln
git commit -m "feat: WarzoneEQ.Cli — emit generated config to stdout"
```

---

## Task 18: Final cleanup + README stub

- [ ] **Step 1: Run the full test suite one more time**

Run: `dotnet test`
Expected: every test passes. Note the total count for the README.

- [ ] **Step 2: Add a brief README**

Create `README.md` at repo root:
```markdown
# Warzone EQ

Ultimate compliant footstep audio app for Call of Duty Warzone. See [the design spec](docs/superpowers/specs/2026-05-14-warzone-eq-design.md).

## Sub-plan progress

- [x] **#1 EQ APO Config Generator** — `src/WarzoneEQ.ConfigGenerator/`, `src/WarzoneEQ.Cli/`
- [ ] #2 Audio Device Detection
- [ ] #3 EQ APO + Windows Audio Integration
- [ ] #4 Control App GUI
- [ ] #5 First-Run Wizard + Auto-Tune
- [ ] #6 Sharing & Personalized HRTF Tiers
- [ ] #7 Installer + Clean Uninstall

## Quick test of the config generator

```powershell
dotnet run --project src/WarzoneEQ.Cli -- `
  --mode Cinematic `
  --curve Aggressive `
  --headphone HD600 `
  --dac "Speakers Sound Blaster GC7 Game" `
  --linear-phase `
  --adaptive-loudness `
  --wider
```
```

- [ ] **Step 3: Commit**

```powershell
git add README.md
git commit -m "docs: README with sub-plan progress and CLI quickstart"
```

---

## Self-review checklist (run before handing off)

- [ ] Every spec requirement in §5.2 Competitive chain is implemented (Device, HP safety, FL/FR preamp, FC ducker, BL/BR/SL/SR shaper, LFE cut, post-mix HRIR/correction/curve/comp/limiter) — Tasks 13–14.
- [ ] Every spec requirement in §5.3 Cinematic adds (Polyverse Wider, linear-phase, adaptive loudness) is implemented — Task 15.
- [ ] §7 FPS curve numeric values match the spec — Task 4.
- [ ] §7.3 intensity scaling math matches the spec — Task 5.
- [ ] No placeholders. Every code block is complete and runnable.
- [ ] Every task ends with a commit.
- [ ] Type names referenced in later tasks match earlier definitions (e.g., `TdrNova.TransientShaper` and `TdrNova.SpectralDucker` are used consistently across Tasks 7, 14, 15).
- [ ] Snapshot files (`*.verified.txt`) are committed; `*.received.txt` is in `.gitignore`.

## Not covered by this sub-plan (handled in later sub-plans)

- Writing the generated config to disk (sub-plan #3)
- Triggering EQ APO hot-reload (sub-plan #3)
- AutoEQ database extraction (sub-plan #2)
- VID/PID detection that decides which `DacEndpoint` to pass in (sub-plan #2)
- Auto-tune calibration that decides `Intensity` per-band (sub-plan #5)
- Windows Loudness EQ toggle (sub-plan #3)

These are real spec requirements — they live in their own focused sub-plans where they have proper test coverage and clear scope.
