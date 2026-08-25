using System;
using System.Collections.Generic;
using UnityEngine;

public class HoldNote : Note
{
    public float HoldingStartTime { get; protected set; } = float.MaxValue;
    public float HeldDuration  { get; protected set; }
    public float HoldProgress { get; protected set; }
    public List<int> HoldingFingers { get; } = new List<int>(2);

    private bool playedHitSoundAtBegin;
    /// <summary>
    /// Finger left after the hold start. Judgment waits for <see cref="OnGameUpdate"/>
    /// so HeldDuration includes the current music frame (input runs first).
    /// </summary>
    private bool pendingReleaseJudgment;

    public bool IsHolding => HoldingFingers.Count > 0;

    protected override NoteRenderer CreateRenderer()
    {
        return Game.Config.UseClassicStyle
            ? (NoteRenderer) new ClassicHoldNoteRenderer(this)
            : throw new NotSupportedException();
    }

    public override void Collect()
    {
        if (IsCollected) return;
        
        HoldingStartTime = float.MaxValue;
        HeldDuration = default;
        HoldProgress = default;
        HoldingFingers.Clear();
        playedHitSoundAtBegin = false;
        pendingReleaseJudgment = false;
        base.Collect();
    }

    protected override void OnGameUpdate(Game _)
    {
        // Grade a same-frame release before Note.ShouldMiss can treat it as a timeout.
        // 0-tick stay: complete on Time >= end while still holding (see HoldNoteTiming).
        TickHoldJudgment();
        base.OnGameUpdate(_);
        // Autoplay may bind in Note.OnGameUpdate; complete the hold on this tick.
        TickHoldJudgment();
    }

    public override bool ShouldMiss()
    {
        if (IsHolding || pendingReleaseJudgment) return false;
        return base.ShouldMiss();
    }
    
    public override bool OnTouch(Vector2 screenPos)
    {
        // Hold start is owned by InputController (FingerDown / Update → UpdateFinger).
        // false keeps TryClear off the Down path; binding consumes the event there.
        return false;
    }

    public void UpdateFinger(int finger, bool isHolding)
    {
        var previouslyHolding = IsHolding;
        
        if (isHolding)
        {
            HoldingFingers.Add(finger);
            if (!previouslyHolding)
            {
                HoldingStartTime = Game.Time;
                pendingReleaseJudgment = false;
            }
        }
        else
        {
            HoldingFingers.Remove(finger);
        }

        // Do not Clear here. GameTouchInput (-100) runs before Game.Update
        // advances Time / ticks this hold. Immediate release judgment uses a
        // stale HeldDuration. 0-tick / ultra-short holds are played by sliding
        // onto the note and staying; completion is owned by OnGameUpdate.
        if (previouslyHolding && HoldingFingers.Count == 0 &&
            HoldNoteTiming.ShouldJudgeRelease(Game.Time, Model.start_time, JudgmentOffset, Game.State.IsPlaying))
        {
            pendingReleaseJudgment = true;
        }
    }

    public override NoteGrade CalculateGrade()
    {
        var grade = NoteGrade.Miss;
        var rankedGrade = NoteGrade.Miss;
        // print($"HeldDuration: {HeldDuration}, ModelDuration: {Model.Duration}, HoldingStartTime: {HoldingStartTime}, ModelStartTime: {Model.start_time}");
        if (HeldDuration > Model.Duration - 0.05f) grade = NoteGrade.Perfect;
        else if (HeldDuration > Model.Duration * 0.7f) grade = NoteGrade.Great;
        else if (HeldDuration > Model.Duration * 0.5f) grade = NoteGrade.Good;
        else if (HeldDuration > Model.Duration * 0.3f) grade = NoteGrade.Bad;

        if (Game.State.Mode != GameMode.Practice)
        {
            if (HoldingStartTime != float.MaxValue && Mathf.Max(HoldingStartTime, Model.start_time + JudgmentOffset) > Model.start_time + JudgmentOffset)
            {
                var lateBy = HoldingStartTime - (Model.start_time + JudgmentOffset);
                if (lateBy < 0.200f) rankedGrade = NoteGrade.Bad;
                if (lateBy < 0.150f) rankedGrade = NoteGrade.Good;
                if (lateBy < 0.070f) rankedGrade = NoteGrade.Great;
                if (lateBy <= 0.040f) rankedGrade = NoteGrade.Perfect;
                if (rankedGrade == NoteGrade.Great) GreatGradeWeight = 1.0f - (lateBy - 0.040f) / (0.070f - 0.040f);
            }
            else
            {
                rankedGrade = grade;
                if (rankedGrade == NoteGrade.Great) GreatGradeWeight = 1.0f - (HeldDuration - Model.Duration * 0.70f) /
                                       (Model.Duration - 0.050f - Model.Duration * 0.70f);
            }
        }

        if (Game.State.Mode != GameMode.Practice && rankedGrade < grade)
            return rankedGrade; // Return the "worse" ranking (Note miss < bad < good < great < perfect)
        return grade;
    }

    public override bool IsAutoEnabled()
    {
        return base.IsAutoEnabled() || Game.State.Mods.Contains(Mod.AutoHold);
    }

    private void TickHoldJudgment()
    {
        if (IsCleared) return;

        if (IsHolding)
        {
            SyncHoldProgress();

            if (!playedHitSoundAtBegin && HoldProgress >= 0 && Context.Player.Settings.HoldHitSoundTiming.Let(it => it == HoldHitSoundTiming.Begin || it == HoldHitSoundTiming.Both))
            {
                playedHitSoundAtBegin = true;
                PlayHitSound();
            }

            if (HoldNoteTiming.ShouldCompleteWhileHolding(Game.Time, Model.start_time, Model.end_time, JudgmentOffset) &&
                Game.State.IsPlaying)
            {
                HoldingFingers.Clear();
                pendingReleaseJudgment = false;
                Clear(IsAutoEnabled() ? NoteGrade.Perfect : CalculateGrade());
            }
        }
        else if (pendingReleaseJudgment)
        {
            SyncHoldProgress();
            pendingReleaseJudgment = false;
            if (HoldNoteTiming.ShouldJudgeRelease(Game.Time, Model.start_time, JudgmentOffset, Game.State.IsPlaying))
            {
                Clear(IsAutoEnabled() ? NoteGrade.Perfect : CalculateGrade());
            }
        }
        else
        {
            HoldProgress = 0;
        }
    }

    private void SyncHoldProgress()
    {
        HeldDuration = HoldNoteTiming.ComputeHeldDuration(
            Game.Time, Model.start_time, JudgmentOffset, HoldingStartTime);
        HoldProgress = HoldNoteTiming.ComputeHoldProgress(
            Game.Time, Model.start_time, Model.end_time, JudgmentOffset);
    }
}
