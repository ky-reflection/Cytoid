using UnityEngine;

public partial class HoldNote
{
    internal bool UseTimelineHoldProgress { get; private set; }

    /// <summary>True when hold body/line progress should render (finger down or post-scrub preview).</summary>
    public bool ShouldShowHoldBody => IsHolding || UseTimelineHoldProgress;

    internal void ApplyResyncVisualState(float time)
    {
        var hitWindowStart = Model.start_time + JudgmentOffset;
        var hitWindowEnd = Model.end_time + JudgmentOffset;
        if (time < hitWindowStart || time > hitWindowEnd) return;

        UseTimelineHoldProgress = true;
        HoldProgress = Mathf.Clamp01((time - hitWindowStart) / Model.Duration);
    }

    internal void RefreshTimelineHoldProgress()
    {
        if (!UseTimelineHoldProgress) return;

        var hitWindowStart = Model.start_time + JudgmentOffset;
        var hitWindowEnd = Model.end_time + JudgmentOffset;
        if (Game.Time < hitWindowStart || Game.Time > hitWindowEnd)
        {
            HoldProgress = 0;
            return;
        }

        HoldProgress = Mathf.Clamp01((Game.Time - hitWindowStart) / Model.Duration);
    }

    internal void ClearTimelineHoldProgress()
    {
        UseTimelineHoldProgress = false;
    }
}
