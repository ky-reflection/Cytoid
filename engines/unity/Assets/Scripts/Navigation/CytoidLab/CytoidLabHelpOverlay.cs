using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Modal help panel for Cytoid Lab (shortcuts, timeline scrub, tips).
/// Uses anchored layout — avoids nested LayoutGroup sizing bugs in scroll modals.
/// </summary>
public class CytoidLabHelpOverlay : MonoBehaviour
{
    private const int BodyFontSize = 14;
    private const int SectionFontSize = 16;
    private const int TitleFontSize = 20;
    private const float HeaderHeight = 48f;
    private const float PanelPadding = 20f;

    private static readonly Color PanelBg = new Color(0.11f, 0.13f, 0.19f, 0.98f);
    private static readonly Color BackdropColor = new Color(0, 0, 0, 0.72f);
    private static readonly Color HeadingColor = new Color(0.72f, 0.84f, 1f);
    private static readonly Color BodyColor = new Color(0.9f, 0.91f, 0.94f);
    private static readonly Color MutedColor = new Color(0.65f, 0.68f, 0.74f);

    public static CytoidLabHelpOverlay Open(Transform canvasTransform, Font font)
    {
        var existing = canvasTransform.GetComponentInChildren<CytoidLabHelpOverlay>(true);
        if (existing != null)
        {
            existing.Close();
        }

        var go = new GameObject("CytoidLabHelpOverlay", typeof(RectTransform));
        go.transform.SetParent(canvasTransform, false);
        go.transform.SetAsLastSibling();
        var overlay = go.AddComponent<CytoidLabHelpOverlay>();
        overlay.Build(font);
        return overlay;
    }

    private void Build(Font font)
    {
        StretchFull(GetComponent<RectTransform>());

        // Backdrop (sibling layer below panel — not a parent of panel).
        var backdrop = CreateChild("Backdrop", transform);
        StretchFull(backdrop.GetComponent<RectTransform>());
        backdrop.AddComponent<Image>().color = BackdropColor;
        var backdropBtn = backdrop.AddComponent<Button>();
        backdropBtn.transition = Selectable.Transition.None;
        backdropBtn.onClick.AddListener(Close);
        CytoidLabUiInput.DisableKeyboardNavigation(backdropBtn);

        var panelW = Mathf.Min(UnityEngine.Screen.width * 0.88f, 560f);
        var panelH = Mathf.Min(UnityEngine.Screen.height * 0.78f, 520f);

        var panel = CreateChild("Panel", transform);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(panelW, panelH);
        var panelImage = panel.AddComponent<Image>();
        panelImage.color = PanelBg;
        CytoidLabUi.ApplyRoundedCorners(panelImage, CytoidLabUi.SoftRadius);

        // Header
        var header = CreateChild("Header", panel.transform);
        var headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta = new Vector2(0, HeaderHeight);

        var title = CreateChild("Title", header.transform);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(PanelPadding, 0);
        titleRect.offsetMax = new Vector2(-(PanelPadding + 88), 0);
        var titleText = title.AddComponent<Text>();
        titleText.font = font;
        titleText.text = "Help";
        titleText.fontSize = TitleFontSize;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.color = Color.white;

        CreateHeaderButton(header.transform, font, "Close", Close);

        // Scroll area
        var scrollRoot = CreateChild("Scroll", panel.transform);
        var scrollRect = scrollRoot.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(PanelPadding, PanelPadding);
        scrollRect.offsetMax = new Vector2(-PanelPadding, -(HeaderHeight + 4));

        var scroll = scrollRoot.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;

        var viewport = CreateChild("Viewport", scrollRoot.transform);
        StretchFull(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<RectMask2D>();
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.12f);

        var content = CreateChild("Content", viewport.transform);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        var contentVlg = content.AddComponent<VerticalLayoutGroup>();
        contentVlg.spacing = 14;
        contentVlg.padding = new RectOffset(4, 4, 2, 12);
        contentVlg.childAlignment = TextAnchor.UpperLeft;
        contentVlg.childControlWidth = true;
        contentVlg.childControlHeight = true;
        contentVlg.childForceExpandWidth = true;
        contentVlg.childForceExpandHeight = false;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = viewport.GetComponent<RectTransform>();

        AddSection(content.transform, font, "About", GetAboutText());
        AddSection(content.transform, font, "Level menu", GetMenuText());
        AddSection(content.transform, font, "Keyboard (in-game)", GetKeyboardText());
        AddSection(content.transform, font, "Timeline", GetTimelineText());
        AddSection(content.transform, font, "In-game HUD", GetHudText());
        AddSection(content.transform, font, "Tips", GetTipsText());

        var footer = CreateChild("Footer", content.transform);
        var footerLe = footer.AddComponent<LayoutElement>();
        footerLe.preferredHeight = 20;
        var footerText = footer.AddComponent<Text>();
        footerText.font = font;
        footerText.text = "Press Esc or click outside to close";
        footerText.fontSize = 12;
        footerText.alignment = TextAnchor.MiddleCenter;
        footerText.color = MutedColor;
        footerText.fontStyle = FontStyle.Italic;

        CytoidLabUiInput.ClearUiSelection();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        Canvas.ForceUpdateCanvases();
        CytoidLabUi.RefreshRoundedCorners();
    }

