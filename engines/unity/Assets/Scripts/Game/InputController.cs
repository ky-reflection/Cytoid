using System;
using System.Collections.Generic;
using UnityEngine;

public class InputController : MonoBehaviour
{
    /// <summary>
    /// Max note-to-note span (seconds) for a same-beat hit cluster (真双押 / 伪双).
    /// Span is measured against the earliest effectiveNoteTime in the cluster
    /// (no adjacent-gap chain expansion). Soft fallthrough to later clusters remains.
    /// </summary>
    public const float NoteClusterGapSeconds = 0.015f;

    public Game game;

    public readonly Dictionary<int, FlickNote> FlickingNotes = new Dictionary<int, FlickNote>(); // Finger index to note
    public readonly Dictionary<int, HoldNote> HoldingNotes = new Dictionary<int, HoldNote>(); // Finger index to note
    public readonly List<Note> TouchableDragNotes = new List<Note>(); // Drag head, Drag child, CDrag child
    public readonly List<HoldNote> TouchableHoldNotes = new List<HoldNote>(); // Hold, Long hold
    public readonly List<Note> TouchableNormalNotes = new List<Note>(); // Click, CDrag head, Flick (Hold/LongHold: FingerUpdate only)

    private readonly List<Note> hitCandidates = new List<Note>();
    private readonly List<Note> clusterScratch = new List<Note>();

    private void Awake()
    {
        game.onGameUpdate.AddListener(OnGameUpdate);
        game.onGamePaused.AddListener(OnGamePaused);
    }

    public void EnableInput()
    {
        GameTouchInput.FingerDown += OnFingerDown;
        GameTouchInput.FingerUpdate += OnFingerUpdate;
        GameTouchInput.FingerUp += OnFingerUp;
    }

    public void DisableInput()
    {
        GameTouchInput.FingerDown -= OnFingerDown;
        GameTouchInput.FingerUpdate -= OnFingerUpdate;
        GameTouchInput.FingerUp -= OnFingerUp;
    }

    public void OnNoteCollected(Note note)
    {
        if (note.Type == NoteType.Hold || note.Type == NoteType.LongHold)
        {
            // Since you only have 10 fingers, this doesn't need to be optimized
            HoldingNotes.RemoveAll(it => it == note);
        }
        if (note.Type == NoteType.Flick)
        {
            // Since you only have 10 fingers, this doesn't need to be optimized
            FlickingNotes.RemoveAll(it => it == note);
        }
    }

    public void OnGamePaused(Game game)
    {
        HoldingNotes.Values.ForEach(note =>
        {
            note.HoldingFingers.Clear();
        });
        HoldingNotes.Clear();
    }

    public void OnGameUpdate(Game game)
    {
        TouchableNormalNotes.Clear();
        TouchableDragNotes.Clear();
        TouchableHoldNotes.Clear();
        foreach (var id in game.SpawnedNotes.Keys)
        {
            var note = game.SpawnedNotes[id];
            if (!note.HasEmerged || note.IsCleared) continue;

            if (note.Type == NoteType.DragHead || note.Type == NoteType.DragChild || note.Type == NoteType.CDragChild)
            {
                TouchableDragNotes.Add(note);
            }
            else if (note.Type != NoteType.Hold && note.Type != NoteType.LongHold)
            {
                TouchableNormalNotes.Add(note);
            }

            if ((note.Type == NoteType.Hold || note.Type == NoteType.LongHold) &&
                !((HoldNote) note).IsHolding)
            {
                TouchableHoldNotes.Add((HoldNote) note);
            }
        }
    }

    protected virtual void OnFingerDown(GameFinger finger)
    {
        var pressedPosition = game.camera.orthographic
            ? game.camera.ScreenToWorldPoint(finger.ScreenPosition)
            : game.camera.ScreenToWorldPoint(new Vector3(finger.ScreenPosition.x, finger.ScreenPosition.y, 10));

        var collidedDrag = false;
        // Query drag notes first — note-time clusters, then x within cluster
        CollectColliding(TouchableDragNotes, pressedPosition, _ => true);
        foreach (var note in OrderHitCandidatesByNoteTimeClusters(pressedPosition.x))
        {
            if (!note.OnTouch(finger.ScreenPosition)) continue;
            collidedDrag = true;
            break;
        }

        CollectColliding(TouchableNormalNotes, pressedPosition, note =>
        {
            if (note is FlickNote flickNote)
            {
                return !FlickingNotes.ContainsKey(finger.Index) && !FlickingNotes.ContainsValue(flickNote);
            }

            if (collidedDrag && Math.Abs(note.TimeUntilStart) > note.Page.Duration / 8f) return false;
            if (note.Model.page_index > game.Chart.CurrentPageId &&
                note.Model.start_time - game.Time >
                game.Chart.Model.page_list[game.Chart.CurrentPageId].Duration * 0.5f)
            {
                return false;
            }

            return true;
        });

        foreach (var note in OrderHitCandidatesByNoteTimeClusters(pressedPosition.x))
        {
            if (note is FlickNote flickNote)
            {
                FlickingNotes.Add(finger.Index, flickNote);
                flickNote.StartFlicking(pressedPosition);
            }
            else
            {
                if (!note.OnTouch(finger.ScreenPosition)) continue;
            }

            return;
        }
    }

