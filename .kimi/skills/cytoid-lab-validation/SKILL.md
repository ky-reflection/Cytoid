# Cytoid Lab Validation

Use this skill when you need to verify that changes developed on another branch work correctly with the Cytoid Lab Windows standalone build.

## When to use

- You have finished work on a feature/bugfix branch that touches the Unity gameplay core.
- You want to make sure the changes do not break the PC player experience.
- You need to produce a testable Windows build for manual QA.

## Workflow

1. **Save current work.**
   ```bash
   git stash push -m "wip before player validation"
   # or commit if the work is ready
   ```

2. **Switch to the player branch and bring in the target branch.**
   ```bash
   git fetch origin
   git checkout feature/cytoid-player
   git rebase origin/main
   # alternative: git merge origin/main
   ```

3. **Resolve merge conflicts.** Pay special attention to files the player branch modifies:
   - `engines/unity/Assets/Scripts/Context.cs`
   - `engines/unity/Assets/Scripts/Game/Game.cs`
   - `engines/unity/Assets/Scripts/Game/GameLaunchBridge.cs`
   - `engines/unity/Assets/Scripts/LevelManager.cs`
   - `engines/unity/Assets/Scripts/Navigation/CytoidLabMenuController.cs`
   - `engines/unity/Assets/Scripts/Navigation/CytoidLab/CytoidLabHudController.cs`
   - `engines/unity/Assets/Scripts/Game/Notes/Note.cs`
   - `engines/unity/Assets/Scripts/Game/Notes/DragChildNote.cs`
   - `engines/unity/Assets/Scripts/Game/Notes/DragHeadNote.cs`
   - `engines/unity/Assets/Scripts/Editor/CytoidCoreBuild.cs`
   - `engines/unity/Assets/link.xml`

4. **Build the Windows player.**
   ```bash
   "C:\Program Files\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe" -batchmode -quit \
     -projectPath "E:/Code/Cytoid/engines/unity" \
     -executeMethod CytoidCoreBuild.BuildCytoidLabWindows64 \
     -logFile "E:/Code/Cytoid/engines/unity/Builds/CytoidLab/build.log"
   ```

5. **Run and verify.**
   - Launch `engines/unity/Builds/CytoidLab/CytoidLab.exe`.
   - If the level list is empty, import a `.cytoidlevel` file.
   - Check: level list visibility and scrolling, selection/highlighting, import, deletion, Auto toggle, hitsound toggle, seeking sync, pause/resume behavior, focus-loss behavior, HUD auto-hide.

6. **Package for sharing (optional).**
   - Remove `*_DoNotShip` and `*_BackUpThisFolder_ButDontShipItWithYourGame` folders.
   - Remove `build.log` and any test screenshots.
   - Zip the remaining files.

7. **Return to your original branch.**
   ```bash
   git checkout <your-branch>
   git stash pop
   ```

## Notes

- The player branch intentionally keeps PC-specific UX separate from the bridge-embedded Flutter flow.
- IL2CPP builds take several minutes; prefer batchmode for reproducible output.
- Player logs are written to `%USERPROFILE%\AppData\LocalLow\TigerHix\Cytoid Lab\Player.log`.
- Local player builds and zip packages are gitignored (see `.gitignore`).
