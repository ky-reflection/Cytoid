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

**Current core touchpoints** (audit as of v0.1.1):

| File | Intrusion |
|------|-----------|
| `Context.cs` | `#if UNITY_STANDALONE_WIN`: shell bootstrap, menu inject, graphics-quality window sizing |
| `AllPerfectSplash.cs`, `FullComboSplash.cs` | Skip result splashes when `CytoidLabShell.IsActive` |
| `Game.cs`, `HoldNote.cs` | Comments pointing at `*.CytoidLab.cs` partials (no runtime Lab logic) |
| `Chart.cs`, `GenericStateParser.cs` | **No Lab branches** — full-window `Screen` coords (overlay HUD does not crop the camera) |

Bridge / mobile builds are unaffected (`GameEmbedMode.IsBridgeEmbedded` gates Lab shell).

## Features (v0.1.1)

- Level menu with installed levels and **Import .cytoidlevel**; compact header; Start on difficulty row
- **1280×720** play window; HUD is **Screen Space Overlay** (no camera viewport crop)
- In-game HUD: play/pause, auto, hitsound, note IDs, fullscreen; edge-reveal auto-hide via **Input System** pointer
- Full-width timeline scrub at bottom (thin track, round handle)
- Soft preview while dragging; full playfield resync on release
- Hold/long-hold progress restored after timeline seek; storyboard resync on scrub
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

See [cytoid-lab-v0.1.0.md](releases/cytoid-lab-v0.1.0.md) for GitHub Release notes (update for v0.1.1 when publishing).

## Related docs

- [Cytoid Lab / Core 调研文档总览](2026-07-01-cytoid-lab-research-index.md)
- [Cytoid Lab / Core 开发方向评审](2026-07-01-cytoid-lab-core-direction.md)
- [Timeline resync 系统评审](2026-07-01-cytoid-lab-resync-system-review.md)
- [c2v3 调研方案（Page Function + UI Animation）](2026-07-01-c2v3-research-plan.md)
- [Storyboard 内存/性能/Bug 审计报告](storyboard-memory-performance-audit.md)
- [Cytoid Lab macOS 移植评估](cytoid-lab-macos-adaptation.md)
- [Hold seek 问题调研](2026-07-01-cytoid-lab-hold-seek-investigation.md)
