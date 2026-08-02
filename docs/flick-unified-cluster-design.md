# Click / Flick Unified Hit-Cluster Design

> Status: design only; no runtime implementation in this document.
>
> Target: `InputController` FingerDown select arbitration.
>
> Primary cases: Click + Flick and Flick + Flick at the same or nearby note time.

## 1. Background

The current select path applies note-time clustering and rendered-X ordering to
Click-like candidates, while Flick remains a `SpawnedNotes`/note-ID-order bind.
This leaves two related failure modes when hitboxes overlap:

- a Click and Flick at the same or nearby time can be selected by list order
  instead of the finger's spatial intent;
- two nearby Flicks can bind to the wrong fingers, and asymmetric hitbox overlap
  can leave one Flick unbound even when two fingers are present.

Flick also has a separate lifecycle issue: after its displacement threshold is
crossed, `UpdateFingerPosition` currently treats the attempt as handled even if
`TryClear` returns false. A too-early gesture can therefore lose its binding
without clearing the note.

This design unifies candidate clustering and spatial arbitration. It does not
turn Flick into an immediate Click judgment: Click clears on FingerDown, while
Flick still completes after a later movement or FingerUp event.

## 2. Goals

1. Put Click-like and Flick candidates into the same note-time clusters.
2. Use rendered horizontal distance, not note ID, as the primary in-cluster
   selector.
3. Cover exact-tick and pseudo-simultaneous Click + Flick and Flick + Flick.
4. Preserve monotonic note-time selection outside the cluster window.
5. Preserve the invariant that one FingerDown allocates at most one select note.
6. Make same-frame multi-finger allocation skip notes already cleared or
   reserved by an earlier finger.
7. Stop a failed/too-early Flick attempt from reporting a false clear and
   silently dropping its binding.
8. Keep judgment windows, grades, scoring, Drag ordering, and DragCoHit
   unchanged.

## 3. Non-goals

- Do not merge Drag, CDrag child, or DropDrag into select clustering.
- Do not change Click or Flick Perfect/Great windows.
- Do not change the Flick displacement threshold or add velocity detection.
- Do not implement rotated/directional Flick recognition in this change.
- Do not allow one finger to clear both a Click and Flick from one FingerDown.
- Do not batch all touches into a global minimum-cost assignment in the first
  implementation.
- Do not defer every Click to infer whether the finger will later Flick.
- Do not add a player-facing cluster-window setting.

## 4. Candidate scope

The unified select candidate list contains:

| Kind | Note types | Completion |
|------|------------|------------|
| Click-like | Click, CDrag head, DropClick | Immediate `OnTouch` / `TryClear` |
| Flick | Flick | Reserve on FingerDown; clear on movement or FingerUp |
| Hold | Unheld Hold / LongHold already admitted by the current select path | Bind on FingerDown; behavior otherwise unchanged |

Hold remains in the shared data path to avoid regressing the current
Click/Hold arbitration. The behavioral target and acceptance tests of this
design remain Click + Flick and Flick + Flick.

The following remain in the continuous-contact Drag bucket and never enter the
unified cluster:

- Drag head and Drag child;
- CDrag child;
- DropDrag.

## 5. Candidate model

Each candidate records or computes:

```text
note
kind
effectiveNoteTime = note.Model.start_time + note.JudgmentOffset
renderedCenterX
```

`renderedCenterX` is resolved from `note.Renderer.GetCollider().bounds.center.x`
when a collider exists, with `note.transform.position.x` as the fallback. Raw
chart `Model.x` must not be used because it can differ from the rendered or
storyboard-adjusted position.

Before reading `Model`, the collector must reject candidates that are:

- null;
- collected;
- already cleared;
- not emerged;
- not colliding with the FingerDown world position;
- blocked by the current DragCoHit rule;
- blocked by the existing cross-page early-selection rule.

Type-specific filters are then applied:

- Flick: the finger is not already bound to a Flick, and the note is not
  reserved by another finger;
- Hold: the note is not already holding, and the finger is not bound to a Hold.

## 6. Time clustering

Use the existing constant:

```text
NoteClusterGapSeconds = 0.015 seconds
```

Candidates are first sorted by:

```text
effectiveNoteTime ascending
note ID ascending
```

Starting at the earliest remaining candidate, build a cluster whose total span
is at most 15 ms:

```text
candidate.effectiveNoteTime - clusterMinTime <= 15 ms
```

The cluster is anchored at its minimum time. Adjacent-gap chain expansion is
forbidden. For example:

