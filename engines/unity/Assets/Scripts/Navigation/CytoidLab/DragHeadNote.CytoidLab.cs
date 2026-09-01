using UnityEngine;

public partial class DragHeadNote
{
    internal override void ForceDespawnForResync()
    {
        IsCollecting = false;
        FromNoteModel = default;
        ToNoteModel = default;
        StartToNoteModel = default;
        EndNoteModel = default;
        OriginalPosition = default;
        hasFromNote = default;
        fromNote = default;
        hasToNote = default;
        toNote = default;
        base.ForceDespawnForResync();
    }

    /// <summary>
    /// After a timeline resync, jump the drag head to the correct chain segment and position.
    /// </summary>
    internal void FastForwardToTime(float time)
    {
        if (time < Model.start_time) return;

        while (ToNoteModel != EndNoteModel && time >= ToNoteModel.start_time)
        {
            FromNoteModel = ToNoteModel;
            ToNoteModel = Chart.note_map[FromNoteModel.next_id];
        }

        hasFromNote = false;
        hasToNote = false;
        fromNote = null;
        toNote = null;

        transform.localEulerAngles = ChartModel.Note.RotationBetweenPositions(
            FromNoteModel.CalculatePosition(Game.Chart),
            ToNoteModel.CalculatePosition(Game.Chart));

        if (ToNoteModel == EndNoteModel && time >= ToNoteModel.start_time)
        {
            transform.localPosition = ToNoteModel.CalculatePosition(Game.Chart);
            OriginalPosition = transform.localPosition;
            return;
        }

        var duration = ToNoteModel.start_time - FromNoteModel.start_time;
        if (duration <= 0) return;

        var t = (time - FromNoteModel.start_time) / duration;
        transform.localPosition = Vector3.Lerp(
            FromNoteModel.CalculatePosition(Game.Chart),
            ToNoteModel.CalculatePosition(Game.Chart),
            Mathf.Clamp01(t));
        OriginalPosition = transform.localPosition;
    }
}
