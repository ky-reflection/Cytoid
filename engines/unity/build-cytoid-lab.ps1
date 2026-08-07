#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe",
    [string]$ProjectPath,
    [string]$OutputPath,
    [switch]$Package,
    [switch]$KeepLog,
    [switch]$SkipClean,
    [switch]$Run
)

$ErrorActionPreference = "Stop"
$script:BuildTimings = [ordered]@{}
$totalStopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# Param defaults cannot use $PSScriptRoot; resolve paths at runtime.
if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = $PSScriptRoot
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $PSScriptRoot "Builds\CytoidLab"
}
$ProjectPath = (Resolve-Path $ProjectPath).Path
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)

$exeName = "CytoidLab"
$method = "CytoidCoreBuild.BuildCytoidLabWindows64"

function Write-Info($message) {
    Write-Host "[build-cytoid-lab] $message" -ForegroundColor Cyan
}

function Format-Duration([TimeSpan]$duration) {
    if ($duration.TotalHours -ge 1) {
        return $duration.ToString('h\:mm\:ss')
    }
    if ($duration.TotalMinutes -ge 1) {
        return $duration.ToString('m\:ss')
    }
    return "{0:N1}s" -f $duration.TotalSeconds
}

function Measure-Phase([string]$name, [scriptblock]$action) {
    $phaseStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        & $action
    } finally {
        $phaseStopwatch.Stop()
        $script:BuildTimings[$name] = $phaseStopwatch.Elapsed
        Write-Info ("{0}: {1}" -f $name, (Format-Duration $phaseStopwatch.Elapsed))
    }
}

function Write-TimingSummary {
    $totalStopwatch.Stop()
    $script:BuildTimings['Total'] = $totalStopwatch.Elapsed

    Write-Info '--- Build timing ---'
    foreach ($entry in $script:BuildTimings.GetEnumerator()) {
        if ($entry.Key -eq 'Total') { continue }
        Write-Info ("  {0,-22} {1}" -f $entry.Key, (Format-Duration $entry.Value))
    }
    Write-Info ("  {0,-22} {1}" -f 'Total', (Format-Duration $totalStopwatch.Elapsed))
}

