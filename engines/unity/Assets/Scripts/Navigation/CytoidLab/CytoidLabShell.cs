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

    public static void RestoreWindowedSize()
    {
        if (!IsActive) return;
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

        if (appliedGraphicsQuality == quality)
        {
            SyncWindowSize();
            return;
        }

        appliedGraphicsQuality = quality;
        var (width, height) = GetPlayAreaSizeForQuality(quality);
        ApplyWindowSize(width, height);
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
            ApplyWindowSize();
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
