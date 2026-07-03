using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class CytoidLabHudController : MonoBehaviour
{
    public const float UiSpacing = 6f;
    public const float ButtonHeight = 32f;

    private Font uiFont;
    private Canvas canvas;
    private Game game;
    private Slider timeSlider;
    private Text timeText;
    private Text statusText;
    private Text versionText;
    private Button playPauseButton;
    private Button fullscreenButton;
    private Button autoButton;
    private Button hitSoundButton;
    private Button noteIdsButton;
    private Button skipEndButton;
    private Transform topBar;
    private Transform bottomBar;
    private Transform versionLabel;
    private bool isSliderInteracting;
    private bool sliderDidDrag;
    private bool isResyncing;
    private bool wasPlayingBeforeDrag;
    private float topHudVisibility;
    private float bottomHudVisibility;

    private const float HudEdgeSize = 40f;
    private const float HudAnimationSpeed = 12f;
    private const float TimelineTrackHeight = 6f;
    private const float TimelineHitHeight = 28f;
    private const float TimelineHandleSize = 12f;

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

        if (timeSlider != null && !isSliderInteracting && game.State.IsPlaying)
        {
            var progress = game.MusicLength > 0 ? game.Music.SourceTimeSeconds / game.MusicLength : 0;
            timeSlider.SetValueWithoutNotify(Mathf.Clamp01(progress));
        }

        if (timeText != null)
        {
            var current = game.MusicLength > 0 ? game.Music.SourceTimeSeconds : 0;
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

            CytoidLabUiInput.ClearUiSelection();
            return;
        }

        if (GameInputCompat.WasKeyPressedThisFrame(Key.F11))
        {
            ToggleFullscreen();
            return;
        }

        if (!isSliderInteracting && !isResyncing && GameInputCompat.WasSpacePressedThisFrame())
        {
            TogglePause();
            CytoidLabUiInput.ClearUiSelection();
        }
    }

    private void LateUpdate()
    {
        UpdateHudVisibility();
    }

    private void UpdateHudVisibility()
    {
        if (topBar == null || bottomBar == null) return;

        var topBandPx = CytoidLabShell.GetTopHudOverlayHeightPx();
        var bottomBandPx = CytoidLabShell.GetBottomHudOverlayHeightPx();
        var edgeSize = HudEdgeSize * UnityEngine.Screen.height / Mathf.Max(1, CytoidLabShell.PlayAreaHeight);
        var step = Time.unscaledDeltaTime * HudAnimationSpeed;

        var wantTopVisible = false;
        var wantBottomVisible = isSliderInteracting;

        if (GameInputCompat.TryGetPointerScreenPosition(out var mouse))
        {
            wantTopVisible = mouse.y >= UnityEngine.Screen.height - edgeSize;
            wantBottomVisible = wantBottomVisible || mouse.y <= edgeSize;
        }

        topHudVisibility = Mathf.MoveTowards(topHudVisibility, wantTopVisible ? 1f : 0f, step);
        bottomHudVisibility = Mathf.MoveTowards(bottomHudVisibility, wantBottomVisible ? 1f : 0f, step);

        ApplyHudBarVisibility(topBar, topHudVisibility, topBandPx, topAnchored: true);
        ApplyHudBarVisibility(bottomBar, bottomHudVisibility, bottomBandPx, topAnchored: false,
            forceInteractable: isSliderInteracting);

        if (versionLabel != null)
        {
            var versionRect = versionLabel.GetComponent<RectTransform>();
            versionRect.anchoredPosition = new Vector2(-12, -8 + topBandPx * (1f - topHudVisibility));
            if (versionText != null)
            {
                versionText.color = new Color(1f, 1f, 1f, 0.45f * topHudVisibility);
            }
        }
    }

    private static void ApplyHudBarVisibility(Transform bar, float visibility, float bandPx, bool topAnchored,
        bool forceInteractable = false)
    {
        var rect = bar.GetComponent<RectTransform>();
        var hideOffset = bandPx * (1f - visibility);
        rect.anchoredPosition = topAnchored
            ? new Vector2(0f, hideOffset)
            : new Vector2(0f, -hideOffset);

        var canvasGroup = bar.GetComponent<CanvasGroup>();
        if (canvasGroup == null) return;

        canvasGroup.alpha = Mathf.Max(visibility, forceInteractable ? 1f : 0f);
        var interactable = forceInteractable || visibility > 0.15f;
        canvasGroup.interactable = interactable;
        canvasGroup.blocksRaycasts = interactable;
    }

    private void OnGameLoaded()
    {
        CytoidLabUiInput.ClearUiSelection();
        SetStatus("");
        UpdatePlayPauseLabel();
        UpdateAutoButton();
        UpdateHitSoundButton();
        UpdateNoteIdsButton();
        UpdateSkipEndButton();
    }

    private void OnGameStarted()
    {
        UpdatePlayPauseLabel();
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

        CytoidLabUiInput.EnsureEventSystem();

        var root = CreateUiObject("Root", canvas.transform).transform;
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        // Top bar (overlay)
        topBar = CreateUiObject("TopBar", root).transform;
        var topRect = topBar.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0, 1);
        topRect.anchorMax = Vector2.one;
        topRect.pivot = new Vector2(0.5f, 1);
        topRect.sizeDelta = new Vector2(0, CytoidLabShell.TopHudOverlayHeightPx);
        var topImage = topBar.gameObject.AddComponent<Image>();
        topImage.color = new Color(0, 0, 0, 0.55f);
        topBar.gameObject.AddComponent<CanvasGroup>();
        var topHlg = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        topHlg.padding = new RectOffset(6, 6, 2, 2);
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

        skipEndButton = CreateButton(topBar, "End: On", () => ToggleSkipEnd());
        skipEndButton.GetComponent<LayoutElement>().preferredWidth = 64;
        var skipEndColors = skipEndButton.colors;
        skipEndColors.normalColor = new Color(0.25f, 0.25f, 0.3f);
        skipEndButton.colors = skipEndColors;

        fullscreenButton = CreateButton(topBar, "Fullscreen", () => ToggleFullscreen());
        fullscreenButton.GetComponent<LayoutElement>().preferredWidth = 90;

        timeText = CreateText(topBar, "00:00 / 00:00", 14, TextAnchor.MiddleCenter);
        timeText.GetComponent<LayoutElement>().preferredWidth = 110;

        var spacer = CreateUiObject("Spacer", topBar);
        spacer.AddComponent<LayoutElement>().flexibleWidth = 1;

        statusText = CreateText(topBar, "", 14, TextAnchor.MiddleRight);
        statusText.color = new Color(1, 0.6f, 0.3f);
        statusText.GetComponent<LayoutElement>().preferredWidth = 160;

        // Top-right version watermark (follows top bar auto-hide)
        versionLabel = CreateUiObject("Version", root).transform;
        var versionRect = versionLabel.GetComponent<RectTransform>();
        versionRect.anchorMin = new Vector2(1, 1);
        versionRect.anchorMax = new Vector2(1, 1);
        versionRect.pivot = new Vector2(1, 1);
        versionRect.anchoredPosition = new Vector2(-12, -8);
        versionRect.sizeDelta = new Vector2(64, 20);
        versionText = versionLabel.gameObject.AddComponent<Text>();
        versionText.font = uiFont;
        versionText.text = CytoidLabVersion.DisplayName;
        versionText.fontSize = 13;
        versionText.alignment = TextAnchor.MiddleRight;
        versionText.color = new Color(1f, 1f, 1f, 0.45f);
        versionText.raycastTarget = false;

        // Bottom bar with timeline slider (thin overlay)
        bottomBar = CreateUiObject("BottomBar", root).transform;
        var bottomRect = bottomBar.GetComponent<RectTransform>();
        bottomRect.anchorMin = Vector2.zero;
        bottomRect.anchorMax = new Vector2(1, 0);
        bottomRect.pivot = new Vector2(0.8f, 0);
        bottomRect.sizeDelta = new Vector2(0, CytoidLabShell.BottomHudOverlayHeightPx);
        var bottomImage = bottomBar.gameObject.AddComponent<Image>();
        bottomImage.color = new Color(0, 0, 0, 0.55f);
        bottomBar.gameObject.AddComponent<CanvasGroup>();
        var bottomHlg = bottomBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        bottomHlg.padding = new RectOffset(8, 8, 2, 2);
        bottomHlg.spacing = 0;
        bottomHlg.childControlWidth = true;
        bottomHlg.childForceExpandWidth = true;
        bottomHlg.childControlHeight = true;
        bottomHlg.childForceExpandHeight = true;

        var sliderGo = CreateUiObject("TimeSlider", bottomBar);
        timeSlider = sliderGo.AddComponent<Slider>();
        timeSlider.minValue = 0;
        timeSlider.maxValue = 1;
        timeSlider.value = 0;
        var sliderLayout = sliderGo.AddComponent<LayoutElement>();
        sliderLayout.flexibleWidth = 1;
        sliderLayout.preferredHeight = TimelineTrackHeight;
        sliderLayout.minWidth = 0;

        // Tall transparent hit target; visual track stays thin.
        var hitArea = CreateUiObject("HitArea", sliderGo.transform);
        var hitAreaRect = hitArea.GetComponent<RectTransform>();
        hitAreaRect.anchorMin = new Vector2(0f, 0.5f);
        hitAreaRect.anchorMax = new Vector2(1f, 0.5f);
        hitAreaRect.pivot = new Vector2(0.5f, 0.5f);
        hitAreaRect.sizeDelta = new Vector2(0f, TimelineHitHeight);
        var hitImage = hitArea.AddComponent<Image>();
        hitImage.color = new Color(0f, 0f, 0f, 0f);
        hitImage.raycastTarget = true;
        timeSlider.targetGraphic = hitImage;

        var sliderBg = CreateUiObject("Background", sliderGo.transform);
        var sliderBgRect = sliderBg.GetComponent<RectTransform>();
        sliderBgRect.anchorMin = new Vector2(0f, 0.5f);
        sliderBgRect.anchorMax = new Vector2(1f, 0.5f);
        sliderBgRect.pivot = new Vector2(0.5f, 0.5f);
        sliderBgRect.sizeDelta = new Vector2(0f, TimelineTrackHeight);
        var sliderBgImage = sliderBg.AddComponent<Image>();
        sliderBgImage.color = new Color(0.2f, 0.2f, 0.2f);
        sliderBgImage.raycastTarget = false;

        var fillArea = CreateUiObject("Fill Area", sliderGo.transform);
        var fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
        fillAreaRect.sizeDelta = new Vector2(0f, TimelineTrackHeight);

        var fill = CreateUiObject("Fill", fillArea.transform);
        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        var fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.3f, 0.6f, 1f);
        fillImage.raycastTarget = false;

        var handleInset = TimelineHandleSize * 0.5f;
        var handleArea = CreateUiObject("Handle Slide Area", sliderGo.transform);
        var handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(handleInset, 0f);
        handleAreaRect.offsetMax = new Vector2(-handleInset, 0f);

        var handle = CreateUiObject("Handle", handleArea.transform);
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(TimelineHandleSize, TimelineHitHeight);
        var handleImage = handle.AddComponent<Image>();
        handleImage.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        handleImage.color = Color.white;

        var handleVisual = CreateUiObject("HandleVisual", handle.transform);
        var handleVisualRect = handleVisual.GetComponent<RectTransform>();
        handleVisualRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleVisualRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleVisualRect.pivot = new Vector2(0.5f, 0.5f);
        handleVisualRect.sizeDelta = new Vector2(TimelineHandleSize, TimelineHandleSize);
        var handleVisualImage = handleVisual.AddComponent<Image>();
        handleVisualImage.sprite = handleImage.sprite;
        handleVisualImage.color = Color.white;
        handleVisualImage.raycastTarget = false;
        handleImage.color = new Color(1f, 1f, 1f, 0f);

        timeSlider.fillRect = fillRect;
        timeSlider.handleRect = handleRect;

        timeSlider.onValueChanged.AddListener(OnSliderValueChanged);

        var sliderEvents = sliderGo.AddComponent<EventTrigger>();

        var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pointerDown.callback.AddListener(_ => OnSliderPointerDown());
        sliderEvents.triggers.Add(pointerDown);

        var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pointerUp.callback.AddListener(_ => OnSliderPointerUp());
        sliderEvents.triggers.Add(pointerUp);

        var beginDrag = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
        beginDrag.callback.AddListener(_ => OnSliderBeginDrag());
        sliderEvents.triggers.Add(beginDrag);

        var endDrag = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        endDrag.callback.AddListener(_ => OnSliderEndDrag());
        sliderEvents.triggers.Add(endDrag);

        topHudVisibility = 0f;
        bottomHudVisibility = 0f;
        UpdateHudVisibility();
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
        CytoidLabUiInput.DisableKeyboardNavigation(btn);

        // LayoutElement is required by callers that set preferredWidth.
        var layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = ButtonHeight;

        var textGo = CreateUiObject("Label", go.transform);
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var text = textGo.AddComponent<Text>();
        text.font = uiFont;
        text.text = label;
        text.fontSize = 14;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        return btn;
    }

    private void OnSliderValueChanged(float value)
    {
        if (!isSliderInteracting || game == null || !game.IsLoaded || isResyncing) return;

        var targetTime = value * game.MusicLength;
        game.PreviewTimeline(targetTime);
    }

    private void OnSliderPointerDown()
    {
        if (game == null || !game.IsLoaded || isResyncing) return;

        sliderDidDrag = false;
        isSliderInteracting = true;
        wasPlayingBeforeDrag = game.State.IsPlaying;
        // Suppress Auto/clear for entire drag until CommitSliderSeekAsync ends (RC-10).
        game.SuppressTimelineGameplayMutations = true;
        if (game.State.IsPlaying)
        {
            game.Pause();
        }
    }

    private void OnSliderBeginDrag()
    {
        sliderDidDrag = true;
    }

    private void OnSliderPointerUp()
    {
        // Click on track: no BeginDrag/EndDrag — settle on release like drag.
        if (!sliderDidDrag)
        {
            CommitSliderSeek();
        }
    }

    private void OnSliderEndDrag()
    {
        CommitSliderSeek();
    }

    private void CommitSliderSeek()
    {
        if (!isSliderInteracting) return;

        isSliderInteracting = false;
        CommitSliderSeekAsync().Forget();
    }

    private async UniTask CommitSliderSeekAsync()
    {
        // Let Slider apply click/drag position before resync.
        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

        if (game == null || !game.IsLoaded || isResyncing) return;

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
            // ResyncPlayfieldToTime clears suppress in its own finally; this covers early exit.
            game?.EndTimelineScrub();
            UpdatePlayPauseLabel();
        }
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
            if (Context.AudioManager.IsSfxLoaded("HitSound")) return;
            var resource = await Resources.LoadAsync<AudioClip>("Audio/HitSounds/" + Context.Player.Settings.HitSound);
            Context.AudioManager.LoadSfx("HitSound", resource as AudioClip, isResource: true);
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

    private void ToggleSkipEnd()
    {
        var enabled = !Context.Player.Settings.SkipMusicOnCompletion;
        Context.Player.Settings.SkipMusicOnCompletion = enabled;
        UpdateSkipEndButton();
        SetStatus(enabled ? "Skip end enabled." : "Skip end disabled — music will play out.");
    }

    private void UpdateSkipEndButton()
    {
        if (skipEndButton == null) return;

        var enabled = Context.Player.Settings.SkipMusicOnCompletion;
        var text = skipEndButton.GetComponentInChildren<Text>();
        if (text != null) text.text = enabled ? "End: On" : "End: Off";
        var colors = skipEndButton.colors;
        colors.normalColor = enabled ? new Color(0.2f, 0.7f, 0.3f) : new Color(0.25f, 0.25f, 0.3f);
        skipEndButton.colors = colors;
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
