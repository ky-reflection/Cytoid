# Cytoid Lab ffmpeg-lite

This folder contains a reproducible Windows x64 build recipe for the optional
Cytoid Lab video repair helper. It does not commit FFmpeg binaries.

The build is intentionally narrow:

- MP4/MOV input and MP4 output
- H.264 decode
- `libx264` encode
- file protocol only
- no `--enable-nonfree`

The produced binary is GPL-compatible and intended for Cytoid Lab's repair
command:

```powershell
ffmpeg.exe -i input.mp4 -an -c:v libx264 -pix_fmt yuv420p -profile:v baseline -level 3.1 -r 60 -g 60 -keyint_min 60 -sc_threshold 0 -bf 0 -movflags +faststart output.mp4
```

## Prerequisites

Install MSYS2, then run from PowerShell:

```powershell
.\tools\ffmpeg-lite\build-windows-msys2.ps1 -InstallDependencies
```

The script expects MSYS2 at `C:\msys64` by default. Pass `-Msys2Root` if your
installation is elsewhere.

For local verification, the wrapper can also use Git Bash plus an existing
MinGW-w64 toolchain:

```powershell
.\tools\ffmpeg-lite\build-windows-msys2.ps1 `
  -BashPath D:\Software\Git\bin\bash.exe `
  -ToolchainBin D:\Software\StrawberryPerl\c\bin `
  -Jobs 32
```

The wrapper clears global compiler environment variables such as
`C_INCLUDE_PATH`, `INCLUDE`, `LIB`, and `PKG_CONFIG_PATH` before building so
local LLVM/MSVC/CUDA settings do not leak into the MinGW build.

## Build

```powershell
.\tools\ffmpeg-lite\build-windows-msys2.ps1
```

Default output:

```text
tools/ffmpeg-lite/artifacts/windows-x64/bin/ffmpeg.exe
```

Build cache:

```text
tools/ffmpeg-lite/.build/
```

Both folders are gitignored.

## Install Into A Local Cytoid Lab Build

Cytoid Lab checks for an optional bundled helper at:

```text
<CytoidLab.exe directory>/ffmpeg-lite/ffmpeg.exe
```

After building Cytoid Lab, copy the helper into the build output:

```powershell
.\tools\ffmpeg-lite\build-windows-msys2.ps1 `
  -CopyTo .\engines\unity\Builds\CytoidLab\ffmpeg-lite
```

Or have the Cytoid Lab build script include an existing helper:

```powershell
.\engines\unity\build-cytoid-lab.ps1 `
  -FfmpegLitePath .\tools\ffmpeg-lite\artifacts\windows-x64\bin\ffmpeg.exe `
  -Package
```

If this file is absent, Cytoid Lab falls back to saved user selection, PATH, and
then the file picker.

## Version Pins

Defaults:

- FFmpeg: `n8.1`
- x264: `stable`

Override when needed:

```powershell
.\tools\ffmpeg-lite\build-windows-msys2.ps1 -FfmpegRef n8.1 -X264Ref stable
```

## License Notes

This build enables GPL and `libx264`; do not enable FFmpeg's `nonfree` option.
FFmpeg reports this configuration as GPL version 2 or later, which can be
distributed under GPL-3.0 with Cytoid. When distributing a binary produced by
this script, include the generated `licenses/` folder and `build-info.txt`, and
keep corresponding source access for the exact FFmpeg and x264 refs used.
