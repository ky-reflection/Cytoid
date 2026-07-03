using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

    private const string DefaultSelectionHint = "Select a level or import a .cytoidlevel / .zip";

    private static readonly Difficulty[] DifficultyOptions = { Difficulty.Easy, Difficulty.Hard, Difficulty.Extreme };
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
    private bool isRefreshingLevelList;

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
        CytoidLabShell.ApplyViewportFromSettings();
        UpdateViewportCornerButton();
        ShowGameErrorIfAny();
        await RefreshLevelList();
        ProcessCommandLineImport();
    }

    private void OnEnable()
    {
        if (GameEmbedMode.IsBridgeEmbedded) return;
        if (!Context.IsInitialized) return;
        CytoidLabShell.ApplyViewportFromSettings();
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
        bgImage.color = new Color(0.05f, 0.05f, 0.08f, 1f);

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

        var importButton = CreateButton(root, "Import level", () => ImportLevelFile().Forget());
        importButton.GetComponent<LayoutElement>().preferredHeight = ButtonHeight;

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

        const float scrollbarWidth = 12f;

        var viewport = CreateUiObject("Viewport", scroll.transform);
        var vpRect = viewport.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero;
        vpRect.offsetMax = new Vector2(-scrollbarWidth, 0);
        viewport.AddComponent<RectMask2D>();
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.2f);

        levelListRoot = CreateUiObject("LevelList", viewport.transform).transform;
        var listRect = levelListRoot.GetComponent<RectTransform>();
        listRect.anchorMin = new Vector2(0, 1);
        listRect.anchorMax = new Vector2(1, 1);
        listRect.pivot = new Vector2(0.5f, 1);
        listRect.anchoredPosition = Vector2.zero;
        listRect.sizeDelta = new Vector2(0, 0);
        var listVlg = levelListRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        listVlg.spacing = 4;
        listVlg.padding = new RectOffset(0, 0, 0, 8);
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
        sbBg.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
        var sbHandle = CreateUiObject("Handle", scrollbar.transform);
        var sbHandleRect = sbHandle.GetComponent<RectTransform>();
        sbHandleRect.sizeDelta = new Vector2(scrollbarWidth, scrollbarWidth);
        var sbHandleImage = sbHandle.AddComponent<Image>();
        sbHandleImage.color = new Color(0.3f, 0.6f, 1f);
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
        var startColors = startButton.colors;
        startColors.normalColor = new Color(0.2f, 0.8f, 0.3f);
        startColors.highlightedColor = new Color(0.28f, 0.88f, 0.38f);
        startColors.pressedColor = new Color(0.15f, 0.65f, 0.22f);
        startButton.colors = startColors;

        selectedDifficulty = Difficulty.Hard;
        UpdateDifficultyButtons();
    }

    private static void ApplyLabMenuDefaults()
    {
        Context.Player.Settings.HitSound = "none";
        Context.Player.Settings.RestrictPlayAreaAspectRatio = true;
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

    private void BuildCornerButtons(Transform parent)
    {
        const float buttonSize = 40f;
        const float viewportWidth = 56f;
        const float spacing = 8f;
        const float margin = 16f;

        viewportCornerButton = CreateCornerButton(parent, "ViewportCornerButton",
            new Vector2(-(margin + buttonSize + spacing + viewportWidth) + 20f, -margin),
            new Vector2(viewportWidth, buttonSize),
            OpenViewportOverlay, out viewportCornerLabel);
        viewportCornerLabel.fontSize = 16;
        viewportCornerLabel.fontStyle = FontStyle.Bold;

        CreateCornerButton(parent, "HelpButton",
            new Vector2(-margin, -margin),
            new Vector2(buttonSize, buttonSize),
            () => CytoidLabHelpOverlay.Open(canvas.transform, uiFont),
            out var helpLabel);
        helpLabel.text = "?";
        helpLabel.fontSize = 24;
        helpLabel.fontStyle = FontStyle.Bold;

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
        image.color = new Color(0.22f, 0.34f, 0.52f, 0.95f);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.32f, 0.48f, 0.72f);
        colors.pressedColor = new Color(0.16f, 0.24f, 0.38f);
        btn.colors = colors;
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
        image.color = new Color(0.25f, 0.35f, 0.55f);
        image.type = Image.Type.Simple;

        var btn = go.AddComponent<Button>();
        if (btn == null) throw new InvalidOperationException("Failed to add Button component.");
        btn.onClick.AddListener(onClick);
        CytoidLabUiInput.DisableKeyboardNavigation(btn);

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
        UpdateDifficultyButtons();
        UpdateSelectionHint();
    }

    private void UpdateSelectionHint()
    {
        if (selectionHintText == null) return;

        if (selectedLevel == null)
        {
            selectionHintText.text = selectedDifficulty != null
                ? $"Select a level ({selectedDifficulty.Id}) or import a .cytoidlevel / .zip"
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
            rowImage.color = isSelected ? new Color(0.2f, 0.35f, 0.55f, 1f) : new Color(0.12f, 0.12f, 0.16f, 1f);
        }

        if (!LevelHasChart(level, selectedDifficulty))
        {
            foreach (var diff in new[] { Difficulty.Hard, Difficulty.Extreme, Difficulty.Easy })
            {
                if (!LevelHasChart(level, diff)) continue;
                selectedDifficulty = diff;
                break;
            }
        }

        UpdateDifficultyButtons();
        UpdateSelectionHint();
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

            try
            {
#if UNITY_STANDALONE_WIN
                // On PC the source .cytoidlevel files are kept wherever the user picked them,
                // so scanning UserDataPath for packages is not enough. Load already-installed
                // level folders directly.
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
                var empty = CreateButton(levelListRoot, "No levels installed. Use Import to add a level.", () => { });
                empty.interactable = false;
                empty.GetComponent<LayoutElement>().preferredHeight = LevelButtonHeight;
                SetStatus("No levels installed.");
                return;
            }

            foreach (var level in Context.LevelManager.LoadedLocalLevels.Values.OrderBy(l => l.Meta.title ?? l.Meta.id))
            {
                CreateLevelListItem(level);
            }

            SetStatus($"{Context.LevelManager.LoadedLocalLevels.Count} level(s) installed.");

            if (!string.IsNullOrEmpty(pendingSelectLevelId) &&
                Context.LevelManager.LoadedLocalLevels.TryGetValue(pendingSelectLevelId, out var pendingLevel))
            {
                SelectLevel(pendingLevel);
                pendingSelectLevelId = null;
            }
            else if (selectedLevel == null || !Context.LevelManager.LoadedLocalLevels.ContainsKey(selectedLevel.Meta.id))
            {
                SelectLevel(Context.LevelManager.LoadedLocalLevels.Values.First());
            }
            else
            {
                SelectLevel(selectedLevel);
            }

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
        rowImage.color = new Color(0.12f, 0.12f, 0.16f, 1f);
        rowImage.raycastTarget = false;

        var rowHlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowHlg.spacing = UiSpacing;
        rowHlg.padding = new RectOffset(8, 8, 4, 4);
        rowHlg.childAlignment = TextAnchor.MiddleLeft;
        rowHlg.childControlWidth = true;
        rowHlg.childControlHeight = true;
        rowHlg.childForceExpandWidth = false;
        rowHlg.childForceExpandHeight = true;

        var infoGo = CreateUiObject("Info", row);
        var infoLe = infoGo.AddComponent<LayoutElement>();
        infoLe.flexibleWidth = 1;
        infoLe.minWidth = 200;
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

        var deleteButton = CreateButton(row, "Delete", () => OnDeleteLevelClicked(localLevel));
        var deleteLe = deleteButton.GetComponent<LayoutElement>();
        deleteLe.preferredWidth = 80;
        var deleteColors = deleteButton.colors;
        deleteColors.normalColor = new Color(0.6f, 0.2f, 0.2f);
        deleteColors.highlightedColor = new Color(0.8f, 0.25f, 0.25f);
        deleteColors.pressedColor = new Color(0.5f, 0.15f, 0.15f);
        deleteButton.colors = deleteColors;

        levelRowMap[level] = row;
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
        CytoidLabShell.ApplyViewportFromSettings();
        GameLaunchBridge.StartDebugGame(selectedLevel, selectedDifficulty, new List<Mod> { Mod.Auto });
    }

    private async UniTask ImportLevelFile()
    {
        var path = await PickCytoidLevelFile();
        if (string.IsNullOrEmpty(path))
        {
            SetStatus(CytoidLabLevelImport.LastPickerMessage ?? "No file selected.");
            return;
        }

        await InstallLevelPackage(path);
    }

    private async UniTask InstallLevelPackage(string path)
    {
        SetStatus($"Installing {Path.GetFileName(path)}...");
        try
        {
            // Keep the source file; the user picked it from their own storage.
            var installed = await Context.LevelManager.InstallLevels(new List<string> { path }, LevelType.User, deleteSource: false);
            if (installed == null || installed.Count == 0)
            {
                SetStatus($"Import failed for {Path.GetFileName(path)}. See Player.log.");
                return;
            }
            // Remember the newly imported level so the list can select it after refresh.
            pendingSelectLevelId = ResolveLevelIdFromInstalledPaths(installed);
            // Ensure the newly installed level is loaded into LoadedLocalLevels.
            await Context.LevelManager.LoadLevelsOfType(LevelType.User);
            await RefreshLevelList();
            SetStatus($"Installed {Path.GetFileName(path)}.");
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

    private async UniTask<string> PickCytoidLevelFile()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        await UniTask.SwitchToMainThread();
        try
        {
            return CytoidLabLevelImport.PickLevelPackageWindows();
        }
        catch (Exception e)
        {
            Debug.LogError($"[CytoidLab] OpenFileDialog failed: {e}");
            return null;
        }
#else
        await UniTask.CompletedTask;
        return null;
#endif
    }

    private void ProcessCommandLineImport()
    {
        var args = Environment.GetCommandLineArgs();
        foreach (var arg in args)
        {
            if (!CytoidLabLevelImport.IsLevelPackagePath(arg)) continue;
            if (!File.Exists(arg)) continue;
            InstallLevelPackage(arg).Forget();
            break;
        }
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
    private const int FileBufferChars = 8192;
    private const int FileTitleBufferChars = 256;

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
        public string lpstrFile;
        public int nMaxFile;
        public string lpstrFileTitle;
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

    public static string PickLevelPackageWindows()
    {
        LastPickerMessage = null;

        var ofn = new OpenFileName
        {
            lStructSize = Marshal.SizeOf(typeof(OpenFileName)),
            lpstrFilter = "Cytoid level\0*.cytoidlevel;*.zip\0ZIP archive\0*.zip\0All files\0*.*\0\0",
            nFilterIndex = 1,
            lpstrFile = new string('\0', FileBufferChars),
            nMaxFile = FileBufferChars,
            lpstrFileTitle = new string('\0', FileTitleBufferChars),
            nMaxFileTitle = FileTitleBufferChars,
            lpstrTitle = "Select a Cytoid level",
            Flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir
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

        var path = ofn.lpstrFile;
        if (string.IsNullOrWhiteSpace(path))
        {
            LastPickerMessage = "File picker returned an empty path.";
            return null;
        }

        path = path.Split('\0')[0].Trim();
        if (path.Length == 0 || !File.Exists(path))
        {
            LastPickerMessage = "Selected file was not found.";
            Debug.LogError($"[CytoidLab] Picked path missing on disk: {path}");
            return null;
        }

        if (!IsLevelPackagePath(path))
        {
            LastPickerMessage = "Select a .cytoidlevel or .zip file.";
            return null;
        }

        return path;
    }
#endif
}
