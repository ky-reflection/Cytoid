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

        MarkCheckedNow();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[CytoidLab] Update check HTTP error: {request.error}");
            return null;
        }

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
                if (assets == null) continue;

                string zipUrl = null;
                long zipSize = 0;
                foreach (var assetToken in assets)
                {
                    if (assetToken is not JObject asset) continue;
                    var name = asset.Value<string>("name");
                    if (!string.Equals(name, ZipAssetName, StringComparison.OrdinalIgnoreCase)) continue;
                    zipUrl = asset.Value<string>("browser_download_url");
                    zipSize = asset.Value<long?>("size") ?? 0;
                    break;
                }

                if (string.IsNullOrEmpty(zipUrl)) continue;

                return new UpdateInfo
                {
                    Version = version,
                    TagName = tag,
                    ZipUrl = zipUrl,
                    HtmlUrl = release.Value<string>("html_url") ?? ReleasesPageUrl,
                    ZipSizeBytes = zipSize,
                };
            }
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
        var scriptPath = Path.Combine(workDir, "apply-update.cmd");

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
            var startInfo = new ProcessStartInfo
            {
                FileName = scriptPath,
                Arguments = $"\"{installDir}\" \"{zipPath}\" {pid} \"{exePath}\"",
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
        // %1 install dir, %2 zip path, %3 pid, %4 exe path
        return @"@echo off
setlocal
set ""INSTALL=%~1""
set ""ZIP=%~2""
set ""PID=%~3""
set ""EXE=%~4""
set ""EXTRACT=%TEMP%\CytoidLabUpdate\extract""
echo Waiting for Cytoid Lab (PID %PID%) to exit...
:wait
tasklist /FI ""PID eq %PID%"" 2>NUL | find ""%PID%"" >NUL
if not errorlevel 1 (
  timeout /t 1 /nobreak >NUL
  goto wait
)
if exist ""%EXTRACT%"" rmdir /s /q ""%EXTRACT%""
mkdir ""%EXTRACT%"" >NUL 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command ""Expand-Archive -LiteralPath '%ZIP%' -DestinationPath '%EXTRACT%' -Force""
if errorlevel 1 (
  echo Expand failed. Opening releases page...
  start """" ""https://github.com/ky-reflection/Cytoid/releases""
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -Command ""Copy-Item -LiteralPath '%EXTRACT%\*' -Destination '%INSTALL%' -Recurse -Force""
if errorlevel 1 (
  echo Copy failed. Opening releases page...
  start """" ""https://github.com/ky-reflection/Cytoid/releases""
  exit /b 1
)
rmdir /s /q ""%EXTRACT%"" >NUL 2>&1
del /f /q ""%ZIP%"" >NUL 2>&1
start """" ""%EXE%""
exit /b 0
";
    }
}
