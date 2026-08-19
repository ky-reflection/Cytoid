using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Disk-backed Cytoid Lab preferences (PlayerPrefs). Survives app exit.
/// </summary>
public static class CytoidLabPreferences
{
    private const string ViewportPresetKey = "CytoidLab.ViewportPreset";
    private const string ViewportSizeKey = "CytoidLab.ViewportSize";
    private const string SelectedLevelIdKey = "CytoidLab.SelectedLevelId";
    private const string SelectedDifficultyIdKey = "CytoidLab.SelectedDifficultyId";
    private const string AutoKey = "CytoidLab.Auto";
    private const string HitSoundKey = "CytoidLab.HitSound";
    private const string DisplayNoteIdsKey = "CytoidLab.DisplayNoteIds";
    private const string SkipMusicOnCompletionKey = "CytoidLab.SkipMusicOnCompletion";
    private const string FullscreenKey = "CytoidLab.Fullscreen";

    /// <summary>Lab defaults to Auto on for chart preview.</summary>
    public static bool Auto
    {
        get => GetBool(AutoKey, true);
        set => SetBool(AutoKey, value);
    }

    public static bool HitSoundEnabled
    {
        get => GetBool(HitSoundKey, false);
        set => SetBool(HitSoundKey, value);
    }

    public static bool DisplayNoteIds
    {
        get => GetBool(DisplayNoteIdsKey, false);
        set => SetBool(DisplayNoteIdsKey, value);
    }

    public static bool SkipMusicOnCompletion
    {
        get => GetBool(SkipMusicOnCompletionKey, true);
        set => SetBool(SkipMusicOnCompletionKey, value);
    }

    public static bool Fullscreen
    {
        get => GetBool(FullscreenKey, false);
        set => SetBool(FullscreenKey, value);
    }

    public static List<Mod> CreateLaunchMods()
    {
        var mods = new List<Mod>();
        if (Auto) mods.Add(Mod.Auto);
        return mods;
    }

    public static void SaveViewport(string presetId, string sizeId)
    {
        PlayerPrefs.SetString(ViewportPresetKey, CytoidLabShell.NormalizeViewportPresetId(presetId));
        PlayerPrefs.SetString(ViewportSizeKey, CytoidLabShell.NormalizeViewportSizeId(sizeId));
        PlayerPrefs.Save();
    }

    public static bool TryGetViewport(out string presetId, out string sizeId)
    {
        var hasPreset = PlayerPrefs.HasKey(ViewportPresetKey);
        var hasSize = PlayerPrefs.HasKey(ViewportSizeKey);
        if (!hasPreset && !hasSize)
        {
            presetId = CytoidLabShell.ViewportPreset16x9;
            sizeId = CytoidLabShell.ViewportSizeSmall;
            return false;
        }

        presetId = CytoidLabShell.NormalizeViewportPresetId(
            hasPreset ? PlayerPrefs.GetString(ViewportPresetKey) : CytoidLabShell.ViewportPreset16x9);
        sizeId = CytoidLabShell.NormalizeViewportSizeId(
            hasSize ? PlayerPrefs.GetString(ViewportSizeKey) : CytoidLabShell.ViewportSizeSmall);
        return true;
    }

    public static void SaveSelectedLevel(string levelId, string difficultyId)
    {
        if (string.IsNullOrEmpty(levelId))
        {
            PlayerPrefs.DeleteKey(SelectedLevelIdKey);
        }
        else
        {
            PlayerPrefs.SetString(SelectedLevelIdKey, levelId);
        }

        if (string.IsNullOrEmpty(difficultyId))
        {
            PlayerPrefs.DeleteKey(SelectedDifficultyIdKey);
        }
        else
        {
            PlayerPrefs.SetString(SelectedDifficultyIdKey, difficultyId);
        }

        PlayerPrefs.Save();
    }

    public static bool TryGetSelectedLevel(out string levelId, out string difficultyId)
    {
        levelId = PlayerPrefs.HasKey(SelectedLevelIdKey)
            ? PlayerPrefs.GetString(SelectedLevelIdKey)
            : null;
        difficultyId = PlayerPrefs.HasKey(SelectedDifficultyIdKey)
            ? PlayerPrefs.GetString(SelectedDifficultyIdKey)
            : null;
        return !string.IsNullOrEmpty(levelId);
    }

    public static void LoadInto(LocalPlayerSettings settings)
    {
        if (settings == null) return;
        settings.HitSound = HitSoundEnabled ? "click1" : "none";
        settings.DisplayNoteIds = DisplayNoteIds;
        settings.SkipMusicOnCompletion = SkipMusicOnCompletion;
        if (!TryGetViewport(out var presetId, out var sizeId)) return;

        settings.LabViewportPreset = presetId;
        settings.LabViewportSize = sizeId;
    }

    static bool GetBool(string key, bool defaultValue)
    {
        if (!PlayerPrefs.HasKey(key)) return defaultValue;
        return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0;
    }

    static void SetBool(string key, bool value)
    {
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
