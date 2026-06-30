using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Cytoid Player timeline scrubbing and playfield resync (partial Game extension).
/// </summary>
public partial class Game
{
    public bool UseInstantPauseResume;

    public void PreviewTimeline(float targetTime)
    {
        if (!IsLoaded || State == null || State.IsCompleted || State.IsFailed) return;

        targetTime = Mathf.Clamp(targetTime, 0, MusicLength);
        Music.Stop();
        Music.PlaybackTime = targetTime;

        var nowDspTime = AudioSettings.dspTime;
        MusicStartedTimestamp = nowDspTime - targetTime;
        Time = targetTime;
        MusicProgress = MusicLength > 0 ? Time / MusicLength : 0;
        ChartProgress = ChartLength > 0 ? Time / ChartLength : 0;
        Music.Play(AudioTrackIndex.Reserved1);
    }

    public void Seek(float targetTime)
    {
        ResyncPlayfieldToTime(targetTime, State != null && State.IsPlaying).Forget();
    }

    public async UniTask ResyncPlayfieldToTime(float targetTime, bool resumePlaying = false)
    {
        if (!IsLoaded || State == null || State.IsCompleted || State.IsFailed) return;

        targetTime = Mathf.Clamp(targetTime, 0, MusicLength);
        var wasPlaying = resumePlaying;

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
                Debug.LogError($"[CytoidPlayer] Storyboard resync failed: {e}");
            }
        }

        Music.Play(AudioTrackIndex.Reserved1);
        if (wasPlaying)
        {
            GameStartedOrResumedTimestamp = UnityEngine.Time.realtimeSinceStartup;
            AudioListener.pause = false;
            State.IsPlaying = true;
        }

        onGameUpdate.Invoke(this);
        onGameLateUpdate.Invoke(this);
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

        var noteList = Chart.Model.note_list;
        Chart.CurrentNoteId = 0;
        while (Chart.CurrentNoteId < noteList.Count && noteList[Chart.CurrentNoteId].intro_time - 1f < targetTime)
        {
            Chart.CurrentNoteId++;
        }
    }

    private void SpawnActiveNotesAtTime(float targetTime)
    {
        var noteList = Chart.Model.note_list;
        var judgmentOffset = Context.Player.Settings.JudgmentOffset;
        var spawnedHeads = new HashSet<int>();

        foreach (var note in noteList)
        {
            var type = (NoteType) note.type;
            if (type == NoteType.DragHead || type == NoteType.CDragHead)
            {
                if (!IsNoteActiveAtTime(note, targetTime, judgmentOffset) || State.IsJudged(note.id)) continue;
                if (!spawnedHeads.Add(note.id)) continue;
                SpawnDragChainFromHead(note, targetTime);
                continue;
            }

            if (type == NoteType.DragChild || type == NoteType.CDragChild) continue;

            if (!IsNoteActiveAtTime(note, targetTime, judgmentOffset) || State.IsJudged(note.id)) continue;
            ObjectPool.SpawnNote(note);
        }
    }

    private static bool IsNoteActiveAtTime(ChartModel.Note note, float targetTime, float judgmentOffset)
    {
        if (note.intro_time - 1f >= targetTime) return false;
        var missThresh = ((NoteType) note.type).GetDefaultMissThreshold();
        return targetTime <= note.end_time + missThresh + judgmentOffset;
    }

    private void SpawnDragChainFromHead(ChartModel.Note head, float targetTime)
    {
        var id = head.id;
        while (id > 0)
        {
            var note = Chart.Model.note_map[id];
            if (note.intro_time - 1f >= targetTime) break;
            if (State.IsJudged(note.id)) break;

            if (note.next_id > 0 && Chart.Model.note_map.ContainsKey(note.next_id))
            {
                ObjectPool.SpawnDragLine(note, Chart.Model.note_map[note.next_id]);
            }

            ObjectPool.SpawnNote(note);
            id = note.next_id;
        }
    }

    private void ClearSpawnedObjects()
    {
        var notesToClear = new List<Note>(ObjectPool.SpawnedNotes.Values);
        foreach (var note in notesToClear)
        {
            if (note != null && !note.IsCollected) note.Collect();
        }

        var dragLinesToClear = new List<DragLineElement>(ObjectPool.SpawnedDragLines.Values);
        foreach (var dragLine in dragLinesToClear)
        {
            if (dragLine != null) ObjectPool.CollectDragLine(dragLine);
        }
    }
}
