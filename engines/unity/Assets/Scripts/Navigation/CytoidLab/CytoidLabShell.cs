using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Cytoid Lab shell: window chrome, HUD injection, and gameplay camera bands.
/// Gameplay camera viewport (player area) is independent of in-game overlay UI layout.
/// </summary>
public class CytoidLabShell : MonoBehaviour
{
    // Default window: play area + top/bottom HUD chrome (see WindowHeight).
    public const int PlayAreaWidth = 1280;
    public const int PlayAreaHeight = 720;
    public const float HudBandHeightPx = 64f;

    public static int WindowHeight => PlayAreaHeight + Mathf.RoundToInt(HudBandHeightPx * 2f);

    public static int CurrentPlayAreaWidth { get; private set; } = PlayAreaWidth;
    public static int CurrentPlayAreaHeight { get; private set; } = PlayAreaHeight;
    public static int CurrentWindowWidth { get; private set; } = PlayAreaWidth;
    public static int CurrentWindowHeight { get; private set; } = WindowHeight;

    public static bool IsActive =>
        !GameEmbedMode.IsBridgeEmbedded &&
        (Application.platform == RuntimePlatform.WindowsPlayer ||
         Application.platform == RuntimePlatform.WindowsEditor);

    public static CytoidLabShell Instance { get; private set; }

    private static readonly Dictionary<Camera, float> OriginalAspects = new Dictionary<Camera, float>();
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
        scaler.referenceResolution = new Vector2(PlayAreaWidth, WindowHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    public static int GetWindowHeightForPlayArea(int playAreaHeight) =>
        playAreaHeight + Mathf.RoundToInt(HudBandHeightPx * 2f);

    /// <summary>HUD band height in current screen pixels (scales with window height).</summary>
    public static float GetHudBandHeightPx() =>
        HudBandHeightPx * UnityEngine.Screen.height / Mathf.Max(1, WindowHeight);

    /// <summary>Pixel height of the gameplay camera viewport (not the in-game overlay UI).</summary>
    public static float GetPlayViewportHeightPx() =>
        Mathf.Max(1f, UnityEngine.Screen.height - GetHudBandHeightPx() * 2f);

    /// <summary>
    /// Screen height for Chart screenRatio and storyboard note-unit canvas scale.
    /// Matches gameplay camera aspect when HUD bands are active.
    /// </summary>
    public static float GetCoordinateScreenHeightPx() =>
        IsActive ? GetPlayViewportHeightPx() : UnityEngine.Screen.height;

    public static void ApplyWindowSize(int playAreaWidth = PlayAreaWidth, int playAreaHeight = PlayAreaHeight)
    {
        if (!IsActive) return;

        CurrentPlayAreaWidth = playAreaWidth;
        CurrentPlayAreaHeight = playAreaHeight;
        CurrentWindowWidth = playAreaWidth;
        CurrentWindowHeight = GetWindowHeightForPlayArea(playAreaHeight);

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

    public static void ApplyCameraBands(Camera cam)
    {
        if (!IsActive || cam == null) return;

        if (!OriginalAspects.ContainsKey(cam))
        {
            OriginalAspects[cam] = cam.aspect;
        }

        var bandPx = GetHudBandHeightPx();
        var bandNorm = bandPx / Mathf.Max(1, UnityEngine.Screen.height);
        var playHeightPx = GetPlayViewportHeightPx();
        cam.rect = new Rect(0f, bandNorm, 1f, Mathf.Max(0.01f, 1f - 2f * bandNorm));
        cam.aspect = UnityEngine.Screen.width / playHeightPx;
    }

    public static void ResetCamera(Camera cam)
    {
        if (cam == null) return;

        cam.rect = new Rect(0f, 0f, 1f, 1f);
        if (OriginalAspects.TryGetValue(cam, out var aspect))
        {
            cam.aspect = aspect;
            OriginalAspects.Remove(cam);
        }
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

    private void LateUpdate()
    {
        if (boundGame?.camera != null)
        {
            ApplyCameraBands(boundGame.camera);
        }
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

        var bandPx = GetHudBandHeightPx();
        CurrentPlayAreaWidth = w;
        CurrentPlayAreaHeight = Mathf.Max(1, Mathf.RoundToInt(h - bandPx * 2f));
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsActive) return;

        SyncWindowSize();

        if (scene.name == "Game")
        {
            EnsureGameHud();
            BindGame(FindObjectOfType<Game>());
            return;
        }

        UnbindGame();
        ResetGameplayCameras();
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
        ApplyCameraBands(game.camera);
    }

    private static void OnGameDisposed(Game game)
    {
        ResetCamera(game.camera);
        SyncWindowSize();
    }

    private static void EnsureGameHud()
    {
        if (Object.FindObjectOfType<CytoidLabHudController>() != null) return;

        var hudGo = new GameObject("CytoidLabHud");
        hudGo.AddComponent<CytoidLabHudController>();
    }

    private static void ResetGameplayCameras()
    {
        foreach (var cam in Object.FindObjectsOfType<Camera>())
        {
            ResetCamera(cam);
        }
    }
}
