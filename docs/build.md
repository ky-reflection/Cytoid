# Build Guide

This repository has two primary build products, one derived smoke-test APK, and
one cross-repository app build. They are easy to confuse because all of them
involve the Unity project.

| Goal | Built here? | Primary entry point | Output |
|------|-------------|---------------------|--------|
| Unity Android plugin artifacts | Yes | `Cytoid -> Build Android Plugin Artifacts` | `cytoid-unity-core.aar` and dependency AARs |
| Flutter example APK | Yes, for integration smoke testing | Build Android artifacts, then `flutter build apk --release` | `app-release.apk` |
| Production Cytoid APK | No; final assembly belongs to sibling `cytoid_flutter` | `cytoid_flutter/scripts/export_unity_android.sh`, then Flutter | Production app APK |
| Cytoid Lab | Yes, Windows x64 only | `build-cytoid-lab.ps1` | `CytoidLab.exe`, optionally `CytoidLab.zip` |

The Unity project is `engines/unity/` and requires Unity **6000.0.75f1**.
Generated exports, artifacts, APKs, logs, and Lab builds are local-only and
must not be committed.

## Prerequisites

Install the Unity editor version recorded in
`engines/unity/ProjectSettings/ProjectVersion.txt` and the modules needed by
the target:

- Android: Android Build Support, SDK, NDK, OpenJDK, Flutter, and `bash` in
  `PATH` on Windows (Git for Windows is sufficient). The Unity menu and
  batchmethod run `flutter_plugin/tool/build_unity_aar.sh` after export.
- Cytoid Lab: Windows Build Support (IL2CPP) plus a Visual Studio C++ toolchain
  and Windows SDK supported by Unity.
- Optional licensed storyboard effects: install the maintainer vendor bundle
  described in [vendor.md](vendor.md). In-repository fallbacks are used when it
  is absent.

## Android Unity artifacts

This is the Android product owned by this repository. It is a Unity-as-Library
export for a Flutter host, **not an installable APK**.

The export uses:

- scenes: `CoreHostBootstrap` and `Game`;
- define: `CYTOID_FLUTTER_HOST`;
- ABI: ARM64;
- library application ID: `com.example.cytoid_flutter.unity`;
- plugin namespace: `org.cytoid.gamecore`.

### Unity Editor

Open `engines/unity/`, then select:

`Cytoid -> Build Android Plugin Artifacts`

The menu exports the Gradle project and packages its AARs in one operation.

### Windows batchmode

Run from the repository root:

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe"
$Project = (Resolve-Path .\engines\unity).Path
$LogDir = Join-Path $Project "flutter_plugin\.cytoid_game_core\build"
New-Item -ItemType Directory -Force $LogDir | Out-Null

& $Unity -batchmode `
  -projectPath $Project `
  -executeMethod CytoidCoreBuild.ExportAndroidLibraryForFlutter `
  -logFile (Join-Path $LogDir "unity-android.log")

if ($LASTEXITCODE -ne 0) { throw "Unity Android export failed: $LASTEXITCODE" }
```

Do **not** add `-quit`. Switching build targets can trigger script compilation;
the build resumes through `EditorApplication.update` and exits batchmode itself.
Passing `-quit` can exit Unity before any artifact is produced.

### Outputs

```text
engines/unity/flutter_plugin/.cytoid_game_core/exports/android/unityLibrary/
engines/unity/flutter_plugin/.cytoid_game_core/artifacts/unity/android/
  cytoid-unity-core.aar
  *.aar
```

The first build can take 5–15+ minutes because it includes an ARM64 IL2CPP
export and Gradle packaging.

To re-package an existing Unity export without running Unity again:

```powershell
Push-Location .\engines\unity\flutter_plugin
try {
  bash ./tool/build_unity_aar.sh
} finally {
  Pop-Location
}
```

The Bash script is the tracked, cross-platform packaging entry point and is also
what the Unity buildmethod invokes.

## Flutter example APK

The example APK proves that the Flutter host links the real Unity AAR. It is a
development/smoke-test app with application ID `com.example.cytoid_flutter`; it
is **not the production Cytoid APK**.

After building the Android artifacts, create the local artifact manifest and
verify the artifact layout before building the APK:

```powershell
Push-Location .\engines\unity\flutter_plugin
try {
  $env:MANIFEST_PLATFORM = "android"
  $env:MANIFEST_VERSION = "0.1.0"
  bash ./tool/write_manifest.sh
  bash ./android/scripts/verify_artifacts.sh

  Push-Location .\example
  try {
    flutter clean
    flutter pub get
    flutter build apk --release
  } finally {
    Pop-Location
  }
} finally {
  Pop-Location
}
```

