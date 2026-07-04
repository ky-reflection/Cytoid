# Cytoid Lab

> Branch: `feature/cytoid-player` (historical name; ships **Cytoid Lab**)  
> Version: **v0.1.2**

**Cytoid Lab** is a Windows standalone chart preview and playtest tool built from the Unity core. It is aimed at chart authors and core developers — not at end players (use the main Cytoid app for that).

## Core integration boundary

Lab behavior should stay in `Navigation/CytoidLab/` and `*.CytoidLab.cs` partials. Avoid new `CytoidLabShell` branches in chart/storyboard math unless unavoidable.

| Layer | Location | Role |
|-------|----------|------|
| Lab shell | `CytoidLabShell.cs` | Window sizing, overlay HUD injection, viewport presets |
| Lab UI | `CytoidLabMenuController.cs`, `CytoidLabHudController.cs` | Level menu, in-game controls, timeline scrub |
| Lab partials | `Navigation/CytoidLab/*.CytoidLab.cs` | Timeline resync, hold/drag extensions on core types |
| Core touchpoints | see below | Minimal `#if UNITY_STANDALONE_WIN` / `IsActive` guards only |

**Current core touchpoints** (audit as of v0.1.2):

| File | Intrusion | Notes |
|------|-----------|-------|
| `Context.cs` | `#if UNITY_STANDALONE_WIN`: shell bootstrap, menu inject, graphics-quality window sizing | Lab shell only |
| `AllPerfectSplash.cs`, `FullComboSplash.cs` | Skip result splashes when `CytoidLabShell.IsActive` | Lab shell only |
| `ProgressRing.cs` | `MaterialPropertyBlock` per instance (hold-seek RC-1) | **Universal fix** — all builds benefit; not Lab-gated |
| `Note.cs` | Skip Auto + miss/clear when `Game.SuppressTimelineGameplayMutations` | Property lives on `Game.CytoidLab` partial; always `false` on Bridge/mobile |
| `HoldNote.cs` | Clear `UseTimelineVisualState` when scrub suppress ends (RC-9) | Calls Lab partial `ClearTimelineVisualState()`; no-op path when flag already false |
| `ClassicHoldNoteRenderer.cs` | `JudgmentOffset` on ProgressRing gate (RC-3); read `TimelineApproachScale` only while suppress (RC-9) | Reads internal fields on `HoldNote.CytoidLab` partial |
| `Game.cs` | Skip Escape pause when `CytoidLabShell.IsActive` (HUD owns keyboard) | Lab guard only |
| `Chart.cs`, `GenericStateParser.cs` | **No Lab branches** | Full-window `Screen` coords; overlay HUD does not crop the camera |

**Lab-only (no core edits beyond the table above):**

| File | Role |
|------|------|
| `Game.CytoidLab.cs` | `PreviewTimeline` light resync, `ResyncPlayfieldToTime`, `SuppressTimelineGameplayMutations`, spawn/prune at seek time |
| `HoldNote.CytoidLab.cs` | `FastForwardVisualStateToTime`, timeline hold body + head approach snapshot |
| `CytoidLabHudController.cs` | Slider suppress lifecycle, keyboard shortcuts, `EndTimelineScrub` on release |
| `CytoidLabUiInput.cs` | `InputSystemUIInputModule` setup; disable button keyboard navigation for shortcuts |
| `CytoidLabHelpOverlay.cs` | Help modal (shortcuts, timeline, tips) |
| `DragHeadNote.CytoidLab.cs`, `DragLineElement.CytoidLab.cs`, `GameState.CytoidLab.cs`, storyboard partials | Drag fast-forward, score rewind, storyboard resync |

Bridge / mobile builds are unaffected (`GameEmbedMode.IsBridgeEmbedded` gates Lab shell; timeline seek APIs are never called).

## Features

**v0.1.2**
- Viewport presets (16:9 / 4:3, Small / Large); applies on Start
- Skip end on chart clear (`End: On/Off`)
- `.zip` import; Unicode / non-ASCII file picker paths
- Keyboard shortcuts (Space / Esc / F11); **?** help on level menu
- AudioServer refactor; timeline seek via `PlayFrom`
- Storyboard video prepare / VFS / timeline sync improvements

**v0.1.1**
- 1280×720 play window; Screen Space Overlay HUD (no camera viewport crop)
- Edge-reveal auto-hide HUD; wider timeline hit area
- Light resync while dragging; full resync on release
- Hold / long-hold body + head approach after timeline seek
- Keyboard shortcuts and in-app help overlay

**v0.1.0**
- Level menu, `.cytoidlevel` import, difficulty selection
- Play/pause, auto, hitsound, note IDs, fullscreen, timeline scrub, hard reset

## Build

**Unity menu:** `Cytoid → Build Cytoid Lab (Windows x64)`

**PowerShell:**

```powershell
.\engines\unity\build-cytoid-lab.ps1 -KeepLog -Run
```

**Batchmode:**

```bash
Unity -batchmode -quit -projectPath engines/unity \
  -executeMethod CytoidCoreBuild.BuildCytoidLabWindows64
```

**Output:** `engines/unity/Builds/CytoidLab/CytoidLab.exe`

| Setting | Value |
|---------|-------|
| Application ID | `org.cytoid.lab` |
| Product name | Cytoid Lab |
| Default window | 1280×720 (HUD overlays gameplay) |
| Scenes | Bootstrapper → Navigation → Game |

## Code layout

Runtime code lives under `engines/unity/Assets/Scripts/Navigation/CytoidLab/`:

| File | Role |
|------|------|
| `CytoidLabShell.cs` | Window sizing, overlay HUD lifecycle |
| `CytoidLabMenuController.cs` | Level selection menu |
| `CytoidLabHudController.cs` | In-game HUD, timeline slider, edge auto-hide |
| `CytoidLabVersion.cs` | Release version constant |
| `Game.CytoidLab.cs` | Timeline preview, resync, playfield restore at seek time |
| `GameState.CytoidLab.cs` | Score/judgement rewind on seek |
| `HoldNote.CytoidLab.cs` | Hold progress for timeline scrub |
| `*.CytoidLab.cs` | Other partial extensions (drag, storyboard, …) |

IL2CPP stripping anchors: `engines/unity/Assets/link.xml` (`CytoidLab*` types).

## Logs

Player log (Windows):

`%USERPROFILE%\AppData\LocalLow\TigerHix\Cytoid Lab\Player.log`

## See also

- [Release notes](releases.md)
- Local notes (backlog, planning, research): `docs/local/README.md` (gitignored)
