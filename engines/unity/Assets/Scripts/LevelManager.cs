using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Unity.SharpZipLib.Zip;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Local level loader for standalone/editor debugging and Cytoid Lab import.
/// Bridge-embedded sessions receive validated metadata and a local VFS root from Flutter.
/// </summary>
public class LevelManager
{
    public readonly Dictionary<string, Level> LoadedLocalLevels = new Dictionary<string, Level>();
    private readonly HashSet<string> loadedPaths = new HashSet<string>();

    public async UniTask<List<string>> CopyBuiltInLevelsToDownloads(List<string> levelIds)
    {
        var packagePaths = new List<string>();
        foreach (var id in levelIds)
        {
            var packagePath = Application.streamingAssetsPath + "/Levels/" + id + ".cytoidlevel";
            byte[] bytes;
#if UNITY_EDITOR
            if (!File.Exists(packagePath))
            {
                Debug.LogError($"Failed to find built-in debug level {id}");
                continue;
            }

            bytes = File.ReadAllBytes(packagePath);
#else
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                packagePath = "file://" + packagePath;
            }

            using (var request = UnityWebRequest.Get(packagePath))
            {
                await request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Failed to read built-in debug level {id}: {request.error}");
                    continue;
                }

                bytes = request.downloadHandler.data;
            }
#endif

