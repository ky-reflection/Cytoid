using UnityEngine;

// Ported from DragHeadNote judgment. DropDrag is standalone and intentionally does not inherit
// DragHead chain logic.
public class DropDragNote : Note
{
    protected override NoteRenderer CreateRenderer()
    {
        return new DropDragNoteRenderer(this);
    }

    public override void OnTouch(Vector2 screenPos)
    {
        // Current Lab Note.OnTouch returns void; this is the only signature adaptation from the branch.
        if (Model.start_time - Game.Time > 0.31f) return;
        if (Model.page_index > Game.Chart.CurrentPageId &&
            Model.start_time - Game.Time > Page.Duration / 2f) return;
        base.OnTouch(screenPos);
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
