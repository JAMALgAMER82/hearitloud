# Warzone EQ — Ultimate Compliant Footstep Audio App

**Status:** Design approved, ready for implementation planning
**Date:** 2026-05-14
**Author:** George (with Claude)
**Working title:** Warzone EQ (final name TBD)

---

## 1. Summary

A single Windows app that gives a Call of Duty Warzone player the best possible footstep audibility and directional precision available in 2026, **without any anti-cheat exposure**. Everything ships in one installer. Everything is automated — auto-detect headphones, auto-detect DAC, auto-tune to the user's hearing in 60 seconds, auto-switch modes when Warzone gains focus, auto-load per-headphone correction on plug-in. The user never has to know what an EQ band is unless they want to.

The pitch in one sentence:

> A Windows tray app that auto-detects your headphones and DAC, runs a 60-second hearing test the first time you launch it, then generates and hot-reloads an Equalizer APO config personalized to your ears and your hardware — with one-click mode switching, sharing, and zero anti-cheat exposure.

## 2. Goals & non-goals

### 2.1 Goals
- **Beat ArtTuneKit on every measurable axis** (DSP sophistication, UX, headphone coverage, personalization, simplicity of install).
- **One app, one installer, one running tray icon.** No "now install this other thing." All bundled DSP, HRIR, headphone DB, and config tooling live inside our package.
- **Fully automatic by default.** A non-technical user can launch it once, answer five short questions, and never touch a setting again. Power users can dive as deep as JSFX editing.
- **Zero anti-cheat risk** for the user or their friends. Every component is verified safe with Ricochet (as of BO7 Warzone Season 03, May 2026).
- **Multi-endpoint DAC native** — first-class support for Sound Blaster GC7 / G8 and similar dual-output gaming DACs, so Warzone audio and Spotify/Discord stay completely isolated.
- **Per-user calibration** — each Windows user on a PC gets their own auto-tuned profile based on their hearing, headphones, and DAC.
- **Shareable** — `.warzeq` files let a player give their tuning to a friend in one click.

### 2.2 Non-goals (v1)
- Any visual overlay on top of the Warzone window (ESP-flavored — dropped for safety).
- Real-time ML / AI classification of game audio (directional inference territory — dropped).
- Bluetooth headphone support as a first-class path (latency too inconsistent in 2026 — deferred to v1.1).
- Console support (PS5/Xbox cannot run system APOs — out of scope).
- Cross-platform (Windows only).
- Code signing for the installer (deferred to v1.1 once download volume justifies the cost).
- Cloud sync of profiles (deferred to v1.1).
- Auto-update of the app itself (manual "Check for updates" in v1).

## 3. Anti-cheat compliance principles

These are the **hard engineering rules**. Any change to v1 or v1.1+ that violates them is rejected.

1. **Never touch the Call of Duty process.** No DLL injection, no DXGI hook, no memory read/write, no file modification.
2. **Never render an overlay over the game window.** All visualizers live inside the control app's own top-level WPF window.
3. **Never run AI source separation on the game audio in real time.** Even "passive" footstep classifiers move toward directional inference and ESP territory. Off-line tuning aids (iZotope RX) are fine; real-time AI on game audio is not.
4. **Stay user-mode + system mixer bus only.** All DSP runs inside Equalizer APO's APO host, which Windows AudioEngine loads at the audio endpoint level — Ricochet's kernel monitor does not inspect it.
5. **Never bundle a kernel driver beyond what EQ APO 1.4.2 already ships.** No custom WDM drivers. No VB-Cable. No VoiceMeeter. The risk surface stays at zero net-new kernel code.
6. **Never auto-update Equalizer APO.** Its existing signed kernel module has years of clean Ricochet history. Pin to 1.4.2.
7. **No telemetry without opt-in.** Default off. If enabled, anonymized only.

Compliance is documented and verified by the **Audio Chain Health Check** feature (Section 11).

## 4. High-level architecture

