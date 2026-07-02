using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Cytoid Lab timeline scrubbing and playfield resync (partial Game extension).
/// </summary>
public partial class Game
{
    public bool UseInstantPauseResume;

    /// <summary>
    /// Cytoid Lab only (set during timeline drag / resync). When true, <see cref="Note.OnGameUpdate"/>
    /// skips Auto and miss/clear so scrubbing does not mutate gameplay state (RC-10).
    /// Always false during normal play and on Bridge/mobile builds.
    /// </summary>
    public bool SuppressTimelineGameplayMutations { get; internal set; }

    /// <summary>Called when timeline scrub UI releases the slider (after resync or cancel).</summary>
    internal void EndTimelineScrub()
    {
        SuppressTimelineGameplayMutations = false;
        ClearSpawnedHoldTimelineVisualState();
    }

    public void PreviewTimeline(float targetTime)
    {
        if (!IsLoaded || State == null || State.IsCompleted || State.IsFailed) return;

        // Drag preview: light resync (visual-only). Full rebuild on slider release.
        SuppressTimelineGameplayMutations = true;
        targetTime = Mathf.Clamp(targetTime, 0, MusicLength);
        Music.Stop();
        Music.PlaybackTime = targetTime;

        var nowDspTime = AudioSettings.dspTime;
        MusicStartedTimestamp = nowDspTime - targetTime;
        Time = targetTime;
        MusicProgress = MusicLength > 0 ? Time / MusicLength : 0;
        ChartProgress = ChartLength > 0 ? Time / ChartLength : 0;

        ResetChartIndicesToTime(targetTime);
        PruneInactiveSpawnedObjects(targetTime);
        // Ignore judged/cleared state so scrubbing back shows notes at targetTime (RS-4).
        SpawnActiveNotesAtTime(targetTime, visualPreviewOnly: true);
        RefreshSpawnedHoldProgress(targetTime);
        RefreshSpawnedDragVisualState(targetTime, visualPreviewOnly: true);

        Music.Play(AudioTrackIndex.Reserved1);
        onGameUpdate.Invoke(this);
        onGameLateUpdate.Invoke(this);
    }

    public void Seek(float targetTime)
    {
        ResyncPlayfieldToTime(targetTime, State != null && State.IsPlaying).Forget();
    }

    /// <summary>
    /// Hard reset: reload the Game scene from scratch (same as Retry).
    /// </summary>
    public void HardReloadPlayfield()
    {
        if (!IsLoaded) return;
        Retry();
    }

