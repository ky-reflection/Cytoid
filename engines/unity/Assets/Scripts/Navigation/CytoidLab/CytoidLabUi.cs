using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime UI helpers for Cytoid Lab, aligned with Cytoid's RoundedCorners SoftButton / pill radii.
/// </summary>
public static class CytoidLabUi
{
    /// <summary>Matches SoftButton / PillRadioButton default radius in Cytoid.</summary>
    public const float SoftRadius = 16f;

    /// <summary>Slightly tighter radius used on compact Cytoid buttons.</summary>
    public const float ButtonRadius = 12f;

    /// <summary>Level list rows / panels.</summary>
    public const float PanelRadius = 12f;

    /// <summary>Square icon buttons (folder).</summary>
    public const float IconRadius = 10f;

    public static readonly Color ButtonColor = new Color(0.22f, 0.32f, 0.52f, 1f);
    public static readonly Color ButtonHighlight = new Color(0.30f, 0.42f, 0.64f, 1f);
    public static readonly Color ButtonPressed = new Color(0.16f, 0.24f, 0.40f, 1f);
    public static readonly Color RowColor = new Color(0.12f, 0.13f, 0.18f, 1f);
    public static readonly Color RowSelectedColor = new Color(0.18f, 0.32f, 0.52f, 1f);
    public static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.92f);
    public static readonly Color DangerColor = new Color(0.62f, 0.22f, 0.26f, 1f);
    public static readonly Color AccentGreen = new Color(0.22f, 0.72f, 0.38f, 1f);
    public static readonly Color AccentBlue = new Color(0.28f, 0.58f, 0.95f, 1f);

    private static readonly int WidthHeightRadiusId = Shader.PropertyToID("_WidthHeightRadius");
    private static readonly int BorderWidthId = Shader.PropertyToID("_BorderWidth");
    private static Material roundedTemplate;
    private static readonly List<ImageWithRoundedCorners> ActiveRounded = new List<ImageWithRoundedCorners>();

    public static void ApplyRoundedCorners(Image image, float radius, float borderWidth = 0f)
    {
        if (image == null) return;

        var template = GetRoundedTemplate();
        if (template == null)
        {
            Debug.LogWarning("[CytoidLab] RoundedCorners material/shader missing; leaving square Image.");
            return;
        }

        var mat = image.material;
        if (mat == null || mat.shader != template.shader)
        {
            mat = new Material(template)
            {
                name = $"CytoidLabRounded_{image.GetInstanceID()}",
                hideFlags = HideFlags.HideAndDontSave,
            };
            image.material = mat;
        }

        var rounded = image.GetComponent<ImageWithRoundedCorners>();
        if (rounded == null) rounded = image.gameObject.AddComponent<ImageWithRoundedCorners>();
        rounded.material = mat;
        rounded.radius = radius;
        rounded.borderWidth = borderWidth;
        PushRoundedProps(image.rectTransform, mat, radius, borderWidth);

        if (!ActiveRounded.Contains(rounded)) ActiveRounded.Add(rounded);
    }

    public static void ApplyRoundedButtonColors(Button button, Color? normal = null)
    {
        if (button == null) return;
        var colors = button.colors;
        var baseColor = normal ?? ButtonColor;
        colors.normalColor = baseColor;
        colors.highlightedColor = ButtonHighlight;
        colors.pressedColor = ButtonPressed;
        colors.selectedColor = baseColor;
        colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.45f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
    }

    /// <summary>
    /// Call after layout rebuilds so SDF corners match the final rect size.
    /// </summary>
    public static void RefreshRoundedCorners()
    {
        for (var i = ActiveRounded.Count - 1; i >= 0; i--)
        {
            var rounded = ActiveRounded[i];
            if (rounded == null)
            {
                ActiveRounded.RemoveAt(i);
                continue;
            }

            if (rounded.material == null) continue;
            PushRoundedProps(
                (RectTransform)rounded.transform,
                rounded.material,
                rounded.radius,
                rounded.borderWidth);
        }
    }

    private static void PushRoundedProps(RectTransform rectTransform, Material mat, float radius, float borderWidth)
    {
        if (rectTransform == null || mat == null) return;
        var rect = rectTransform.rect;
        var w = Mathf.Max(1f, rect.width);
        var h = Mathf.Max(1f, rect.height);
        mat.SetVector(WidthHeightRadiusId, new Vector4(w, h, radius, 0f));
        mat.SetFloat(BorderWidthId, borderWidth);
    }

    private static Material GetRoundedTemplate()
    {
        if (roundedTemplate != null) return roundedTemplate;
        roundedTemplate = Resources.Load<Material>("CytoidLab/RoundedCorners");
        if (roundedTemplate != null) return roundedTemplate;

        var shader = Shader.Find("UI/RoundedCorners/RoundedCorners");
        if (shader == null) return null;
        roundedTemplate = new Material(shader)
        {
            name = "CytoidLabRoundedTemplate",
            hideFlags = HideFlags.HideAndDontSave,
        };
        return roundedTemplate;
    }
}
