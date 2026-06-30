/// <summary>
/// Synchronous despawn for Cytoid Player timeline resync (bypasses async DragHeadNote.Collect).
/// </summary>
public partial class Note
{
    internal virtual void ForceDespawnForResync()
    {
        if (IsCollected) return;
        IsCollected = true;

        Renderer?.OnCollect();
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
        GreatGradeWeight = default;
        JudgmentOffset = default;
    }
}
