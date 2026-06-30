# Cytoid Lab

> Branch: `feature/cytoid-player` (historical name; ships **Cytoid Lab**)
> Version: **v0.1.0**

**Cytoid Lab** is a Windows standalone chart preview and playtest tool built from the Unity core. It is aimed at chart authors and core developers — not at end players (use the main Cytoid app for that).

## Features (v0.1.0)

- Level menu with installed levels and **Import .cytoidlevel**
- In-game HUD: play/pause, auto, hitsound toggle, note ID overlay, fullscreen (F11)
- **Timeline scrub** with soft preview while dragging and full playfield resync on release
- Hold/long-hold progress restored after timeline seek
- Storyboard camera and elements resync on scrub
- Hard **Reset** (scene reload) for a clean retry
- Version display in menu and HUD (`CytoidLabVersion`)

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
| Window | 1280×848 (720 play area + HUD bands) |
| Scenes | Bootstrapper → Navigation → Game |

## Code layout

Runtime code lives under `engines/unity/Assets/Scripts/Navigation/CytoidLab/`:

| File | Role |
|------|------|
| `CytoidLabShell.cs` | Window chrome, camera bands, HUD injection |
| `CytoidLabMenuController.cs` | Level selection menu |
| `CytoidLabHudController.cs` | In-game HUD and timeline slider |
| `CytoidLabVersion.cs` | Release version constant |
| `Game.CytoidLab.cs` | Timeline preview, resync, note respawn |
| `GameState.CytoidLab.cs` | Score/judgement rewind on seek |
| `HoldNote.CytoidLab.cs` | Hold progress for timeline scrub |
| `*.CytoidLab.cs` | Other partial extensions (drag, storyboard, …) |

IL2CPP stripping anchors: `engines/unity/Assets/link.xml` (`CytoidLab*` types).

## Logs

Player log (Windows):

`%USERPROFILE%\AppData\LocalLow\TigerHix\Cytoid Lab\Player.log`

## Release

See [cytoid-lab-v0.1.0.md](releases/cytoid-lab-v0.1.0.md) for GitHub Release notes.
