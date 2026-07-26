using System;
using System.Collections.Generic;
using UnityEngine;

public class InputController : MonoBehaviour
{
    /// <summary>
    /// Timing cluster window (seconds) for overlapping hitboxes (伪双 / tight 纵连).
    /// Aligned with the Perfect judgment window (±40ms). When any colliding candidate
    /// lies inside this band, only those in-band notes compete (no fallthrough to a
    /// farther overlapping note on the same touch). Among competitors the smallest
    /// |Δt| wins (id as tie-break). If none are in-band, the same |Δt| rule applies
    /// over the full colliding set so wider grades still work.
    /// </summary>
    public const float HitTimingClusterWindowSeconds = 0.040f;

    public Game game;

    public readonly Dictionary<int, FlickNote> FlickingNotes = new Dictionary<int, FlickNote>(); // Finger index to note
    public readonly Dictionary<int, HoldNote> HoldingNotes = new Dictionary<int, HoldNote>(); // Finger index to note
    public readonly List<Note> TouchableDragNotes = new List<Note>(); // Drag head, Drag child, CDrag child
    public readonly List<HoldNote> TouchableHoldNotes = new List<HoldNote>(); // Hold, Long hold
    public readonly List<Note> TouchableNormalNotes = new List<Note>(); // Click, CDrag head, Flick (Hold/LongHold: FingerUpdate only)

    private readonly List<Note> hitCandidates = new List<Note>();

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
        // Query drag notes first — among overlaps, prefer smallest |Δt|
        CollectColliding(TouchableDragNotes, pressedPosition, note => true);
        foreach (var note in OrderHitCandidatesByAbsDelta())
        {
            if (!note.OnTouch(finger.ScreenPosition)) continue;
            collidedDrag = true;
            break;
        }

        CollectColliding(TouchableNormalNotes, pressedPosition, note =>
        {
            if (note is FlickNote)
            {
                return !FlickingNotes.ContainsKey(finger.Index) && !FlickingNotes.ContainsValue((FlickNote) note);
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

        foreach (var note in OrderHitCandidatesByAbsDelta())
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

        // Query drag notes — among overlaps, prefer smallest |Δt|
        CollectColliding(TouchableDragNotes, pos, note => true);
        foreach (var note in OrderHitCandidatesByAbsDelta())
        {
            if (!note.OnTouch(finger.ScreenPosition)) continue;
            break;
        }

        // If this is a new finger
        if (!HoldingNotes.ContainsKey(finger.Index))
        {
            var switchedToNewNote = false; // If the finger holds a new note

            // Query unheld hold notes — among overlaps, prefer smallest |Δt|
            CollectColliding(TouchableHoldNotes, pos, note => true);
            foreach (var note in OrderHitCandidatesByAbsDelta())
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
                CollectColliding(HoldingNotes.Values, pos, note => true);
                foreach (var note in OrderHitCandidatesByAbsDelta())
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

    private static float AbsJudgmentDelta(Note note) =>
        Math.Abs(note.TimeUntilStart + note.JudgmentOffset);

    private void CollectColliding(IEnumerable<Note> notes, Vector2 worldPos, Func<Note, bool> predicate)
    {
        hitCandidates.Clear();
        foreach (var note in notes)
        {
            if (note == null || !note.DoesCollide(worldPos)) continue;
            if (!predicate(note)) continue;
            hitCandidates.Add(note);
        }
    }

    /// <summary>
    /// Yield hit candidates ordered by ascending |Δt|, then note id.
    /// If any candidate is inside <see cref="HitTimingClusterWindowSeconds"/>,
    /// only the in-band subset is yielded (伪双 / 纵连 protection).
    /// </summary>
    private IEnumerable<Note> OrderHitCandidatesByAbsDelta()
    {
        if (hitCandidates.Count == 0) yield break;

        var hasInBand = false;
        for (var i = 0; i < hitCandidates.Count; i++)
        {
            if (AbsJudgmentDelta(hitCandidates[i]) <= HitTimingClusterWindowSeconds)
            {
                hasInBand = true;
                break;
            }
        }

        hitCandidates.Sort((a, b) =>
        {
            var cmp = AbsJudgmentDelta(a).CompareTo(AbsJudgmentDelta(b));
            if (cmp != 0) return cmp;
            return a.Model.id.CompareTo(b.Model.id);
        });

        foreach (var note in hitCandidates)
        {
            // Sorted by |Δt|: once past the band, remaining notes are out-of-band.
            if (hasInBand && AbsJudgmentDelta(note) > HitTimingClusterWindowSeconds) yield break;
            yield return note;
        }
    }

}