    protected virtual void OnFingerUpdate(GameFinger finger)
    {
        var pos = game.camera.orthographic
            ? game.camera.ScreenToWorldPoint(finger.ScreenPosition)
            : game.camera.ScreenToWorldPoint(new Vector3(finger.ScreenPosition.x, finger.ScreenPosition.y, 10));

        // Query flick note
        if (FlickingNotes.ContainsKey(finger.Index))
        {
            var flickingNote = FlickingNotes[finger.Index];
            var cleared = flickingNote.UpdateFingerPosition(pos);
            if (cleared) FlickingNotes.Remove(finger.Index);
        }

        // Query drag notes — note-time clusters, then x within cluster
        CollectColliding(TouchableDragNotes, pos, _ => true);
        foreach (var note in OrderHitCandidatesByNoteTimeClusters(pos.x))
        {
            if (!note.OnTouch(finger.ScreenPosition)) continue;
            break;
        }

        // If this is a new finger
        if (!HoldingNotes.ContainsKey(finger.Index))
        {
            var switchedToNewNote = false; // If the finger holds a new note

            // Query unheld hold notes — note-time clusters, then x within cluster
            CollectColliding(TouchableHoldNotes, pos, _ => true);
            foreach (var note in OrderHitCandidatesByNoteTimeClusters(pos.x))
            {
                var holdNote = (HoldNote) note;
                HoldingNotes.Add(finger.Index, holdNote);
                holdNote.UpdateFinger(finger.Index, true);
                switchedToNewNote = true;
                break;
            }

            // Query held hold notes (i.e. multiple fingers on the same hold note)
            if (!switchedToNewNote)
            {
                CollectColliding(HoldingNotes.Values, pos, _ => true);
                foreach (var note in OrderHitCandidatesByNoteTimeClusters(pos.x))
                {
                    var holdNote = (HoldNote) note;
                    HoldingNotes.Add(finger.Index, holdNote);
                    holdNote.UpdateFinger(finger.Index, true);
                    break;
                }
            }
        }
        else // The finger is already holding a note
        {
            var holdNote = HoldingNotes[finger.Index];

            if (holdNote.IsCleared) // If cleared <-- This should be impossible since the note should have called OnNoteCollected
            {
                throw new InvalidOperationException();
                // HoldingNotes.Remove(finger.Index);
            }
            else if (!holdNote.DoesCollide(pos)) // If holding elsewhere
            {
                holdNote.UpdateFinger(finger.Index, false);
                HoldingNotes.Remove(finger.Index);
            }
        }
    }

    protected virtual void OnFingerUp(GameFinger finger)
    {
        if (HoldingNotes.ContainsKey(finger.Index))
        {
            var holdNote = HoldingNotes[finger.Index];
            holdNote.UpdateFinger(finger.Index, false);
            HoldingNotes.Remove(finger.Index);
        }
        if (FlickingNotes.ContainsKey(finger.Index))
        {
            var pos = game.camera.orthographic
                ? game.camera.ScreenToWorldPoint(finger.ScreenPosition)
                : game.camera.ScreenToWorldPoint(new Vector3(finger.ScreenPosition.x, finger.ScreenPosition.y, 10));

            var flickingNote = FlickingNotes[finger.Index];
            flickingNote.UpdateFingerPosition(pos);
            FlickingNotes.Remove(finger.Index);
        }
    }

    private static float EffectiveNoteTime(Note note) =>
        note.Model.start_time + note.JudgmentOffset;

    private static float RenderedCenterX(Note note)
    {
        var collider = note.Renderer != null ? note.Renderer.GetCollider() : null;
        if (collider != null) return collider.bounds.center.x;
        return note.transform.position.x;
    }

    private void CollectColliding(IEnumerable<Note> notes, Vector2 worldPos, Func<Note, bool> predicate)
    {
        hitCandidates.Clear();
        foreach (var note in notes)
        {
            if (note == null || note.IsCleared) continue;
            if (!note.DoesCollide(worldPos)) continue;
            if (!predicate(note)) continue;
            hitCandidates.Add(note);
        }
    }

    /// <summary>
    /// Yield candidates in beat order: cluster by effectiveNoteTime span ≤
    /// <see cref="NoteClusterGapSeconds"/> (no chain expansion), process earlier
    /// clusters first; within a cluster prefer closer rendered center X, then time, then id.
    /// Soft fallthrough: caller continues when Accept fails.
    /// </summary>
    private IEnumerable<Note> OrderHitCandidatesByNoteTimeClusters(float touchWorldX)
    {
        if (hitCandidates.Count == 0) yield break;

        hitCandidates.Sort((a, b) =>
        {
            var cmp = EffectiveNoteTime(a).CompareTo(EffectiveNoteTime(b));
            if (cmp != 0) return cmp;
            return a.Model.id.CompareTo(b.Model.id);
        });

        var index = 0;
        while (index < hitCandidates.Count)
        {
            var clusterMinTime = EffectiveNoteTime(hitCandidates[index]);
            clusterScratch.Clear();
            clusterScratch.Add(hitCandidates[index]);
            index++;

            while (index < hitCandidates.Count &&
                   EffectiveNoteTime(hitCandidates[index]) - clusterMinTime <= NoteClusterGapSeconds)
            {
                clusterScratch.Add(hitCandidates[index]);
                index++;
            }

            clusterScratch.Sort((a, b) =>
            {
                var dxA = Math.Abs(touchWorldX - RenderedCenterX(a));
                var dxB = Math.Abs(touchWorldX - RenderedCenterX(b));
                var cmp = dxA.CompareTo(dxB);
                if (cmp != 0) return cmp;
                cmp = EffectiveNoteTime(a).CompareTo(EffectiveNoteTime(b));
                if (cmp != 0) return cmp;
                return a.Model.id.CompareTo(b.Model.id);
            });

            foreach (var note in clusterScratch)
            {
                yield return note;
            }
        }
    }

}
