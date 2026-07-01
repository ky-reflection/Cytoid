using UnityEngine;

/// <summary>
/// Cytoid Lab timeline scrubbing extensions for <see cref="HoldNote"/>.
/// Restores body progress and head approach after seek/preview (hold-seek RC-9).
/// Drag chains use <see cref="DragHeadNote.FastForwardToTime"/>; holds need the same here.
/// </summary>
public partial class HoldNote
{
    internal bool UseTimelineHoldProgress { get; private set; }

    /// <summary>
    /// When true during timeline scrub/resync (<see cref="Game.SuppressTimelineGameplayMutations"/>),
    /// renderers read <see cref="TimelineApproachScale"/> instead of live Game.Time.
    /// Must not stay set after scrub ends or head scale freezes while opacity keeps advancing (RC-9).
    /// </summary>
    internal bool UseTimelineVisualState { get; private set; }

    /// <summary>Head approach scale [0,1] captured at the last timeline seek/preview time.</summary>
    internal float TimelineApproachScale { get; private set; }

    /// <summary>True when hold body/line progress should render (finger down or post-scrub preview).</summary>
    public bool ShouldShowHoldBody => IsHolding || UseTimelineHoldProgress;

    public override void SetData(int noteId)
    {
        ResetHoldRuntimeState();
        base.SetData(noteId);
    }

    internal override void ForceDespawnForResync()
    {
        ResetHoldRuntimeState();
        base.ForceDespawnForResync();
    }

    internal void ResetHoldRuntimeState()
    {
        HoldingStartTime = float.MaxValue;
        HeldDuration = 0;
        HoldProgress = 0;
        HoldingFingers.Clear();
        playedHitSoundAtBegin = false;
        UseTimelineHoldProgress = false;
        UseTimelineVisualState = false;
        TimelineApproachScale = 0f;
    }

    /// <summary>
    /// Set hold line/ring progress for timeline scrub or resync without finger input.
    /// </summary>
    internal void ApplyTimelineHoldProgress(float time)
    {
        var hitWindowStart = Model.start_time + JudgmentOffset;
        var hitWindowEnd = Model.end_time + JudgmentOffset;

        if (time < hitWindowStart)
        {
            UseTimelineHoldProgress = false;
            HoldProgress = 0;
            return;
        }

        UseTimelineHoldProgress = true;
        HoldProgress = time >= hitWindowEnd
            ? 1f
            : Mathf.Clamp01((time - hitWindowStart) / Model.Duration);
    }

    /// <summary>
    /// Timeline seek fast-forward: body <see cref="HoldProgress"/> plus head approach scale.
    /// <see cref="ClassicHoldNoteRenderer"/> reads <see cref="TimelineApproachScale"/> when
    /// <see cref="UseTimelineVisualState"/> is set instead of recomputing from live Game.Time.
    /// </summary>
    internal void FastForwardVisualStateToTime(float time)
    {
        ApplyTimelineHoldProgress(time);

        var approachStart = Model.intro_time;
        var approachEnd = Model.start_time;

        if (time < approachStart)
        {
            UseTimelineVisualState = true;
            TimelineApproachScale = 0f;
            return;
        }

        if (time < approachEnd)
        {
            UseTimelineVisualState = true;
            var denom = approachEnd - approachStart;
            TimelineApproachScale = denom > 0f
                ? Mathf.Clamp01((time - approachStart) / denom)
                : 1f;
            return;
        }

        UseTimelineVisualState = true;
        TimelineApproachScale = 1f;
    }

    internal void RefreshTimelineHoldProgress()
    {
        if (!UseTimelineHoldProgress) return;
        FastForwardVisualStateToTime(Game.Time);
    }

    /// <summary>Drop seek snapshot for head approach only; keeps body <see cref="HoldProgress"/> when mid-hold.</summary>
    internal void ClearTimelineVisualState()
    {
        UseTimelineVisualState = false;
        TimelineApproachScale = 0f;
    }

    internal void ClearTimelineHoldProgress()
    {
        UseTimelineHoldProgress = false;
        ClearTimelineVisualState();
    }
}
