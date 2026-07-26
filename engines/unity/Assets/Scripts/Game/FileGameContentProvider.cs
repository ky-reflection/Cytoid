using System;
using System.IO;
using Cysharp.Threading.Tasks;
using Polyglot;
using UnityEngine;

public sealed class FileGameContentProvider : IGameContentProvider
{
    private readonly Level level;
    private readonly Difficulty difficulty;
    private AudioClipLoader audioClipLoader;

    public bool IsExternal => false;
    public Level Level => level;
    public Difficulty Difficulty => difficulty;
    public LevelMeta.ChartSection ChartSection => level.Meta.GetChartSection(difficulty.Id);

    public FileGameContentProvider(Level level, Difficulty difficulty)
    {
        this.level = level ?? throw new ArgumentNullException(nameof(level));
        this.difficulty = difficulty ?? throw new ArgumentNullException(nameof(difficulty));
    }

    public UniTask<string> LoadChartText()
    {
        // Read via filesystem APIs so non-ASCII filenames in level.json work.
        // (Raw "file://" + path concatenation breaks UnityWebRequest for those paths.)
        var chartPath = level.Path + ChartSection.path;
        if (!File.Exists(chartPath))
        {
            throw new Exception($"Failed to load chart from {chartPath}: file not found");
        }

        return UniTask.FromResult(File.ReadAllText(chartPath));
    }

    public async UniTask<AudioClip> LoadMusic()
    {
        var audioFsPath = level.Path + level.Meta.GetMusicPath(difficulty.Id);
        if (!File.Exists(audioFsPath))
        {
            throw new Exception($"Failed to download audio from {audioFsPath}: file not found");
        }

        var audioPath = GameLaunchVfs.ToFileUri(audioFsPath);
        audioClipLoader = new AudioClipLoader(audioPath);
        await audioClipLoader.Load();
        if (audioClipLoader.Error != null)
        {
            throw new Exception($"Failed to download audio from {audioPath}: {audioClipLoader.Error}");
        }

        return audioClipLoader.AudioClip;
    }

    public UniTask<string> LoadStoryboardText()
    {
        var storyboardPath = ResolveStoryboardPath();
        if (storyboardPath == null || !File.Exists(storyboardPath))
        {
            return UniTask.FromResult<string>(null);
        }

        return UniTask.FromResult(File.ReadAllText(storyboardPath));
    }

    private string ResolveStoryboardPath()
    {
        var chartMeta = ChartSection;
        string sbFile = null;
        if (chartMeta.storyboard != null)
        {
            if (chartMeta.storyboard.localizations != null)
            {
                chartMeta.storyboard.localizations.TryGetValue(Localization.Instance.SelectedLanguage.ToString(), out sbFile);
            }

            if (sbFile == null)
            {
                sbFile = chartMeta.storyboard.path;
            }
        }

        return level.Path + (sbFile ?? "storyboard.json");
    }

    public void Dispose()
    {
        audioClipLoader?.DisposeDecoder();
        audioClipLoader = null;
    }
}
