using UnityEngine;

/// <summary>
/// Disk-backed Cytoid Lab preferences (PlayerPrefs). Survives app exit.
/// </summary>
public static class CytoidLabPreferences
{
    private const string ViewportPresetKey = "CytoidLab.ViewportPreset";
    private const string ViewportSizeKey = "CytoidLab.ViewportSize";

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

    public static void LoadInto(LocalPlayerSettings settings)
    {
        if (settings == null) return;
        if (!TryGetViewport(out var presetId, out var sizeId)) return;

        settings.LabViewportPreset = presetId;
        settings.LabViewportSize = sizeId;
    }
}