```
┌─────────────────────────────────────────────────────┐
│ Warzone (7.1 Surround ON, Audio Mix: Bass Cut,      │
│  Music=0, Effects=100, Enhanced Headphone Mode OFF) │
└────────────────────┬────────────────────────────────┘
                     │ Windows AudioEngine
                     ▼
┌─────────────────────────────────────────────────────┐
│ Equalizer APO 1.4.2 chain (system APO)              │
│  Scoped to the Game endpoint only via Device:       │
│  1. Per-channel pre-gain (7.1 still discrete)       │
│  2. TDR Nova #1 — transient shaper (sides+rear)     │
│  3. TDR Nova #2 — spectral ducker (FC, your gun)    │
│  4. Polyverse Wider — sides/rear decorrelation      │
│  5. HeSuVi HRIR convolution → stereo fold-down      │
│  6. AutoEQ per-headphone correction                 │
│  7. TDR Nova #3 (linear-phase) — FPS target curve   │
│  8. Adaptive-loudness JSFX (envelope-aware scaling) │
│  9. ReaXcomp — footstep-band upward compression     │
│  10. LoudMax — safety limiter                       │
└────────────────────┬────────────────────────────────┘
                     │
                     ├─ Headphones (Game endpoint)
                     │
                     └─ Voice endpoint: completely untouched
                        Windows default for Spotify, Discord,
                        browser audio. No chain applied.

   ┌──────────────────────────────────────────────────────┐
   │ Control App (C# WPF, .NET 8, system tray)            │
   │  • Headphone auto-detect (WMI USB + WinRT Bluetooth) │
   │  • DAC auto-detect (multi-endpoint hardware DB)      │
   │  • 60-second first-run auto-tune                     │
   │  • Mode toggle (Competitive / Cinematic)             │
   │  • Foreground-window auto-switch (Warzone in focus)  │
   │  • Generates EQ APO config.txt → hot-reload          │
   │  • Audio chain health check                          │
   │  • Spectrum/VU meter (inside app window only)        │
   │  • Sharing via .warzeq files                         │
   │  • Clean uninstall tool                              │
   └──────────────────────────────────────────────────────┘
```

Two operating modes share most stages:
- **Competitive** (latency-leanest): stages 1, 2, 3, 5 (light HRIR), 6, 7 (minimum-phase), 9, 10.
- **Cinematic** (full chain, ~10 ms more latency): all stages, stage 7 in linear-phase mode, stage 4 active, stage 8 active.

## 5. Audio chain in detail

### 5.1 File layout under Equalizer APO

```
C:\Program Files\EqualizerAPO\config\
├── config.txt                  # EQ APO master — we add one line: Include: warzone\current.txt
└── warzone\
    ├── current.txt             # generated & hot-reloaded by our app
    ├── profiles\
    │   ├── competitive.txt
    │   └── cinematic.txt
    ├── headphone-correction\
    │   ├── HD600.txt           # AutoEQ files, lazy-extracted on demand
    │   ├── ArctisNovaPro.txt
    │   └── ... (2,500+ AutoEQ models)
    ├── hrir\
    │   └── hesuvi-active.wav   # symlinked/copied per user choice
    ├── jsfx\
    │   └── adaptive-loudness.jsfx
    └── fps-curves\
        ├── minimalist.txt
        ├── moderate.txt
        └── aggressive.txt
```

