using UnityEngine;

public partial class HoldNote
{
    internal bool UseTimelineHoldProgress { get; private set; }

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

    internal void RefreshTimelineHoldProgress()
    {
        if (!UseTimelineHoldProgress) return;
        ApplyTimelineHoldProgress(Game.Time);
    }

    internal void ClearTimelineHoldProgress()
    {
        UseTimelineHoldProgress = false;
    }
}