            var targetDirectory = Path.Combine(Application.temporaryCachePath, "BuiltInLevelPackages");
            Directory.CreateDirectory(targetDirectory);
            var targetFile = Path.Combine(targetDirectory, id + ".cytoidlevel");
            File.WriteAllBytes(targetFile, bytes);
            packagePaths.Add(targetFile);
        }

        return packagePaths;
    }

    private async UniTask<List<string>> InstallBuiltInLevels(
        List<string> packagePaths,
        LevelType type,
        string expectedId)
    {
        var metadataPaths = new List<string>();
        foreach (var packagePath in packagePaths)
        {
            var tempFolder = Path.Combine(type.GetDataPath(), Guid.NewGuid().ToString());
            try
            {
                if (!await UnpackLevelPackage(packagePath, tempFolder))
                {
                    continue;
                }

                var metadataPath = Path.Combine(tempFolder, "level.json");
                if (!File.Exists(metadataPath))
                {
                    Debug.LogError($"level.json not found in {packagePath}");
                    continue;
                }

                var meta = JsonConvert.DeserializeObject<LevelMeta>(File.ReadAllText(metadataPath));
                if (meta == null || string.IsNullOrEmpty(meta.id) ||
                    !string.Equals(meta.id, expectedId, StringComparison.Ordinal))
                {
                    Debug.LogError(
                        $"Invalid level.json in {packagePath}: expected id {expectedId}, got {meta?.id ?? "<missing>"}");
                    continue;
                }
                if (!Regex.IsMatch(meta.id, @"^[a-z0-9_]+([-_.][a-z0-9_]+)*$"))
                {
                    Debug.LogError($"Invalid built-in level id: {meta.id}");
                    continue;
                }

                var destination = Path.Combine(type.GetDataPath(), expectedId);
                string backup = null;
                if (Directory.Exists(destination))
                {
                    backup = destination + ".backup-" + Guid.NewGuid().ToString("N");
                    Directory.Move(destination, backup);
                }

                try
                {
                    Directory.Move(tempFolder, destination);
                }
                catch
                {
                    if (backup != null && Directory.Exists(backup))
                    {
                        if (Directory.Exists(destination))
                        {
                            Directory.Delete(destination, true);
                        }
                        Directory.Move(backup, destination);
                    }
                    throw;
                }

                if (backup != null && Directory.Exists(backup))
                {
                    try
                    {
                        Directory.Delete(backup, true);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"Failed to remove built-in level backup {backup}: {exception.Message}");
                    }
                }
                metadataPaths.Add(Path.Combine(destination, "level.json"));
            }
            finally
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                }

                if (File.Exists(packagePath))
                {
                    File.Delete(packagePath);
                }
            }
        }

        return metadataPaths;
    }

    /// <summary>
    /// Installs .cytoidlevel packages found under the user data path (Lab / non-Windows refresh).
    /// </summary>
    public async UniTask<List<string>> InstallUserCommunityLevels()
    {
        var userDataPath = LevelType.User.GetDataPath();
        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            var files = new List<string>();
            var inboxPath = userDataPath + "/Inbox/";
            if (Directory.Exists(inboxPath))
            {
                files.AddRange(Directory.GetFiles(inboxPath, "*.cytoidlevel"));
                files.AddRange(Directory.GetFiles(inboxPath, "*.cytoidlevel.zip"));
            }

            // iOS 13+ temporary inbox path (Documents sibling under tmp).
            var iOSTemporaryInboxPath = userDataPath
                .Replace("Documents/", "")
                .Replace("Documents", "") + "/tmp/me.tigerhix.cytoid-Inbox/";
            if (Directory.Exists(iOSTemporaryInboxPath))
            {
                files.AddRange(Directory.GetFiles(iOSTemporaryInboxPath, "*.cytoidlevel"));
                files.AddRange(Directory.GetFiles(iOSTemporaryInboxPath, "*.cytoidlevel.zip"));
            }

            foreach (var file in files)
            {
                if (file == null) continue;

                var toPath = userDataPath + "/" + Path.GetFileName(file);
                try
                {
                    if (File.Exists(toPath))
                    {
                        File.Delete(toPath);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    Debug.LogError($"Failed to delete .cytoidlevel file at {toPath}");
                    continue;
                }

                try
                {
                    File.Move(file, toPath);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    Debug.LogError($"Failed to move .cytoidlevel file from {file} to {toPath}");
                }
            }
        }

        var levelFiles = new List<string>();
        try
        {
            levelFiles.AddRange(Directory.GetFiles(userDataPath, "*.cytoidlevel"));
            levelFiles.AddRange(Directory.GetFiles(userDataPath, "*.cytoidlevel.zip"));
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            Debug.LogError("Cannot read from data path");
            return new List<string>();
        }

        var installedJsonFiles = await InstallLevels(levelFiles, LevelType.User);
        await LoadLevelsOfType(LevelType.User);
        return installedJsonFiles;
    }

    /// <summary>
    /// Installs arbitrary .cytoidlevel packages (Lab multi-select import keeps sources when deleteSource is false).
    /// <paramref name="onProgress"/> receives 1-based index, total count, and package path.
    /// </summary>
    public async UniTask<List<string>> InstallLevels(
        List<string> packagePaths,
        LevelType type,
        bool deleteSource = true,
        Action<int, int, string> onProgress = null)
    {
        var loadedLevelJsonFiles = new List<string>();
        var index = 1;
        foreach (var levelFile in packagePaths)
        {
            onProgress?.Invoke(index, packagePaths.Count, levelFile);
            // Let UI status text paint between packages.
            await UniTask.Yield();

            var tempFolder = Path.Combine(type.GetDataPath(), Guid.NewGuid().ToString());
            if (await UnpackLevelPackage(levelFile, tempFolder))
            {
                var levelJsonPath = Path.Combine(tempFolder, "level.json");
                if (!File.Exists(levelJsonPath))
                {
                    Debug.LogError($"level.json not found in {levelFile}");
                    Directory.Delete(tempFolder, true);
                    index++;
                    continue;
                }

                var meta = JsonConvert.DeserializeObject<LevelMeta>(File.ReadAllText(levelJsonPath));
                if (meta == null || string.IsNullOrEmpty(meta.id))
                {
                    Debug.LogError($"Invalid level.json in {levelFile}");
                    Directory.Delete(tempFolder, true);
                    index++;
                    continue;
                }

                if (!Regex.IsMatch(meta.id, @"^[a-z0-9_]+([-_.][a-z0-9_]+)+$"))
                {
                    Debug.LogError($"Invalid level id format in {levelFile}: {meta.id}");
                    Directory.Delete(tempFolder, true);
                    index++;
                    continue;
                }

                var destFolder = Path.Combine(type.GetDataPath(), meta.id);
                foreach (var file in Directory.GetFiles(tempFolder, "*.*", SearchOption.AllDirectories))
                {
                    var relativePath = file.Substring(tempFolder.Length + 1);
                    var destFile = Path.Combine(destFolder, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                    File.Copy(file, destFile, true);
                }

                Directory.Delete(tempFolder, true);
                loadedLevelJsonFiles.Add(Path.Combine(destFolder, "level.json"));
                Debug.Log($"Installed {index}/{packagePaths.Count}: {levelFile} to {destFolder}");
            }
            else
            {
                Debug.LogWarning($"Could not install {index}/{packagePaths.Count}: {levelFile}");
            }

            if (deleteSource)
            {
                try
                {
                    File.Delete(levelFile);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    Debug.LogError($"Could not delete level file at {levelFile}");
                }
            }

            index++;
        }

        return loadedLevelJsonFiles;
    }

    public void DeleteLocalLevel(string id)
    {
        if (!LoadedLocalLevels.ContainsKey(id))
        {
            Debug.LogWarning($"Warning: Could not find level {id}");
            return;
        }

        var level = LoadedLocalLevels[id];
        var directory = level.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrEmpty(directory))
        {
            throw new InvalidOperationException($"Invalid level path for {id}");
        }

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }

        LoadedLocalLevels.Remove(id);
        loadedPaths.Remove(level.Path);
    }

    public async UniTask<List<Level>> LoadLevelsOfType(LevelType type)
    {
        try
        {
            Directory.CreateDirectory(type.GetDataPath());
        }
        catch (Exception error)
        {
            Debug.LogError("Failed to create data folder.");
            Debug.LogError(error);
            return new List<Level>();
        }

        var jsonPaths = Directory.EnumerateDirectories(type.GetDataPath())
            .SelectMany(it => Directory.EnumerateFiles(it, "level.json"))
            .ToList();
        Debug.Log($"Found {jsonPaths.Count} levels with type {type}");
        return await LoadFromMetadataFiles(type, jsonPaths);
    }

    private async UniTask<bool> UnpackLevelPackage(string packagePath, string destination)
    {
        const int bufferSize = 256 * 1024;
        ZipStrings.CodePage = Encoding.UTF8.CodePage;
        Directory.CreateDirectory(destination);
        var destinationRoot = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;

        try
        {
            using (var fileStream = File.OpenRead(packagePath))
            using (var zipFile = new ZipFile(fileStream))
            {
                foreach (ZipEntry entry in zipFile)
                {
                    if (!entry.IsFile || entry.Name.Contains("__MACOSX"))
                    {
                        continue;
                    }

                    var targetPath = Path.GetFullPath(Path.Combine(destination, entry.Name));
                    if (!targetPath.StartsWith(destinationRoot, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException($"Archive entry escapes destination: {entry.Name}");
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                    using (var input = zipFile.GetInputStream(entry))
                    using (var output = File.Create(targetPath))
                    {
                        var buffer = new byte[bufferSize];
                        int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await output.WriteAsync(buffer, 0, read);
                        }
                    }
                }
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to unpack built-in debug level {packagePath}: {exception.Message}");
            return false;
        }
    }

    public void UnloadLevelsOfType(LevelType type)
    {
        var removals = LoadedLocalLevels.RemoveAll(level => level.Type == type);
        var removedPaths = removals.Select(item => item.Item2.Path).ToHashSet();
        loadedPaths.RemoveWhere(removedPaths.Contains);
    }

    public async UniTask<Level> LoadOrInstallBuiltInLevel(
        string id,
        LevelType loadType,
        bool forceInstall = false)
    {
        async UniTask<Level> LoadInstalled(bool forceReload = false)
        {
            var levels = await LoadFromMetadataFiles(loadType, new List<string>
            {
                Path.Combine(loadType.GetDataPath(), id, "level.json")
            }, forceReload);
            if (levels.Count > 0)
            {
                return levels[0];
            }

            return !forceReload && LoadedLocalLevels.TryGetValue(id, out var loaded) ? loaded : null;
        }

        var level = forceInstall ? null : await LoadInstalled();
        if (level != null)
        {
            return level;
        }

        var packages = await CopyBuiltInLevelsToDownloads(new List<string> {id});
        await InstallBuiltInLevels(packages, loadType, id);
        return await LoadInstalled(forceReload: true);
    }

    public async UniTask<List<Level>> LoadFromMetadataFiles(
        LevelType type,
        List<string> jsonPaths,
        bool forceReload = false)
    {
        var results = new List<Level>();
        foreach (var jsonPath in jsonPaths)
        {
            try
            {
                var info = new FileInfo(jsonPath);
                if (!info.Exists || info.Directory == null)
                {
                    Debug.LogWarning($"Level metadata not found: {jsonPath}");
                    continue;
                }

                var path = info.Directory.FullName + Path.DirectorySeparatorChar;
                if (!forceReload && loadedPaths.Contains(path))
                {
                    var existing = LoadedLocalLevels.Values.FirstOrDefault(level => level.Path == path);
                    if (existing != null)
                    {
                        results.Add(existing);
                    }
                    continue;
                }

                LevelMeta meta;
                await UniTask.SwitchToThreadPool();
                try
                {
                    meta = JsonConvert.DeserializeObject<LevelMeta>(File.ReadAllText(jsonPath));
                }
                finally
                {
                    await UniTask.SwitchToMainThread();
                }
                if (meta == null || !meta.Validate())
                {
                    Debug.LogWarning($"Invalid level metadata: {jsonPath}");
                    continue;
                }

                meta.SortCharts();
                var level = Level.FromLocal(path, type, meta);
                if (type != LevelType.Temp)
                {
                    if (LoadedLocalLevels.TryGetValue(meta.id, out var previous))
                    {
                        loadedPaths.Remove(previous.Path);
                    }
                    LoadedLocalLevels[meta.id] = level;
                    loadedPaths.Add(path);
                }
                results.Add(level);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Failed to load level metadata {jsonPath}: {exception}");
            }
        }

        return results;
    }
}
