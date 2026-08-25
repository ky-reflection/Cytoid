using NUnit.Framework;

public class HoldNoteTimingTests
{
    const float Start = 1.000f;
    const float Offset = 0f;

    [Test]
    public void HeldDuration_IsZero_BeforeStart()
    {
        var held = HoldNoteTiming.ComputeHeldDuration(0.990f, Start, Offset, 0.980f);
        Assert.AreEqual(0f, held);
    }

    [Test]
    public void HeldDuration_Ignores_TimeBeforeStart()
    {
        var held = HoldNoteTiming.ComputeHeldDuration(1.032f, Start, Offset, 0.980f);
        Assert.AreEqual(0.032f, held, 1e-6f);
    }

    [Test]
    public void HeldDuration_UsesLateGrab()
    {
        var held = HoldNoteTiming.ComputeHeldDuration(1.050f, Start, Offset, 1.020f);
        Assert.AreEqual(0.030f, held, 1e-6f);
    }

    [Test]
    public void HoldProgress_IsFinite_ForZeroDuration()
    {
        Assert.AreEqual(0f, HoldNoteTiming.ComputeHoldProgress(0.9f, Start, Start, Offset));
        Assert.AreEqual(1f, HoldNoteTiming.ComputeHoldProgress(1.0f, Start, Start, Offset));
        Assert.IsFalse(float.IsNaN(HoldNoteTiming.ComputeHoldProgress(1.0f, Start, Start, Offset)));
        Assert.IsFalse(float.IsInfinity(HoldNoteTiming.ComputeHoldProgress(1.0f, Start, Start, Offset)));
    }

    [Test]
    public void Release_BeforeStart_DoesNotJudge()
    {
        Assert.IsFalse(HoldNoteTiming.ShouldJudgeRelease(0.995f, Start, Offset, true));
    }

    [Test]
    public void Release_AfterStart_JudgesWhenPlaying()
    {
        Assert.IsTrue(HoldNoteTiming.ShouldJudgeRelease(1.001f, Start, Offset, true));
        Assert.IsFalse(HoldNoteTiming.ShouldJudgeRelease(1.001f, Start, Offset, false));
    }

    [Test]
    public void ZeroTick_CompletesAtExactStart_WhileStillHolding()
    {
        // 0-tick hold: end == start. Swipe-on-and-stay must complete at Time == start,
        // not wait for a later frame and not drop the bind without judging.
        Assert.IsFalse(HoldNoteTiming.ShouldCompleteWhileHolding(Start - 0.001f, Start, Start, Offset));
        Assert.IsTrue(HoldNoteTiming.ShouldCompleteWhileHolding(Start, Start, Start, Offset));
        Assert.IsTrue(HoldNoteTiming.ShouldCompleteWhileHolding(Start + 0.016f, Start, Start, Offset));
    }

    [Test]
    public void ShortHold_CompletesWhenTimeReachesEnd_WhileStillHolding()
    {
        const float end = Start + 0.040f;
        const float frame = 0.016f;

        Assert.IsFalse(HoldNoteTiming.ShouldCompleteWhileHolding(Start, Start, end, Offset));
        Assert.IsTrue(HoldNoteTiming.ShouldCompleteWhileHolding(end, Start, end, Offset));
        Assert.IsTrue(HoldNoteTiming.ShouldCompleteWhileHolding(Start + 3f * frame, Start, end, Offset));
    }

    [Test]
    public void SwipeRelease_IncludesTheInputFrame_AfterTimeAdvances()
    {
        // GameTouchInput (-100) unbinds with stale Time; OnGameUpdate must
        // recompute HeldDuration after SynchronizeMusic so the swipe frame counts.
        const float bindTime = Start;
        const float staleTime = Start + 0.016f;
        const float advancedTime = Start + 0.032f;

        var staleHeld = HoldNoteTiming.ComputeHeldDuration(staleTime, Start, Offset, bindTime);
        var advancedHeld = HoldNoteTiming.ComputeHeldDuration(advancedTime, Start, Offset, bindTime);

        Assert.AreEqual(0.016f, staleHeld, 1e-6f);
        Assert.AreEqual(0.032f, advancedHeld, 1e-6f);
        Assert.Greater(advancedHeld, staleHeld);
        Assert.IsTrue(HoldNoteTiming.ShouldJudgeRelease(advancedTime, Start, Offset, true));
    }

    [Test]
    public void FortyMsHold_OneStaleFrame_MissesThirtyPercent_ButTwoFramesHit()
    {
        // 40ms hold: Bad needs HeldDuration > 12ms. Judging on the stale
        // bind frame (0ms) is a Miss; including the next music frame (16ms) is Bad.
        const float duration = 0.040f;
        var staleHeld = HoldNoteTiming.ComputeHeldDuration(Start, Start, Offset, Start);
        var nextFrameHeld = HoldNoteTiming.ComputeHeldDuration(Start + 0.016f, Start, Offset, Start);

        Assert.AreEqual(0f, staleHeld);
        Assert.IsFalse(staleHeld > duration * 0.3f);
        Assert.IsTrue(nextFrameHeld > duration * 0.3f);
    }
}
