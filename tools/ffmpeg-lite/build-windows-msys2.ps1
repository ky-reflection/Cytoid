#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$Msys2Root = "C:\msys64",
    [string]$BashPath,
    [string]$ToolchainBin,
    [string]$OutputPath,
    [string]$BuildRoot,
    [string]$FfmpegRef = "n8.1",
    [string]$X264Ref = "stable",
    [int]$Jobs = 0,
    [switch]$InstallDependencies,
    [string]$CopyTo
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $PSScriptRoot "artifacts\windows-x64"
}
if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $BuildRoot = Join-Path $PSScriptRoot ".build"
}

function Write-Info($message) {
    Write-Host "[ffmpeg-lite] $message" -ForegroundColor Cyan
}

function Convert-ToMsysPath([string]$Path) {
    $full = [System.IO.Path]::GetFullPath($Path).Replace('\', '/')
    if ($full.Length -lt 3 -or $full[1] -ne ':') {
        throw "Cannot convert path to MSYS path: $Path"
    }

    return "/" + $full.Substring(0, 1).ToLowerInvariant() + $full.Substring(2)
}

function Convert-ToNativeSlashPath([string]$Path) {
    return [System.IO.Path]::GetFullPath($Path).Replace('\', '/')
}

$bash = if (-not [string]::IsNullOrWhiteSpace($BashPath)) {
    [System.IO.Path]::GetFullPath($BashPath)
} else {
    Join-Path $Msys2Root "usr\bin\bash.exe"
}
if (-not (Test-Path $bash)) {
    throw "bash was not found at: $bash`nInstall MSYS2, pass -Msys2Root, or pass -BashPath."
}

$script = Join-Path $PSScriptRoot "build-windows-msys2.sh"
if (-not (Test-Path $script)) {
    throw "Missing build script: $script"
}

if ($InstallDependencies) {
    if (-not $bash.StartsWith((Join-Path $Msys2Root "usr"), [StringComparison]::OrdinalIgnoreCase)) {
        throw "-InstallDependencies requires MSYS2 bash under -Msys2Root."
    }

    Write-Info "Installing MSYS2 dependencies..."
    $packages = @(
        "git",
        "make",
        "diffutils",
        "pkgconf",
        "nasm",
        "yasm",
        "mingw-w64-ucrt-x86_64-gcc",
        "mingw-w64-ucrt-x86_64-pkgconf"
    )
    $installCommand = "pacman --noconfirm -S --needed " + ($packages -join " ")
    & $bash -lc $installCommand
    if ($LASTEXITCODE -ne 0) {
        throw "pacman failed with exit code $LASTEXITCODE"
    }
}

$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$BuildRoot = [System.IO.Path]::GetFullPath($BuildRoot)
New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
New-Item -ItemType Directory -Force -Path $BuildRoot | Out-Null

$oldEnv = @{
    MSYSTEM = $env:MSYSTEM
    PATH = $env:PATH
    CPATH = $env:CPATH
    C_INCLUDE_PATH = $env:C_INCLUDE_PATH
    CPLUS_INCLUDE_PATH = $env:CPLUS_INCLUDE_PATH
    OBJC_INCLUDE_PATH = $env:OBJC_INCLUDE_PATH
    INCLUDE = $env:INCLUDE
    LIB = $env:LIB
    LIBRARY_PATH = $env:LIBRARY_PATH
    PKG_CONFIG_PATH = $env:PKG_CONFIG_PATH
    CYTOID_FFMPEG_LITE_PREFIX = $env:CYTOID_FFMPEG_LITE_PREFIX
    CYTOID_FFMPEG_LITE_BUILD_ROOT = $env:CYTOID_FFMPEG_LITE_BUILD_ROOT
    CYTOID_FFMPEG_LITE_FFMPEG_REF = $env:CYTOID_FFMPEG_LITE_FFMPEG_REF
    CYTOID_FFMPEG_LITE_X264_REF = $env:CYTOID_FFMPEG_LITE_X264_REF
    CYTOID_FFMPEG_LITE_JOBS = $env:CYTOID_FFMPEG_LITE_JOBS
    CYTOID_FFMPEG_LITE_MAKE = $env:CYTOID_FFMPEG_LITE_MAKE
    CYTOID_FFMPEG_LITE_PKG_CONFIG = $env:CYTOID_FFMPEG_LITE_PKG_CONFIG
}

try {
    $env:MSYSTEM = "UCRT64"
    foreach ($name in @(
        "CPATH",
        "C_INCLUDE_PATH",
        "CPLUS_INCLUDE_PATH",
        "OBJC_INCLUDE_PATH",
        "INCLUDE",
        "LIB",
        "LIBRARY_PATH",
        "PKG_CONFIG_PATH"
    )) {
        Remove-Item "Env:\$name" -ErrorAction SilentlyContinue
    }

    if (-not [string]::IsNullOrWhiteSpace($ToolchainBin)) {
        $toolchainPath = [System.IO.Path]::GetFullPath($ToolchainBin)
        if (-not (Test-Path $toolchainPath -PathType Container)) {
            throw "Toolchain directory was not found: $toolchainPath"
        }

        $env:PATH = $toolchainPath + [System.IO.Path]::PathSeparator + $env:PATH
    }

    $env:CYTOID_FFMPEG_LITE_PREFIX = Convert-ToNativeSlashPath $OutputPath
    $env:CYTOID_FFMPEG_LITE_BUILD_ROOT = Convert-ToMsysPath $BuildRoot
    $env:CYTOID_FFMPEG_LITE_FFMPEG_REF = $FfmpegRef
    $env:CYTOID_FFMPEG_LITE_X264_REF = $X264Ref
    if ($Jobs -gt 0) {
        $env:CYTOID_FFMPEG_LITE_JOBS = [string]$Jobs
    } else {
        Remove-Item Env:\CYTOID_FFMPEG_LITE_JOBS -ErrorAction SilentlyContinue
    }

    $msysScript = Convert-ToMsysPath $script
    Write-Info "Output: $OutputPath"
    Write-Info "Build:  $BuildRoot"
    & $bash -lc "bash '$msysScript'"
    if ($LASTEXITCODE -ne 0) {
        throw "ffmpeg-lite build failed with exit code $LASTEXITCODE"
    }
} finally {
    foreach ($entry in $oldEnv.GetEnumerator()) {
        if ($null -eq $entry.Value) {
            Remove-Item "Env:\$($entry.Key)" -ErrorAction SilentlyContinue
        } else {
            Set-Item "Env:\$($entry.Key)" $entry.Value
        }
    }
}

$ffmpegExe = Join-Path $OutputPath "bin\ffmpeg.exe"
if (-not (Test-Path $ffmpegExe)) {
    throw "Build completed but ffmpeg.exe was not found at: $ffmpegExe"
}

if (-not [string]::IsNullOrWhiteSpace($CopyTo)) {
    $copyDirectory = [System.IO.Path]::GetFullPath($CopyTo)
    New-Item -ItemType Directory -Force -Path $copyDirectory | Out-Null
    Copy-Item -LiteralPath $ffmpegExe -Destination (Join-Path $copyDirectory "ffmpeg.exe") -Force
    Copy-Item -LiteralPath (Join-Path $OutputPath "build-info.txt") -Destination (Join-Path $copyDirectory "build-info.txt") -Force
    $licenseDestination = Join-Path $copyDirectory "licenses"
    if (Test-Path $licenseDestination) {
        Remove-Item -LiteralPath $licenseDestination -Recurse -Force
    }
    Copy-Item -LiteralPath (Join-Path $OutputPath "licenses") -Destination $licenseDestination -Recurse -Force
    Write-Info "Copied ffmpeg-lite to: $copyDirectory"
}

$item = Get-Item $ffmpegExe
Write-Info ("ffmpeg-lite ready: {0} ({1:N1} MB)" -f $item.FullName, ($item.Length / 1MB))
