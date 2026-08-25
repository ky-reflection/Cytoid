/// <summary>
/// Pure hold-timeline helpers shared by <see cref="HoldNote"/> and EditMode tests.
/// </summary>
public static class HoldNoteTiming
{
    public const float ZeroDurationEpsilon = 1e-4f;

    public static float EffectiveStart(float startTime, float judgmentOffset) =>
        startTime + judgmentOffset;

    public static float EffectiveEnd(float endTime, float judgmentOffset) =>
        endTime + judgmentOffset;

    public static float ComputeHeldDuration(
        float gameTime,
        float startTime,
        float judgmentOffset,
        float holdingStartTime)
    {
        var start = EffectiveStart(startTime, judgmentOffset);
        if (gameTime < start) return 0f;
        return gameTime - UnityEngine.Mathf.Max(start, holdingStartTime);
    }

    public static float ComputeHoldProgress(
        float gameTime,
        float startTime,
        float endTime,
        float judgmentOffset)
    {
        var start = EffectiveStart(startTime, judgmentOffset);
        var duration = endTime - startTime;
        if (duration > ZeroDurationEpsilon)
            return (gameTime - start) / duration;
        return gameTime >= start ? 1f : 0f;
    }

    /// <summary>
    /// Complete while the finger is still down. For a 0-tick hold, end == start,
    /// so <c>Time &gt;= end</c> is enough — do not also require <c>Time &gt; start</c>.
    /// The old pair (clear fingers on <c>&gt;= end</c>, judge only on <c>&gt; start</c>)
    /// dropped a stay-on-note bind at the exact start instant without judging.
    /// </summary>
    public static bool ShouldCompleteWhileHolding(
        float gameTime,
        float startTime,
        float endTime,
        float judgmentOffset) =>
        gameTime >= EffectiveEnd(endTime, judgmentOffset);

    /// <summary>
    /// FingerUp / slide-off after the hold start must wait for the next
    /// <c>OnGameUpdate</c> so <see cref="ComputeHeldDuration"/> sees this
    /// frame's music time. <see cref="GameTouchInput"/> runs at execution
    /// order -100, before <see cref="Game"/> advances <c>Time</c>.
    /// </summary>
    public static bool ShouldJudgeRelease(
        float gameTime,
        float startTime,
        float judgmentOffset,
        bool isPlaying) =>
        isPlaying && gameTime > EffectiveStart(startTime, judgmentOffset);
}
