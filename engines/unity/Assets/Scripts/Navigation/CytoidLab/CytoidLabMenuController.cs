using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CytoidLabMenuController : MonoBehaviour
{
    public const float UiSpacing = 8f;
    public const float ButtonHeight = 40f;
    public const float LevelButtonHeight = 40f;
    public const int TitleFontSize = 32;
    public const int HintFontSize = 15;
    public const int StatusFontSize = 15;
    public const int SectionFontSize = 20;
    public const int LevelRowFontSize = 17;
    public const int ButtonFontSize = 20;
    public const float LevelRowHeight = 80f;

    private const string DefaultSelectionHint = "Select a level or import .cytoidlevel / .zip files";

    private static readonly Difficulty[] DifficultyOptions = { Difficulty.Easy, Difficulty.Hard, Difficulty.Extreme };
    private static bool initialViewportApplied;
    private static readonly Color DifficultyAvailableColor = new Color(0.25f, 0.35f, 0.55f);
    private static readonly Color DifficultySelectedColor = new Color(0.3f, 0.6f, 1f);
    private static readonly Color DifficultyUnavailableColor = new Color(0.22f, 0.22f, 0.25f);

    private Font uiFont;
    private Canvas canvas;
    private Transform root;
    private Text statusText;
    private Text selectionHintText;
    private Transform levelListRoot;
    private ScrollRect levelScrollRect;
    private readonly Dictionary<Difficulty, Button> difficultyButtonMap = new Dictionary<Difficulty, Button>();
    private Button viewportCornerButton;
    private Text viewportCornerLabel;
    private Button helpButton;
    private GameObject helpUpdateBadge;
    private CytoidLabUpdater.UpdateInfo pendingUpdate;
    private bool isRefreshingLevelList;
    private bool updateCheckStarted;

    private Level selectedLevel;
    private Difficulty selectedDifficulty;
    private string pendingSelectLevelId;
    private readonly Dictionary<Level, Transform> levelRowMap = new Dictionary<Level, Transform>();

    private void Awake()
    {
        if (GameEmbedMode.IsBridgeEmbedded)
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

        try
        {
            BuildUi();
            Canvas.ForceUpdateCanvases();
            CytoidLabUi.RefreshRoundedCorners();
            Debug.Log("[CytoidLab] Menu UI built successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[CytoidLab] Failed to build menu UI: {e}");
        }
    }

    private async void Start()
    {
        if (GameEmbedMode.IsBridgeEmbedded) return;

        SetStatus("Initializing...");
        await UniTask.WaitUntil(() => Context.IsInitialized);
        ApplyLabMenuDefaults();
        if (!initialViewportApplied)
        {
            CytoidLabShell.ApplyViewportFromSettings();
            initialViewportApplied = true;
        }
        else
        {
            CytoidLabShell.CaptureWindowSizeFromScreen();
        }
        CytoidLabShell.ApplyPersistedDisplayMode();
        UpdateViewportCornerButton();
        ShowGameErrorIfAny();
        await RefreshLevelList();
        ProcessCommandLineImport();
        CheckForUpdatesInBackground();
    }

    private void OnEnable()
    {
        if (GameEmbedMode.IsBridgeEmbedded) return;
        if (!Context.IsInitialized) return;
        CytoidLabShell.CaptureWindowSizeFromScreen();
        UpdateViewportCornerButton();
        RefreshLevelList().Forget();
    }

    private void BuildUi()
    {
        var go = new GameObject("CytoidLabMenu");
        canvas = go.AddComponent<Canvas>();
        if (canvas == null) throw new InvalidOperationException("Failed to add Canvas component.");
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = go.AddComponent<CanvasScaler>();
        if (scaler == null) throw new InvalidOperationException("Failed to add CanvasScaler component.");
        CytoidLabShell.ConfigureCanvasScaler(scaler);
        if (go.AddComponent<GraphicRaycaster>() == null) throw new InvalidOperationException("Failed to add GraphicRaycaster component.");

        CytoidLabUiInput.EnsureEventSystem();

        // Full-screen background so the menu is visible even if layout has issues.
        var bgGo = CreateUiObject("Background", canvas.transform);
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        var bgImage = bgGo.AddComponent<Image>();
        bgImage.color = new Color(0.05f, 0.055f, 0.08f, 1f);

        BuildCornerButtons(canvas.transform);

        root = CreateUiObject("Root", canvas.transform).transform;
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = new Vector2(48, 16);
        rootRect.offsetMax = new Vector2(-48, -16);

        var vlg = root.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        var title = CreateText(root, $"Cytoid Lab {CytoidLabVersion.DisplayName}", TitleFontSize, TextAnchor.MiddleCenter);
        title.GetComponent<LayoutElement>().preferredHeight = 44;

        selectionHintText = CreateText(root,
            DefaultSelectionHint,
            HintFontSize, TextAnchor.MiddleCenter);
        selectionHintText.GetComponent<LayoutElement>().preferredHeight = 22;

        var importRow = CreateUiObject("ImportRow", root).transform;
        var importRowLe = importRow.gameObject.AddComponent<LayoutElement>();
        importRowLe.preferredHeight = ButtonHeight;
        importRowLe.minHeight = ButtonHeight;
        importRowLe.flexibleHeight = 0;
        var importHlg = importRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        importHlg.spacing = UiSpacing;
        importHlg.childAlignment = TextAnchor.MiddleCenter;
        importHlg.childControlWidth = true;
        importHlg.childControlHeight = true;
        importHlg.childForceExpandWidth = false;
        importHlg.childForceExpandHeight = false;

        // Keep Import at the previous fixed width (CreateButton default 400); square folder sits beside it.
        var importButton = CreateButton(importRow, "Import levels", () => ImportLevelFiles().Forget());
        var importLe = importButton.GetComponent<LayoutElement>();
        importLe.preferredHeight = ButtonHeight;
        importLe.minHeight = ButtonHeight;
        importLe.preferredWidth = 400;
        importLe.flexibleWidth = 0;
        importLe.flexibleHeight = 0;

        CreateFolderIconButton(importRow, OpenDataFolder);

        statusText = CreateText(root, "", StatusFontSize, TextAnchor.MiddleLeft);
        statusText.color = new Color(1, 0.8f, 0.4f);
        statusText.GetComponent<LayoutElement>().preferredHeight = 22;

        var scroll = CreateUiObject("LevelScroll", root);
        var scrollLe = scroll.AddComponent<LayoutElement>();
        scrollLe.preferredHeight = 200;
        scrollLe.minHeight = 160;
        scrollLe.flexibleHeight = 1;
        scrollLe.flexibleWidth = 1;
        scrollLe.minWidth = 400;
        var scrollComp = scroll.AddComponent<ScrollRect>();
        levelScrollRect = scrollComp;

        const float scrollbarWidth = 10f;

        var viewport = CreateUiObject("Viewport", scroll.transform);
        var vpRect = viewport.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero;
        vpRect.offsetMax = new Vector2(-(scrollbarWidth + 6f), 0);
        // RoundedCorners materials do not honor RectMask2D clip rects; use stencil Mask instead.
        var viewportBg = viewport.AddComponent<Image>();
        viewportBg.color = CytoidLabUi.PanelColor;
        CytoidLabUi.ApplyRoundedCorners(viewportBg, CytoidLabUi.PanelRadius);
        var viewportMask = viewport.AddComponent<Mask>();
        viewportMask.showMaskGraphic = true;

        levelListRoot = CreateUiObject("LevelList", viewport.transform).transform;
        var listRect = levelListRoot.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0, 1);
        listRect.anchorMax = new Vector2(1, 1);
        listRect.pivot = new Vector2(0.5f, 1);
        listRect.anchoredPosition = Vector2.zero;
        listRect.sizeDelta = new Vector2(0, 0);
        var listVlg = levelListRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        listVlg.spacing = 6;
        listVlg.padding = new RectOffset(4, 4, 4, 8);
        listVlg.childControlWidth = true;
        listVlg.childControlHeight = true;
        listVlg.childForceExpandWidth = true;
        listVlg.childForceExpandHeight = false;
        levelListRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollbar = CreateUiObject("Scrollbar", scroll.transform);
        var sbRect = scrollbar.GetComponent<RectTransform>();
        sbRect.anchorMin = new Vector2(1, 0);
        sbRect.anchorMax = Vector2.one;
        sbRect.pivot = new Vector2(1, 0.5f);
        sbRect.sizeDelta = new Vector2(scrollbarWidth, 0);
        var sb = scrollbar.AddComponent<Scrollbar>();
        sb.direction = Scrollbar.Direction.BottomToTop;
        var sbBg = scrollbar.AddComponent<Image>();
        sbBg.color = new Color(0.10f, 0.12f, 0.16f, 0.7f);
        CytoidLabUi.ApplyRoundedCorners(sbBg, scrollbarWidth * 0.5f);

        var slidingArea = CreateUiObject("Sliding Area", scrollbar.transform);
        var slidingRect = slidingArea.GetComponent<RectTransform>();
        slidingRect.anchorMin = Vector2.zero;
        slidingRect.anchorMax = Vector2.one;
        slidingRect.offsetMin = new Vector2(1f, 2f);
        slidingRect.offsetMax = new Vector2(-1f, -2f);

        var sbHandle = CreateUiObject("Handle", slidingArea.transform);
        var sbHandleRect = sbHandle.GetComponent<RectTransform>();
        sbHandleRect.anchorMin = Vector2.zero;
        sbHandleRect.anchorMax = Vector2.one;
        sbHandleRect.offsetMin = Vector2.zero;
        sbHandleRect.offsetMax = Vector2.zero;
        var sbHandleImage = sbHandle.AddComponent<Image>();
        sbHandleImage.color = new Color(0.38f, 0.58f, 0.90f, 0.95f);
        CytoidLabUi.ApplyRoundedCorners(sbHandleImage, (scrollbarWidth - 2f) * 0.5f);
        sb.targetGraphic = sbHandleImage;
        sb.handleRect = sbHandleRect;

        scrollComp.content = listRect;
        scrollComp.viewport = vpRect;
        scrollComp.verticalScrollbar = sb;
        scrollComp.vertical = true;
        scrollComp.horizontal = false;
        scrollComp.movementType = ScrollRect.MovementType.Clamped;

        var diffRoot = CreateUiObject("DifficultyRow", root).transform;
        diffRoot.gameObject.AddComponent<LayoutElement>().preferredHeight = ButtonHeight;
        var diffHlg = diffRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        diffHlg.spacing = UiSpacing;
        diffHlg.childControlWidth = true;
        diffHlg.childForceExpandWidth = true;
        diffHlg.childControlHeight = true;
        diffHlg.childForceExpandHeight = false;

        foreach (var diff in DifficultyOptions)
        {
            var captured = diff;
            var diffButton = CreateButton(diffRoot, diff.Id, () => SelectDifficulty(captured));
            var diffLe = diffButton.GetComponent<LayoutElement>();
            diffLe.preferredHeight = ButtonHeight;
            diffLe.preferredWidth = 0;
            diffLe.flexibleWidth = 1;
            difficultyButtonMap[diff] = diffButton;
        }

        var startButton = CreateButton(diffRoot, "Start", () => StartGame());
        var startLe = startButton.GetComponent<LayoutElement>();
        startLe.preferredHeight = ButtonHeight;
        startLe.preferredWidth = 132;
        startLe.flexibleWidth = 0;
        CytoidLabUi.ApplyRoundedButtonColors(startButton, CytoidLabUi.AccentGreen);
        // SoftButton-style pill radius on difficulty / start row.
        var startImage = startButton.GetComponent<Image>();
        if (startImage != null) CytoidLabUi.ApplyRoundedCorners(startImage, CytoidLabUi.SoftRadius);

        selectedDifficulty = Difficulty.Hard;
        UpdateDifficultyButtons();
        foreach (var pair in difficultyButtonMap)
        {
            var image = pair.Value.GetComponent<Image>();
            if (image != null) CytoidLabUi.ApplyRoundedCorners(image, CytoidLabUi.SoftRadius);
        }
    }

    private static void ApplyLabMenuDefaults()
    {
        Context.Player.Settings.RestrictPlayAreaAspectRatio = true;
        CytoidLabPreferences.LoadInto(Context.Player.Settings);
    }

    private void SelectViewportPreset(string presetId)
    {
        CytoidLabShell.ApplyViewportPreset(presetId);
        UpdateViewportCornerButton();
        SetViewportStatus();
    }

    private void SelectViewportSize(string sizeId)
    {
        CytoidLabShell.ApplyViewportSize(sizeId);
        UpdateViewportCornerButton();
        SetViewportStatus();
    }

    private void SetViewportStatus()
    {
        if (!Context.IsInitialized) return;

        var presetId = CytoidLabShell.NormalizeViewportPresetId(Context.Player.Settings.LabViewportPreset);
        var sizeId = CytoidLabShell.NormalizeViewportSizeId(Context.Player.Settings.LabViewportSize);
        var dimensions = CytoidLabShell.FormatViewportDimensions(presetId, sizeId);
        var sizeLabel = sizeId == CytoidLabShell.ViewportSizeLarge ? "Large" : "Small";
        SetStatus($"Viewport {presetId} ({sizeLabel}, {dimensions}). Press Start to apply layout.");
    }

    private void UpdateViewportCornerButton()
    {
        if (viewportCornerLabel == null) return;

        var selected = CytoidLabShell.NormalizeViewportPresetId(
            Context.IsInitialized ? Context.Player.Settings.LabViewportPreset : CytoidLabShell.ViewportPreset16x9);
        viewportCornerLabel.text = selected;

        if (viewportCornerButton == null) return;
        var colors = viewportCornerButton.colors;
        colors.normalColor = new Color(0.22f, 0.34f, 0.52f, 0.95f);
        viewportCornerButton.colors = colors;
    }

    private void OpenViewportOverlay()
    {
        CytoidLabViewportOverlay.Open(canvas.transform, uiFont, SelectViewportPreset, SelectViewportSize);
    }

    private void CheckForUpdatesInBackground()
    {
        if (updateCheckStarted || !CytoidLabUpdater.IsSupported) return;
        updateCheckStarted = true;
        CheckForUpdatesAsync().Forget();
    }

    private async UniTask CheckForUpdatesAsync()
    {
        var info = await CytoidLabUpdater.CheckForUpdateAsync(force: true);
        if (info == null)
        {
            pendingUpdate = null;
            if (helpUpdateBadge != null) helpUpdateBadge.SetActive(false);
            return;
        }

        pendingUpdate = info;
        if (helpUpdateBadge != null) helpUpdateBadge.SetActive(true);
        SetStatus($"Cytoid Lab v{info.Version} is available. Open Help (?) for the release link.");
        Debug.Log($"[CytoidLab] Update available: v{info.Version} {info.HtmlUrl}");
    }

    private void OpenHelp()
    {
        CytoidLabHelpOverlay.Open(canvas.transform, uiFont, pendingUpdate);
    }

    private void BuildCornerButtons(Transform parent)
    {
        const float buttonSize = 40f;
        const float viewportWidth = 56f;
        const float spacing = 8f;
        const float margin = 16f;

        helpButton = CreateCornerButton(parent, "HelpButton",
            new Vector2(-margin, -margin),
            new Vector2(buttonSize, buttonSize),
            OpenHelp,
            out var helpLabel);
        helpLabel.text = "?";
        helpLabel.fontSize = 24;
        helpLabel.fontStyle = FontStyle.Bold;
        helpUpdateBadge = CreateUpdateBadge(helpButton.transform);
        helpUpdateBadge.SetActive(false);

        var viewportX = -(margin + buttonSize + spacing);
        viewportCornerButton = CreateCornerButton(parent, "ViewportCornerButton",
            new Vector2(viewportX, -margin),
            new Vector2(viewportWidth, buttonSize),
            OpenViewportOverlay, out viewportCornerLabel);
        viewportCornerLabel.fontSize = 16;
        viewportCornerLabel.fontStyle = FontStyle.Bold;

        UpdateViewportCornerButton();
    }

    private Button CreateCornerButton(Transform parent, string name, Vector2 anchoredPosition, Vector2 size,
        UnityEngine.Events.UnityAction onClick, out Text label)
    {
        var go = CreateUiObject(name, parent);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var image = go.AddComponent<Image>();
        image.color = Color.white;
        CytoidLabUi.ApplyRoundedCorners(image, CytoidLabUi.ButtonRadius);

        var btn = go.AddComponent<Button>();
        CytoidLabUi.ApplyRoundedButtonColors(btn);
        btn.onClick.AddListener(onClick);
        CytoidLabUiInput.DisableKeyboardNavigation(btn);

        var labelGo = CreateUiObject("Label", go.transform);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        label = labelGo.AddComponent<Text>();
        label.font = uiFont;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;

        return btn;
    }

    private static GameObject CreateUpdateBadge(Transform parent)
    {
        var go = CreateUiObject("Badge", parent);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-4f, -4f);
        rect.sizeDelta = new Vector2(10f, 10f);
        var image = go.AddComponent<Image>();
        image.color = new Color(0.95f, 0.35f, 0.35f, 1f);
        image.raycastTarget = false;
        CytoidLabUi.ApplyRoundedCorners(image, 5f);
        return go;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
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
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize + 8;
        return text;
    }

    private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = CreateUiObject("Button", parent);
        var image = go.AddComponent<Image>();
        if (image == null) throw new InvalidOperationException("Failed to add Image to button.");
        image.color = Color.white;
        image.type = Image.Type.Simple;
        CytoidLabUi.ApplyRoundedCorners(image, CytoidLabUi.ButtonRadius);

        var btn = go.AddComponent<Button>();
        if (btn == null) throw new InvalidOperationException("Failed to add Button component.");
        btn.onClick.AddListener(onClick);
        CytoidLabUiInput.DisableKeyboardNavigation(btn);
        CytoidLabUi.ApplyRoundedButtonColors(btn);

        // LayoutElement is required by callers that set preferredHeight/Width.
        var buttonLe = go.AddComponent<LayoutElement>();
        buttonLe.preferredWidth = 400;

        var textGo = CreateUiObject("Label", go.transform);
        var textRect = textGo.GetComponent<RectTransform>();
        if (textRect == null) throw new InvalidOperationException("Label has no RectTransform.");
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        var text = textGo.AddComponent<Text>();
        if (text == null) throw new InvalidOperationException("Failed to add Text to label.");
        text.font = uiFont;
        text.text = label;
        text.fontSize = ButtonFontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        return btn;
    }

    private Button CreateFolderIconButton(Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        const float size = ButtonHeight;

        var go = CreateUiObject("OpenDataButton", parent);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);

        var image = go.AddComponent<Image>();
        image.color = Color.white;
        CytoidLabUi.ApplyRoundedCorners(image, CytoidLabUi.IconRadius);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = image;
        btn.onClick.AddListener(onClick);
        CytoidLabUiInput.DisableKeyboardNavigation(btn);
        CytoidLabUi.ApplyRoundedButtonColors(btn);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = size;
        le.preferredHeight = size;
        le.minWidth = size;
        le.minHeight = size;
        le.flexibleWidth = 0;
        le.flexibleHeight = 0;

        // Simple folder glyph (tab + body) so we don't depend on emoji glyphs in Nunito.
        var tab = CreateUiObject("FolderTab", go.transform);
        var tabRect = tab.GetComponent<RectTransform>();
        tabRect.anchorMin = new Vector2(0.18f, 0.62f);
        tabRect.anchorMax = new Vector2(0.52f, 0.80f);
        tabRect.offsetMin = Vector2.zero;
        tabRect.offsetMax = Vector2.zero;
        var tabImage = tab.AddComponent<Image>();
        tabImage.color = new Color(1f, 0.86f, 0.35f, 1f);
        tabImage.raycastTarget = false;
        CytoidLabUi.ApplyRoundedCorners(tabImage, 3f);

        var body = CreateUiObject("FolderBody", go.transform);
        var bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.16f, 0.20f);
        bodyRect.anchorMax = new Vector2(0.84f, 0.64f);
        bodyRect.offsetMin = Vector2.zero;
        bodyRect.offsetMax = Vector2.zero;
        var bodyImage = body.AddComponent<Image>();
        bodyImage.color = new Color(1f, 0.78f, 0.2f, 1f);
        bodyImage.raycastTarget = false;
        CytoidLabUi.ApplyRoundedCorners(bodyImage, 4f);

        return btn;
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        if (!string.IsNullOrEmpty(message)) Debug.Log($"[CytoidLab] {message}");
    }

    private void ShowGameErrorIfAny()
    {
        var error = Context.GameErrorState;
        if (error == null) return;

        var message = error.Message;
        if (error.Exception != null)
        {
            message += $"\n{error.Exception.GetType().Name}: {error.Exception.Message}";
        }

        SetStatus($"ERROR: {message}");
        Context.GameErrorState = null;
    }

    private void SelectDifficulty(Difficulty difficulty)
    {
        if (selectedLevel != null && !LevelHasChart(selectedLevel, difficulty)) return;

        selectedDifficulty = difficulty;
        PersistSelection();
        UpdateDifficultyButtons();
        UpdateSelectionHint();
    }

    private void UpdateSelectionHint()
    {
        if (selectionHintText == null) return;

        if (selectedLevel == null)
        {
            selectionHintText.text = selectedDifficulty != null
                ? $"Select a level ({selectedDifficulty.Id}) or import .cytoidlevel / .zip files"
                : DefaultSelectionHint;
            return;
        }

        var levelLabel = GetLevelTitle(selectedLevel);
        if (selectedDifficulty != null && LevelHasChart(selectedLevel, selectedDifficulty))
        {
            var lv = selectedLevel.Meta.GetDifficultyLevel(selectedDifficulty.Id);
            selectionHintText.text = $"Selected: {levelLabel} [{selectedDifficulty.Id} {lv}]";
        }
        else
        {
            selectionHintText.text = $"Selected: {levelLabel} — choose an available difficulty";
        }
    }

    private void UpdateDifficultyButtons()
    {
        foreach (var pair in difficultyButtonMap)
        {
            var diff = pair.Key;
            var btn = pair.Value;
            var available = selectedLevel != null && LevelHasChart(selectedLevel, diff);
            var label = btn.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = available
                    ? $"{diff.Id} {Difficulty.ConvertToDisplayLevel(selectedLevel.Meta.GetDifficultyLevel(diff.Id))}"
                    : diff.Id;
            }

            btn.interactable = available;
            var colors = btn.colors;
            if (!available)
            {
                colors.normalColor = DifficultyUnavailableColor;
                colors.highlightedColor = DifficultyUnavailableColor;
                colors.pressedColor = DifficultyUnavailableColor;
                colors.disabledColor = DifficultyUnavailableColor;
            }
            else
            {
                var selected = selectedDifficulty != null && selectedDifficulty.Id == diff.Id;
                var baseColor = selected ? DifficultySelectedColor : DifficultyAvailableColor;
                colors.normalColor = baseColor;
                colors.highlightedColor = new Color(baseColor.r + 0.08f, baseColor.g + 0.08f, baseColor.b + 0.08f);
                colors.pressedColor = new Color(baseColor.r - 0.05f, baseColor.g - 0.05f, baseColor.b - 0.05f);
            }

            btn.colors = colors;
        }
    }

    private static bool LevelHasChart(Level level, Difficulty difficulty)
    {
        return level.Meta.charts.Any(c => c.type == difficulty.Id);
    }

    private void SelectLevel(Level level)
    {
        selectedLevel = level;

        foreach (var pair in levelRowMap)
        {
            var rowImage = pair.Value.GetComponent<Image>();
            if (rowImage == null) continue;
            var isSelected = pair.Key.Meta.id == level.Meta.id;
            rowImage.color = isSelected ? CytoidLabUi.RowSelectedColor : CytoidLabUi.RowColor;
        }

        if (selectedDifficulty == null || !LevelHasChart(level, selectedDifficulty))
        {
            foreach (var diff in new[] { Difficulty.Hard, Difficulty.Extreme, Difficulty.Easy })
            {
                if (!LevelHasChart(level, diff)) continue;
                selectedDifficulty = diff;
                break;
            }
        }

        PersistSelection();
        UpdateDifficultyButtons();
        UpdateSelectionHint();
    }

    private void PersistSelection()
    {
        CytoidLabPreferences.SaveSelectedLevel(
            selectedLevel?.Meta?.id,
            selectedDifficulty?.Id);
    }

    private void ApplyRestoredSelection(Dictionary<string, Level> loaded)
    {
        // Priority: pending import → in-memory selection → persisted prefs → first in list.
        Level toSelect = null;

        if (!string.IsNullOrEmpty(pendingSelectLevelId) &&
            loaded.TryGetValue(pendingSelectLevelId, out var pendingLevel))
        {
            toSelect = pendingLevel;
            pendingSelectLevelId = null;
        }
        else if (selectedLevel != null &&
                 !string.IsNullOrEmpty(selectedLevel.Meta?.id) &&
                 loaded.TryGetValue(selectedLevel.Meta.id, out var currentLevel))
        {
            toSelect = currentLevel;
        }
        else if (CytoidLabPreferences.TryGetSelectedLevel(out var savedLevelId, out var savedDifficultyId) &&
                 loaded.TryGetValue(savedLevelId, out var savedLevel))
        {
            toSelect = savedLevel;
            if (!string.IsNullOrEmpty(savedDifficultyId))
            {
                selectedDifficulty = Difficulty.Parse(savedDifficultyId);
            }
        }
        else
        {
            toSelect = loaded.Values.OrderBy(l => l.Meta.title ?? l.Meta.id).FirstOrDefault();
            pendingSelectLevelId = null;
        }

        if (toSelect != null)
        {
            SelectLevel(toSelect);
        }
        else
        {
            selectedLevel = null;
            PersistSelection();
            UpdateDifficultyButtons();
            UpdateSelectionHint();
        }
    }

    private void OnDeleteLevelClicked(Level level)
    {
        DeleteLevel(level);
    }

    private async void DeleteLevel(Level level)
    {
        SetStatus($"Deleting {GetLevelTitle(level)}...");
        try
        {
            Context.LevelManager.DeleteLocalLevel(level.Meta.id);
            await Context.LevelManager.LoadLevelsOfType(LevelType.User);
            await RefreshLevelList();
            SetStatus($"Deleted {GetLevelTitle(level)}.");
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            SetStatus($"Failed to delete: {e.Message}");
        }
    }

    private async UniTask RefreshLevelList()
    {
        if (levelListRoot == null || isRefreshingLevelList) return;
        isRefreshingLevelList = true;

        try
        {
            ClearLevelListUi();

            var migrated = 0;
            try
            {
#if UNITY_STANDALONE_WIN
                // On PC the source .cytoidlevel files are kept wherever the user picked them,
                // so scanning UserDataPath for packages is not enough. Load already-installed
                // level folders directly.
                migrated = CytoidLabPaths.MigrateLegacyLevelsIfNeeded();
                await Context.LevelManager.LoadLevelsOfType(LevelType.User);
#else
                await Context.LevelManager.InstallUserCommunityLevels();
#endif
            }
            catch (Exception e)
            {
                Debug.LogError($"[CytoidLab] Failed to refresh levels: {e}");
            }

            if (Context.LevelManager.LoadedLocalLevels.Count == 0)
            {
                var empty = CreateButton(levelListRoot, "No levels installed. Use Import to add levels.", () => { });
                empty.interactable = false;
                empty.GetComponent<LayoutElement>().preferredHeight = LevelButtonHeight;
                selectedLevel = null;
                pendingSelectLevelId = null;
                PersistSelection();
                UpdateDifficultyButtons();
                UpdateSelectionHint();
                SetStatus(migrated > 0
                    ? $"Migrated {migrated} level(s), but none loaded. See Player.log."
                    : "No levels installed.");
                return;
            }

            foreach (var level in Context.LevelManager.LoadedLocalLevels.Values.OrderBy(l => l.Meta.title ?? l.Meta.id))
            {
                CreateLevelListItem(level);
            }

            SetStatus(migrated > 0
                ? $"{Context.LevelManager.LoadedLocalLevels.Count} level(s) installed (migrated {migrated} from AppData)."
                : $"{Context.LevelManager.LoadedLocalLevels.Count} level(s) installed.");

            ApplyRestoredSelection(Context.LevelManager.LoadedLocalLevels);

            RebuildLevelListScroll();
        }
        finally
        {
            isRefreshingLevelList = false;
        }
    }

    private void RebuildLevelListScroll()
    {
        if (levelListRoot == null) return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(levelListRoot as RectTransform);
        CytoidLabUi.RefreshRoundedCorners();

        if (levelScrollRect != null)
        {
            levelScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void ClearLevelListUi()
    {
        levelRowMap.Clear();
        for (var i = levelListRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(levelListRoot.GetChild(i).gameObject);
        }
    }

    private void CreateLevelListItem(Level level)
    {
        var localLevel = level;
        var row = CreateUiObject("LevelRow", levelListRoot).transform;
        var rowLe = row.gameObject.AddComponent<LayoutElement>();
        rowLe.preferredHeight = LevelRowHeight;
        rowLe.minHeight = LevelRowHeight;

        var rowImage = row.gameObject.AddComponent<Image>();
        rowImage.color = CytoidLabUi.RowColor;
        rowImage.raycastTarget = false;
        CytoidLabUi.ApplyRoundedCorners(rowImage, CytoidLabUi.PanelRadius);

        var rowHlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowHlg.spacing = UiSpacing;
        rowHlg.padding = new RectOffset(10, 10, 6, 6);
        rowHlg.childAlignment = TextAnchor.MiddleLeft;
        rowHlg.childControlWidth = true;
        rowHlg.childControlHeight = true;
        rowHlg.childForceExpandWidth = false;
        rowHlg.childForceExpandHeight = false;

        var infoGo = CreateUiObject("Info", row);
        var infoLe = infoGo.AddComponent<LayoutElement>();
        infoLe.flexibleWidth = 1;
        infoLe.minWidth = 200;
        infoLe.preferredHeight = LevelRowHeight - 12;
        infoLe.flexibleHeight = 1;
        var infoHit = infoGo.AddComponent<Image>();
        infoHit.color = Color.clear;
        var infoButton = infoGo.AddComponent<Button>();
        infoButton.targetGraphic = infoHit;
        infoButton.transition = Selectable.Transition.None;
        infoButton.onClick.AddListener(() => SelectLevel(localLevel));
        CytoidLabUiInput.DisableKeyboardNavigation(infoButton);

        var infoTextGo = CreateUiObject("Label", infoGo.transform);
        var infoTextRect = infoTextGo.GetComponent<RectTransform>();
        infoTextRect.anchorMin = Vector2.zero;
        infoTextRect.anchorMax = Vector2.one;
        infoTextRect.sizeDelta = Vector2.zero;
        var infoText = infoTextGo.AddComponent<Text>();
        infoText.font = uiFont;
        infoText.text = $"{GetLevelTitle(level)}\n{GetLevelArtist(level)}\n{GetLevelDifficultyLine(level)}";
        infoText.fontSize = LevelRowFontSize;
        infoText.alignment = TextAnchor.MiddleLeft;
        infoText.color = Color.white;
        infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
        infoText.verticalOverflow = VerticalWrapMode.Truncate;
        infoText.resizeTextForBestFit = false;
        infoText.raycastTarget = false;

        CreateTrashIconButton(row, () => OnDeleteLevelClicked(localLevel));

        levelRowMap[level] = row;
    }

    private Button CreateTrashIconButton(Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        const float size = 36f;

        var go = CreateUiObject("DeleteButton", parent);
        var image = go.AddComponent<Image>();
        image.color = Color.white;
        CytoidLabUi.ApplyRoundedCorners(image, CytoidLabUi.IconRadius);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = image;
        btn.onClick.AddListener(onClick);
        CytoidLabUiInput.DisableKeyboardNavigation(btn);
        CytoidLabUi.ApplyRoundedButtonColors(btn, CytoidLabUi.DangerColor);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = size;
        le.preferredHeight = size;
        le.minWidth = size;
        le.minHeight = size;
        le.flexibleWidth = 0;
        le.flexibleHeight = 0;

        // Procedural trash-can glyph (lid + body + slots).
        var lid = CreateUiObject("Lid", go.transform);
        var lidRect = lid.GetComponent<RectTransform>();
        lidRect.anchorMin = new Vector2(0.22f, 0.68f);
        lidRect.anchorMax = new Vector2(0.78f, 0.78f);
        lidRect.offsetMin = Vector2.zero;
        lidRect.offsetMax = Vector2.zero;
        var lidImage = lid.AddComponent<Image>();
        lidImage.color = new Color(1f, 0.92f, 0.92f, 0.95f);
        lidImage.raycastTarget = false;
        CytoidLabUi.ApplyRoundedCorners(lidImage, 2f);

        var handle = CreateUiObject("Handle", go.transform);
        var handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.40f, 0.78f);
        handleRect.anchorMax = new Vector2(0.60f, 0.88f);
        handleRect.offsetMin = Vector2.zero;
        handleRect.offsetMax = Vector2.zero;
        var handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(1f, 0.92f, 0.92f, 0.95f);
        handleImage.raycastTarget = false;
        CytoidLabUi.ApplyRoundedCorners(handleImage, 2f);

        var body = CreateUiObject("Body", go.transform);
        var bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0.26f, 0.18f);
        bodyRect.anchorMax = new Vector2(0.74f, 0.68f);
        bodyRect.offsetMin = Vector2.zero;
        bodyRect.offsetMax = Vector2.zero;
        var bodyImage = body.AddComponent<Image>();
        bodyImage.color = new Color(1f, 0.92f, 0.92f, 0.95f);
        bodyImage.raycastTarget = false;
        CytoidLabUi.ApplyRoundedCorners(bodyImage, 3f);

        for (var i = 0; i < 3; i++)
        {
            var slot = CreateUiObject($"Slot{i}", go.transform);
            var slotRect = slot.GetComponent<RectTransform>();
            var x = 0.36f + i * 0.12f;
            slotRect.anchorMin = new Vector2(x, 0.28f);
            slotRect.anchorMax = new Vector2(x + 0.04f, 0.58f);
            slotRect.offsetMin = Vector2.zero;
            slotRect.offsetMax = Vector2.zero;
            var slotImage = slot.AddComponent<Image>();
            slotImage.color = new Color(0.55f, 0.18f, 0.22f, 0.9f);
            slotImage.raycastTarget = false;
            CytoidLabUi.ApplyRoundedCorners(slotImage, 1.5f);
        }

        return btn;
    }

    private static string GetLevelTitle(Level level)
    {
        var title = level.Meta.title ?? level.Meta.id;
        if (!string.IsNullOrEmpty(level.Meta.title_localized))
            title += $" ({level.Meta.title_localized})";
        return title;
    }

    private static string GetLevelArtist(Level level)
    {
        var artist = string.IsNullOrEmpty(level.Meta.artist) ? "Unknown artist" : level.Meta.artist;
        if (!string.IsNullOrEmpty(level.Meta.charter))
            artist += $"  ·  Charted by {level.Meta.charter}";
        return artist;
    }

    private static string GetLevelDifficultyLine(Level level)
    {
        var parts = level.Meta.charts.Select(c =>
        {
            var diff = Difficulty.Parse(c.type);
            return $"{diff.Id} {c.difficulty}";
        });
        return string.Join("  ·  ", parts);
    }

    private void StartGame()
    {
        if (selectedLevel == null)
        {
            SetStatus("Please select a level first.");
            return;
        }

        if (selectedLevel.Meta.charts.All(c => c.type != selectedDifficulty.Id))
        {
            SetStatus($"Selected difficulty {selectedDifficulty.Id} is not available.");
            return;
        }

        SetStatus("Starting game...");
        Context.GameErrorState = null;
        CytoidLabShell.CaptureWindowSizeFromScreen();
        GameLaunchBridge.StartDebugGame(selectedLevel, selectedDifficulty, CytoidLabPreferences.CreateLaunchMods());
    }

    private async UniTask ImportLevelFiles()
    {
        var paths = await PickCytoidLevelFiles();
        if (paths == null || paths.Count == 0)
        {
            SetStatus(CytoidLabLevelImport.LastPickerMessage ?? "No file selected.");
            return;
        }

        await InstallLevelPackages(paths);
    }

    private void OpenDataFolder()
    {
        // Prefer the selected level folder when available so SB edits are one click away.
        if (selectedLevel != null && !string.IsNullOrEmpty(selectedLevel.Path))
        {
            if (CytoidLabPaths.TryOpenSelectedLevelFolder(selectedLevel.Path))
            {
                SetStatus($"Opened {selectedLevel.Meta?.id ?? "level"} folder.");
                return;
            }
        }

        var root = CytoidLabPaths.GetUserLevelsRoot();
        if (CytoidLabPaths.TryOpenDirectory(root))
        {
            var label = CytoidLabPaths.IsUsingPortableUserLevelsRoot()
                ? "./data"
                : "AppData levels folder";
            SetStatus($"Opened {label}.");
        }
        else
        {
            SetStatus($"Failed to open folder: {root}");
        }
    }

    private async UniTask InstallLevelPackages(List<string> paths)
    {
        paths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (paths.Count == 0) return;

        SetStatus(paths.Count == 1
            ? $"Importing 1/1: {Path.GetFileName(paths[0])}..."
            : $"Importing 0/{paths.Count}...");
        try
        {
            // Keep the source files; the user picked them from their own storage.
            var installed = await Context.LevelManager.InstallLevels(
                paths,
                LevelType.User,
                deleteSource: false,
                onProgress: (index, total, path) =>
                {
                    SetStatus($"Importing {index}/{total}: {Path.GetFileName(path)}...");
                });
            if (installed == null || installed.Count == 0)
            {
                SetStatus(paths.Count == 1
                    ? $"Import failed for {Path.GetFileName(paths[0])}. See Player.log."
                    : $"Import failed for all {paths.Count} selected levels. See Player.log.");
                return;
            }
            // Remember the newly imported level so the list can select it after refresh.
            pendingSelectLevelId = ResolveLevelIdFromInstalledPaths(installed);
            SetStatus($"Loading {installed.Count} imported level(s)...");
            // Ensure the newly installed level is loaded into LoadedLocalLevels.
            await Context.LevelManager.LoadLevelsOfType(LevelType.User);
            await RefreshLevelList();
            if (paths.Count == 1)
            {
                SetStatus($"Installed {Path.GetFileName(paths[0])}.");
            }
            else if (installed.Count == paths.Count)
            {
                SetStatus($"Installed {installed.Count}/{paths.Count} levels.");
            }
            else
            {
                SetStatus($"Installed {installed.Count}/{paths.Count} selected levels. See Player.log for failures.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            SetStatus($"Failed to install: {e.Message}");
        }
    }

    private static string ResolveLevelIdFromInstalledPaths(List<string> installedJsonFiles)
    {
        if (installedJsonFiles == null || installedJsonFiles.Count == 0) return null;
        try
        {
            var meta = JsonConvert.DeserializeObject<LevelMeta>(File.ReadAllText(installedJsonFiles[0]));
            return meta?.id;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CytoidLab] Failed to read installed level meta: {e}");
            return null;
        }
    }

    private async UniTask<List<string>> PickCytoidLevelFiles()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        await UniTask.SwitchToMainThread();
        try
        {
            return CytoidLabLevelImport.PickLevelPackagesWindows();
        }
        catch (Exception e)
        {
            Debug.LogError($"[CytoidLab] OpenFileDialog failed: {e}");
            return null;
        }
#else
        await UniTask.CompletedTask;
        return new List<string>();
#endif
    }

    private void ProcessCommandLineImport()
    {
        var paths = Environment.GetCommandLineArgs()
            .Where(CytoidLabLevelImport.IsLevelPackagePath)
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (paths.Count > 0) InstallLevelPackages(paths).Forget();
    }
}

