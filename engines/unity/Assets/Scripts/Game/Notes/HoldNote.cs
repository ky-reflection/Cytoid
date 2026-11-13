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
    private long chainTimingBoundAt;
    private long? chainTimingPausedAt;

    private long ChainTimingClose => ChainTimingJudgment.HoldClose(ChainTimingStart, ChainTimingEnd);
    internal bool ChainTimingColliderActiveForHold =>
        ChainTimingNow < (IsHolding || chainTimingPausedAt.HasValue || IsAutoEnabled() ? ChainTimingEnd : ChainTimingClose);

    internal bool CanBindFinger =>
        !UsesChainTimingJudgment || (!IsAutoEnabled() && !chainTimingPausedAt.HasValue &&
            (IsHolding ? ChainTimingNow < ChainTimingEnd : ChainTimingJudgment.CanStartHold(ChainTimingNow, ChainTimingStart, ChainTimingEnd)));
    
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
        chainTimingBoundAt = default;
        chainTimingPausedAt = null;
        base.Collect();
    }

    protected override void OnGameUpdate(Game _)
    {
        base.OnGameUpdate(_);
        if (IsHolding)
        {
            if (UsesChainTimingJudgment)
            {
                HeldDuration = ChainTimingHeldTime(ChainTimingNow) / 1_000_000f;
            }
            else if (Game.Time >= Model.start_time + JudgmentOffset)
            {
                HeldDuration = Game.Time - Mathf.Max(Model.start_time + JudgmentOffset, HoldingStartTime);
            }
            else
            {
                HeldDuration = 0;
            }
            HoldProgress = (Game.Time - (Model.start_time + JudgmentOffset)) / Model.Duration;
            
            if (!playedHitSoundAtBegin && HoldProgress >= 0 && Context.Player.Settings.HoldHitSoundTiming.Let(it => it == HoldHitSoundTiming.Begin || it == HoldHitSoundTiming.Both))
            {
                playedHitSoundAtBegin = true;
                PlayHitSound();
            }

            // Already completed?
            if (UsesChainTimingJudgment)
            {
                if (ChainTimingNow >= ChainTimingEnd) FinishChainTimingHold(ChainTimingEnd);
            }
            else if (Game.Time >= Model.end_time + JudgmentOffset)
            {
                HoldingFingers.Clear();
                if (Game.Time > Model.start_time + JudgmentOffset && Game.State.IsPlaying)
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
        if (UsesChainTimingJudgment)
            return !IsHolding && !chainTimingPausedAt.HasValue && ChainTimingNow >= ChainTimingClose;
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
        if (UsesChainTimingJudgment)
        {
            if (!Game.State.IsPlaying || IsCleared) return;
            if (isHolding)
            {
                if (HoldingFingers.Contains(finger)) return;
                if (!IsHolding)
                {
                    HoldingStartTime = Game.Time;
                    chainTimingBoundAt = ChainTimingNow;
                }
                HoldingFingers.Add(finger);
            }
            else if (HoldingFingers.Remove(finger) && !IsHolding)
            {
                ReleaseChainTimingHold(ChainTimingNow);
            }
            return;
        }

        var previouslyHolding = IsHolding;
        
        if (isHolding)
        {
            HoldingFingers.Add(finger);
            if (!previouslyHolding)
            {
                HoldingStartTime = Game.Time;
            }
        }
        else
        {
            HoldingFingers.Remove(finger);
        }

        if (HoldingFingers.Count == 0 && Game.Time > Model.start_time + JudgmentOffset)
        {
            if (Game.Time > Model.start_time + JudgmentOffset && Game.State.IsPlaying)
            {
                Clear(IsAutoEnabled() ? NoteGrade.Perfect : CalculateGrade());
            }
        }
    }

    internal void SuspendChainTimingHold()
    {
        if (UsesChainTimingJudgment && IsHolding) chainTimingPausedAt = ChainTimingNow;
    }

    internal void ResumeChainTimingHold()
    {
        if (!chainTimingPausedAt.HasValue) return;
        var pausedAt = chainTimingPausedAt.Value;
        chainTimingPausedAt = null;
        if (!IsHolding) ReleaseChainTimingHold(pausedAt);
    }

    private void ReleaseChainTimingHold(long time)
    {
        if (time >= ChainTimingStart)
        {
            FinishChainTimingHold(time);
        }
        else
        {
            HoldingStartTime = float.MaxValue;
            HeldDuration = 0;
        }
    }

    private long ChainTimingHeldTime(long time) => ChainTimingJudgment.HeldTime(ChainTimingStart, ChainTimingEnd, chainTimingBoundAt, time);

    private void FinishChainTimingHold(long time)
    {
        var held = ChainTimingHeldTime(time);
        HeldDuration = held / 1_000_000f;
        HoldingFingers.Clear();
        Clear(IsAutoEnabled() ? NoteGrade.Perfect : ChainTimingJudgment.Hold(ChainTimingEnd - ChainTimingStart, held));
    }

    public override NoteGrade CalculateGrade()
    {
        if (UsesChainTimingJudgment) return ChainTimingJudgment.Hold(ChainTimingEnd - ChainTimingStart, ChainTimingHeldTime(ChainTimingNow));
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
}