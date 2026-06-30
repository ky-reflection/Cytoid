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

        if (-not $SkipClean -and (Test-Path $OutputPath)) {
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
            "-quit",
            "-projectPath", $ProjectPath,
            "-executeMethod", $method,
            "-logFile", $logFile
        )

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
    if (-not (Test-Path $builtExe)) {
        $logTail = if (Test-Path $logFile) {
            Get-Content $logFile -Tail 40 | Out-String
        } else {
            "(log file missing)"
        }
        throw @"
Build failed: $builtExe was not produced.
Unity exit code: $script:UnityExitCode
Log: $logFile

$logTail
"@
    }

    if ($script:UnityExitCode -ne 0) {
        Write-Warning "Unity reported exit code $($script:UnityExitCode), but $builtExe exists; treating build as successful."
    }

    Measure-Phase 'Post-process' {
        Write-Info "Removing non-shippable debug folders..."
        Get-ChildItem -Path $OutputPath -Recurse -Directory -ErrorAction SilentlyContinue | Where-Object {
            $_.Name -like "*DoNotShip*" -or
            $_.Name -like "*BackUpThisFolder_ButDontShipItWithYourGame*"
        } | Remove-Item -Recurse -Force

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
            Write-Info "Packaging $zipPath ..."
            Compress-Archive -Path "$OutputPath\*" -DestinationPath $zipPath
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
    $totalStopwatch.Stop()
    Write-Error $_
    if ($script:BuildTimings.Count -gt 0) {
        Write-TimingSummary
    }
    exit 1
}
