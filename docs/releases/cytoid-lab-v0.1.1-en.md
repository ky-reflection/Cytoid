## Cytoid Lab v0.1.1

Patch release for **Cytoid Lab** — improves timeline scrubbing, hold seek visuals, HUD layout, keyboard shortcuts, and in-app help. Built on Unity gameplay core **6000.0.75f1**.

### Download

- **Platform:** Windows 10/11 x64
- **Attach:** `CytoidLab.zip` (contains `CytoidLab.exe` and its data folder)
- No Unity install required to run the prebuilt binary

Build locally:

```powershell
.\engines\unity\build-cytoid-lab.ps1 -Package -KeepLog
```

### What's new

**Window & HUD**
- **1280×720** play window with **Screen Space Overlay** HUD (gameplay uses the full window; no camera viewport crop)
- Edge-reveal auto-hide for top/bottom HUD bars
- Wider timeline slider hit area (easier to grab without changing track appearance)
- Level menu scrollbar and layout tidy-up

**Timeline scrubbing**
- **Live preview while dragging** the timeline: notes, drag chains, and hold body/head visuals stay aligned with the slider (light resync)
- **Full resync on release**: score/judgement, storyboard, and input state rebuild at the target time (unchanged semantics from v0.1.0)
- Hold / long-hold **body progress and head approach** restored correctly after seek
- Auto and miss/clear suppressed during scrub so preview does not mutate gameplay state

**Keyboard & help**
- **Space** — play / pause (ignored while dragging the timeline)
- **Esc** — play / pause (windowed), or exit fullscreen
- **F11** — toggle fullscreen
- **?** button on the level menu opens a help overlay (shortcuts, timeline tips, version)

**Core fixes (all builds benefit)**
- Hold **ProgressRing** uses per-instance `MaterialPropertyBlock` (fixes color/cutoff crosstalk when multiple holds are on screen)

### Known limitations

- Windows only
- Levels stored under `%USERPROFILE%\AppData\LocalLow\TigerHix\Cytoid Lab\` (Unity persistent data)
- **Storyboard during drag preview** is not resynced; storyboard state commits on slider release (same as v0.1.0 full resync path)
- **Storyboard trigger replay** after seek is not implemented — seek rebuilds static timeline objects, not trigger side-effect history
- **Some chart videos** may not resume correctly after timeline seek (known issue; tracked separately)
- Style 2 hold body may still show early during approach on seek (minor; Style 1 is primary)

### Build from source

```powershell
git checkout feature/cytoid-player
git checkout cytoid-lab-v0.1.1   # after tag is published
.\engines\unity\build-cytoid-lab.ps1 -Package -KeepLog
```

Requires Unity **6000.0.75f1**.

Player log: `%USERPROFILE%\AppData\LocalLow\TigerHix\Cytoid Lab\Player.log`
