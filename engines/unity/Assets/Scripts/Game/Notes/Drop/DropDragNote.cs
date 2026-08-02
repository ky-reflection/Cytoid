using UnityEngine;

// SYNC-WARNING: DropDrag judgment mirrors DragHeadNote's OnTouch, CalculateGrade, IsAutoEnabled,
// and PlayHitSound. If you change judgment in either file, update the other. DragHead chain logic
// (OnGameLateUpdate, Collect, FromNoteModel/ToNoteModel) is INTENTIONALLY NOT inherited — DropDrag
// is standalone per design decision.
public class DropDragNote : Note
{
    protected override NoteRenderer CreateRenderer()
    {
        return new DropDragNoteRenderer(this);
    }

    public override bool OnTouch(Vector2 screenPos)
    {
        if (Model.start_time - Game.Time > 0.31f) return false;
        if (Model.page_index > Game.Chart.CurrentPageId &&
            Model.start_time - Game.Time > Page.Duration / 2f) return false;
        return base.OnTouch(screenPos);
    }

    public override NoteGrade CalculateGrade()
    {
        var grade = NoteGrade.Miss;
        var timeUntilStart = TimeUntilStart + JudgmentOffset;
        if (timeUntilStart >= 0)
        {
            grade = NoteGrade.None;
            if (timeUntilStart < 0.500f) grade = NoteGrade.Perfect;
        }
        else
        {
            var timePassed = -timeUntilStart;
            if (timePassed < 0.200f)
            {
                grade = NoteGrade.Perfect;
            }
        }
        return grade;
    }

    public override bool IsAutoEnabled()
    {
        return base.IsAutoEnabled() || Game.State.Mods.Contains(Mod.AutoDrag);
    }

    public override void PlayHitSound()
    {
        if (Context.Player.Settings.HitSound == "none") return;
        if (Context.AudioManager.IsSfxLoaded("HitSound")) Context.AudioManager.GetSfx("HitSound").Play();
        Context.Haptic(HapticTypes.Selection, false);
    }
}
