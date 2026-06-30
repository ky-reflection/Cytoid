## Cytoid Lab v0.1.0

First public preview of **Cytoid Lab** — a Windows desktop tool for chart authors to import, preview, and debug Cytoid levels using the Unity gameplay core.

### Download

- **Platform:** Windows 10/11 x64
- **Attach:** `CytoidLab.zip` (contains `CytoidLab.exe` and its data folder)
- No Unity install required to run the prebuilt binary

Build locally:

```powershell
.\engines\unity\build-cytoid-lab.ps1 -Package -KeepLog
```

### Highlights

**Level browser**
- Browse locally installed levels
- Import `.cytoidlevel` files (file picker, or drag onto the executable)
- Difficulty selection: Easy / Hard / Extreme

**Playtest HUD**
- Play / pause, auto mode, hitsound on/off
- Note ID overlay for chart verification
- Fullscreen toggle
- Timeline slider: click or drag to preview; full chart resync on release
- Hard reset reloads the playfield from scratch

**Chart debugging**
- Timeline seek re-syncs on-screen notes, drag chains, and hold progress to the target time
- Storyboard elements and camera state resync after scrub
- MAX/FC completion splashes disabled in Lab

### Known limitations

- Windows only in this release
- Installed and imported levels are stored under `%USERPROFILE%\AppData\LocalLow\TigerHix\` (Unity persistent data on Windows)
- While the slider is held, only audio/time are previewed; chart and storyboard state commit on release

### Build from source

```powershell
git checkout feature/cytoid-player
.\engines\unity\build-cytoid-lab.ps1 -KeepLog
```

Requires Unity **6000.0.75f1**.
