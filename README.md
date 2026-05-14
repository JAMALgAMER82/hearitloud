# Warzone EQ

Ultimate compliant footstep audio app for Call of Duty Warzone. See [the design spec](docs/superpowers/specs/2026-05-14-warzone-eq-design.md) for the full architecture and anti-cheat compliance principles.

## One-click install (for your friends)

Send them [publish/installer/WarzoneEQ-Setup.exe](publish/installer/WarzoneEQ-Setup.exe) (23 MB).

They double-click it. The installer:
1. Downloads + silently installs **Equalizer APO** (free, open-source, the audio engine).
2. Copies **WarzoneEQ.exe** to `C:\Program Files\Warzone EQ\`.
3. Auto-detects their headphones + DAC and writes a personalized EQ config.
4. Creates Start-menu and desktop shortcuts.
5. Reboots Windows (Equalizer APO requires it).

After reboot, footsteps are louder and more directional in every CoD title. They need to set Warzone audio settings once (the installer tells them how):

> **Settings → Audio → Audio Mix = Headphones Bass Cut, Surround = 7.1, Music = 0, Enhanced Headphone Mode = OFF**

That's it. No CLI required, no tweaking sliders, no manual config files.

## Building the installer from source

```powershell
# 1. Build the self-contained .exe
dotnet publish src/WarzoneEQ.Cli -c Release -r win-x64 --self-contained `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o publish/win-x64

# 2. Compile the Inno Setup installer
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer/installer.iss

# Output: publish/installer/WarzoneEQ-Setup.exe
```

## CLI (for power users or scripting)

```powershell
# Detect what's plugged in
WarzoneEQ.exe --detect

# Print a config (don't install)
WarzoneEQ.exe --mode Cinematic --curve Aggressive `
  --headphone HD600 `
  --dac "Speakers Sound Blaster GC7 Game" `
  --linear-phase --adaptive-loudness --wider

# Install the config into Equalizer APO
WarzoneEQ.exe --install --mode Competitive

# Best of all worlds (auto-detect + install with sensible defaults)
WarzoneEQ.exe --auto
```

## Architecture

- **WarzoneEQ.ConfigGenerator** — pure-logic library that produces Equalizer APO config text from a `ProfileInput`.
- **WarzoneEQ.DeviceDetection** — Windows audio enumeration via WMI; matches USB VID/PID + Bluetooth names against a bundled overlay of headphones and multi-endpoint DACs.
- **WarzoneEQ.WindowsIntegration** — writes the generated config to Equalizer APO's config directory, manages Windows Loudness Equalization on the Game endpoint.
- **WarzoneEQ.Cli** — single-file `WarzoneEQ.exe` that ties them all together.
- **installer/installer.iss** — Inno Setup 6 script that bootstraps Equalizer APO and runs `--auto` post-install.

## Sub-plan progress

- [x] **#1 EQ APO Config Generator** — [plan](docs/superpowers/plans/2026-05-14-eq-apo-config-generator.md)
- [x] **#2 Audio Device Detection** — [plan](docs/superpowers/plans/2026-05-14-audio-device-detection.md)
- [x] **#3 EQ APO + Windows Audio Integration** — [plan](docs/superpowers/plans/2026-05-14-eq-apo-windows-audio-integration.md)
- [x] **#4 End-user CLI** (auto-detect + install)
- [x] **#7 One-click installer** (`installer/installer.iss` → `WarzoneEQ-Setup.exe`)
- [ ] #5 First-Run Wizard + Auto-Tune (v1.1 — adds 60-second hearing test for per-user calibration)
- [ ] #6 Sharing & Personalized HRTF Tiers (v1.1 — `.warzeq` files, Embody / Sonarworks SoundID integration)

**v1 ships now.** The installer auto-detects + auto-configures with sensible defaults; the audiogram wizard and `.warzeq` sharing are quality-of-life additions for v1.1.

## Tests

```powershell
dotnet test
```

101 tests, all passing.

## Anti-cheat compliance

Every component runs strictly within Windows' user-mode audio APO layer. We never inject into the CoD process, never read or write game memory, never render overlays over the game window, and never run real-time AI source separation on game audio. See [the spec](docs/superpowers/specs/2026-05-14-warzone-eq-design.md#3-anti-cheat-compliance-principles) for the full hard-engineering rules.