    private void Update()
    {
        if (GameInputCompat.WasEscapePressedThisFrame())
        {
            Close();
        }
    }

    public void Close()
    {
        Destroy(gameObject);
    }

    private static void CreateHeaderButton(Transform parent, Font font, string label,
        UnityEngine.Events.UnityAction onClick)
    {
        var go = CreateChild("CloseButton", parent);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1, 0.5f);
        rect.pivot = new Vector2(1, 0.5f);
        rect.anchoredPosition = new Vector2(-PanelPadding, 0);
        rect.sizeDelta = new Vector2(76, 32);

        var image = go.AddComponent<Image>();
        image.color = Color.white;
        CytoidLabUi.ApplyRoundedCorners(image, CytoidLabUi.ButtonRadius);
        var btn = go.AddComponent<Button>();
        CytoidLabUi.ApplyRoundedButtonColors(btn);
        btn.onClick.AddListener(onClick);
        CytoidLabUiInput.DisableKeyboardNavigation(btn);

        var labelGo = CreateChild("Label", go.transform);
        StretchFull(labelGo.GetComponent<RectTransform>());
        var text = labelGo.AddComponent<Text>();
        text.font = font;
        text.text = label;
        text.fontSize = 14;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
    }

    private static void AddSection(Transform parent, Font font, string heading, string body)
    {
        var block = CreateChild("Section", parent);
        var blockVlg = block.AddComponent<VerticalLayoutGroup>();
        blockVlg.spacing = 4;
        blockVlg.childControlWidth = true;
        blockVlg.childControlHeight = true;
        blockVlg.childForceExpandWidth = true;
        blockVlg.childForceExpandHeight = false;
        var blockLe = block.AddComponent<LayoutElement>();
        blockLe.flexibleWidth = 1;

        var headingGo = CreateChild("Heading", block.transform);
        var headingLe = headingGo.AddComponent<LayoutElement>();
        headingLe.preferredHeight = SectionFontSize + 6;
        var headingText = headingGo.AddComponent<Text>();
        headingText.font = font;
        headingText.text = heading;
        headingText.fontSize = SectionFontSize;
        headingText.fontStyle = FontStyle.Bold;
        headingText.alignment = TextAnchor.MiddleLeft;
        headingText.color = HeadingColor;

        var bodyGo = CreateChild("Body", block.transform);
        var bodyText = bodyGo.AddComponent<Text>();
        bodyText.font = font;
        bodyText.text = body;
        bodyText.fontSize = BodyFontSize;
        bodyText.lineSpacing = 1.1f;
        bodyText.alignment = TextAnchor.UpperLeft;
        bodyText.color = BodyColor;
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        bodyText.resizeTextForBestFit = false;

        var bodyFitter = bodyGo.AddComponent<ContentSizeFitter>();
        bodyFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var bodyLe = bodyGo.AddComponent<LayoutElement>();
        bodyLe.flexibleWidth = 1;
    }

    private static string GetAboutText()
    {
        return "Cytoid Lab is a Windows chart preview and playtest tool for authors and core developers. "
               + "It runs the Unity gameplay core in a standalone window — not a replacement for the main Cytoid app.";
    }

    private static string GetMenuText()
    {
        return "Top-right Viewport button (left of ?) — aspect 16:9 or 4:3; size Small (1280 wide, default) or Large (1920 wide). "
               + "Example: 16:9 Small = 1280×720, 16:9 Large = 1920×1080. Changes the window size; press Start to apply note/storyboard layout.\n"
               + "Viewport choice is remembered after quit.\n"
               + "When a newer Lab release is on GitHub, an Update button appears under the title.\n"
               + "Play area is restricted to 4:3–16:9 by default (same as the main app).";
    }

    private static string GetKeyboardText()
    {
        return "Space — Play / Pause\n"
               + "Esc — Play / Pause (windowed), or exit fullscreen\n"
               + "F11 — Toggle fullscreen (uses monitor aspect; windowed mode restores Viewport preset)";
    }

    private static string GetTimelineText()
    {
        return "Drag the bottom timeline to preview any moment while paused.\n"
               + "Release the slider to fully resync notes, score, and storyboard to that time.\n"
               + "Space is ignored while dragging the timeline.";
    }

    private static string GetHudText()
    {
        return "Move the pointer to the top or bottom edge to reveal controls.\n"
               + "Auto — autoplay chart; Hitsound — toggle hit sound; IDs — note id overlay.\n"
               + "End — skip end: fast fade and exit when chart clears (default On). Off plays out music and post-chart storyboard.\n"
               + "Reset — reload the playfield; Back — return to level menu.";
    }

    private static string GetTipsText()
    {
        return "Import — pick one or more .cytoidlevel / .zip files, or drag packages onto CytoidLab.exe.\n"
               + "Levels live in ./data next to CytoidLab.exe. The folder button opens the selected level (or ./data).\n"
               + "Older AppData installs migrate into ./data automatically on load.\n"
               + "Player.log stays under AppData LocalLow\\TigerHix\\Cytoid Lab\\.\n"
               + $"Version: {CytoidLabVersion.DisplayName}";
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
