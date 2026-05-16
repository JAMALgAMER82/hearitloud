# Hear It Loud — Roadmap

The "super ultimate" feature list, sequenced into shippable phases.
Each phase ships as a real GitHub Release; the auto-updater
(`UpdateChecker` in v1.1.2+) delivers each phase to existing users.

## Status of the original 13-item ask

| # | Feature                              | Shipped as | Phase  |
|---|--------------------------------------|------------|--------|
| 1 | Detect installed audio devices       | v1.0       | done   |
| 2 | Install/configure EQ APO + HeSuVi    | v1.0 (EQ APO) + v1.2 (HeSuVi auto-install) | done |
| 3 | Per-headphone EQ correction curves   | v1.2 (via AutoEQ runtime fetch)            | done |
| 4 | Per-game EQ profiles                 | v1.0–v1.1 (Competitive / Cinematic / FootstepHunter) | done |
| 5 | HRIR 7.1→binaural                    | v1.0 (HeSuVi Include)                      | done |
| 6 | Hotkey profile switching             | v1.2                                       | done |
| 7 | Live A/B mid-game (no dropout)       | v1.2                                       | done |
| 8 | Auto-detect game (foreground process)| v1.3                                       | queued |
| 9 | AutoEQ runtime fetch                 | v1.2                                       | done |
| 10| Visual EQ editor (drag points)       | v1.4                                       | queued |
| 11| Community gallery                    | **out of scope**                           | needs web service |
| 12| Telemetry-free / offline-first       | v1.0 (only update check phones home)       | done |
| 13| Free + paid Pro                      | **out of scope (recommendation: skip)**    | conflicts with #12; needs business infra |

## Why #11 and #13 are out of scope

**#11 Community gallery** is not a desktop-app feature — it requires:
- a hosted web service (server, database, file storage)
- a moderation system (curve files can encode anything; we can't ship untrusted code)
- a submission/review flow + accounts
- ongoing operational cost + uptime

The desktop side of sharing — export/import .warzeq files — already
ships in v1.1.0. Anyone can put a .warzeq on a Discord / forum / GitHub
gist and share the link. That covers the actual sharing need. Building a
centralized gallery is a separate product (a web service), not a
desktop-app patch.

**#13 Paid tier** has three real problems beyond "I have to build it":

1. **Conflicts with #12** (offline-first / telemetry-free). License
   validation requires *either* (a) a network call to a license server
   on every launch (kills offline-first), (b) signed offline keys
   (trivially crackable for a freely-distributable desktop app), or (c)
   Microsoft Store / Steam wrapper (not free, takes 30% cut).
2. **Strong free competition** in this niche: Equalizer APO, HeSuVi,
   FxSound, SteelSeries Sonar, Razer Synapse — all free, several
   backed by hardware vendors. Selling a paid version against well-
   funded free competitors as a one-person project is a tough sell.
3. **Real legal/tax/payment infrastructure** is out of scope for a
   coding session: Stripe / Paddle account, ToS, refund policy, VAT
   compliance, GDPR, support obligations.

Recommendation: stay free + open. If real demand for Pro features
materializes later, revisit with a clear value prop and a small
Microsoft Store wrapper.

## Phase plan

### v1.2 (this session)

- HeSuVi auto-installer: GUI button that downloads + silently runs
  the official HeSuVi installer, then prompts to enable it.
- TDR Nova + LoudMax auto-installer: download the official zip(s),
  extract the .dll into `EqualizerAPO\VSTPlugins\`, prompt to reload.
- **AutoEQ runtime fetch**: when the Advanced tab references a
  headphone, fetch its correction curve from
  `github.com/jaakkopasanen/AutoEq` (cached to `%APPDATA%\HearItLoud\
  autoeq-cache\`). Adds ~5,000 headphones to the supported list — the
  static `vidpid-overlay.json` becomes a hardware-detection hint only.
- **Hotkey profile switching**: global hotkey (default Ctrl+Shift+F8)
  toggles between FootstepHunter and Cinematic. Configurable in
  Advanced tab. Uses Win32 RegisterHotKey + WndProc message handling.
- **Live A/B (no dropout)**: pre-generates *all* profile configs to
  disk on Apply. A tiny `warzone\selector.txt` file does an `Include:`
  to the currently-active config. Hotkey just rewrites selector.txt
  (~10 ms), EQ APO hot-reloads (~50 ms). Total switch time ≈ 60 ms.
- **FootstepHunter chain tweaks** (from research):
  - 1.2 kHz Q=4 notch on L+R+FC channels (reduce gunshot bleed)
  - Second transient shaper at 5 kHz on rears (scuff sounds on hard
    surfaces)
  - More aggressive FC duck: threshold −24 dB, ratio 8:1
  - Optional downward expander in 2–5 kHz on rears (noise-floor cleanup)

### v1.3 (next session)

- **Game auto-detection (#8)**: small tray service polls foreground
  window via `GetForegroundWindow` + `GetWindowThreadProcessId`. When
  it sees a process in a configurable list (cod.exe, ModernWarfare.exe,
  ...), auto-switches to FootstepHunter; when foreground leaves the
  game list, switches back to Cinematic (or whatever the user's
  "default outside game" preference is). Adds:
  - Tray icon with right-click menu (current profile, switch, quit)
  - Start-with-Windows option
  - Per-process profile mapping in Advanced tab
- **More footstep DSP**:
  - M/S processing (split center vs sides, duck center harder than
    sides — gunshots are mostly mono center, footsteps mostly lateral)
  - Sub-harmonic restoration (re-introduce footfall weight in 60–100 Hz
    band that the high-pass removed, via harmonic synthesis from
    200–400 Hz)
  - Gunshot-triggered FC sidechain (RMS-based detector that triggers
    deeper FC duck specifically on gunshot transients vs all loud
    sounds)
- **Reference test sound button**: bake in a 5-second known-good
  Warzone footstep clip. User clicks → hears it through their current
  chain → instantly knows if the chain is working.

### v1.4 (multi-session)

- **Visual EQ editor (#10)**: custom-painted WinForms chart with:
  - Frequency response curve (log-X frequency, dB-Y)
  - Drag-able EQ points (click+drag to add filters; drag existing
    points to retune)
  - Real-time biquad coefficient calculation (peaking, shelving, HPF,
    LPF)
  - HRIR overlay (show the HeSuVi target frequency response behind
    the user's chain)
  - Headphone correction overlay (show the AutoEQ curve being applied)
  - "Snap to AutoEQ" button (load the headphone's correction as the
    starting point)

### v1.5+ (deferred)

- Headphone-class-specific tuning (open-back vs closed-back vs IEM vs
  planar — slightly different chain shapes)
- Per-game profile presets (separate from "audio mode" — e.g., a CS2
  profile, an Apex profile)
- Multi-output routing for streamers (chain on game out; bypass on
  stream out)
- Localization (currently English-only)

### Permanently out of scope

- #11 community gallery (needs web service — separate product)
- #13 paid tier (see "Why #11 and #13 are out of scope" above)
- Real-time AI source separation (anti-cheat compliance — reads the
  game's audio stream, would risk Ricochet flagging)
- Anything that injects into the game process or reads game memory
  (same anti-cheat reason)
