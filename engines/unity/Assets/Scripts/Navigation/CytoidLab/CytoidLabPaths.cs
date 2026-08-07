using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Resolves Cytoid Lab install / portable user-level paths.
/// User charts live under <c>./data/{levelId}/</c> next to CytoidLab.exe on Windows Player
/// (not Unity's <see cref="Application.dataPath"/> which is <c>CytoidLab_Data</c>).
/// </summary>
public static class CytoidLabPaths
{
    public const string UserDataFolderName = "data";

    private static bool? portableRootWritable;
    private static string cachedPortableRoot;
    private static string cachedInstallDirectory;
    private static bool migrationAttempted;

    /// <summary>
    /// Directory that contains CytoidLab.exe (parent of Unity's Application.dataPath).
    /// </summary>
    public static string GetInstallDirectory()
    {
        if (!string.IsNullOrEmpty(cachedInstallDirectory)) return cachedInstallDirectory;

        try
        {
            var unityDataPath = Application.dataPath;
            if (string.IsNullOrEmpty(unityDataPath)) return null;
            cachedInstallDirectory = Directory.GetParent(unityDataPath)?.FullName;
            return cachedInstallDirectory;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Legacy AppData root used by Lab ≤0.2.0 (and Editor). Always <see cref="Application.persistentDataPath"/>.
    /// </summary>
    public static string GetLegacyUserLevelsRoot() => Application.persistentDataPath;

    /// <summary>
    /// Effective user levels root. Portable <c>./data</c> on writable Lab Windows Player; otherwise AppData.
    /// </summary>
    public static string GetUserLevelsRoot()
    {
        if (TryGetPortableUserLevelsRoot(out var portableRoot)) return portableRoot;
        return GetLegacyUserLevelsRoot();
    }

    public static bool IsUsingPortableUserLevelsRoot() =>
        TryGetPortableUserLevelsRoot(out _);

    public static bool TryGetPortableUserLevelsRoot(out string root)
    {
        root = null;

        if (!ShouldUsePortableUserLevelsRoot()) return false;

        if (portableRootWritable == false) return false;

        if (!string.IsNullOrEmpty(cachedPortableRoot) && portableRootWritable == true)
        {
            root = cachedPortableRoot;
            return true;
        }

        var installDir = GetInstallDirectory();
        if (string.IsNullOrEmpty(installDir))
        {
            portableRootWritable = false;
            return false;
        }

        var candidate = Path.Combine(installDir, UserDataFolderName);
        if (!EnsureWritableDirectory(candidate))
        {
            portableRootWritable = false;
            Debug.LogWarning(
                $"[CytoidLab] Cannot write portable levels root at '{candidate}'. Falling back to AppData.");
            return false;
        }

        portableRootWritable = true;
        cachedPortableRoot = candidate;
        root = candidate;
        return true;
    }

    /// <summary>
    /// Moves level folders with level.json from AppData into <c>./data</c> when portable root is active.
    /// Skips ids that already exist under <c>./data</c>. Idempotent based on remaining source folders.
    /// </summary>
    public static int MigrateLegacyLevelsIfNeeded()
    {
        if (migrationAttempted) return 0;
        migrationAttempted = true;

        if (!TryGetPortableUserLevelsRoot(out var destRoot)) return 0;

        var sourceRoot = GetLegacyUserLevelsRoot();
        if (string.IsNullOrEmpty(sourceRoot) || !Directory.Exists(sourceRoot)) return 0;

        // Same physical path — nothing to migrate (fallback or misconfig).
        if (PathsEqual(sourceRoot, destRoot)) return 0;

        var migrated = 0;
        string[] sourceDirs;
        try
        {
            sourceDirs = Directory.GetDirectories(sourceRoot);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CytoidLab] Legacy levels scan failed: {e.Message}");
            return 0;
        }

        foreach (var sourceDir in sourceDirs)
        {
            var levelJson = Path.Combine(sourceDir, "level.json");
            if (!File.Exists(levelJson)) continue;

            var id = Path.GetFileName(sourceDir);
            if (string.IsNullOrEmpty(id)) continue;

            var destDir = Path.Combine(destRoot, id);
            if (Directory.Exists(destDir))
            {
                Debug.Log(
                    $"[CytoidLab] Skip migrate '{id}': already exists under ./data (keeping portable copy).");
                continue;
            }

            try
            {
                MoveOrCopyDirectory(sourceDir, destDir);
                migrated++;
                Debug.Log($"[CytoidLab] Migrated level '{id}' from AppData to ./data.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CytoidLab] Failed to migrate '{id}': {e.Message}");
                try
                {
                    if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
                }
                catch
                {
                    // ignore cleanup failure
                }
            }
        }

        if (migrated > 0)
        {
            Debug.Log($"[CytoidLab] Migrated {migrated} level(s) from AppData to '{destRoot}'.");
        }

        return migrated;
    }

    public static bool TryOpenDirectory(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;

        string full;
        try
        {
            full = Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!Directory.Exists(full)) Directory.CreateDirectory(full);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CytoidLab] Failed to resolve folder '{path}': {e.Message}");
            return false;
        }

        // ShellExecute is the reliable Windows path; explorer.exe with quoted args often fails in Player.
        if (TryShellExecuteOpen(full)) return true;

        try
        {
            var uri = new Uri(full + Path.DirectorySeparatorChar);
            Application.OpenURL(uri.AbsoluteUri);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CytoidLab] Failed to open folder '{full}': {e.Message}");
            return false;
        }
    }

    public static bool TryOpenSelectedLevelFolder(string levelPath)
    {
        if (string.IsNullOrEmpty(levelPath)) return false;
        var dir = levelPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return TryOpenDirectory(dir);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ShellExecuteW(
        IntPtr hwnd,
        string lpOperation,
        string lpFile,
        string lpParameters,
        string lpDirectory,
        int nShowCmd);

    private static bool TryShellExecuteOpen(string fullPath)
    {
        try
        {
            // Per MSDN, values > 32 mean success.
            var result = ShellExecuteW(IntPtr.Zero, "open", fullPath, null, null, 1);
            return result.ToInt64() > 32;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CytoidLab] ShellExecute open failed for '{fullPath}': {e.Message}");
            return false;
        }
    }

    private static bool ShouldUsePortableUserLevelsRoot()
    {
        if (Application.platform != RuntimePlatform.WindowsPlayer) return false;
        if (GameEmbedMode.IsBridgeEmbedded) return false;
        return CytoidLabShell.IsActive;
    }

    private static bool EnsureWritableDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".write-probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void MoveOrCopyDirectory(string sourceDir, string destDir)
    {
        try
        {
            Directory.Move(sourceDir, destDir);
            return;
        }
        catch (IOException)
        {
            // Cross-volume moves throw; fall through to copy.
        }
        catch (UnauthorizedAccessException)
        {
            // Fall through to copy.
        }

        CopyDirectoryRecursive(sourceDir, destDir);
        Directory.Delete(sourceDir, true);
    }

    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            File.Copy(file, Path.Combine(destDir, name), overwrite: false);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            CopyDirectoryRecursive(dir, Path.Combine(destDir, name));
        }
    }

    private static bool PathsEqual(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        return string.Equals(
            Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }
}
