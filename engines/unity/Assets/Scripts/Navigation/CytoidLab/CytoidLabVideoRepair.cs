using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

internal sealed class CytoidLabVideoStallReport
{
    public string VideoPath { get; set; }
    public string VideoId { get; set; }
    public string Message { get; set; }
}

internal sealed class CytoidLabVideoRepairResult
{
    public bool Success { get; set; }
    public int RepairedCount { get; set; }
    public string BackupDirectory { get; set; }
    public string Message { get; set; }
}

internal static class CytoidLabVideoRepair
{
    public static event Action<CytoidLabVideoStallReport> StalledVideoDetected;

    private const int ProcessTimeoutMs = 120000;
    private const string FfmpegPlayerPrefsKey = "CytoidLab.FfmpegPath";
    private static readonly HashSet<string> ReportedStalls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static string cachedFfmpegPath;

    public static void ReportStalledVideo(string videoPath, string videoId, string message)
    {
        if (string.IsNullOrWhiteSpace(videoPath)) return;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(videoPath);
        }
        catch
        {
            fullPath = videoPath;
        }

        if (!ReportedStalls.Add(fullPath)) return;

        Debug.LogWarning($"[CytoidLab] Storyboard video stall detected: id={videoId ?? "?"} path={fullPath} message={message}");
        StalledVideoDetected?.Invoke(new CytoidLabVideoStallReport
        {
            VideoPath = fullPath,
            VideoId = videoId,
            Message = message
        });
    }

    public static async UniTask<CytoidLabVideoRepairResult> RepairLevelVideosAsync(
        string levelPath,
        Action<string> setStatus = null)
    {
        var levelDirectory = ResolveLevelDirectory(levelPath);
        if (string.IsNullOrWhiteSpace(levelDirectory) || !Directory.Exists(levelDirectory))
        {
            return Fail("Level directory was not found.");
        }

        var videoPaths = Directory.EnumerateFiles(levelDirectory, "*.mp4", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).IndexOf(".cytoidlab-repair", StringComparison.OrdinalIgnoreCase) < 0)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (videoPaths.Count == 0)
        {
            return Fail("No .mp4 storyboard videos were found.");
        }

        return await RepairVideosAsync(videoPaths, setStatus);
    }

    public static async UniTask<CytoidLabVideoRepairResult> RepairVideosAsync(
        IReadOnlyCollection<string> videoPaths,
        Action<string> setStatus = null)
    {
        if (videoPaths == null || videoPaths.Count == 0)
        {
            return Fail("No video files were selected.");
        }

        var ffmpegPath = await EnsureFfmpegPathAsync(setStatus);
        if (string.IsNullOrWhiteSpace(ffmpegPath))
        {
            return Fail("ffmpeg was not found or selected.");
        }

        var backupDirectory = CreateBackupDirectory(Path.GetDirectoryName(videoPaths.First()) ?? "");
        Directory.CreateDirectory(backupDirectory);

        var repaired = 0;
        foreach (var videoPath in videoPaths)
        {
            if (!File.Exists(videoPath))
            {
                Debug.LogWarning($"[CytoidLab] Video repair skipped missing file: {videoPath}");
                continue;
            }

            var fileName = Path.GetFileName(videoPath);
            setStatus?.Invoke($"Repairing {fileName}...");

            var backupPath = Path.Combine(backupDirectory, fileName);
            File.Copy(videoPath, backupPath, overwrite: false);

            var tempOutput = Path.Combine(
                Path.GetDirectoryName(videoPath) ?? "",
                Path.GetFileNameWithoutExtension(videoPath) + ".cytoidlab-repair.tmp.mp4");
            if (File.Exists(tempOutput)) File.Delete(tempOutput);

            var args = new[]
            {
                "-y",
                "-hide_banner",
                "-nostats",
                "-loglevel",
                "error",
                "-i",
                videoPath,
                "-an",
                "-c:v",
                "libx264",
                "-pix_fmt",
                "yuv420p",
                "-profile:v",
                "baseline",
                "-level",
                "3.1",
                "-r",
                "60",
                "-g",
                "60",
                "-keyint_min",
                "60",
                "-sc_threshold",
                "0",
                "-bf",
                "0",
                "-movflags",
                "+faststart",
                tempOutput
            };

            var process = await RunProcessAsync(ffmpegPath, args, ProcessTimeoutMs);
            if (process.ExitCode != 0 || !File.Exists(tempOutput))
            {
                var message = $"ffmpeg failed for {fileName}: {process.Output}";
                Debug.LogError($"[CytoidLab] {message}");
                return Fail(message, backupDirectory, repaired);
            }

            try
            {
                File.Copy(tempOutput, videoPath, overwrite: true);
                File.Delete(tempOutput);
                repaired++;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CytoidLab] Failed to replace repaired video {videoPath}: {e}");
                return Fail($"Failed to replace {fileName}: {e.Message}", backupDirectory, repaired);
            }
        }

        return new CytoidLabVideoRepairResult
        {
            Success = repaired > 0,
            RepairedCount = repaired,
            BackupDirectory = backupDirectory,
            Message = repaired > 0
                ? $"Repaired {repaired} video(s). Backup: {backupDirectory}"
                : "No videos were repaired."
        };
    }

    private static async UniTask<string> EnsureFfmpegPathAsync(Action<string> setStatus)
    {
        if (await ValidateFfmpegAsync(cachedFfmpegPath)) return cachedFfmpegPath;

        var bundledPath = FindBundledFfmpegLite();
        if (await ValidateFfmpegAsync(bundledPath))
        {
            cachedFfmpegPath = bundledPath;
            Debug.Log($"[CytoidLab] Using bundled ffmpeg-lite: {cachedFfmpegPath}");
            return cachedFfmpegPath;
        }

        var savedPath = PlayerPrefs.GetString(FfmpegPlayerPrefsKey, null);
        if (await ValidateFfmpegAsync(savedPath))
        {
            cachedFfmpegPath = savedPath;
            return cachedFfmpegPath;
        }

        var pathFfmpeg = FindExecutableOnPath("ffmpeg.exe") ?? FindExecutableOnPath("ffmpeg");
        if (await ValidateFfmpegAsync(pathFfmpeg))
        {
            cachedFfmpegPath = pathFfmpeg;
            PlayerPrefs.SetString(FfmpegPlayerPrefsKey, cachedFfmpegPath);
            PlayerPrefs.Save();
            Debug.Log($"[CytoidLab] Using ffmpeg from PATH: {cachedFfmpegPath}");
            return cachedFfmpegPath;
        }

        setStatus?.Invoke("Select ffmpeg.exe...");
        var pickedPath = PickFfmpegWindows();
        if (await ValidateFfmpegAsync(pickedPath))
        {
            cachedFfmpegPath = pickedPath;
            PlayerPrefs.SetString(FfmpegPlayerPrefsKey, cachedFfmpegPath);
            PlayerPrefs.Save();
            return cachedFfmpegPath;
        }

        if (!string.IsNullOrWhiteSpace(pickedPath))
        {
            setStatus?.Invoke("Selected file is not ffmpeg.");
        }

        return null;
    }

    private static string FindBundledFfmpegLite()
    {
        var candidates = new List<string>();

        try
        {
            var playerDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            candidates.Add(Path.Combine(playerDirectory, "ffmpeg-lite", "ffmpeg.exe"));
        }
        catch
        {
            // Ignore invalid Unity data paths.
        }

        try
        {
            candidates.Add(Path.Combine(Application.streamingAssetsPath, "ffmpeg-lite", "ffmpeg.exe"));
        }
        catch
        {
            // StreamingAssets may not exist in every runtime mode.
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private static async UniTask<bool> ValidateFfmpegAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;

        try
        {
            var result = await RunProcessAsync(path, new[] { "-version" }, 5000);
            return result.ExitCode == 0 &&
                   result.Output.IndexOf("ffmpeg version", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CytoidLab] ffmpeg validation failed for {path}: {e.Message}");
            return false;
        }
    }

    private static string FindExecutableOnPath(string executableName)
    {
        foreach (var directory in GetPathDirectories())
        {
            try
            {
                var candidate = Path.Combine(directory, executableName);
                if (File.Exists(candidate)) return candidate;
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }

        return null;
    }

    private static IEnumerable<string> GetPathDirectories()
    {
        var pathValues = new[]
        {
            Environment.GetEnvironmentVariable("PATH"),
            Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User),
            Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine)
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pathValue in pathValues)
        {
            if (string.IsNullOrWhiteSpace(pathValue)) continue;

            foreach (var entry in pathValue.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;

                var directory = Environment.ExpandEnvironmentVariables(entry.Trim().Trim('"'));
                if (!Directory.Exists(directory)) continue;
                if (seen.Add(directory)) yield return directory;
            }
        }
    }

    private static string ResolveLevelDirectory(string levelPath)
    {
        if (string.IsNullOrWhiteSpace(levelPath)) return null;
        if (Directory.Exists(levelPath)) return Path.GetFullPath(levelPath);
        if (File.Exists(levelPath)) return Path.GetDirectoryName(Path.GetFullPath(levelPath));
        return null;
    }

    private static string CreateBackupDirectory(string directory)
    {
        var baseName = "_video_backup_" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var candidate = Path.Combine(directory, baseName);
        var suffix = 1;
        while (Directory.Exists(candidate))
        {
            candidate = Path.Combine(directory, baseName + "_" + suffix);
            suffix++;
        }

        return candidate;
    }

    private static CytoidLabVideoRepairResult Fail(string message, string backupDirectory = null, int repaired = 0)
    {
        return new CytoidLabVideoRepairResult
        {
            Success = false,
            RepairedCount = repaired,
            BackupDirectory = backupDirectory,
            Message = message
        };
    }

    private static async UniTask<ProcessResult> RunProcessAsync(string fileName, IReadOnlyList<string> args, int timeoutMs)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = string.Join(" ", args.Select(QuoteArgument)),
            WorkingDirectory = ResolveProcessWorkingDirectory(fileName),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using (var process = new Process { StartInfo = psi })
        {
            var output = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) output.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) output.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var elapsedMs = 0;
            while (!process.HasExited)
            {
                if (elapsedMs >= timeoutMs)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Process may have exited between checks.
                    }

                    return new ProcessResult
                    {
                        ExitCode = -1,
                        Output = $"Process timed out after {timeoutMs / 1000}s."
                    };
                }

                await UniTask.Delay(100);
                elapsedMs += 100;
            }

            return new ProcessResult
            {
                ExitCode = process.ExitCode,
                Output = output.ToString().Trim()
            };
        }
    }

    private static string QuoteArgument(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return value;
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static string ResolveProcessWorkingDirectory(string fileName)
    {
        try
        {
            var directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)) return directory;
        }
        catch
        {
            // Fall back below.
        }

        return Application.dataPath;
    }

    private sealed class ProcessResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const int OfnExplorer = 0x00080000;
    private const int OfnFileMustExist = 0x00001000;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnNoChangeDir = 0x00000008;
    private const int FileBufferChars = 8192;
    private const int FileTitleBufferChars = 256;

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

    [DllImport("comdlg32.dll")]
    private static extern int CommDlgExtendedError();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private class OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner = IntPtr.Zero;
        public IntPtr hInstance = IntPtr.Zero;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public string lpstrFile;
        public int nMaxFile;
        public string lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData = IntPtr.Zero;
        public IntPtr lpfnHook = IntPtr.Zero;
        public string lpTemplateName;
        public IntPtr pvReserved = IntPtr.Zero;
        public int dwReserved;
        public int FlagsEx;
    }

    private static string PickFfmpegWindows()
    {
        var ofn = new OpenFileName
        {
            lStructSize = Marshal.SizeOf(typeof(OpenFileName)),
            lpstrFilter = "ffmpeg executable\0ffmpeg.exe\0Executable\0*.exe\0All files\0*.*\0\0",
            nFilterIndex = 1,
            lpstrFile = new string('\0', FileBufferChars),
            nMaxFile = FileBufferChars,
            lpstrFileTitle = new string('\0', FileTitleBufferChars),
            nMaxFileTitle = FileTitleBufferChars,
            lpstrTitle = "Select ffmpeg.exe",
            Flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir
        };

        if (!GetOpenFileName(ofn))
        {
            var dialogError = CommDlgExtendedError();
            if (dialogError != 0)
            {
                Debug.LogError($"[CytoidLab] ffmpeg picker failed: CommDlgExtendedError={dialogError}, Win32={Marshal.GetLastWin32Error()}");
            }

            return null;
        }

        var path = ofn.lpstrFile;
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = path.Split('\0')[0].Trim();
        return File.Exists(path) ? path : null;
    }
#else
    private static string PickFfmpegWindows()
    {
        return null;
    }
#endif
}
