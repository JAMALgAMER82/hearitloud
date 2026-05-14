# Warzone EQ

Ultimate compliant footstep audio app for Call of Duty Warzone. See [the design spec](docs/superpowers/specs/2026-05-14-warzone-eq-design.md) for the full architecture and anti-cheat compliance principles.

## Quick start (for end users)

1. Install [Equalizer APO](https://equalizerapo.com) (free, open-source). Reboot when prompted.
2. Build: `dotnet build -c Release` (requires .NET 8 SDK).
3. Run end-to-end auto-config:
   ```powershell
   dotnet run --project src/WarzoneEQ.Cli -c Release -- --auto
   ```
   Detects your headphones + DAC, generates a tuned Equalizer APO config, and installs it. Equalizer APO hot-reloads automatically.
4. In Warzone: **Settings → Audio → Audio Mix = Headphones Bass Cut, Surround = 7.1, Music = 0, Enhanced Headphone Mode = OFF**.

## CLI usage

```powershell
# Detect what's plugged in
dotnet run --project src/WarzoneEQ.Cli -- --detect

# Print a config (don't install)
dotnet run --project src/WarzoneEQ.Cli -- `
  --mode Cinematic --curve Aggressive `
  --headphone HD600 `
  --dac "Speakers Sound Blaster GC7 Game" `
  --linear-phase --adaptive-loudness --wider

# Install the config into Equalizer APO
dotnet run --project src/WarzoneEQ.Cli -- --install --mode Competitive

# Best of all worlds (auto-detect + install with sensible defaults)
dotnet run --project src/WarzoneEQ.Cli -- --auto
```

## Sub-plan progress

- [x] **#1 EQ APO Config Generator** — `src/WarzoneEQ.ConfigGenerator/` ([plan](docs/superpowers/plans/2026-05-14-eq-apo-config-generator.md))
- [x] **#2 Audio Device Detection** — `src/WarzoneEQ.DeviceDetection/` ([plan](docs/superpowers/plans/2026-05-14-audio-device-detection.md))
- [x] **#3 EQ APO + Windows Audio Integration** — `src/WarzoneEQ.WindowsIntegration/` ([plan](docs/superpowers/plans/2026-05-14-eq-apo-windows-audio-integration.md))
- [x] **#4 End-user CLI (detect + install + auto)** — `src/WarzoneEQ.Cli/`
- [ ] #5 First-Run Wizard + Auto-Tune (planned for v1.1; depends on WPF GUI)
- [ ] #6 Sharing & Personalized HRTF Tiers (planned for v1.1; depends on WPF GUI)
- [ ] #7 Installer + Clean Uninstall (planned for v1.1)

**v1 ships as a CLI tool.** The full tray-GUI experience (5-tab WPF window, audiogram wizard, .warzeq sharing) is intentionally deferred — the CLI is functionally complete for the "send to friends" use case and exposes every feature the GUI would.

## Tests

```powershell
dotnet test
```

101 tests, all passing.