Output:

```text
engines/unity/flutter_plugin/example/build/app/outputs/flutter-apk/app-release.apk
```

If the AAR is absent, the example intentionally builds with the mock engine.
For a Unity integration smoke test, that is not a valid pass: run
`android/scripts/verify_artifacts.sh` before building and inspect the app's
reported engine mode on a real device.

## Production Cytoid APK

The production Android application is assembled by the separate
`cytoid_flutter` repository. This core repository does not contain a production
APK buildmethod, app signing configuration, or release packaging flow. Do not
use the plugin example APK as a release candidate.

The expected sibling checkout is:

```text
<workspace>/Cytoid/          # this repository
<workspace>/cytoid_flutter/ # production Flutter application
```

Follow `cytoid_flutter/docs/unity-android-export.md`. The maintained flow is:

```bash
cd ../cytoid_flutter
export UNITY_PATH="/path/to/Unity"
./scripts/export_unity_android.sh
flutter pub get
flutter build apk --release
```

The export script places the Unity project at
`cytoid_flutter/android/unityLibrary/`; Flutter then owns the final APK,
application ID (`me.tigerhix.cytoid`), versioning, and signing. The sibling
repository is not present in every core-development workspace, so its own
documentation and scripts are authoritative for release flags and output.

## Cytoid Lab (Windows x64)

The recommended entry point is the PowerShell wrapper because it validates the
Unity process, detects stale output, removes non-shippable IL2CPP directories,
and can create the release zip:

```powershell
.\engines\unity\build-cytoid-lab.ps1 -KeepLog -Run
```

Release package:

```powershell
.\engines\unity\build-cytoid-lab.ps1 -Package -KeepLog
```

Common options:

| Option | Effect |
|--------|--------|
| `-UnityPath <path>` | Use a non-default Unity installation |
| `-OutputPath <path>` | Override the player output directory |
| `-SkipClean` | Keep previous output for a faster incremental IL2CPP build |
| `-KeepLog` | Preserve `build.log`; otherwise remove it after success |
| `-Package` | Create `engines/unity/Builds/CytoidLab.zip` |
| `-Run` | Launch the built player after success |

The default build is clean. It stops a running `CytoidLab.exe` before replacing
the output because the player can lock its files.

Alternative entries:

- Unity Editor: `Cytoid -> Build Cytoid Lab (Windows x64)`.
- Direct batchmode:

  ```powershell
  $Project = (Resolve-Path .\engines\unity).Path
  $Output = Join-Path $Project "Builds\CytoidLab"
  New-Item -ItemType Directory -Force $Output | Out-Null

  & "C:\Program Files\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe" `
    -batchmode `
    -projectPath $Project `
    -executeMethod CytoidCoreBuild.BuildCytoidLabWindows64 `
    -logFile (Join-Path $Output "build.log")
  ```

Direct batchmode also must not use `-quit`. Prefer the wrapper for release
builds; the direct Editor/batchmethod entries do not provide its cleanup,
stale-output validation, zip packaging, or launch options.

Default outputs:

```text
engines/unity/Builds/CytoidLab/CytoidLab.exe
engines/unity/Builds/CytoidLab/build.log       # only with -KeepLog
engines/unity/Builds/CytoidLab.zip             # only with -Package
```

The player uses application ID `org.cytoid.lab`, product name `Cytoid Lab`, a
resizable 1280x720 default window, and the `Bootstrapper`, `Navigation`, and
`Game` scenes. More runtime and release details are in
[lab/guide.md](lab/guide.md) and [lab/releases.md](lab/releases.md).

## Troubleshooting

| Symptom | Check |
|---------|-------|
| Unity exits successfully but produces nothing | Remove `-quit`; both buildmethods exit explicitly after their async continuation completes. |
| Android export finishes, then packaging reports `bash` missing | Put Git Bash or another compatible Bash in `PATH`, then retry or re-package the completed export with `build_unity_aar.sh`. |
| Example reports mock mode | Confirm `cytoid-unity-core.aar` is non-empty, write `manifest.android.json`, run `verify_artifacts.sh`, then `flutter clean`. |
| Android IL2CPP/Gradle cannot find Java, SDK, or NDK | Add Unity's Android Build Support modules; packaging prefers Unity's OpenJDK and Flutter's Gradle wrapper. |
| Lab output is locked or stale | Close Lab, omit `-SkipClean`, and use `build-cytoid-lab.ps1`; it stops the player and validates the rebuilt executable timestamp. |
| A build target switch appears idle | Allow Unity to finish domain reload/script compilation. The batchmethods have a 10-minute compilation timeout. |
