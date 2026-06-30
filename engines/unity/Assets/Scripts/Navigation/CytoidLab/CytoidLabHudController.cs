using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CytoidLabHudController : MonoBehaviour
{
    public const float UiSpacing = 8f;
    public const float ButtonHeight = 48f;
    public static float HudBandHeight => CytoidLabShell.HudBandHeightPx;

    private static float ScaledHudBandHeightPx => CytoidLabShell.GetHudBandHeightPx();

    private Font uiFont;
    private Canvas canvas;
    private Game game;
    private Slider timeSlider;
    private Text timeText;
    private Text levelInfoText;
    private Text statusText;
    private Text versionText;
    private Button playPauseButton;
    private Button fullscreenButton;
    private Button autoButton;
    private Button hitSoundButton;
    private Button noteIdsButton;
    private Transform topBar;
    private Transform bottomBar;
    private bool isDraggingSlider;
    private bool isResyncing;
    private bool wasPlayingBeforeDrag;
    private float hudVisibility;

    private const float HudEdgeSize = 60f;
    private const float HudAnimationSpeed = 10f;

    private void Awake()
    {
        if (!ShouldShowHud())
        {
            enabled = false;
            return;
        }

        uiFont = Resources.Load<Font>("Fonts/Nunito-Regular");
        if (uiFont == null)
        {
            Debug.LogWarning("[CytoidLab] Nunito-Regular font not found; falling back to default font.");
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }

    private bool ShouldShowHud()
    {
        return Application.platform == RuntimePlatform.WindowsPlayer ||
               Application.platform == RuntimePlatform.WindowsEditor;
    }

    private async void Start()
    {
        if (!ShouldShowHud()) return;

        await UniTask.WaitUntil(() => FindObjectOfType<Game>() != null);
        game = FindObjectOfType<Game>();
        if (game == null)
        {
            Debug.LogError("[CytoidLabHud] No Game instance found.");
            return;
        }

        game.UseInstantPauseResume = true;

        game.onGameLoaded.AddListener(_ => OnGameLoaded());
        game.onGameStarted.AddListener(_ => OnGameStarted());
        game.onGamePaused.AddListener(_ => UpdatePlayPauseLabel());
        game.onGameUnpaused.AddListener(_ => UpdatePlayPauseLabel());
        game.onGameCompleted.AddListener(_ => UpdatePlayPauseLabel());
        game.onGameFailed.AddListener(_ => UpdatePlayPauseLabel());
        game.onGameAborted.AddListener(_ => Destroy(gameObject));
        game.onGameDisposed.AddListener(_ => Destroy(gameObject));

        try
        {
            BuildHud();
            Debug.Log("[CytoidLab] HUD built successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[CytoidLab] Failed to build HUD: {e}");
        }
    }

    private void Update()
    {
        if (game == null || !game.IsLoaded) return;

        if (timeSlider != null && !isDraggingSlider && game.State.IsPlaying)
        {
            var progress = game.MusicLength > 0 ? game.Music.PlaybackTime / game.MusicLength : 0;
            timeSlider.SetValueWithoutNotify(Mathf.Clamp01(progress));
        }

        if (timeText != null)
        {
            var current = game.MusicLength > 0 ? game.Music.PlaybackTime : 0;
            var total = game.MusicLength;
            timeText.text = $"{FormatTime(current)} / {FormatTime(total)}";
        }

        if (GameInputCompat.WasEscapePressedThisFrame())
        {
            if (UnityEngine.Screen.fullScreen)
            {
                ExitFullscreen();
            }
            else
            {
                TogglePause();
            }
        }

        if (Input.GetKeyDown(KeyCode.F11))
        {
            ToggleFullscreen();
        }

        UpdateHudVisibility();
    }

    private void UpdateHudVisibility()
    {
        if (topBar == null || bottomBar == null) return;

        var mouseY = Input.mousePosition.y;
        var bandPx = ScaledHudBandHeightPx;
        var edgeSize = HudEdgeSize * UnityEngine.Screen.height / Mathf.Max(1, CytoidLabShell.WindowHeight);
        var nearTop = mouseY >= UnityEngine.Screen.height - edgeSize;
        var nearBottom = mouseY <= edgeSize;
        var wantVisible = nearTop || nearBottom || isDraggingSlider;
        hudVisibility = Mathf.MoveTowards(hudVisibility, wantVisible ? 1f : 0f, Time.unscaledDeltaTime * HudAnimationSpeed);

        var topRect = topBar.GetComponent<RectTransform>();
        topRect.anchoredPosition = new Vector2(0, bandPx * (1 - hudVisibility));

        var bottomRect = bottomBar.GetComponent<RectTransform>();
        bottomRect.anchoredPosition = new Vector2(0, -bandPx * (1 - hudVisibility));
    }

    private void OnGameLoaded()
    {
        SetStatus("");
        UpdatePlayPauseLabel();
        UpdateLevelInfo();
        UpdateAutoButton();
        UpdateHitSoundButton();
        UpdateNoteIdsButton();
    }

    private void OnGameStarted()
    {
        UpdatePlayPauseLabel();
        UpdateLevelInfo();
    }

    private void UpdateLevelInfo()
    {
        if (levelInfoText == null || game == null || !game.IsLoaded) return;

        var level = game.Level;
        var difficulty = game.Difficulty;
        if (level == null || difficulty == null)
        {
            levelInfoText.text = "";
            return;
        }

        var title = level.Meta.title ?? level.Meta.id;
        var diffLevel = level.Meta.GetDifficultyLevel(difficulty.Id);
        levelInfoText.text = $"{title}  ·  {difficulty.Id} {diffLevel}";
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private void BuildHud()
    {
        var go = new GameObject("CytoidLabHud");
        canvas = go.AddComponent<Canvas>();
        if (canvas == null) throw new InvalidOperationException("Failed to add Canvas component.");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = go.AddComponent<CanvasScaler>();
        if (scaler == null) throw new InvalidOperationException("Failed to add CanvasScaler component.");
        CytoidLabShell.ConfigureCanvasScaler(scaler);
        if (go.AddComponent<GraphicRaycaster>() == null) throw new InvalidOperationException("Failed to add GraphicRaycaster component.");

        if (FindObjectOfType<EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        var root = CreateUiObject("Root", canvas.transform).transform;
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        CreateHudChromeBand(root, true);
        CreateHudChromeBand(root, false);

        // Top bar
        topBar = CreateUiObject("TopBar", root).transform;
        var topRect = topBar.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0, 1);
        topRect.anchorMax = Vector2.one;
        topRect.pivot = new Vector2(0.5f, 1);
        topRect.sizeDelta = new Vector2(0, HudBandHeight);
        var topImage = topBar.gameObject.AddComponent<Image>();
        topImage.color = new Color(0, 0, 0, 0.5f);
        var topHlg = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        topHlg.padding = new RectOffset(8, 8, 4, 4);
        topHlg.spacing = UiSpacing;
        topHlg.childControlWidth = false;
        topHlg.childForceExpandWidth = false;
        topHlg.childControlHeight = true;
        topHlg.childForceExpandHeight = true;

        var backButton = CreateButton(topBar, "Back", () => game?.Abort());
        backButton.GetComponent<LayoutElement>().preferredWidth = 70;

        var resetButton = CreateButton(topBar, "Reset", HardReloadPlayfield);
        resetButton.GetComponent<LayoutElement>().preferredWidth = 70;

        playPauseButton = CreateButton(topBar, "Pause", () => TogglePause());
        playPauseButton.GetComponent<LayoutElement>().preferredWidth = 70;

        autoButton = CreateButton(topBar, "Auto", () => ToggleAuto());
        autoButton.GetComponent<LayoutElement>().preferredWidth = 60;
        var autoColors = autoButton.colors;
        autoColors.normalColor = new Color(0.25f, 0.25f, 0.3f);
        autoButton.colors = autoColors;

        hitSoundButton = CreateButton(topBar, "Hitsound", () => ToggleHitSound());
        hitSoundButton.GetComponent<LayoutElement>().preferredWidth = 80;
        var hitSoundColors = hitSoundButton.colors;
        hitSoundColors.normalColor = new Color(0.25f, 0.35f, 0.55f);
        hitSoundButton.colors = hitSoundColors;

        noteIdsButton = CreateButton(topBar, "IDs", () => ToggleNoteIds());
        noteIdsButton.GetComponent<LayoutElement>().preferredWidth = 56;
        var noteIdsColors = noteIdsButton.colors;
        noteIdsColors.normalColor = new Color(0.25f, 0.25f, 0.3f);
        noteIdsButton.colors = noteIdsColors;

        fullscreenButton = CreateButton(topBar, "Fullscreen", () => ToggleFullscreen());
        fullscreenButton.GetComponent<LayoutElement>().preferredWidth = 90;

        timeText = CreateText(topBar, "00:00 / 00:00", 16, TextAnchor.MiddleCenter);
        timeText.GetComponent<LayoutElement>().preferredWidth = 110;

        levelInfoText = CreateText(topBar, "", 16, TextAnchor.MiddleCenter);
        levelInfoText.GetComponent<LayoutElement>().flexibleWidth = 1;

        var spacer = CreateUiObject("Spacer", topBar);
        spacer.AddComponent<LayoutElement>().flexibleWidth = 1;

        statusText = CreateText(topBar, "", 14, TextAnchor.MiddleRight);
        statusText.color = new Color(1, 0.6f, 0.3f);
        statusText.GetComponent<LayoutElement>().preferredWidth = 240;

        // Bottom bar with timeline slider
        bottomBar = CreateUiObject("BottomBar", root).transform;
        var bottomRect = bottomBar.GetComponent<RectTransform>();
        bottomRect.anchorMin = Vector2.zero;
        bottomRect.anchorMax = new Vector2(1, 0);
        bottomRect.pivot = new Vector2(0.5f, 0);
        bottomRect.sizeDelta = new Vector2(0, HudBandHeight);
        var bottomImage = bottomBar.gameObject.AddComponent<Image>();
        bottomImage.color = new Color(0, 0, 0, 0.5f);
        var bottomHlg = bottomBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        bottomHlg.padding = new RectOffset(8, 8, 4, 4);
        bottomHlg.spacing = UiSpacing;
        bottomHlg.childControlWidth = true;
        bottomHlg.childForceExpandWidth = false;
        bottomHlg.childControlHeight = true;
        bottomHlg.childForceExpandHeight = true;

        var sliderGo = CreateUiObject("TimeSlider", bottomBar);
        timeSlider = sliderGo.AddComponent<Slider>();
        timeSlider.minValue = 0;
        timeSlider.maxValue = 1;
        timeSlider.value = 0;
        var sliderLayout = sliderGo.AddComponent<LayoutElement>();
        sliderLayout.flexibleWidth = 1;
        sliderLayout.minWidth = 200;

        versionText = CreateText(bottomBar, CytoidLabVersion.DisplayName, 13, TextAnchor.MiddleRight);
        versionText.color = new Color(1f, 1f, 1f, 0.45f);
        var versionLayout = versionText.GetComponent<LayoutElement>();
        versionLayout.flexibleWidth = 0;
        versionLayout.preferredWidth = 52;

        var sliderBg = CreateUiObject("Background", sliderGo.transform);
        var sliderBgRect = sliderBg.GetComponent<RectTransform>();
        sliderBgRect.anchorMin = Vector2.zero;
        sliderBgRect.anchorMax = Vector2.one;
        sliderBgRect.sizeDelta = Vector2.zero;
        var sliderBgImage = sliderBg.AddComponent<Image>();
        sliderBgImage.color = new Color(0.2f, 0.2f, 0.2f);
        timeSlider.targetGraphic = sliderBgImage;

        var fillArea = CreateUiObject("Fill Area", sliderGo.transform);
        var fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = Vector2.zero;

        var fill = CreateUiObject("Fill", fillArea.transform);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        var fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.3f, 0.6f, 1f);

        var handleArea = CreateUiObject("Handle Slide Area", sliderGo.transform);
        var handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.sizeDelta = Vector2.zero;

        var handle = CreateUiObject("Handle", handleArea.transform);
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(20, 20);
        var handleImage = handle.AddComponent<Image>();
        handleImage.color = Color.white;

        timeSlider.fillRect = fillRect;
        timeSlider.handleRect = handleRect;

        timeSlider.onValueChanged.AddListener(OnSliderValueChanged);

        var sliderEvents = sliderGo.AddComponent<EventTrigger>();
        var beginDrag = new EventTrigger.Entry {eventID = EventTriggerType.BeginDrag};
        beginDrag.callback.AddListener(_ => OnSliderBeginDrag());
        sliderEvents.triggers.Add(beginDrag);

        var endDrag = new EventTrigger.Entry {eventID = EventTriggerType.EndDrag};
        endDrag.callback.AddListener(_ => OnSliderEndDrag());
        sliderEvents.triggers.Add(endDrag);
    }

    private void CreateHudChromeBand(Transform parent, bool top)
    {
        var band = CreateUiObject(top ? "TopChrome" : "BottomChrome", parent).transform;
        var rect = band.GetComponent<RectTransform>();
        var bandPx = HudBandHeight;
        if (top)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(0, bandPx);
            rect.anchoredPosition = Vector2.zero;
        }
        else
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.sizeDelta = new Vector2(0, bandPx);
            rect.anchoredPosition = Vector2.zero;
        }

        var image = band.gameObject.AddComponent<Image>();
        image.color = new Color(0.02f, 0.02f, 0.04f, 1f);
        image.raycastTarget = false;
    }

    private Text CreateText(Transform parent, string content, int fontSize, TextAnchor anchor)
    {
        var go = CreateUiObject("Text", parent);
        var text = go.AddComponent<Text>();
        text.font = uiFont;
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        go.AddComponent<LayoutElement>();
        return text;
    }

    private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = CreateUiObject("Button", parent);
        var image = go.AddComponent<Image>();
        image.color = new Color(0.25f, 0.35f, 0.55f);
        image.type = Image.Type.Simple;

        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        // LayoutElement is required by callers that set preferredWidth.
        go.AddComponent<LayoutElement>();

        var textGo = CreateUiObject("Label", go.transform);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var text = textGo.AddComponent<Text>();
        text.font = uiFont;
        text.text = label;
        text.fontSize = 16;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        return btn;
    }

    private void OnSliderValueChanged(float value)
    {
        if (!isDraggingSlider || game == null || !game.IsLoaded || isResyncing) return;

        var targetTime = value * game.MusicLength;
        game.PreviewTimeline(targetTime);
    }

    private void OnSliderBeginDrag()
    {
        if (game == null || !game.IsLoaded || isResyncing) return;
        isDraggingSlider = true;
        wasPlayingBeforeDrag = game.State.IsPlaying;
        if (game.State.IsPlaying)
        {
            game.Pause();
        }
    }

    private async UniTask OnSliderEndDragAsync()
    {
        isDraggingSlider = false;
        if (game == null || !game.IsLoaded) return;

        isResyncing = true;
        try
        {
            var targetTime = timeSlider != null ? timeSlider.value * game.MusicLength : 0f;
            await game.ResyncPlayfieldToTime(targetTime, wasPlayingBeforeDrag);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CytoidLab] Timeline resync failed: {e}");
            SetStatus("Resync failed.");
        }
        finally
        {
            isResyncing = false;
            UpdatePlayPauseLabel();
        }
    }

    private void OnSliderEndDrag()
    {
        OnSliderEndDragAsync().Forget();
    }

    private void HardReloadPlayfield()
    {
        if (game == null || !game.IsLoaded || isResyncing) return;

        if (game.State.IsPlaying)
        {
            game.Pause();
        }

        game.HardReloadPlayfield();
        SetStatus("Reloading...");
    }

    private void TogglePause()
    {
        if (game == null || !game.IsLoaded) return;

        if (game.State.IsPlaying)
        {
            game.Pause();
        }
        else if (!game.State.IsCompleted && !game.State.IsFailed)
        {
            game.WillUnpause();
        }
    }

    private void ToggleAuto()
    {
        if (game == null || game.State == null) return;

        if (game.State.Mods.Contains(Mod.Auto))
        {
            game.State.Mods.Remove(Mod.Auto);
        }
        else
        {
            game.State.Mods.Add(Mod.Auto);
        }

        UpdateAutoButton();
        SetStatus(game.State.Mods.Contains(Mod.Auto) ? "Auto enabled." : "Auto disabled.");
    }

    private void UpdateAutoButton()
    {
        if (autoButton == null || game == null || game.State == null) return;

        var enabled = game.State.Mods.Contains(Mod.Auto);
        var text = autoButton.GetComponentInChildren<Text>();
        if (text != null) text.text = enabled ? "Auto: On" : "Auto: Off";
        var colors = autoButton.colors;
        colors.normalColor = enabled ? new Color(0.2f, 0.7f, 0.3f) : new Color(0.25f, 0.25f, 0.3f);
        autoButton.colors = colors;
    }

    private async void ToggleHitSound()
    {
        var wasEnabled = Context.Player.Settings.HitSound != "none";
        Context.Player.Settings.HitSound = wasEnabled ? "none" : "click1";
        if (!wasEnabled)
        {
            await LoadHitSoundAsync();
        }
        UpdateHitSoundButton();
        SetStatus(wasEnabled ? "Hitsound disabled." : "Hitsound enabled.");
    }

    private async UniTask LoadHitSoundAsync()
    {
        try
        {
            if (Context.Player.Settings.HitSound == "none") return;
            if (Context.AudioManager.IsLoaded("HitSound")) return;
            var resource = await Resources.LoadAsync<AudioClip>("Audio/HitSounds/" + Context.Player.Settings.HitSound);
            Context.AudioManager.Load("HitSound", resource as AudioClip, isResource: true);
        }
        catch (Exception e)
        {
            Debug.LogError($"[CytoidLabHud] Failed to load hit sound: {e}");
        }
    }

    private void UpdateHitSoundButton()
    {
        if (hitSoundButton == null) return;

        var enabled = Context.Player.Settings.HitSound != "none";
        var text = hitSoundButton.GetComponentInChildren<Text>();
        if (text != null) text.text = enabled ? "Hitsound: On" : "Hitsound: Off";
        var colors = hitSoundButton.colors;
        colors.normalColor = enabled ? new Color(0.3f, 0.6f, 1f) : new Color(0.25f, 0.25f, 0.3f);
        hitSoundButton.colors = colors;
    }

    private void ToggleNoteIds()
    {
        if (game == null || game.Config == null) return;

        var enabled = !Context.Player.Settings.DisplayNoteIds;
        Context.Player.Settings.DisplayNoteIds = enabled;
        game.Config.DisplayNoteIds = enabled;
        RefreshSpawnedNoteIdLabels();
        UpdateNoteIdsButton();
        SetStatus(enabled ? "Note IDs shown." : "Note IDs hidden.");
    }

    private void RefreshSpawnedNoteIdLabels()
    {
        if (game?.ObjectPool == null) return;

        foreach (var note in game.ObjectPool.SpawnedNotes.Values)
        {
            if (note?.Renderer is ClassicNoteRenderer classicRenderer)
            {
                classicRenderer.OnNoteLoaded();
            }
        }
    }

    private void UpdateNoteIdsButton()
    {
        if (noteIdsButton == null) return;

        var enabled = Context.Player.Settings.DisplayNoteIds;
        var text = noteIdsButton.GetComponentInChildren<Text>();
        if (text != null) text.text = enabled ? "IDs: On" : "IDs: Off";
        var colors = noteIdsButton.colors;
        colors.normalColor = enabled ? new Color(0.55f, 0.45f, 0.2f) : new Color(0.25f, 0.25f, 0.3f);
        noteIdsButton.colors = colors;
    }

    private void UpdatePlayPauseLabel()
    {
        if (playPauseButton == null || game == null || !game.IsLoaded) return;

        var text = playPauseButton.GetComponentInChildren<Text>();
        if (text == null) return;

        if (game.State.IsCompleted || game.State.IsFailed)
        {
            text.text = "Done";
            playPauseButton.interactable = false;
        }
        else if (game.State.IsPlaying)
        {
            text.text = "Pause";
            playPauseButton.interactable = true;
        }
        else
        {
            text.text = "Play";
            playPauseButton.interactable = true;
        }
    }

    private void ToggleFullscreen()
    {
        if (UnityEngine.Screen.fullScreen)
        {
            ExitFullscreen();
        }
        else
        {
            EnterFullscreen();
        }
    }

    private void EnterFullscreen()
    {
        var res = UnityEngine.Screen.currentResolution;
        UnityEngine.Screen.SetResolution(res.width, res.height, FullScreenMode.FullScreenWindow);
        UpdateFullscreenButtonLabel();
    }

    private void ExitFullscreen()
    {
        CytoidLabShell.RestoreWindowedSize();
        UpdateFullscreenButtonLabel();
    }

    private void UpdateFullscreenButtonLabel()
    {
        if (fullscreenButton == null) return;
        var text = fullscreenButton.GetComponentInChildren<Text>();
        if (text != null) text.text = UnityEngine.Screen.fullScreen ? "Windowed" : "Fullscreen";
    }

    public void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }

    private static string FormatTime(float seconds)
    {
        var t = TimeSpan.FromSeconds(Mathf.Max(0, seconds));
        return $"{t.Minutes:D2}:{t.Seconds:D2}";
    }
}