EQ APO watches `config.txt` (and any `Include:`'d files) for changes. Our app writes to `warzone\current.txt`; EQ APO hot-reloads with ~5–20 ms of audio silence, no popping.

### 5.2 Competitive mode generated config (example)

```
# warzone\current.txt — generated when user picks Competitive on GC7
Device: Speakers Sound Blaster GC7 Game

Channel: L R
Filter: HP 80Hz                                    # safety high-pass

Stage: pre-mix

Channel: FL FR
Preamp: -3 dB

Channel: FC
Preamp: -6 dB
Plugin: "TDR Nova" -bandA-thresh -28 -bandA-ratio 4 \
                   -bandA-fLow 200 -bandA-fHigh 5000   # spectral duck on your gun

Channel: BL BR SL SR
Preamp: +2 dB
Plugin: "TDR Nova" -bandB-gain +5 -bandB-freq 3000 -bandB-Q 1.5  # footstep transient boost

Channel: LFE
Preamp: -12 dB                                     # explosions

Stage: post-mix

Include: warzone\hrir\hesuvi-active.wav            # default sbx33.wav
Include: warzone\headphone-correction\HD600.txt    # auto-loaded per detected headphone
Include: warzone\fps-curves\moderate.txt           # default 6-band

Plugin: "ReaXcomp" -band 1 -freq-low 2000 -freq-high 4500 \
                   -threshold -42 -ratio 1:2 -attack 5 -release 80   # upward compressor

Plugin: "LoudMax" -ceiling -1.0                    # safety limiter
```

### 5.3 Cinematic mode additions

Cinematic generates the same skeleton plus:
- **Stage 4: Polyverse Wider** on `BL BR SL SR` (mono-safe widener)
- **Stage 7 in linear-phase mode**: TDR Nova `-mode linear-phase` (adds ~10 ms, smears nothing)
- **Stage 8: adaptive-loudness JSFX**: reads 200 ms RMS, scales post-FPS-curve preamp 0.75× → 1.15× based on envelope

### 5.4 Power-user enhancements (toggleable per profile)

| Feature | Default | What it does |
|---|---|---|
| Footstep-band upward compressor | ON (both modes) | Quiet footsteps amplified, loud sounds untouched. ReaXcomp single-band sidechain. |
| Linear-phase FPS curve | ON in Cinematic, OFF in Competitive | Preserves transient sharpness at +10 ms latency. |
| Adaptive loudness | OFF | Envelope-aware scaling — more boost when game is quiet. |
| Mid/Side mode | OFF | Applies EQ to side component only (advanced). |
| **Windows Loudness Equalization (LEQ) on Game endpoint** | **ON, release time = SHORT (2)** | **Native Windows APO (`WMALFXGFXDSP.dll`) — ArtIsWar staple. Constant audibility lift on quiet transients like footsteps. SHORT release time = fastest recovery = most aggressive footstep boost.** |

### 5.5 Windows Loudness Equalization integration

ArtIsWar's `LEQControlPanel` toggles this same setting — it's a Microsoft-shipped enhancement APO that sits in the LFX/SFX chain *before* Equalizer APO. Anti-cheat irrelevant (Microsoft's own code). When enabled on the Game endpoint with the release-time slider at its leftmost position, it acts as a transparent multiband compressor that raises quiet sounds toward the loudness target without crushing peaks.

Our app **enables this automatically** when:
1. A multi-endpoint DAC is detected and we're routing to the Game endpoint, OR
2. The user is on a single endpoint and accepts the wizard's "Enable Windows Loudness EQ?" prompt (Step 2 of wizard).

**Implementation:**

The Loudness Equalization state and release time live in the audio endpoint's FX property store under the registry key:

```
HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render\
  {endpoint-guid}\FxProperties\
  {fc52a749-4be9-4510-896e-966ba6525980},3   ← LEQ enabled flag (DWORD 1/0)
  {fc52a749-4be9-4510-896e-966ba6525980},9   ← LEQ release time (DWORD 2..7)
```

We set:
- Flag = `1` (enabled)
- Release time = `2` (SHORT — slider all the way left)

Done via `IPropertyStore` on the endpoint, same API surface used by every Windows audio settings utility. Verifiable manually by user in *Sound Control Panel → endpoint → Properties → Enhancements → Loudness Equalization*.

The health check (Section 13.1) verifies both values are still as we set them — Windows occasionally resets them on driver updates, and we re-apply if reset.

**User-facing toggle:** Advanced tab → "Windows Loudness EQ on Game endpoint" (checkbox) and a release-time selector (2 = Short / 4 = Medium / 7 = Long). Default 2 = SHORT.

## 6. Headphone database + auto-detection

### 6.1 Bundled databases

1. **AutoEQ snapshot** — `autoeq-v1.zip`, bundled in installer (~40 MB compressed). Source: `github.com/jaakkopasanen/AutoEq` `results/` tree at build time. 2,500+ models. MIT licensed, redistributable.
2. **VID/PID overlay** — `vidpid-overlay.json`, hand-curated, lives in our repo. Maps USB device IDs to AutoEQ folder slugs.
3. **Squiglink PEQ paste** — Advanced tab text field for power users to paste measurer-specific PEQ blocks (Crinacle, Kuulokenurkka, Super* Review).

```json
{
  "headphones": {
    "VID_1532&PID_0517": { "model": "Razer BlackShark V2 Pro", "autoeq_slug": "razer/BlackShark_V2_Pro" },
    "VID_1038&PID_12AD": { "model": "SteelSeries Arctis Nova Pro Wireless", "autoeq_slug": "steelseries/Arctis_Nova_Pro_Wireless" },
    "VID_0BDA&PID_4014": { "model": "HyperX Cloud III Wireless", "autoeq_slug": "hyperx/Cloud_III_Wireless" }
  },
  "multi_endpoint_dacs": {
    "VID_041E&PID_3260": {
      "model": "Creative Sound Blaster GC7",
      "endpoints": { "game": "Speakers (Sound Blaster GC7 Game)", "voice": "Speakers (Sound Blaster GC7 Chat)" },
      "default_route": "game"
    },
    "VID_041E&PID_3270": {
      "model": "Creative Sound Blaster G8",
      "endpoints": { "game": "Speakers (Sound Blaster G8 Game)", "voice": "Speakers (Sound Blaster G8 Chat)" },
      "default_route": "game"
    },
    "VID_041E&PID_3251": { "model": "Sound Blaster X3", "...": "..." },
    "VID_041E&PID_3253": { "model": "Sound Blaster X4", "...": "..." },
    "VID_041E&PID_3265": { "model": "Sound Blaster X5", "...": "..." },
    "VID_9886&PID_002C": { "model": "Astro MixAmp Pro TR", "...": "..." },
    "VID_1395&PID_011B": { "model": "Sennheiser GSX 1200 Pro", "...": "..." },
    "VID_1038&PID_12CB": { "model": "SteelSeries GameDAC Gen 2", "...": "..." }
  }
}
```

### 6.2 Detection flow

```
                  WMI Win32_DeviceChangeEvent fires
                                  │
                                  ▼
                  Query Win32_PnPEntity / Win32_USBHub
                                  │
        ┌─────────────────────────┼─────────────────────────┐
        ▼                         ▼                         ▼
   USB audio                Bluetooth audio          3.5 mm analog
   (Class 0x01)             (BTHENUM)                (no identifier)
        │                         │                         │
        ▼                         ▼                         ▼
   VID/PID lookup            BluetoothDevice          Manual picker
   in vidpid-overlay         .Name via WinRT          (AutoEQ
        │                    Fuzzy match              autocomplete,
        │                    Levenshtein ≤3           2,500 models)
        └─────────────────────────┴─────────────────────────┘
                                  │
                                  ▼
                Found → extract AutoEQ ParametricEQ.txt to
                warzone\headphone-correction\<model>.txt
                                  │
                                  ▼
                Rewrite current.txt to Include: it
                                  │
                                  ▼
                EQ APO hot-reloads
                                  │
                                  ▼
                Toast: "HD 600 detected — correction applied"
```

### 6.3 Implementation specifics

- **WMI**: `System.Management 10.0.x` NuGet. Watch `Win32_DeviceChangeEvent` via `ManagementEventWatcher`. Microsoft.Management.Infrastructure is archived — do not use.
- **Bluetooth**: CsWinRT with `Microsoft.Windows.SDK.NET.Ref` matching target Windows SDK (`net8.0-windows10.0.22621.0`). Use `DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true))` — BR/EDR audio headphones don't appear in `BluetoothLEDevice` enumeration.
- **Per-user override**: if user manually picks a different model after auto-detection, that choice wins on subsequent connects and is keyed by VID/PID + Windows username.

## 7. Custom FPS target curves

### 7.1 Frequency targets (Warzone-specific)

| Sound type | Frequency range | Action |
|---|---|---|
| Footstep impact transient (boot click) | 1.5 – 4 kHz | **Boost** |
| Footstep Foley body (weight rumble) | 100 – 300 Hz | Slight cut (masks more than helps) |
| Suppressed gunfire (killer masker) | 200 Hz – 5 kHz, peak 800–1500 Hz | Cut |
| Muzzle crack | 5 – 7 kHz | Cut (deafening, masks reloads) |
| Reload / equipment / gear | 4 – 8 kHz | Neutral / mild boost |
| Vehicle hum, wind, ambience | < 150 Hz | Cut hard |

### 7.2 Three shipped curves

**Minimalist (3 bands)** — gentle nudge for already-good headphones:
```
Filter: ON HP Fc 120 Hz
Filter: ON PK Fc 3000 Hz Gain +4 dB Q 1.2
Filter: ON HS Fc 8000 Hz Gain -2 dB
```

**Moderate (6 bands)** — Competitive-mode default:
```
Filter: ON LS Fc 250 Hz Gain -6 dB
Filter: ON PK Fc 800 Hz Gain -3 dB Q 4
Filter: ON PK Fc 2000 Hz Gain +3 dB Q 1.4
Filter: ON PK Fc 3500 Hz Gain +5 dB Q 1.8
Filter: ON PK Fc 5000 Hz Gain +2 dB Q 1.2
Filter: ON HS Fc 10000 Hz Gain -3 dB
```

**Aggressive (10 bands)** — ranked / tournament play:
```
Filter: ON HP Fc 80 Hz
Filter: ON PK Fc 180 Hz Gain -4 dB Q 2
Filter: ON PK Fc 500 Hz Gain -2 dB Q 3
Filter: ON PK Fc 1200 Hz Gain -3 dB Q 5     # suppressed-gunfire scoop
Filter: ON PK Fc 2800 Hz Gain +6 dB Q 2
Filter: ON PK Fc 4000 Hz Gain +4 dB Q 2
Filter: ON PK Fc 6000 Hz Gain -5 dB Q 4     # muzzle-crack scoop
Filter: ON HS Fc 7000 Hz Gain +1 dB
Filter: ON PK Fc 12000 Hz Gain -2 dB Q 1
Filter: ON LP Fc 16000 Hz
```

### 7.3 Intensity slider

Single 0–100% slider per curve, scales only gain values (not Q or Fc):
```
effective_gain = nominal_gain × (slider / 100)
```
Debounced 200 ms to avoid file-watcher thrashing.

## 8. Multi-endpoint DAC support

### 8.1 The flow (GC7 / G8 example)

EQ APO scopes to one endpoint via `Device:` directive. The chain is applied to the **Game endpoint** only. The **Chat/Voice endpoint** stays bit-perfect Windows default — Spotify, Discord, browser audio flow through it untouched.

```
# warzone\current.txt (GC7 example)
Device: Speakers Sound Blaster GC7 Game

Include: warzone\profiles\competitive.txt
```

User configuration (the app sets these automatically where possible):
- **Warzone in-game**: Settings → Audio → Audio Device → "Sound Blaster GC7 Game"
- **Windows default playback device**: "Sound Blaster GC7 Chat"
- Set via `IPolicyConfig` (same API SoundSwitch uses, fully documented Win32, anti-cheat irrelevant)

### 8.2 Supported DACs at v1 ship

- Creative Sound Blaster GC7
- Creative Sound Blaster **G8** (added per final research pass)
- Creative Sound Blaster X3 / X4 / X5
- Astro MixAmp Pro TR
- Sennheiser GSX 1200 Pro
- SteelSeries GameDAC Gen 2
- SteelSeries Arctis Nova Pro Wireless (PC base station — dual-endpoint when on dongle)
- HyperX Cloud III S Wireless (when on PC dongle)
- Razer Kraken V4 Pro (when on PC dongle)

Long tail added by user contributions over time.

### 8.3 Single-endpoint fallback

When user has no dual-endpoint DAC, the chain applies to whichever endpoint is the Windows default. Voice separation falls back to Windows 11's per-app Volume Mixer routing (covered in onboarding).

## 9. Control app GUI

### 9.1 System tray

- Icon color reflects mode: green = Competitive, blue = Cinematic, grey = Bypass
- Single click → cycle modes
- Double click → open full window
- Right click → full menu (mode, open HIVE if installed, bypass, open install folder, health check, quit)
- Windows toasts on important events

### 9.2 Global hotkeys

Registered via Win32 `RegisterHotKey` (anti-cheat irrelevant):
- `Ctrl+Alt+1` → Competitive
- `Ctrl+Alt+2` → Cinematic
- `Ctrl+Alt+0` → Bypass
- `Ctrl+Alt+B` → momentary A/B compare (hold to bypass)

### 9.3 Foreground-window auto-switch

App polls `GetForegroundWindow` once per second. If `WARZONE.exe` becomes foreground → switch to Competitive. If it loses foreground for >30 s → switch to Cinematic (or user-designated "out-of-game" mode). Manual mode change suspends auto-switch until next app launch.

No game-process interaction — only reads which window has focus. Used by every alt-tab utility on Windows.

### 9.4 Full window — five tabs

```
┌──────────────────────────────────────────────────────────────────────┐
│ Warzone EQ — v1.0                                          [_][□][X] │
├────────────────┬─────────────────────────────────────────────────────┤
│  ● Status      │  Hardware: Sound Blaster GC7 (dual-endpoint mode)  │
│    Tuning      │    Game endpoint  → EQ chain ON (Competitive)      │
│    Headphones  │    Voice endpoint → bypassed (Windows default)     │
│    Advanced    │                                                     │
│    About       │  Headphones: Sennheiser HD 600 (auto-detected)      │
│                │  Chain: ✓ Loaded • CPU 0.4% • XRuns 0               │
│                │  Game detected: WARZONE.exe (foreground)            │
│                │  Embody HIVE: not installed  [Install]              │
│                │                                                     │
│                │  [▶ Health check]   [↻ Re-run auto-tune]            │
└────────────────┴─────────────────────────────────────────────────────┘
```

**Status tab** — at-a-glance health.
**Tuning tab** — FPS curve picker, intensity slider, A/B compare button, calibration panner (8-position footstep test signal), reference clip player, large in-app spectrum meter.
**Headphones tab** — auto-detection state, manual picker, AutoEQ response preview.
**Advanced tab** — custom curve editor, JSFX loader, M/S toggle, per-channel gain trims, **Squiglink PEQ paste**, **SOFA file import**, export `.warzeq`.
**About tab** — version, anti-cheat note, links to docs, Activision Security & Enforcement Policy excerpt.

### 9.5 In-app spectrum meter

WASAPI loopback on the playback endpoint → FFT (4096-point, Hann, 50% overlap) → rendered in WPF + SkiaSharp at 60 fps. Lives **only** inside the control app window. Same paradigm as VoiceMeeter's analyser or any DAW master meter. Never an overlay over the game.

## 10. Installer + first-run wizard

### 10.1 Installer (one .exe, ~85 MB)

NSIS-based, bundles everything in correct order:

```
WarzoneEQ-Setup-v1.0.exe
├── Equalizer APO 1.4.2 (silent install — only step requiring reboot)
├── HeSuVi 2.x HRIR bundle (sbx33.wav default, dts_hpx_oe_spac.wav,
│                            smyth_realiser.wav, +3 alternates)
├── ReaPlugs JS bundle (ReaXcomp, ReaJS)
├── TDR Nova 1.2.1
├── LoudMax v1.46
├── Polyverse Wider 2.0 (freeware redistribution per license)
├── AutoEQ snapshot (2,500+ headphones, ~40 MB compressed)
├── Our app (.NET 8 self-contained, ~30 MB)
├── 3 FPS curves + adaptive-loudness.jsfx
└── reference\range.flac (60 s tuning clip)
```

Install flow:
1. UAC prompt → accept (needed for EQ APO system APO registration)
2. Click Install
3. EQ APO installs silently and registers on all playback endpoints
4. Other components install silently
5. **Reboot required** by Windows for EQ APO; installer offers "Reboot now / Reboot later"
6. After reboot, app starts at login automatically and launches the first-run wizard

No code signing in v1. SmartScreen warning expected; install guide covers "More info → Run anyway." Reputation accrues with downloads. v1.1 may add code signing (~$250–$500/yr).

### 10.2 First-run wizard (≤ 2 minutes)

Five steps, all skippable.

1. **Welcome** — explains what the app does and the anti-cheat position. Includes Activision policy excerpt.
2. **Hardware** — shows detected DAC + headphones, asks user to confirm. Multi-endpoint DACs get an explicit "apply chain to Game endpoint only?" prompt.
3. **Auto-tune hearing** (60 s) — audiogram test (see Section 11).
4. **Footstep vs gunfire balance** (30 s) — 6-second reference clip, user picks "too loud / just right / too quiet" for each.
5. **Play style** (5 s) — three checkboxes (ranked focus, long sessions, uses mic). Sets sensible defaults.

If user picks "Skip all" at step 1, app boots into Competitive mode + Moderate curve at 100% intensity with auto-detected headphone correction. That's a fully usable v1 experience without any wizard interaction.

## 11. Auto-tuning algorithm

### 11.1 Step 3 — Audiogram-style hearing test

Plays sine tones at 8 frequencies: 250, 500, 1k, 2k, 3k, 4k, 6k, 10k Hz.
Each ramps from −60 dB upward at ~3 dB/sec.
User clicks "I can hear it" the moment each becomes audible.
Threshold per frequency is recorded as the user's **personal sensitivity curve**.

Stored at `%LOCALAPPDATA%\WarzoneEQ\profiles\<windows-username>.json`:

```json
{
  "user": "WIN_USER_NAME",
  "calibrated_at": "2026-05-14T18:42:00Z",
  "headphones": "Sennheiser HD 600",
  "dac": "Sound Blaster GC7",
  "hearing_thresholds_db": {
    "250":  -45, "500":  -48, "1000": -50, "2000": -52,
    "3000": -49, "4000": -46, "6000": -38, "10000": -28
  },
  "preferences": {
    "footstep_loudness": "just_right",
    "gunfire_loudness":  "just_right",
    "session_length": "long",
    "ranked_focus":  true,
    "uses_mic":      false
  },
  "computed_fps_curve_intensity_per_band": {
    "250":  0.30, "500":  0.40, "1000": 0.55,
    "2000": 0.70, "3000": 0.85, "4000": 0.95,
    "6000": 1.20, "10000": 1.50
  }
}
```

### 11.2 Per-band intensity mapping

```
band_intensity = clamp(0.3 + (avg_threshold - threshold_at_band) / 30, 0.3, 1.5)
```

A user with 6 kHz hearing loss gets +50% boost in that band automatically. A user with sensitive 4 kHz gets less boost to avoid fatigue.

### 11.3 Step 4 — Loudness preference

- "Gunshot too loud" → lower spectral-ducker threshold by 4 dB
- "Footstep too quiet" → lower upward-compressor threshold by 3 dB
- Inverse adjustments for "too quiet" / "too loud" the other direction

### 11.4 Step 5 — Play style

- Ranked focus → defaults to Competitive mode + Aggressive curve
- Long sessions → enables adaptive-loudness JSFX
- Uses mic → recommends EarTrumpet for per-app routing (one-time toast)

### 11.5 Re-runnable

Menu item "Re-run auto-tune." Automatically offered if user swaps to headphones we haven't seen before.

## 12. Sharing & file formats

### 12.1 `.warzeq` export

JSON bundle, one-click export from File → Export:

```json
{
  "format": "warzeq/1",
  "exported_by": "george",
  "exported_at": "2026-05-14T19:00:00Z",
  "headphones": "Sennheiser HD 600",
  "hearing_profile": null,           // omitted by default for privacy
  "fps_curve": "moderate",
  "intensity": 0.85,
  "competitive_mode_default": true,
  "adaptive_loudness": true,
  "linear_phase": false,
  "footstep_compressor": { "threshold": -42, "ratio": "1:2" },
  "ducker_threshold": -28,
  "custom_curve_bands": []
}
```

### 12.2 Import behavior

- File → Import or double-click `.warzeq` in Explorer
- App warns if exported on different hardware
- Offers to merge into the importer's auto-tune (apply curve/dynamics, keep their personal audiogram)
- Files dropped in `%LOCALAPPDATA%\WarzoneEQ\imports\` appear as preset cards in the Tuning tab

### 12.3 Personalized HRTF tiers (four options)

1. **HeSuVi (free baseline, bundled)** — sbx33.wav default, alternates user-selectable.
2. **Sonarworks SoundID Tools (free, personalized)** — released March 2026, phone-camera ear scan. Wizard step lets user paste profile code; app converts to Dolby Headphone-compatible IR for HeSuVi pipeline.
3. **Embody Immerse Gaming HIVE (premium personalized, $14.99/yr or $39.99/5yr)** — user installs separately. App detects HIVE virtual device and offers to route chain → HIVE → DAC.
4. **Power-user SOFA import (free, advanced)** — Genelec Aural ID / SADIE II / 3D Tune-In Toolkit SOFA file paste via Advanced tab.

## 13. Audio chain health check + clean uninstall

### 13.1 Health check

Button on Status tab. Verifies:
- ✅ Game endpoint routing is correct (chain `Device:` matches detected Game endpoint)
- ✅ TPM 2.0 + Secure Boot enabled (read-only; Ranked Play requires both)
- ✅ No rogue APOs hijacking our chain (enumerate APO chain via Win32 audio APIs)
- ✅ EQ APO version pinned to 1.4.2
- ✅ Warzone in-game audio settings match recommendations (best-effort detection via Warzone's config files in `%LOCALAPPDATA%\Activision\COD\...` — read-only)
- ⚠ Windows 11 25H2 detected → display KB5077181 audio-regression warning + link

**Important wording:** never call this a "Ricochet check." Surface as **"Audio chain health check."** No claims about anti-cheat detection.

### 13.2 Clean uninstall tool

Standalone `WarzoneEQ-Uninstall.exe`. Removes:
1. Our app + tray autostart
2. EQ APO + its config residue (critical — manual removal regularly bricks Windows audio per community reports)
3. All bundled VSTs / JSFX
4. `%LOCALAPPDATA%\WarzoneEQ\` (with confirmation — user may want to keep profiles)

Reverts default playback device to pre-install state where recorded.

## 14. File layout (user data)

```
%LOCALAPPDATA%\WarzoneEQ\
├── settings.json              # app preferences (mode, hotkeys, etc.)
├── profiles\
│   └── <username>.json        # auto-tune calibration
├── imports\                   # .warzeq files dropped here appear in UI
├── reference\
│   └── range.flac             # 60s tuning clip
└── logs\                      # rolling, max 10 MB

C:\Program Files\EqualizerAPO\config\warzone\
├── current.txt                # generated, hot-reloaded
├── profiles\                  # competitive.txt, cinematic.txt
├── headphone-correction\      # AutoEQ files (lazy-extracted)
├── hrir\                      # active HeSuVi WAV
├── jsfx\                      # adaptive-loudness.jsfx
└── fps-curves\                # minimalist / moderate / aggressive
```

## 15. Components & licenses

| Component | License | Bundled? | Notes |
|---|---|---|---|
| Equalizer APO 1.4.2 | GPLv2 | Yes (installer) | Pinned version, never auto-updated |
| HeSuVi 2.x | GPLv2 | Yes | HRIR bundle |
| TDR Nova 1.2.1 | Freeware (commercial-use OK) | Yes | Multiband transient shaper / dynamic EQ |
| LoudMax v1.46 | Freeware | Yes | Limiter |
| ReaPlugs (ReaXcomp, ReaJS) | Free, Cockos terms | Yes | Multiband compressor + JSFX host |
| Polyverse Wider 2.0 | Freeware | Yes (per redistribution terms) | Stereo widener |
| AutoEQ snapshot | MIT | Yes | 2,500+ headphone correction files |
| Adaptive-loudness JSFX | Our IP | Yes | Custom envelope-aware gain scaling |
| 3 FPS target curves | Our IP | Yes | Minimalist / Moderate / Aggressive |
| EarTrumpet | MIT (referenced only) | No (recommended in toast) | Per-app routing helper |
| Sonarworks SoundID Tools | Free, proprietary | No (user installs phone app) | Personalized HRTF v1 path |
| Embody Immerse Gaming HIVE | Commercial $14.99/yr | No (user installs separately) | Premium personalized HRTF |
| ASH-Toolset v4.1.0 | AGPL | No (user installs separately) | Advanced HRIR generation |
| Control app | Our code | Yes | C# WPF .NET 8 |

## 16. v1.1+ deferred features

- Bluetooth headphone first-class support (waiting on consistent <30 ms latency; LE Audio + LC3 maturity)
- Auto-update of the control app (Velopack or Squirrel.Windows)
- Code signing certificate
- Cloud sync of profiles
- Multi-game support (other competitive FPS: Apex, Valorant, CS2)
- macOS / Linux ports
- Graphical EQ band editor (Power users use text edit in v1)
- Telemetry-driven anomaly detection for soft-ban correlation (opt-in)

## 17. Hard engineering rules (recap)

1. Never touch the CoD process.
2. Never render an overlay over the game window.
3. Never run AI source separation on game audio in real time.
4. Stay user-mode + system mixer bus.
5. No kernel drivers beyond EQ APO 1.4.2.
6. Never auto-update EQ APO.
7. No telemetry without opt-in.

## 18. Open questions before implementation

None blocking. The implementation plan (writing-plans next step) will sequence the work and surface any second-order questions.

## 19. Sources

- ArtIsWar ArtTuneDB: `github.com/ArtIsWar/ArtTuneDB`, `artiswar.io`
- HeSuVi: `equalizerapo.com/hesuvi.html`
- Embody Immerse Gaming HIVE: `embody.co/pages/gaming-hive`
- Sonarworks SoundID Tools: `audionewsroom.net/2026/03/sonarworks-soundid-tools-free-dolby-atmos-mobile-app.html`
- Genelec Aural ID: `genelec.com/aural-id`
- AutoEQ: `github.com/jaakkopasanen/AutoEq`
- TDR Nova: `tokyodawn.net/tdr-nova/`
- ReaPlugs: `reaper.fm/reaplugs/`
- LoudMax: `loudmax.blogspot.com`
- Polyverse Wider: `polyversemusic.com/products/wider/`
- EarTrumpet: GitHub `File-New-Project/EarTrumpet`
- RICOCHET Anti-Cheat S03 update (BO7 May 2026): `callofduty.com/blog/2026/04/...ricochet-anti-cheat-season-03`
- Activision Security & Enforcement Policy: `support.activision.com/articles/call-of-duty-security-and-enforcement-policy`
- TPM 2.0 + Secure Boot CoD requirement: `support.activision.com/articles/trusted-platform-module-and-secure-boot`
- Windows 11 25H2 KB5077181 audio regressions: Unreal Engine forums Jan–Feb 2026
- Sound Blaster G8: `us.creative.com/p/sound-blaster/sound-blaster-g8`
- Sound Blaster GC7: `us.creative.com/p/sound-blaster/sound-blaster-gc7`
- 3D Tune-In Toolkit: `github.com/3DTune-In/3dti_AudioToolkit`
- SADIE II Database: `york.ac.uk/sadie-project/database.html`
- Squiglink ecosystem: `squig.link`
