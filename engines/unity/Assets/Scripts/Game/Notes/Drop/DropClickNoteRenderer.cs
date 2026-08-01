using UnityEngine;
using Object = UnityEngine.Object;

// Drop note renderer for DropClick. Inherits ClassicClickNoteRenderer (which is currently a
// pass-through to ClassicNoteRenderer) and adds:
//   1. A Core child SpriteRenderer that sits above Fill, always white.
//   2. The drop "falling" Y offset ported faithfully from Cylheim.
// Drop notes do NOT use the approach scale/fill animations — they are full-size from intro_time
// and reach the scanline via the Y offset alone, so UpdateTransformScale / UpdateFillScale are
// overridden to set fixed values.
public class DropClickNoteRenderer : ClassicClickNoteRenderer
{
    protected SpriteRenderer Core;

    public DropClickNoteRenderer(ClickNote clickNote) : base(clickNote)
    {
        // The Core child is provided by the DropClick prefab.
        var coreTransform = Note.transform.Find("NoteCore");
        if (coreTransform != null) Core = coreTransform.GetComponent<SpriteRenderer>();
    }

    public override void OnNoteLoaded()
    {
        base.OnNoteLoaded();
        // Drop note Ring sprites are solid bars (unlike Classic note rings which are outlines).
        // Render Fill on top of Ring so the colored Fill is visible.
        Fill.sortingOrder = Ring.sortingOrder + 1;
        if (Core != null) Core.sortingOrder = Fill.sortingOrder + 1;
    }

    protected override void Render()
    {
        base.Render();
        ApplyDropOffset();
    }

    private void ApplyDropOffset()
    {
        var page = Note.Page;
        double durationTick = (page.end_tick - page.start_tick) * 5.0;
        if (durationTick <= 0 || !double.IsFinite(durationTick)) return;

        // Cylheim uses screen coords (Y down): Up=+1, Down=-1.
        // Unity uses world coords (Y up): flip the sign so Down falls from above (+Y), Up rises from below (-Y).
        float dirSign = Note.Model.NoteDirection == 1 ? -1f : 1f;
        float timeDiffSeconds = (float) (Note.Model.start_time - Note.Game.Time);
        if (timeDiffSeconds <= 0f) return;

        float screenHeightWorld = 2f * Note.Game.camera.orthographicSize;
        float offsetYWorld = dirSign * (8_000_000f / (float) durationTick) * timeDiffSeconds / 1080f *
                             screenHeightWorld;
        var pos = Note.transform.localPosition;
        pos.y += offsetYWorld;
        Note.transform.localPosition = pos;
    }

    protected override void UpdateTransformScale()
    {
        var scale = BaseTransformSize * Note.Model.Override.SizeMultiplier;
        Note.transform.localScale = new Vector3(scale, scale, Note.transform.localScale.z);
    }

    protected override void UpdateFillScale()
    {
        var z = Fill.transform.localScale.z;
        Fill.transform.localScale = new Vector3(1, 1, z);
    }

    protected override void UpdateComponentStates()
    {
        base.UpdateComponentStates();
        if (Core == null) return;
        if (!Note.IsCleared && Game.Time >= Note.Model.intro_time &&
            Game.Time <= Note.Model.end_time + Note.MissThreshold)
        {
            Core.enabled = !Game.State.Mods.Contains(Mod.HideNotes);
        }
        else
        {
            Core.enabled = false;
        }
    }

    protected override void UpdateComponentOpacity()
    {
        base.UpdateComponentOpacity();
        // Core is always pure white — only alpha is modulated, no tint applied.
        if (Core != null) Core.color = Color.white.WithAlpha(EasedOpacity);
    }

    public override void Dispose()
    {
        base.Dispose();
        if (Core != null) Object.Destroy(Core);
    }
}
