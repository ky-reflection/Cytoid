# Cytoid Lab v0.1.0

First public preview of **Cytoid Lab** — a Windows desktop tool for chart authors to import, preview, and debug Cytoid levels using the Unity gameplay core.

## Download

Attach the build artifact from CI or local packaging:

```powershell
.\engines\unity\build-cytoid-lab.ps1 -Package -KeepLog
```

Ship `engines/unity/Builds/CytoidLab.zip` (contains `CytoidLab.exe` and data folder).

**Requirements:** Windows 10/11 x64. No separate Unity install needed.

## What's included

### Level browser
- Browse locally installed levels
- Import `.cytoidlevel` files via file picker or drag-and-drop on the executable
- Difficulty selection (Easy / Hard / Extreme)

### Playtest HUD
- Play / pause, auto mode, hitsound on/off
- Note ID overlay for chart verification
- Fullscreen toggle (F11); ESC exits fullscreen or returns to menu
- Timeline slider: scrub audio while dragging; full chart/storyboard resync on release
- Hard reset reloads the playfield from scratch

### Chart debugging
- Timeline seek respawns active notes, drag chains, and hold progress at the target time
- Storyboard elements and camera state resync after scrub
- MAX/FC completion splashes suppressed in Lab (focus on inspection, not celebration UI)

## Known limitations

- Windows only in this release
- Uses the same local level storage as legacy debug builds (`AppData/LocalLow/TigerHix/…`)
- Timeline scrub during drag previews audio/time only; full note/storyboard state updates on slider release
- Not a replacement for the main Cytoid mobile/desktop app

## Build from source

Repository: [cytoid-core-unity](https://github.com/Cytoid/cytoid-core-unity)  
Branch: `feature/cytoid-player`

```powershell
git checkout feature/cytoid-player
.\engines\unity\build-cytoid-lab.ps1 -KeepLog
```

Unity **6000.0.75f1** required for building.

## Changelog

### Added
- Cytoid Lab Windows standalone (`CytoidLab.exe`)
- Runtime-built menu and in-game HUD
- Timeline scrub with playfield resync
- Note ID toggle; hold progress on seek
- Version display (v0.1.0)

### Changed
- Renamed from working title "Cytoid Player" to **Cytoid Lab** to avoid confusion with the existing Cytoid Player product

### Fixed
- Drag chain and hold respawn after timeline resync
- Note ID binding on pooled note respawn
- Hold body progress after refresh/seek
- Timeline slider layout (full width in bottom HUD bar)