/// <summary>Cytoid Lab level package import helpers (.cytoidlevel / .zip).</summary>
internal static class CytoidLabLevelImport
{
    public static string LastPickerMessage { get; private set; }

    public static bool IsLevelPackagePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        var extension = Path.GetExtension(path);
        return extension.Equals(".cytoidlevel", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".zip", StringComparison.OrdinalIgnoreCase);
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private const int OfnExplorer = 0x00080000;
    private const int OfnFileMustExist = 0x00001000;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnNoChangeDir = 0x00000008;
    private const int OfnAllowMultiSelect = 0x00000200;
    private const int FileBufferChars = 65536;

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

    [DllImport("comdlg32.dll")]
    private static extern int CommDlgExtendedError();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private class OpenFileName
    {
        public int lStructSize;
        public IntPtr hwndOwner = IntPtr.Zero;
        public IntPtr hInstance = IntPtr.Zero;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle = IntPtr.Zero;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData = IntPtr.Zero;
        public IntPtr lpfnHook = IntPtr.Zero;
        public string lpTemplateName;
        public IntPtr pvReserved = IntPtr.Zero;
        public int dwReserved;
        public int FlagsEx;
    }

    public static List<string> PickLevelPackagesWindows()
    {
        LastPickerMessage = null;
        var fileBuffer = Marshal.AllocHGlobal(FileBufferChars * sizeof(char));

        try
        {
            Marshal.WriteInt16(fileBuffer, 0, 0);
            var ofn = new OpenFileName
            {
                lStructSize = Marshal.SizeOf(typeof(OpenFileName)),
                lpstrFilter = "Cytoid level\0*.cytoidlevel;*.zip\0ZIP archive\0*.zip\0All files\0*.*\0\0",
                nFilterIndex = 1,
                lpstrFile = fileBuffer,
                nMaxFile = FileBufferChars,
                nMaxFileTitle = 0,
                lpstrTitle = "Select Cytoid levels",
                Flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | OfnAllowMultiSelect
            };

            if (!GetOpenFileName(ofn))
            {
                var dialogError = CommDlgExtendedError();
                if (dialogError != 0)
                {
                    LastPickerMessage = $"File picker failed (error {dialogError}). See Player.log.";
                    Debug.LogError($"[CytoidLab] GetOpenFileName failed: CommDlgExtendedError={dialogError}, Win32={Marshal.GetLastWin32Error()}");
                }

                return null;
            }

            var values = ReadNullSeparatedStrings(fileBuffer, FileBufferChars);
            if (values.Count == 0)
            {
                LastPickerMessage = "File picker returned no paths.";
                return null;
            }

            var paths = values.Count == 1
                ? new List<string> { values[0] }
                : values.Skip(1).Select(fileName => Path.Combine(values[0], fileName)).ToList();
            paths = paths
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var invalidPaths = paths
                .Where(path => !File.Exists(path) || !IsLevelPackagePath(path))
                .ToList();
            if (invalidPaths.Count > 0)
            {
                LastPickerMessage = "Select only existing .cytoidlevel or .zip files.";
                Debug.LogError($"[CytoidLab] Invalid selected paths: {string.Join(", ", invalidPaths)}");
                return null;
            }

            return paths;
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
        }
    }

    private static List<string> ReadNullSeparatedStrings(IntPtr buffer, int maxChars)
    {
        var values = new List<string>();
        var value = new StringBuilder();
        for (var index = 0; index < maxChars; index++)
        {
            var character = (char) Marshal.ReadInt16(buffer, index * sizeof(char));
            if (character != '\0')
            {
                value.Append(character);
                continue;
            }

            if (value.Length == 0) break;
            values.Add(value.ToString());
            value.Clear();
        }

        return values;
    }
#endif
}
