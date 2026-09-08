using System;
using System.Collections.Generic;
using UnityEngine;

public class HoldNote : Note
{
    /// <summary>
    /// Held-duration Perfect leniency. Holds shorter than this cannot fail the
    /// duration check (<c>HeldDuration &gt; Duration - leniency</c> is always true).
    /// </summary>
    public const float PerfectHoldDurationLeniency = 0.05f;

    /// <summary>
    /// Max earliness (seconds before start) at which a durationless hold may latch
    /// a sliding/tap contact that then leaves before start. Matches ranked late Bad.
    /// Earlier brushes (e.g. mid-drag overlap ~300ms early) are ignored.
    /// </summary>
    public const float ShortHoldEarlyLatchWindow = 0.200f;

    public float HoldingStartTime { get; protected set; } = float.MaxValue;
    public float HeldDuration  { get; protected set; }
    public float HoldProgress { get; protected set; }
    public List<int> HoldingFingers { get; } = new List<int>(2);

    private bool playedHitSoundAtBegin;
    private bool shortHoldContactLatched;

    public bool IsHolding => HoldingFingers.Count > 0;

    /// <summary>
    /// Durationless hold that received a valid contact and will settle at start
    /// even if the finger already left. Input should not re-bind this note.
    /// </summary>
    public bool IsShortHoldContactLatched => shortHoldContactLatched;

    private bool IsDurationlessHold => Model != null && Model.Duration <= PerfectHoldDurationLeniency;

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
        shortHoldContactLatched = false;
        base.Collect();
    }

    protected override void OnGameUpdate(Game _)
    {
        base.OnGameUpdate(_);
        if (IsCleared) return;

        var start = Model.start_time + JudgmentOffset;
        if (shortHoldContactLatched && Game.Time >= start)
        {
            SettleFromContact();
            return;
        }

        if (IsHolding)
        {
            if (Game.Time >= start)
            {
                HeldDuration = Game.Time - Mathf.Max(start, HoldingStartTime);
            }
            else
            {
                HeldDuration = 0;
            }
            HoldProgress = Model.Duration > 1e-4f
                ? (Game.Time - start) / Model.Duration
                : (Game.Time >= start ? 1f : 0f);
            
            if (!playedHitSoundAtBegin && HoldProgress >= 0 && Context.Player.Settings.HoldHitSoundTiming.Let(it => it == HoldHitSoundTiming.Begin || it == HoldHitSoundTiming.Both))
            {
                playedHitSoundAtBegin = true;
                PlayHitSound();
            }

            // Already completed?
            if (Game.Time >= Model.end_time + JudgmentOffset)
            {
                HoldingFingers.Clear();
                if (Game.Time > start && Game.State.IsPlaying)
                {
                    Clear(IsAutoEnabled() ? NoteGrade.Perfect : CalculateGrade());
                }
            }
        }
        else
        {
            HoldProgress = 0;
        }
    }

    public override bool ShouldMiss()
    {
        if (shortHoldContactLatched) return false;
        return !IsHolding && base.ShouldMiss();
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
            if (!previouslyHolding && !shortHoldContactLatched)
            {
                HoldingStartTime = Game.Time;
                TryLatchShortHoldContact();
            }
        }
        else
        {
            HoldingFingers.Remove(finger);
        }

        if (IsCleared) return;

        if (HoldingFingers.Count == 0 && Game.Time > Model.start_time + JudgmentOffset)
        {
            if (Game.Time > Model.start_time + JudgmentOffset && Game.State.IsPlaying)
            {
                Clear(IsAutoEnabled() ? NoteGrade.Perfect : CalculateGrade());
            }
        }
    }

    /// <summary>
    /// Durationless holds (chart duration ≤ Perfect leniency, e.g. hold_tick=15 ≈ 14ms)
    /// already grade Perfect on any HeldDuration. Sliding contact that leaves before
    /// start never reached CalculateGrade; latch that contact and settle at start.
    /// </summary>
    private void TryLatchShortHoldContact()
    {
        if (!IsDurationlessHold || IsCleared) return;

        var start = Model.start_time + JudgmentOffset;
        var earlyBy = start - Game.Time;
        if (earlyBy > ShortHoldEarlyLatchWindow) return;

        shortHoldContactLatched = true;
        if (Game.Time >= start && Game.State.IsPlaying)
            SettleFromContact();
    }

    private void SettleFromContact()
    {
        if (IsCleared || !Game.State.IsPlaying) return;

        if (!playedHitSoundAtBegin &&
            Context.Player.Settings.HoldHitSoundTiming.Let(it =>
                it == HoldHitSoundTiming.Begin || it == HoldHitSoundTiming.Both))
        {
            playedHitSoundAtBegin = true;
            PlayHitSound();
        }

        HoldingFingers.Clear();
        Clear(IsAutoEnabled() ? NoteGrade.Perfect : CalculateGrade());
    }

    public override NoteGrade CalculateGrade()
    {
        var grade = NoteGrade.Miss;
        var rankedGrade = NoteGrade.Miss;
        if (HeldDuration > Model.Duration - PerfectHoldDurationLeniency) grade = NoteGrade.Perfect;
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
                                       (Model.Duration - PerfectHoldDurationLeniency - Model.Duration * 0.70f);
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
}