    public async UniTask ResyncPlayfieldToTime(float targetTime, bool resumePlaying = false)
    {
        if (!IsLoaded || State == null || State.IsCompleted || State.IsFailed) return;

        targetTime = Mathf.Clamp(targetTime, 0, MusicLength);
        var wasPlaying = resumePlaying;

        // Hold through resync until fast-forward + storyboard catch-up finish (RC-7).
        SuppressTimelineGameplayMutations = true;
        try
        {
            State.IsPlaying = false;
            AudioListener.pause = true;

            Music.Stop();
            Music.PlaybackTime = targetTime;

            var nowDspTime = AudioSettings.dspTime;
            MusicStartedTimestamp = nowDspTime - targetTime;

            Time = targetTime;
            MusicProgress = MusicLength > 0 ? Time / MusicLength : 0;
            ChartProgress = ChartLength > 0 ? Time / ChartLength : 0;

            ResetChartIndicesToTime(targetTime);
            ClearSpawnedObjects();
            State.ResetToTime(this, targetTime);
            SpawnActiveNotesAtTime(targetTime);
            inputController.ResetTouchState();

            State.IsCompleted = false;
            State.IsFailed = false;

            ResynchronizeChartOnNextFrame = true;
            ticksBeforeSynchronization = 600;

            if (Storyboard != null)
            {
                try
                {
                    await Storyboard.ResyncToTime(targetTime);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CytoidLab] Storyboard resync failed: {e}");
                }
            }

            Music.Play(AudioTrackIndex.Reserved1);
            if (wasPlaying)
            {
                GameStartedOrResumedTimestamp = UnityEngine.Time.realtimeSinceStartup;
                AudioListener.pause = false;
                State.IsPlaying = true;
                onGameUnpaused.Invoke(this);
            }

            onGameUpdate.Invoke(this);
            onGameLateUpdate.Invoke(this);
        }
        finally
        {
            SuppressTimelineGameplayMutations = false;
            ClearSpawnedHoldTimelineVisualState();
        }
    }

    private void ResetChartIndicesToTime(float targetTime)
    {
        Chart.CurrentEventId = 0;
        while (Chart.CurrentEventId < Chart.Model.event_order_list.Count &&
               Chart.Model.event_order_list[Chart.CurrentEventId].time < targetTime)
        {
            Chart.CurrentEventId++;
        }

        Chart.CurrentPageId = 0;
        while (Chart.CurrentPageId < Chart.Model.page_list.Count &&
               Chart.Model.page_list[Chart.CurrentPageId].end_time <= targetTime)
        {
            Chart.CurrentPageId++;
        }

        var notes = Chart.Model.note_map;
        Chart.CurrentNoteId = 0;
        while (Chart.CurrentNoteId < notes.Count && notes[Chart.CurrentNoteId].intro_time - 1f < targetTime)
        {
            Chart.CurrentNoteId++;
        }
    }

    private void SpawnActiveNotesAtTime(float targetTime, bool visualPreviewOnly = false)
    {
        var notes = Chart.Model.note_map;
        var judgmentOffset = Context.Player.Settings.JudgmentOffset;
        var ensuredLineHeads = new HashSet<int>();

        for (var noteId = 0; noteId < notes.Count; noteId++)
        {
            var note = notes[noteId];
            if (note.intro_time - 1f >= targetTime) continue;
            if (!visualPreviewOnly && State.IsJudged(note.id)) continue;
            if (!IsNoteActiveAtTime(note, Chart.Model, targetTime, judgmentOffset)) continue;
            if (ObjectPool.SpawnedNotes.ContainsKey(note.id)) continue;

            var type = (NoteType) note.type;
            switch (type)
            {
                case NoteType.DragHead:
                case NoteType.CDragHead:
                    EnsureDragChainLines(note, targetTime, ensuredLineHeads);
                    if (ObjectPool.SpawnNote(note) is DragHeadNote dragHead)
                    {
                        dragHead.FastForwardToTime(targetTime);
                    }
                    break;
                case NoteType.DragChild:
                case NoteType.CDragChild:
                    EnsureDragChainLines(FindDragChainHead(note, Chart.Model), targetTime, ensuredLineHeads);
                    ObjectPool.SpawnNote(note);
                    break;
                default:
                    if (ObjectPool.SpawnNote(note) is HoldNote holdNote)
                    {
                        // Body + head/ring approach (not just ApplyTimelineHoldProgress).
                        holdNote.FastForwardVisualStateToTime(targetTime);
                    }
                    break;
            }
        }
    }

    private static bool IsNoteActiveAtTime(ChartModel.Note note, ChartModel chart, float targetTime, float judgmentOffset)
    {
        if (note.intro_time - 1f >= targetTime) return false;

        var type = (NoteType) note.type;
        float endTime;
        float missThresh;

        if (type == NoteType.DragHead || type == NoteType.CDragHead)
        {
            endTime = note.GetDragEndNote(chart).end_time;
            missThresh = (type == NoteType.CDragHead ? NoteType.CDragChild : NoteType.DragChild)
                .GetDefaultMissThreshold();
        }
        else
        {
            endTime = note.end_time;
            missThresh = type.GetDefaultMissThreshold();
        }

        return targetTime <= endTime + missThresh + judgmentOffset;
    }

    private void EnsureDragChainLines(ChartModel.Note head, float targetTime, HashSet<int> ensuredHeads)
    {
        if (!ensuredHeads.Add(head.id)) return;
        if (head.intro_time - 1f >= targetTime) return;

        var notes = Chart.Model.note_map;
        var id = head.id;
        while (id > 0 && notes[id].next_id > 0)
        {
            var from = notes[id];
            var to = notes[from.next_id];
            if (targetTime < to.start_time)
            {
                var line = ObjectPool.SpawnDragLine(from, to);
                line.ResyncVisualToTime(targetTime);
            }
            id = from.next_id;
        }
    }

    private static ChartModel.Note FindDragChainHead(ChartModel.Note note, ChartModel chart)
    {
        foreach (var candidate in chart.note_list)
        {
            var type = (NoteType) candidate.type;
            if (type != NoteType.DragHead && type != NoteType.CDragHead) continue;

            var id = candidate.id;
            while (id > 0)
            {
                if (id == note.id) return candidate;
                var chainNote = chart.note_map[id];
                if (chainNote.next_id <= 0) break;
                id = chainNote.next_id;
            }
        }

        return note;
    }

    private void RefreshSpawnedHoldProgress(float time)
    {
        foreach (var note in ObjectPool.SpawnedNotes.Values)
        {
            if (note is HoldNote holdNote)
            {
                // Preview drag: keep on-screen holds aligned with slider time.
                holdNote.FastForwardVisualStateToTime(time);
            }
        }
    }

    /// <summary>
    /// Timeline drag preview: PruneInactive clears all drag lines each frame; rebuild and
    /// resync chains for every active head at <paramref name="targetTime"/> (RS-4).
    /// </summary>
    private void RefreshSpawnedDragVisualState(float targetTime, bool visualPreviewOnly = false)
    {
        var judgmentOffset = Context.Player.Settings.JudgmentOffset;
        var ensuredLineHeads = new HashSet<int>();

        foreach (var note in ObjectPool.SpawnedNotes.Values)
        {
            if (note is DragHeadNote dragHead)
            {
                dragHead.FastForwardToTime(targetTime);
            }
        }

        foreach (var candidate in Chart.Model.note_list)
        {
            var type = (NoteType) candidate.type;
            if (type != NoteType.DragHead && type != NoteType.CDragHead) continue;
            if (candidate.intro_time - 1f >= targetTime) continue;
            if (!visualPreviewOnly && State.IsJudged(candidate.id)) continue;
            if (!IsNoteActiveAtTime(candidate, Chart.Model, targetTime, judgmentOffset)) continue;
            EnsureDragChainLines(candidate, targetTime, ensuredLineHeads);
        }
    }

    /// <summary>RC-9: drop frozen head approach snapshots when leaving scrub/resync.</summary>
    private void ClearSpawnedHoldTimelineVisualState()
    {
        foreach (var note in ObjectPool.SpawnedNotes.Values)
        {
            if (note is HoldNote holdNote)
            {
                holdNote.ClearTimelineVisualState();
            }
        }
    }

    /// <summary>
    /// Cytoid Lab timeline preview: remove notes/lines that are not active at
    /// <paramref name="targetTime"/> (hold-seek RC-5). Full resync uses
    /// <see cref="ClearSpawnedObjects"/> instead.
    /// </summary>
    private void PruneInactiveSpawnedObjects(float targetTime)
    {
        var judgmentOffset = Context.Player.Settings.JudgmentOffset;

        var notesToClear = new List<Note>(ObjectPool.SpawnedNotes.Values);
        foreach (var note in notesToClear)
        {
            if (note == null || note.IsCollected) continue;
            if (IsNoteActiveAtTime(note.Model, Chart.Model, targetTime, judgmentOffset)) continue;
            note.ForceDespawnForResync();
        }

        // Drag lines are recreated by SpawnActiveNotesAtTime when needed.
        var dragLinesToClear = new List<DragLineElement>(ObjectPool.SpawnedDragLines.Values);
        foreach (var dragLine in dragLinesToClear)
        {
            if (dragLine != null && !dragLine.IsCollected)
            {
                dragLine.Collect();
            }
        }
    }

    private void ClearSpawnedObjects()
    {
        var notesToClear = new List<Note>(ObjectPool.SpawnedNotes.Values);
        foreach (var note in notesToClear)
        {
            if (note != null && !note.IsCollected)
            {
                note.ForceDespawnForResync();
            }
        }

        var dragLinesToClear = new List<DragLineElement>(ObjectPool.SpawnedDragLines.Values);
        foreach (var dragLine in dragLinesToClear)
        {
            if (dragLine != null && !dragLine.IsCollected)
            {
                dragLine.Collect();
            }
        }
    }

    private partial bool TryGetLiveSkipMusicOnCompletion(out bool skipMusicOnCompletion)
    {
        if (CytoidLabShell.IsActive)
        {
            skipMusicOnCompletion = Context.Player.Settings.SkipMusicOnCompletion;
            return true;
        }

        skipMusicOnCompletion = false;
        return false;
    }
}