```text
0 ms, 10 ms, 20 ms
```

must become:

```text
[0 ms, 10 ms]
[20 ms]
```

and not one 20 ms-wide cluster.

Clusters are processed from earliest to latest. A later cluster is considered
only if every candidate in the earlier cluster rejects the FingerDown. Spatial
proximity must never jump over a viable earlier cluster.

## 7. In-cluster ordering

Candidates within one cluster are sorted by:

```text
abs(touchWorldX - renderedCenterX)
effectiveNoteTime
candidate kind priority
note ID
```

The initial kind priority is:

```text
Click / CDrag head / DropClick = 0
Flick                         = 1
Hold / LongHold               = 2
```

Distance precedes kind. The kind priority is used only when position and time
cannot distinguish candidates.

Consequences:

- nearby same-cluster Click + Flick selects the note closest to the finger;
- nearby same-cluster Flick + Flick selects the closest unreserved Flick;
- exact-position/time Click + Flick deterministically selects Click first;
- exact-position/time Flick + Flick falls back to note ID;
- a second finger re-runs the same ordering after skipping the note allocated
  to the first finger.

## 8. FingerDown arbitration

Drag settlement remains first and continues to produce the existing optional
`acceptedDrag` used by DragCoHit.

The select phase then collects all unified candidates before accepting any of
them. Flick is no longer a list-order boundary that flushes the preceding Click
segment.

Conceptual flow:

```text
acceptedDrag = tryClearFirstCollidingDrag()
candidates = collectUnifiedSelectCandidates(finger, acceptedDrag)

for cluster in clustersOrderedByEffectiveNoteTime(candidates):
    for candidate in orderClusterByRenderedDistance(cluster):
        if candidate became invalid or reserved:
            continue

        if candidate is Click-like:
            if candidate.OnTouch(finger.screenPosition):
                return Consumed
            continue

        if candidate is Flick:
            if reserveFlick(finger, candidate):
                candidate.StartFlicking(finger.worldPosition)
                return Consumed
            continue

        if candidate is Hold:
            if bindHold(finger, candidate):
                candidate.UpdateFinger(finger.index, true)
                return Consumed
            continue

return NotConsumed
```

One accepted candidate always ends select processing for that FingerDown.

## 9. Same-frame multi-finger behavior

The first implementation keeps the current event-by-event FingerDown dispatch.
It does not wait for every Began touch in the frame.

After the first finger accepts a candidate:

- Click-like notes become `IsCleared` immediately;
- Flick notes are inserted into `FlickingNotes` immediately;
- Hold notes are inserted into `HoldingNotes` immediately.

The second finger uses the per-frame candidate snapshot but revalidates every
candidate before selection. It therefore skips the first finger's cleared or
reserved note and selects the nearest remaining candidate.

This is a greedy assignment and intentionally not a global optimum. It fixes
the common two-note/two-finger asymmetric-overlap case, but three-way overlap
can still depend on finger dispatch order.

## 10. Flick lifecycle result

Replace the ambiguous `UpdateFingerPosition` boolean meaning with an explicit
result or equivalent semantics:

```text
Pending
Cleared
Released
```

Movement handling:

```text
displacement below threshold
    -> Pending; keep binding

displacement reaches threshold and TryClear succeeds
    -> Cleared; remove binding

displacement reaches threshold and TryClear fails
    -> Pending; keep binding
    -> reset Flick start position and start time
    -> require another threshold-crossing movement

FingerUp or Canceled
    -> perform one final clear attempt using the release position
    -> remove binding regardless of result
```

Resetting the start point after a too-early failed attempt prevents a player
from flicking far in advance and holding still until the note automatically
enters its judgment window.

`FlickingStartTime` should either participate in the explicit session state or
be removed later as dead state; that cleanup is not required by this design.

## 11. Behavior matrix

| Combination | One finger | Two fingers |
|-------------|------------|-------------|
| Click + Click, same cluster | Closest viable Click clears | Each finger clears the nearest remaining Click |
| Click + Flick, spatially separated | Closest type wins; exactly one note allocated | Each finger selects the nearest remaining note independent of note ID |
| Click + Flick, exact overlap | Click wins the deterministic tie | First finger gets Click; second gets Flick, subject to dispatch order and later gesture |
| Flick + Flick, spatially separated | Closest Flick binds | Each finger binds the nearest remaining Flick |
| Flick + Flick, exact overlap | Lower note ID binds | Reservation lets the second finger bind the other Flick |
| Candidate gaps at 15 ms | Same cluster | Same cluster |
| Candidate gaps above 15 ms | Earlier viable cluster wins | Each finger re-evaluates the remaining earliest viable cluster |

