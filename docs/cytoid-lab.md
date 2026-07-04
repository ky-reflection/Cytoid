# Cytoid Lab

> Branch: `feature/cytoid-player` (historical name; ships **Cytoid Lab**)
> Version: **v0.1.1**

**Cytoid Lab** is a Windows standalone chart preview and playtest tool built from the Unity core. It is aimed at chart authors and core developers — not at end players (use the main Cytoid app for that).

## Core integration boundary

Lab behavior should stay in `Navigation/CytoidLab/` and `*.CytoidLab.cs` partials. Avoid new `CytoidLabShell` branches in chart/storyboard math unless unavoidable.

| Layer | Location | Role |
|-------|----------|------|
| Lab shell | `CytoidLabShell.cs` | 720p window, overlay HUD injection, graphics-quality window sizes |
| Lab UI | `CytoidLabMenuController.cs`, `CytoidLabHudController.cs` | Level menu, in-game controls, timeline scrub |
| Lab partials | `Navigation/CytoidLab/*.CytoidLab.cs` | Timeline resync, hold/drag extensions on core types |
| Core touchpoints | see below | Minimal `#if UNITY_STANDALONE_WIN` / `IsActive` guards only |

**Current core touchpoints** (audit as of v0.1.1 + timeline seek Phase A):

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
| `DragHeadNote.CytoidLab.cs`, `DragLineElement.CytoidLab.cs`, `GameState.CytoidLab.cs`, storyboard partials | Drag fast-forward, score rewind, storyboard resync (pre-existing) |

Bridge / mobile builds are unaffected (`GameEmbedMode.IsBridgeEmbedded` gates Lab shell; timeline seek APIs are never called).

## Features (v0.1.1)

- Level menu with installed levels and **Import .cytoidlevel**; compact header; **?** help button (shortcuts & tips)
- **1280×720** play window; HUD is **Screen Space Overlay** (no camera viewport crop)
- In-game HUD: play/pause, auto, hitsound, note IDs, fullscreen; edge-reveal auto-hide via **Input System** pointer
- Keyboard: **Space** play/pause, **Esc** play/pause (windowed) or exit fullscreen, **F11** fullscreen
- Full-width timeline scrub at bottom (thin track, round handle)
- **Light resync while dragging** (active note prune/spawn, hold/drag visual fast-forward, chart cursor); **full resync on release** (score/judgement, storyboard, input)
- Hold/long-hold body + head approach restored after timeline seek; storyboard resync on scrub release
- Hard **Reset** (scene reload)

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
| Window | 1280×720 (HUD overlays gameplay; no extra chrome height) |
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

## Release

See [cytoid-lab-v0.1.1.md](releases/cytoid-lab-v0.1.1.md) for v0.1.1 release notes (v0.1.0: [cytoid-lab-v0.1.0.md](releases/cytoid-lab-v0.1.0.md)).

## Related docs

- [Cytoid Lab release notes](releases/)
- Local research notes: `docs/local/research-index.md` (gitignored)
