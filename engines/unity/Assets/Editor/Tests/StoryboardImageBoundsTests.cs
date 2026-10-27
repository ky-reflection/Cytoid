using Cytoid.Storyboard.Sprites;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class StoryboardImageBoundsTests
{
    private Camera camera;
    private Canvas canvas;
    private RectTransform parent;
    private Image original;
    private StoryboardImage patched;

    [SetUp]
    public void SetUp()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("Requires a graphics device for pixel comparisons.");

        camera = new GameObject("Bounds camera", typeof(Camera)).GetComponent<Camera>();
        camera.transform.position = new Vector3(0, 26.666667f, -10);
        camera.fieldOfView = 53.2f;
        camera.nearClipPlane = .01f;
        camera.farClipPlane = 5000;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << 31;
        camera.targetTexture = new RenderTexture(800, 600, 24);
        canvas = new GameObject("Original canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup))
            .GetComponent<Canvas>();
        canvas.gameObject.layer = 31;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 100;
        Canvas.ForceUpdateCanvases();
        parent = new GameObject("Note", typeof(RectTransform)).GetComponent<RectTransform>();
        parent.SetParent(canvas.transform, false);
        parent.anchorMin = parent.anchorMax = Vector2.zero;
        parent.anchoredPosition = new Vector2(400, 100);
        parent.sizeDelta = Vector2.zero;
        original = CreateImage<Image>();
        patched = CreateImage<StoryboardImage>();
        patched.gameObject.SetActive(false);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(canvas.gameObject);
        var target = camera.targetTexture;
        camera.targetTexture = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(camera.gameObject);
    }

    [TestCase(400f, 800f)]
    [TestCase(508.9f, 1017.8f)]
    [TestCase(509f, 1018f)]
    [TestCase(1400f, 3200f)]
    [TestCase(1600f, 3200f)]
    public void DepthSpritesRenderWithOriginalGeometry(float y, float z)
    {
        original.transform.localPosition = patched.transform.localPosition = new Vector3(0, y, z);
        var before = Pixels();
        var originalCorners = new Vector3[4];
        original.rectTransform.GetWorldCorners(originalCorners);
        ShowPatched();
        var after = Pixels();
        Assert.That(after, Is.GreaterThan(0));
        if (before > 0) Assert.That(after, Is.EqualTo(before));
        var patchedCorners = new Vector3[4];
        patched.rectTransform.GetWorldCorners(patchedCorners);
        for (var i = 0; i < 4; i++)
            Assert.That(Vector3.Distance(originalCorners[i], patchedCorners[i]), Is.LessThan(.002f));
        Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
    }

    [Test]
    public void RectMaskStillClipsTheOriginalImageGeometry()
    {
        // A sorting-override subcanvas intentionally stops ancestor mask lookup.
        original.GetComponent<Canvas>().overrideSorting = false;
        patched.GetComponent<Canvas>().overrideSorting = false;
        parent.sizeDelta = new Vector2(160, 20);
        parent.gameObject.AddComponent<RectMask2D>();
        original.transform.localPosition = patched.transform.localPosition = new Vector3(0, 10, 0);
        var before = Pixels();
        ShowPatched();
        Assert.That(before, Is.GreaterThan(0));
        Assert.That(Pixels(), Is.EqualTo(before));
        parent.GetComponent<RectMask2D>().enabled = false;
        Assert.That(Pixels(), Is.GreaterThan(before));
    }

    [Test]
    public void OriginalCanvasOpacityAndActivationStillControlSprites()
    {
        ShowPatched();
        Assert.That(Pixels(), Is.GreaterThan(0));
        var group = canvas.GetComponent<CanvasGroup>();
        group.alpha = 0;
        Assert.That(Pixels(), Is.Zero);
        group.alpha = 1;
        Assert.That(Pixels(), Is.GreaterThan(0));
        canvas.gameObject.SetActive(false);
        Assert.That(Pixels(), Is.Zero);
        canvas.gameObject.SetActive(true);
        Assert.That(Pixels(), Is.GreaterThan(0));
    }

    [Test]
    public void CachedMeshCanMoveIntoTheDepthSection()
    {
        ShowPatched();
        Assert.That(Pixels(), Is.GreaterThan(0));
        patched.transform.localPosition = new Vector3(0, 1400, 3200);
        Assert.That(Pixels(), Is.GreaterThan(0));
        patched.transform.localPosition = Vector3.zero;
        Assert.That(Pixels(), Is.GreaterThan(0));
    }

    [Test]
    public void AddingAndRemovingRectMaskRestoresCachedBounds()
    {
        original.GetComponent<Canvas>().overrideSorting = false;
        patched.GetComponent<Canvas>().overrideSorting = false;
        parent.sizeDelta = new Vector2(160, 20);
        original.transform.localPosition = patched.transform.localPosition = new Vector3(0, 10, 0);
        ShowPatched();
        var full = Pixels();
        var mask = parent.gameObject.AddComponent<RectMask2D>();
        var clipped = Pixels();
        Assert.That(clipped, Is.GreaterThan(0).And.LessThan(full));
        patched.gameObject.SetActive(false);
        original.gameObject.SetActive(true);
        original.GetComponent<Canvas>().overrideSorting = false;
        original.RecalculateClipping();
        original.RecalculateMasking();
        Assert.That(Pixels(), Is.EqualTo(clipped));
        ShowPatched();
        mask.enabled = false;
        Assert.That(Pixels(), Is.EqualTo(full));
    }

    [Test]
    public void StencilMaskStillClipsOriginalGeometry()
    {
        original.GetComponent<Canvas>().overrideSorting = false;
        patched.GetComponent<Canvas>().overrideSorting = false;
        parent.sizeDelta = new Vector2(160, 20);
        parent.gameObject.layer = 31;
        parent.gameObject.AddComponent<Image>().color = Color.red;
        var mask = parent.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        original.transform.localPosition = patched.transform.localPosition = new Vector3(0, 10, 0);
        var before = Pixels();
        ShowPatched();
        Assert.That(before, Is.GreaterThan(0));
        Assert.That(Pixels(), Is.EqualTo(before));
        mask.enabled = false;
        Assert.That(Pixels(), Is.GreaterThan(before));
    }

    [Test]
    public void SpritePrefabUsesBoundsFixAndKeepsItsImageSettings()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<Image>("Assets/StaticResources/Prefabs/Storyboard/Sprite.prefab");
        Assert.That(prefab, Is.TypeOf<StoryboardImage>());
        Assert.That(prefab.preserveAspect, Is.True);
        Assert.That(prefab.raycastTarget, Is.False);
        Assert.That(prefab.rectTransform.sizeDelta, Is.EqualTo(new Vector2(200, 200)));
    }

    private T CreateImage<T>() where T : Image
    {
        var go = new GameObject(typeof(T).Name, typeof(RectTransform), typeof(T), typeof(Canvas));
        go.layer = 31;
        go.transform.SetParent(parent, false);
        go.GetComponent<Canvas>().overrideSorting = true;
        go.GetComponent<Canvas>().sortingOrder = 1;
        var image = go.GetComponent<T>();
        image.color = Color.green;
        image.rectTransform.sizeDelta = new Vector2(160, 40);
        image.transform.localRotation = Quaternion.Euler(63.43495f, 0, 0);
        return image;
    }

    private void ShowPatched()
    {
        var overrideSorting = original.GetComponent<Canvas>().overrideSorting;
        original.gameObject.SetActive(false);
        patched.gameObject.SetActive(true);
        // Canvas activation can restore overrideSorting; both sides must use the
        // same setting before comparing mask inheritance.
        patched.GetComponent<Canvas>().overrideSorting = overrideSorting;
        patched.RecalculateClipping();
        patched.RecalculateMasking();
    }

    private int Pixels()
    {
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        var target = camera.targetTexture;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            var count = 0;
            foreach (var p in pixels.GetPixels32())
                if (p.g > 50 && p.g > p.r * 2 && p.g > p.b * 2) count++;
            return count;
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(pixels);
        }
    }
}
