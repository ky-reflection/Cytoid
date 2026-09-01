using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

/// <summary>
/// Checks GitHub Releases for a newer Cytoid Lab build and applies an in-place update on Windows.
/// </summary>
public static class CytoidLabUpdater
{
    public const string ReleasesApiUrl = "https://api.github.com/repos/ky-reflection/Cytoid/releases?per_page=10";
    public const string ReleasesPageUrl = "https://github.com/ky-reflection/Cytoid/releases";
    private const string TagPrefix = "cytoid-lab-v";
    private const string ZipAssetName = "CytoidLab.zip";
    private const string LastCheckUtcKey = "CytoidLab.UpdateLastCheckUtc";
    private const string DismissedVersionKey = "CytoidLab.UpdateDismissedVersion";
    private static readonly TimeSpan MinCheckInterval = TimeSpan.FromHours(6);

    public sealed class UpdateInfo
    {
        public string Version;
        public string TagName;
        public string ZipUrl;
        public string HtmlUrl;
        public long ZipSizeBytes;
    }

    /// <summary>Newest published Lab release newer than this build, or null.</summary>
    public static UpdateInfo Available { get; private set; }

    public static bool IsSupported =>
        Application.platform == RuntimePlatform.WindowsPlayer ||
        Application.platform == RuntimePlatform.WindowsEditor;

