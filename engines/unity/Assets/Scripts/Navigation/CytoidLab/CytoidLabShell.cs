using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Cytoid Lab shell: window sizing and overlay HUD injection.
/// Gameplay uses the full window (no camera viewport crop); HUD draws on Screen Space Overlay.
/// </summary>
public class CytoidLabShell : MonoBehaviour
{
    public const int PlayAreaWidth = 1280;
    public const int PlayAreaHeight = 720;

    public const string ViewportPreset16x9 = "16:9";
    public const string ViewportPreset4x3 = "4:3";

    public const int Viewport16x9Width = 1280;
    public const int Viewport16x9Height = 720;
    public const int Viewport4x3Width = 1280;
    public const int Viewport4x3Height = 960;
    public const int ViewportLargeWidth = 1920;
    public const int ViewportLarge16x9Height = 1080;
    public const int ViewportLarge4x3Height = 1440;

    public const string ViewportSizeSmall = "small";
    public const string ViewportSizeLarge = "large";

    /// <summary>Overlay top chrome height at <see cref="PlayAreaHeight"/> reference.</summary>
    public const float TopHudOverlayHeightPx = 40f;

    /// <summary>Overlay bottom chrome (timeline) height at reference resolution.</summary>
    public const float BottomHudOverlayHeightPx = 28f;

    /// <summary>Window height equals play area (HUD does not reserve viewport pixels).</summary>
    public static int WindowHeight => PlayAreaHeight;

    public static int CurrentPlayAreaWidth { get; private set; } = PlayAreaWidth;
    public static int CurrentPlayAreaHeight { get; private set; } = PlayAreaHeight;
    public static int CurrentWindowWidth { get; private set; } = PlayAreaWidth;
    public static int CurrentWindowHeight { get; private set; } = PlayAreaHeight;

    public static string CurrentViewportPresetId { get; private set; } = ViewportPreset16x9;
    public static string CurrentViewportSizeId { get; private set; } = ViewportSizeSmall;

    public static bool IsActive =>
        !GameEmbedMode.IsBridgeEmbedded &&
        (Application.platform == RuntimePlatform.WindowsPlayer ||
         Application.platform == RuntimePlatform.WindowsEditor);

    public static CytoidLabShell Instance { get; private set; }

    private static GraphicsQuality? appliedGraphicsQuality;
    private static int lastObservedScreenWidth;
    private static int lastObservedScreenHeight;

    private Game boundGame;

    public static void EnsureInitialized()
    {
        if (!IsActive || Instance != null) return;
        var go = new GameObject(nameof(CytoidLabShell));
        go.AddComponent<CytoidLabShell>();
    }

