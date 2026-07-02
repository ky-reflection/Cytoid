using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Compact viewport preset picker for the Cytoid Lab level menu (16:9 / 4:3, small / large).
/// </summary>
public class CytoidLabViewportOverlay : MonoBehaviour
{
    private const int BodyFontSize = 14;
    private const int TitleFontSize = 18;
    private const float PanelWidth = 240f;
    private const float PanelHeight = 216f;

    private static readonly Color PanelBg = new Color(0.11f, 0.13f, 0.19f, 0.98f);
    private static readonly Color BackdropColor = new Color(0, 0, 0, 0.55f);
    private static readonly Color BodyColor = new Color(0.9f, 0.91f, 0.94f);
    private static readonly Color SelectedColor = new Color(0.3f, 0.6f, 1f);
    private static readonly Color NormalColor = new Color(0.25f, 0.35f, 0.55f);

    private Action<string> onPresetSelected;
    private Action<string> onSizeSelected;
    private Button preset16x9Button;
    private Button preset4x3Button;
    private Button sizeSmallButton;
    private Button sizeLargeButton;

    public static CytoidLabViewportOverlay Open(Transform canvasTransform, Font font, Action<string> onPresetSelected,
        Action<string> onSizeSelected)
    {
        var existing = canvasTransform.GetComponentInChildren<CytoidLabViewportOverlay>(true);
        if (existing != null)
        {
            existing.Close();
        }

        var go = new GameObject("CytoidLabViewportOverlay", typeof(RectTransform));
        go.transform.SetParent(canvasTransform, false);
        go.transform.SetAsLastSibling();
        var overlay = go.AddComponent<CytoidLabViewportOverlay>();
        overlay.onPresetSelected = onPresetSelected;
        overlay.onSizeSelected = onSizeSelected;
        overlay.Build(font);
        return overlay;
    }

    private void Build(Font font)
    {
        StretchFull(GetComponent<RectTransform>());

        var backdrop = CreateChild("Backdrop", transform);
        StretchFull(backdrop.GetComponent<RectTransform>());
        backdrop.AddComponent<Image>().color = BackdropColor;
        var backdropBtn = backdrop.AddComponent<Button>();
        backdropBtn.transition = Selectable.Transition.None;
        backdropBtn.onClick.AddListener(Close);
        CytoidLabUiInput.DisableKeyboardNavigation(backdropBtn);

        var panel = CreateChild("Panel", transform);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1, 1);
        panelRect.anchorMax = new Vector2(1, 1);
        panelRect.pivot = new Vector2(1, 1);
        panelRect.anchoredPosition = new Vector2(-16, -64);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panel.AddComponent<Image>().color = PanelBg;