try {
    Measure-Phase 'Prepare' {
        # Stop any running player that might lock build outputs.
        $running = Get-Process -Name $exeName -ErrorAction SilentlyContinue
        if ($running) {
            Write-Info "Stopping running $exeName.exe process(es)..."
            $running | Stop-Process -Force
            Start-Sleep -Seconds 2
        }

        if (-not (Test-Path $UnityPath)) {
            throw "Unity editor not found at: $UnityPath`nInstall Unity 6000.0.75f1 or pass -UnityPath."
        }

        if (-not (Test-Path (Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"))) {
            throw "Unity project not found at: $ProjectPath"
        }

        Write-Info "Project: $ProjectPath"
        Write-Info "Output:  $OutputPath"
        if ($SkipClean) {
            Write-Info "SkipClean: keeping previous build output (incremental IL2CPP friendly)."
        }

        $script:PreservedDataDir = $null
        $dataDir = Join-Path $OutputPath "data"
        if (-not $SkipClean -and (Test-Path $OutputPath)) {
            if (Test-Path $dataDir) {
                $script:PreservedDataDir = Join-Path ([System.IO.Path]::GetTempPath()) ("CytoidLab-data-" + [guid]::NewGuid().ToString("N"))
                Write-Info "Preserving ./data across clean: $script:PreservedDataDir"
                Move-Item -LiteralPath $dataDir -Destination $script:PreservedDataDir
            }
            Write-Info "Cleaning previous build output: $OutputPath"
            Remove-Item -Recurse -Force $OutputPath
        }

        New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
    }

    $logFile = "$OutputPath\build.log"

    Measure-Phase 'Unity IL2CPP build' {
        Write-Info "Starting Windows x64 IL2CPP build..."
        $unityArgs = @(
            "-batchmode",
            "-projectPath", $ProjectPath,
            "-executeMethod", $method,
            "-logFile", $logFile
        )

        $script:BuildStartedUtc = [DateTime]::UtcNow
        $unityProcess = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -PassThru
        if (-not $unityProcess) {
            throw "Failed to start Unity at: $UnityPath"
        }

        Write-Info "Unity PID $($unityProcess.Id) — script blocks here until Unity exits (IL2CPP can take several minutes)."

        $heartbeatSeconds = 30
        while (-not $unityProcess.HasExited) {
            if (-not $unityProcess.WaitForExit($heartbeatSeconds * 1000)) {
                Write-Info "Unity still running (PID $($unityProcess.Id))..."
            }
        }

        # Refresh exit code after the process handle signals completion.
        $unityProcess.WaitForExit()
        $script:UnityExitCode = $unityProcess.ExitCode
        Write-Info "Unity exited (PID $($unityProcess.Id), exit code: $script:UnityExitCode)."
    }

    $builtExe = Join-Path $OutputPath "CytoidLab.exe"
    $logTail = if (Test-Path $logFile) {
        Get-Content $logFile -Tail 40 | Out-String
    } else {
        "(log file missing)"
    }

    if ($script:UnityExitCode -ne 0) {
        throw @"
Build failed: Unity exited with code $($script:UnityExitCode).
Log: $logFile

$logTail
"@
    }

    if (-not (Test-Path $builtExe)) {
        throw @"
Build failed: $builtExe was not produced.
Unity exit code: $script:UnityExitCode
Log: $logFile

$logTail
"@
    }

    if (-not $SkipClean) {
        $exeWrittenUtc = (Get-Item $builtExe).LastWriteTimeUtc
        if ($exeWrittenUtc -lt $script:BuildStartedUtc) {
            throw @"
Build failed: $builtExe was not rebuilt this run (stale artifact from a previous build).
  exe last modified (UTC): $exeWrittenUtc
  build started (UTC):     $($script:BuildStartedUtc)
Log: $logFile

$logTail
"@
        }
    }

    Measure-Phase 'Post-process' {
        Write-Info "Removing non-shippable debug folders..."
        Get-ChildItem -Path $OutputPath -Recurse -Directory -ErrorAction SilentlyContinue | Where-Object {
            $_.Name -like "*DoNotShip*" -or
            $_.Name -like "*BackUpThisFolder_ButDontShipItWithYourGame*"
        } | Remove-Item -Recurse -Force

        if ($script:PreservedDataDir -and (Test-Path -LiteralPath $script:PreservedDataDir)) {
            $restoreTarget = Join-Path $OutputPath "data"
            if (Test-Path -LiteralPath $restoreTarget) {
                Remove-Item -LiteralPath $restoreTarget -Recurse -Force
            }
            Move-Item -LiteralPath $script:PreservedDataDir -Destination $restoreTarget
            $script:PreservedDataDir = $null
            Write-Info "Restored ./data into build output."
        }

        if (-not $KeepLog -and (Test-Path $logFile)) {
            Remove-Item -Force $logFile
        }
    }

    if ($Package) {
        Measure-Phase 'Package zip' {
            $zipPath = Join-Path $PSScriptRoot "Builds\CytoidLab.zip"
            if (Test-Path $zipPath) {
                Remove-Item -Force $zipPath
            }
            Write-Info "Packaging $zipPath (excluding ./data) ..."
            $stageDir = Join-Path ([System.IO.Path]::GetTempPath()) ("CytoidLab-zip-" + [guid]::NewGuid().ToString("N"))
            try {
                New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
                Get-ChildItem -LiteralPath $OutputPath -Force | Where-Object {
                    $_.Name -ne 'data'
                } | ForEach-Object {
                    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $stageDir $_.Name) -Recurse -Force
                }
                Compress-Archive -Path "$stageDir\*" -DestinationPath $zipPath
            } finally {
                if (Test-Path -LiteralPath $stageDir) {
                    Remove-Item -LiteralPath $stageDir -Recurse -Force
                }
            }
            Write-Info "Package ready: $zipPath"
        }
    }

    Write-Info "Build complete: $OutputPath"
    Write-TimingSummary

    if ($Run) {
        Write-Info "Launching $builtExe"
        Start-Process -FilePath $builtExe -WorkingDirectory $OutputPath
    }
}
catch {
    if ($script:PreservedDataDir -and (Test-Path -LiteralPath $script:PreservedDataDir)) {
        try {
            $fallback = Join-Path $OutputPath "data"
            New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
            if (-not (Test-Path -LiteralPath $fallback)) {
                Move-Item -LiteralPath $script:PreservedDataDir -Destination $fallback
                Write-Info "Build failed; restored ./data to $fallback"
            }
        } catch {
            Write-Info "Build failed; preserved ./data left at: $script:PreservedDataDir"
        }
    }
    $totalStopwatch.Stop()
    Write-Error $_
    if ($script:BuildTimings.Count -gt 0) {
        Write-TimingSummary
    }
    exit 1
}