    public static void ConfigureCanvasScaler(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(PlayAreaWidth, PlayAreaHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    public static float ScaleHudPx(float referencePx) =>
        referencePx * UnityEngine.Screen.height / Mathf.Max(1, PlayAreaHeight);

    public static float GetTopHudOverlayHeightPx() => ScaleHudPx(TopHudOverlayHeightPx);

    public static float GetBottomHudOverlayHeightPx() => ScaleHudPx(BottomHudOverlayHeightPx);

    public static void ApplyWindowSize(int playAreaWidth = PlayAreaWidth, int playAreaHeight = PlayAreaHeight)
    {
        if (!IsActive) return;

        CurrentPlayAreaWidth = playAreaWidth;
        CurrentPlayAreaHeight = playAreaHeight;
        CurrentWindowWidth = playAreaWidth;
        CurrentWindowHeight = playAreaHeight;

        if (!UnityEngine.Screen.fullScreen)
        {
            UnityEngine.Screen.SetResolution(CurrentWindowWidth, CurrentWindowHeight, FullScreenMode.Windowed);
        }

        lastObservedScreenWidth = CurrentWindowWidth;
        lastObservedScreenHeight = CurrentWindowHeight;
    }

    public static void ApplyViewportPreset(string presetId, bool persist = true)
    {
        if (!IsActive) return;

        var sizeId = ResolveStoredViewportSizeId();
        ApplyViewport(presetId, sizeId, persist);
    }

    public static void ApplyViewportSize(string sizeId, bool persist = true)
    {
        if (!IsActive) return;

        var presetId = ResolveStoredViewportPresetId();
        ApplyViewport(presetId, sizeId, persist);
    }

    public static void ApplyViewportFromSettings()
    {
        if (!IsActive || !Context.IsInitialized || Context.Player?.Settings == null) return;

        ApplyViewport(Context.Player.Settings.LabViewportPreset, Context.Player.Settings.LabViewportSize, persist: false);
    }

    public static string NormalizeViewportPresetId(string presetId)
    {
        return presetId == ViewportPreset4x3 ? ViewportPreset4x3 : ViewportPreset16x9;
    }

    public static string NormalizeViewportSizeId(string sizeId)
    {
        return sizeId == ViewportSizeLarge ? ViewportSizeLarge : ViewportSizeSmall;
    }

    public static (int width, int height) ResolveViewportDimensions(string presetId, string sizeId)
    {
        presetId = NormalizeViewportPresetId(presetId);
        if (NormalizeViewportSizeId(sizeId) == ViewportSizeLarge)
        {
            return presetId == ViewportPreset4x3
                ? (ViewportLargeWidth, ViewportLarge4x3Height)
                : (ViewportLargeWidth, ViewportLarge16x9Height);
        }

        return presetId == ViewportPreset4x3
            ? (Viewport4x3Width, Viewport4x3Height)
            : (Viewport16x9Width, Viewport16x9Height);
    }

    public static string FormatViewportDimensions(string presetId, string sizeId)
    {
        var (width, height) = ResolveViewportDimensions(presetId, sizeId);
        return $"{width}×{height}";
    }

    private static void ApplyViewport(string presetId, string sizeId, bool persist)
    {
        presetId = NormalizeViewportPresetId(presetId);
        sizeId = NormalizeViewportSizeId(sizeId);
        CurrentViewportPresetId = presetId;
        CurrentViewportSizeId = sizeId;

        var (width, height) = ResolveViewportDimensions(presetId, sizeId);
        ApplyWindowSize(width, height);

        if (persist && Context.IsInitialized && Context.Player?.Settings != null)
        {
            Context.Player.Settings.LabViewportPreset = presetId;
            Context.Player.Settings.LabViewportSize = sizeId;
        }
    }

    private static string ResolveStoredViewportPresetId()
    {
        if (Context.IsInitialized && Context.Player?.Settings != null)
        {
            return NormalizeViewportPresetId(Context.Player.Settings.LabViewportPreset);
        }

        return CurrentViewportPresetId;
    }

    private static string ResolveStoredViewportSizeId()
    {
        if (Context.IsInitialized && Context.Player?.Settings != null)
        {
            return NormalizeViewportSizeId(Context.Player.Settings.LabViewportSize);
        }

        return CurrentViewportSizeId;
    }

    public static void RestoreWindowedSize()
    {
        if (!IsActive) return;

        if (Context.IsInitialized && Context.Player?.Settings != null)
        {
            ApplyViewport(Context.Player.Settings.LabViewportPreset, Context.Player.Settings.LabViewportSize, persist: false);
            return;
        }

        UnityEngine.Screen.SetResolution(CurrentWindowWidth, CurrentWindowHeight, FullScreenMode.Windowed);
        lastObservedScreenWidth = CurrentWindowWidth;
        lastObservedScreenHeight = CurrentWindowHeight;
    }

    public static void SyncWindowSize()
    {
        if (!IsActive || UnityEngine.Screen.fullScreen) return;

        if (UnityEngine.Screen.width != CurrentWindowWidth || UnityEngine.Screen.height != CurrentWindowHeight)
        {
            UnityEngine.Screen.SetResolution(CurrentWindowWidth, CurrentWindowHeight, FullScreenMode.Windowed);
        }

        lastObservedScreenWidth = CurrentWindowWidth;
        lastObservedScreenHeight = CurrentWindowHeight;
    }

    public static void ApplyGraphicsQualityIfNeeded(GraphicsQuality quality)
    {
        if (!IsActive) return;

        appliedGraphicsQuality = quality;
        SyncWindowSize();
    }

    public static (int width, int height) GetPlayAreaSizeForQuality(GraphicsQuality quality)
    {
        int width;
        int height;
        switch (quality)
        {
            case GraphicsQuality.VeryLow:
                width = 1024;
                height = 576;
                break;
            case GraphicsQuality.Low:
                width = PlayAreaWidth;
                height = PlayAreaHeight;
                break;
            case GraphicsQuality.Medium:
                width = 1366;
                height = 768;
                break;
            case GraphicsQuality.High:
            case GraphicsQuality.Ultra:
            default:
                width = 1920;
                height = 1080;
                break;
        }

        var maxWidth = UnityEngine.Screen.currentResolution.width;
        var maxHeight = UnityEngine.Screen.currentResolution.height;
        if (width > maxWidth || height > maxHeight)
        {
            width = Mathf.Min(width, maxWidth);
            height = Mathf.Min(height, maxHeight);
            var scale = Mathf.Min((float)width / 1920, (float)height / 1080);
            width = Mathf.Max(PlayAreaWidth, Mathf.RoundToInt(1920 * scale));
            height = Mathf.Max(PlayAreaHeight, Mathf.RoundToInt(1080 * scale));
        }

        return (width, height);
    }

    private void Awake()
    {
        if (!IsActive)
        {
            Destroy(gameObject);
            return;
        }

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        Debug.Log($"[CytoidLab] {CytoidLabVersion.DisplayName}");

        if (CurrentWindowWidth <= 0 || CurrentWindowHeight <= 0)
        {
            ApplyViewportFromSettings();
            if (CurrentWindowWidth <= 0 || CurrentWindowHeight <= 0)
            {
                ApplyWindowSize();
            }
        }
        else
        {
            SyncWindowSize();
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindGame();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        TrackUserResize();
    }

    private static void TrackUserResize()
    {
        if (!IsActive || UnityEngine.Screen.fullScreen) return;

        var w = UnityEngine.Screen.width;
        var h = UnityEngine.Screen.height;
        if (w == lastObservedScreenWidth && h == lastObservedScreenHeight) return;

        lastObservedScreenWidth = w;
        lastObservedScreenHeight = h;
        CurrentWindowWidth = w;
        CurrentWindowHeight = h;
        CurrentPlayAreaWidth = w;
        CurrentPlayAreaHeight = h;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsActive) return;

        SyncWindowSize();

        if (scene.name == "Game")
        {
            EnsureGameHud();
            BindGame(Object.FindFirstObjectByType<Game>());
            return;
        }

        UnbindGame();
    }

    private void BindGame(Game game)
    {
        UnbindGame();
        if (game == null) return;

        boundGame = game;
        boundGame.onGameReadyToLoad.AddListener(OnGameReadyToLoad);
        boundGame.onGameDisposed.AddListener(OnGameDisposed);
    }

    private void UnbindGame()
    {
        if (boundGame == null) return;

        boundGame.onGameReadyToLoad.RemoveListener(OnGameReadyToLoad);
        boundGame.onGameDisposed.RemoveListener(OnGameDisposed);
        boundGame = null;
    }

    private static void OnGameReadyToLoad(Game game)
    {
        SyncWindowSize();
    }

    private static void OnGameDisposed(Game game)
    {
        SyncWindowSize();
    }

    private static void EnsureGameHud()
    {
        if (Object.FindFirstObjectByType<CytoidLabHudController>() != null) return;

        var hudGo = new GameObject("CytoidLabHud");
        hudGo.AddComponent<CytoidLabHudController>();
    }
}
