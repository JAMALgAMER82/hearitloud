# Warzone EQ

Ultimate compliant footstep audio app for Call of Duty Warzone. See [the design spec](docs/superpowers/specs/2026-05-14-warzone-eq-design.md) for the full architecture and anti-cheat compliance principles.

## Sub-plan progress

- [x] **#1 EQ APO Config Generator** — `src/WarzoneEQ.ConfigGenerator/`, `src/WarzoneEQ.Cli/` ([plan](docs/superpowers/plans/2026-05-14-eq-apo-config-generator.md))
- [x] **#2 Audio Device Detection** — `src/WarzoneEQ.DeviceDetection/` ([plan](docs/superpowers/plans/2026-05-14-audio-device-detection.md))
- [x] **#3 EQ APO + Windows Audio Integration** — `src/WarzoneEQ.WindowsIntegration/` ([plan](docs/superpowers/plans/2026-05-14-eq-apo-windows-audio-integration.md))
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

Outputs an Equalizer APO config to stdout.

## Tests

```powershell
dotnet test
```

101 tests, all passing as of sub-plan #3 completion.