        var title = CreateChild("Title", panel.transform);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -12);
        titleRect.sizeDelta = new Vector2(-24, 28);
        var titleText = title.AddComponent<Text>();
        titleText.font = font;
        titleText.text = "Viewport";
        titleText.fontSize = TitleFontSize;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.color = Color.white;

        var body = CreateChild("Body", panel.transform);
        var bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0, 1);
        bodyRect.anchorMax = new Vector2(1, 1);
        bodyRect.pivot = new Vector2(0.5f, 1);
        bodyRect.anchoredPosition = new Vector2(0, -44);
        bodyRect.sizeDelta = new Vector2(-24, 40);
        var bodyText = body.AddComponent<Text>();
        bodyText.font = font;
        bodyText.text = "Window size for preview. Layout applies on Start.";
        bodyText.fontSize = BodyFontSize;
        bodyText.alignment = TextAnchor.UpperLeft;
        bodyText.color = BodyColor;
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;

        var presetRow = CreateChild("PresetRow", panel.transform);
        var presetRowRect = presetRow.GetComponent<RectTransform>();
        presetRowRect.anchorMin = new Vector2(0, 0);
        presetRowRect.anchorMax = new Vector2(1, 0);
        presetRowRect.pivot = new Vector2(0.5f, 0);
        presetRowRect.anchoredPosition = new Vector2(0, 64);
        presetRowRect.sizeDelta = new Vector2(-24, 40);
        var presetHlg = presetRow.AddComponent<HorizontalLayoutGroup>();
        presetHlg.spacing = 8;
        presetHlg.childControlWidth = true;
        presetHlg.childForceExpandWidth = true;
        presetHlg.childControlHeight = true;
        presetHlg.childForceExpandHeight = true;

        preset16x9Button = CreatePresetButton(presetRow.transform, font, "16:9",
            () => SelectPreset(CytoidLabShell.ViewportPreset16x9));
        preset4x3Button = CreatePresetButton(presetRow.transform, font, "4:3",
            () => SelectPreset(CytoidLabShell.ViewportPreset4x3));

        var sizeRow = CreateChild("SizeRow", panel.transform);
        var sizeRowRect = sizeRow.GetComponent<RectTransform>();
        sizeRowRect.anchorMin = new Vector2(0, 0);
        sizeRowRect.anchorMax = new Vector2(1, 0);
        sizeRowRect.pivot = new Vector2(0.5f, 0);
        sizeRowRect.anchoredPosition = new Vector2(0, 16);
        sizeRowRect.sizeDelta = new Vector2(-24, 40);
        var sizeHlg = sizeRow.AddComponent<HorizontalLayoutGroup>();
        sizeHlg.spacing = 8;
        sizeHlg.childControlWidth = true;
        sizeHlg.childForceExpandWidth = true;
        sizeHlg.childControlHeight = true;
        sizeHlg.childForceExpandHeight = true;

        sizeSmallButton = CreatePresetButton(sizeRow.transform, font, "Small",
            () => SelectSize(CytoidLabShell.ViewportSizeSmall));
        sizeLargeButton = CreatePresetButton(sizeRow.transform, font, "Large",
            () => SelectSize(CytoidLabShell.ViewportSizeLarge));

        RefreshSelection();
        CytoidLabUiInput.ClearUiSelection();
    }

    private void Update()
    {
        if (GameInputCompat.WasEscapePressedThisFrame())
        {
            Close();
        }
    }

    private void SelectPreset(string presetId)
    {
        onPresetSelected?.Invoke(presetId);
        Close();
    }

    private void SelectSize(string sizeId)
    {
        onSizeSelected?.Invoke(sizeId);
        Close();
    }

    private void RefreshSelection()
    {
        string presetId;
        string sizeId;
        if (!Context.IsInitialized)
        {
            presetId = CytoidLabShell.ViewportPreset16x9;
            sizeId = CytoidLabShell.ViewportSizeSmall;
        }
        else
        {
            presetId = CytoidLabShell.NormalizeViewportPresetId(Context.Player.Settings.LabViewportPreset);
            sizeId = CytoidLabShell.NormalizeViewportSizeId(Context.Player.Settings.LabViewportSize);
        }

        SetPresetButtonLabel(preset16x9Button, CytoidLabShell.ViewportPreset16x9, presetId, sizeId);
        SetPresetButtonLabel(preset4x3Button, CytoidLabShell.ViewportPreset4x3, presetId, sizeId);
        ApplyPresetButtonState(preset16x9Button, presetId == CytoidLabShell.ViewportPreset16x9);
        ApplyPresetButtonState(preset4x3Button, presetId == CytoidLabShell.ViewportPreset4x3);

        SetSizeButtonLabel(sizeSmallButton, CytoidLabShell.ViewportSizeSmall, presetId);
        SetSizeButtonLabel(sizeLargeButton, CytoidLabShell.ViewportSizeLarge, presetId);
        ApplyPresetButtonState(sizeSmallButton, sizeId == CytoidLabShell.ViewportSizeSmall);
        ApplyPresetButtonState(sizeLargeButton, sizeId == CytoidLabShell.ViewportSizeLarge);
    }

    private static void SetPresetButtonLabel(Button button, string presetId, string selectedPresetId, string sizeId)
    {
        if (button == null) return;
        var label = button.GetComponentInChildren<Text>();
        if (label == null) return;

        var dimensions = CytoidLabShell.FormatViewportDimensions(presetId, sizeId);
        label.text = $"{presetId}\n{dimensions}";
    }

    private static void SetSizeButtonLabel(Button button, string sizeId, string presetId)
    {
        if (button == null) return;
        var label = button.GetComponentInChildren<Text>();
        if (label == null) return;

        var dimensions = CytoidLabShell.FormatViewportDimensions(presetId, sizeId);
        var title = sizeId == CytoidLabShell.ViewportSizeLarge ? "Large" : "Small";
        label.text = $"{title}\n{dimensions}";
    }

    private static void ApplyPresetButtonState(Button button, bool selected)
    {
        if (button == null) return;
        var colors = button.colors;
        var baseColor = selected ? SelectedColor : NormalColor;
        colors.normalColor = baseColor;
        colors.highlightedColor = new Color(baseColor.r + 0.08f, baseColor.g + 0.08f, baseColor.b + 0.08f);
        colors.pressedColor = new Color(baseColor.r - 0.05f, baseColor.g - 0.05f, baseColor.b - 0.05f);
        button.colors = colors;
    }

    private static Button CreatePresetButton(Transform parent, Font font, string label, UnityEngine.Events.UnityAction onClick)
    {
        var go = CreateChild("PresetButton", parent);
        var image = go.AddComponent<Image>();
        image.color = NormalColor;
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        CytoidLabUiInput.DisableKeyboardNavigation(btn);
        go.AddComponent<LayoutElement>().flexibleWidth = 1;

        var textGo = CreateChild("Label", go.transform);
        StretchFull(textGo.GetComponent<RectTransform>());
        var text = textGo.AddComponent<Text>();
        text.font = font;
        text.text = label;
        text.fontSize = 15;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.lineSpacing = 1.05f;

        return btn;
    }

    private void Close()
    {
        Destroy(gameObject);
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static GameObject CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }
}