## 12. Known limitation: exact-overlap Click + Flick intent

At FingerDown time the engine knows the touch position but not whether that
finger will later move enough to become a Flick. For an exact-position/time
Click + Flick pair, immediate arbitration cannot reliably assign the Flick to
the finger that will eventually swipe.

The first implementation deliberately uses a deterministic Click-first tie and
does not delay Click judgment. A complete solution would require an ambiguous
mixed-cluster session that waits briefly for movement and judges Click using a
captured FingerDown timestamp. That would require a judgment-time override in
`Note.TryClear`, add audiovisual latency, and materially widen the change. It is
reserved for a later phase if real-chart/device tests show the exact-overlap
case remains significant.

## 13. Compatibility and expected behavior changes

Unchanged:

- Click and Flick grade windows;
- Early/Late and score calculation;
- Drag-first settlement and the 30 ms DragCoHit window;
- one FingerDown allocating at most one select note;
- Auto, AutoFlick, AutoHold, and practice-mode grade rules;
- the 15 ms cluster span.

Intentionally changed:

- Flick no longer binds solely by `SpawnedNotes`/note-ID order;
- Click + Flick and Flick + Flick within one cluster use rendered distance;
- a cleared/reserved same-frame candidate cannot consume another finger;
- a failed early Flick threshold crossing no longer reports a false clear.

The selected note ID can change on overlapping charts even when the final grade
and score do not. Play-event telemetry and regression fixtures must expect the
new spatially selected ID.

## 14. Test matrix

### Cluster boundaries

- Click + Flick at 0, 10, 15, 16, and 30 ms note gaps.
- Flick + Flick at 0, 10, 15, 16, and 30 ms note gaps.
- 0/10/20 ms chain verifies that the 20 ms candidate is a second cluster.
- JudgmentOffset is included in `effectiveNoteTime` consistently.

### Spatial arbitration

- note IDs ordered left-to-right and right-to-left;
- finger closer to the higher-ID candidate;
- full hitbox overlap;
- partial/asymmetric overlap where one finger collides with both notes and the
  other finger collides with only one;
- rendered/collider center differs from raw chart X.

### Type combinations

- Click + Flick in both note-ID orders;
- Flick + Flick in both note-ID orders;
- Click + Hold regression;
- Flick + Hold tie behavior;
- CDrag head + Flick and DropClick + Flick.

### Multi-finger and lifecycle

- two FingerDown events in both dispatch orders;
- same-frame cleared Click snapshot is skipped by the second finger;
- Flick reserved by one finger falls through to another Flick;
- one finger cannot allocate two notes;
- FingerUp, Canceled, pause, and note collection release reservations;
- a too-early threshold crossing does not clear or release the Flick;
- a later deliberate movement inside the judgment window clears it;
- a failed early movement followed only by stationary input does not auto-clear.

### Regression

- Click-only cluster ordering remains unchanged;
- no-overlap Flick behavior remains unchanged except false-success handling;
- Drag + Click and Drag + Flick at 29, 30, and 31 ms;
- ranked and practice Flick windows;
- 30, 60, and 120 Hz device sampling.

## 15. Acceptance criteria

1. Two spatially distinguishable same-cluster notes and two corresponding
   fingers produce two allocations independent of note-ID ordering.
2. One finger allocates exactly one closest viable candidate.
3. A candidate at more than 15 ms from the cluster minimum cannot be selected
   ahead of a viable earlier cluster.
4. Click + Flick and Flick + Flick no longer use note ID as the primary
   selector when rendered positions differ.
5. A second same-frame finger cannot bind or clear an already allocated note.
6. A Flick binding is removed on movement only after a real clear, or on
   release/cancel.
7. A too-early Flick attempt cannot become an automatic clear while stationary.
8. Click-only judgments, grade windows, scoring, and DragCoHit remain unchanged.

## 16. Implementation stages

1. Introduce a unified candidate representation and collector.
2. Generalize the existing 15 ms cluster sorter to include Flick.
3. Replace Flick list-boundary scanning with candidate acceptance/reservation.
4. Harden same-frame revalidation for cleared, collected, and reserved notes.
5. Replace Flick movement's false-success boolean semantics.
6. Add focused EditMode logic tests for clustering, ordering, reservation, and
   lifecycle state.
7. Run real-device multi-touch checks for Click + Flick and Flick + Flick before
   enabling the behavior in release builds.
