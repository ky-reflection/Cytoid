using UnityEngine;

public class ClassicDragChildNoteRenderer : ClassicNoteRenderer
{
    protected readonly SpriteMask SpriteMask;

    public ClassicDragChildNoteRenderer(DragChildNote dragChildNote) : base(dragChildNote)
    {
        SpriteMask = Note.transform.GetComponentInChildren<SpriteMask>();
    }

    public override void OnNoteLoaded()
    {
        base.OnNoteLoaded();
        SpriteMask.frontSortingOrder = Note.Model.id + 1;
        SpriteMask.backSortingOrder = Note.Model.id - 2;
        BindNoteId();
    }

    protected override void UpdateComponentStates()
    {
        Ring.enabled = false;

        var showNoteId = false;
        if (Game.State.Mods.Contains(Mod.HideNotes))
        {
            Fill.enabled = false;
            SpriteMask.enabled = false;
        }
        else if (Note.IsCleared)
        {
            Fill.enabled = Game.Time <= Note.Model.start_time;
            showNoteId = Fill.enabled;
            SpriteMask.enabled = false;
        }
        else
        {
            showNoteId = Game.Time >= Note.Model.intro_time;
            SpriteMask.enabled = showNoteId;
            Fill.enabled = showNoteId;
        }

        SetNoteIdVisible(showNoteId);
    }

    protected override void UpdateTransformScale()
    {
        var size = BaseTransformSize * Note.Model.Override.SizeMultiplier;

        var minSize = Note.Model.initial_scale;
        var timeScale = Mathf.Clamp((Game.Time - Note.Model.intro_time) / (Note.Model.start_time - Note.Model.intro_time), 0f, 1f);
        var timeScaledSize = size * minSize + size * (1 - minSize) * timeScale;

        Note.transform.SetLocalScaleXY(timeScaledSize, timeScaledSize);
    }

    protected override void UpdateFillScale()
    {
    }
}
