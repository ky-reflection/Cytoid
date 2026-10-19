/// <summary>
/// Synchronous despawn for Cytoid Lab timeline resync (bypasses async DragHeadNote.Collect).
/// </summary>
public partial class Note
{
    internal virtual void ForceDespawnForResync()
    {
        if (IsCollected) return;
        IsCollected = true;

        Renderer?.OnCollect();
        // Remove membership while Model is still valid, just like normal collection.
        Game.ObjectPool.DragStacks.OnNoteCollected(this);
        Game.ObjectPool.CollectNote(this);
        Game.onGameUpdate.RemoveListener(OnGameUpdate);
        Game.onGameLateUpdate.RemoveListener(OnGameLateUpdate);
        Model = default;
        NextNoteModel = default;
        hasNextNote = default;
        nextNote = default;
        Chart = default;
        Page = default;
        MissThreshold = default;
        IsCleared = default;
        IsArmed = false;
        armedGrade = NoteGrade.None;
        dragStackBudgetPending = false;
        dragStackBudgetId = 0;
        DragStack = null;
        IsDragStackFollower = false;
        GreatGradeWeight = default;
        JudgmentOffset = default;
    }
}