    public static async UniTask<UpdateInfo> CheckForUpdateAsync(bool force = false)
    {
        if (!IsSupported) return null;

        if (!force && !ShouldCheckNow()) return null;

        using var request = UnityWebRequest.Get(ReleasesApiUrl);
        request.SetRequestHeader("Accept", "application/vnd.github+json");
        request.SetRequestHeader("User-Agent", $"CytoidLab/{CytoidLabVersion.Version}");
        request.timeout = 20;

        try
        {
            await request.SendWebRequest();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CytoidLab] Update check failed: {e.Message}");
            return null;
        }

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[CytoidLab] Update check HTTP error: {request.error}");
            return null;
        }

        // Only cache a successful contact. A 403/timeout must not hide updates for 6 hours.

        try
        {
            var releases = JArray.Parse(request.downloadHandler.text);
            foreach (var token in releases)
            {
                if (token is not JObject release) continue;
                if (release.Value<bool?>("draft") == true) continue;
                if (release.Value<bool?>("prerelease") == true) continue;

                var tag = release.Value<string>("tag_name");
                if (string.IsNullOrEmpty(tag) || !tag.StartsWith(TagPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var version = tag.Substring(TagPrefix.Length);
                if (!IsNewerVersion(version, CytoidLabVersion.Version)) continue;

                var assets = release["assets"] as JArray;
                string zipUrl = null;
                long zipSize = 0;
                if (assets != null)
                {
                    foreach (var assetToken in assets)
                    {
                        if (assetToken is not JObject asset) continue;
                        var name = asset.Value<string>("name");
                        if (!string.Equals(name, ZipAssetName, StringComparison.OrdinalIgnoreCase)) continue;
                        zipUrl = asset.Value<string>("browser_download_url");
                        zipSize = asset.Value<long?>("size") ?? 0;
                        break;
                    }
                }

                MarkCheckedNow();
                Available = new UpdateInfo
                {
                    Version = version,
                    TagName = tag,
                    ZipUrl = zipUrl,
                    HtmlUrl = release.Value<string>("html_url") ?? ReleasesPageUrl,
                    ZipSizeBytes = zipSize,
                };
                return Available;
            }

            MarkCheckedNow();
            Available = null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CytoidLab] Failed to parse releases: {e.Message}");
        }

        return null;
    }

    public static bool IsDismissed(UpdateInfo info)
    {
        if (info == null) return true;
        return string.Equals(
            PlayerPrefs.GetString(DismissedVersionKey, string.Empty),
            info.Version,
            StringComparison.OrdinalIgnoreCase);
    }

    public static void Dismiss(UpdateInfo info)
    {
        if (info == null) return;
        PlayerPrefs.SetString(DismissedVersionKey, info.Version);
        PlayerPrefs.Save();
    }

    public static async UniTask<bool> DownloadAndApplyAsync(UpdateInfo info, Action<string> onStatus = null)
    {
        if (!IsSupported || info == null || string.IsNullOrEmpty(info.ZipUrl)) return false;

        var installDir = CytoidLabPaths.GetInstallDirectory();
        if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
        {
            onStatus?.Invoke("Update failed: install directory not found.");
            return false;
        }

        var exePath = Path.Combine(installDir, "CytoidLab.exe");
        if (!File.Exists(exePath))
        {
            onStatus?.Invoke("Update failed: CytoidLab.exe not found (Editor?). Open the release page instead.");
            Application.OpenURL(info.HtmlUrl ?? ReleasesPageUrl);
            return false;
        }

        var workDir = Path.Combine(Path.GetTempPath(), "CytoidLabUpdate");
        Directory.CreateDirectory(workDir);
        var zipPath = Path.Combine(workDir, ZipAssetName);
        var scriptPath = Path.Combine(workDir, "apply-update.ps1");

        onStatus?.Invoke($"Downloading Cytoid Lab v{info.Version}...");

        using (var request = UnityWebRequest.Get(info.ZipUrl))
        {
            request.SetRequestHeader("User-Agent", $"CytoidLab/{CytoidLabVersion.Version}");
            request.timeout = 600;
            try
            {
                await request.SendWebRequest();
            }
            catch (Exception e)
            {
                onStatus?.Invoke($"Download failed: {e.Message}");
                return false;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                onStatus?.Invoke($"Download failed: {request.error}");
                return false;
            }

            File.WriteAllBytes(zipPath, request.downloadHandler.data);
        }

        onStatus?.Invoke("Preparing updater...");
        File.WriteAllText(scriptPath, BuildUpdaterScript(), Encoding.ASCII);

        var pid = Process.GetCurrentProcess().Id;
        try
        {
            // powershell.exe -File: paths with spaces stay one argument. Do not name
            // a parameter $PID — that shadows PowerShell's automatic variable.
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments =
                    "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath +
                    "\" -InstallDir \"" + installDir +
                    "\" -ZipPath \"" + zipPath +
                    "\" -ProcessId " + pid +
                    " -ExePath \"" + exePath + "\"",
                UseShellExecute = true,
                WorkingDirectory = workDir,
                WindowStyle = ProcessWindowStyle.Minimized,
            };
            Process.Start(startInfo);
        }
        catch (Exception e)
        {
            onStatus?.Invoke($"Failed to start updater: {e.Message}");
            Application.OpenURL(info.HtmlUrl ?? ReleasesPageUrl);
            return false;
        }

        onStatus?.Invoke("Updater started — Cytoid Lab will restart.");
        await UniTask.Delay(400);
        Application.Quit();
        return true;
    }

    public static bool IsNewerVersion(string candidate, string current)
    {
        if (!TryParseVersion(candidate, out var candMaj, out var candMin, out var candPatch)) return false;
        if (!TryParseVersion(current, out var curMaj, out var curMin, out var curPatch)) return true;

        if (candMaj != curMaj) return candMaj > curMaj;
        if (candMin != curMin) return candMin > curMin;
        return candPatch > curPatch;
    }

    private static bool ShouldCheckNow()
    {
        var raw = PlayerPrefs.GetString(LastCheckUtcKey, string.Empty);
        if (!DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var last))
        {
            return true;
        }

        return DateTime.UtcNow - last >= MinCheckInterval;
    }

    private static void MarkCheckedNow()
    {
        PlayerPrefs.SetString(LastCheckUtcKey, DateTime.UtcNow.ToString("o"));
        PlayerPrefs.Save();
    }

    private static bool TryParseVersion(string version, out int major, out int minor, out int patch)
    {
        major = minor = patch = 0;
        if (string.IsNullOrWhiteSpace(version)) return false;

        var match = Regex.Match(version.Trim(), @"^(\d+)\.(\d+)\.(\d+)");
        if (!match.Success) return false;

        return int.TryParse(match.Groups[1].Value, out major)
               && int.TryParse(match.Groups[2].Value, out minor)
               && int.TryParse(match.Groups[3].Value, out patch);
    }

    private static string BuildUpdaterScript()
    {
        // ASCII-only. Copy-Item -LiteralPath '...*' does not expand wildcards, and
        // Copy-Item -Recurse into an existing CytoidLab_Data nests a second _Data.
        // robocopy /E overlays files and /XD data leaves portable ./data alone.
        return @"param(
  [Parameter(Mandatory=$true)][string]$InstallDir,
  [Parameter(Mandatory=$true)][string]$ZipPath,
  [Parameter(Mandatory=$true)][int]$ProcessId,
  [Parameter(Mandatory=$true)][string]$ExePath
)
$ErrorActionPreference = 'Stop'
$extract = Join-Path $env:TEMP 'CytoidLabUpdate\extract'
$releases = 'https://github.com/ky-reflection/Cytoid/releases'
try {
  Write-Host ""Waiting for Cytoid Lab (PID $ProcessId) to exit...""
  $deadline = (Get-Date).AddMinutes(2)
  while (Get-Process -Id $ProcessId -ErrorAction SilentlyContinue) {
    if ((Get-Date) -gt $deadline) { throw ""Timed out waiting for PID $ProcessId"" }
    Start-Sleep -Seconds 1
  }
  Start-Sleep -Seconds 1
  if (Test-Path -LiteralPath $extract) { Remove-Item -LiteralPath $extract -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $extract | Out-Null
  Expand-Archive -LiteralPath $ZipPath -DestinationPath $extract -Force
  $children = @(Get-ChildItem -LiteralPath $extract -Force)
  if ($children.Count -eq 1 -and $children[0].PSIsContainer) {
    $nestedExe = Join-Path $children[0].FullName 'CytoidLab.exe'
    if (Test-Path -LiteralPath $nestedExe) { $extract = $children[0].FullName }
  }
  $robo = Start-Process -FilePath 'robocopy.exe' -ArgumentList @(
    $extract, $InstallDir, '/E', '/XD', 'data', '/NFL', '/NDL', '/NJH', '/NJS', '/R:3', '/W:1'
  ) -Wait -PassThru -NoNewWindow
  if ($robo.ExitCode -ge 8) { throw ""robocopy failed with exit $($robo.ExitCode)"" }
  Remove-Item -LiteralPath $extract -Recurse -Force -ErrorAction SilentlyContinue
  Remove-Item -LiteralPath $ZipPath -Force -ErrorAction SilentlyContinue
  Start-Process -FilePath $ExePath -WorkingDirectory $InstallDir
} catch {
  Write-Host $_.Exception.Message
  Start-Process $releases
  exit 1
}
exit 0
";
    }
}
