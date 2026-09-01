using UnityEngine;

public partial class DragLineElement
{
    internal void ResyncVisualToTime(float time)
    {
        if (IsCollected || FromNoteModel.id <= 0) return;

        UpdateTransform();
        ApplyRendererEnabled();

        var introDuration = FromNoteModel.nextdraglinestoptime - FromNoteModel.nextdraglinestarttime;
        if (introDuration > 0)
        {
            introRatio = (FromNoteModel.nextdraglinestoptime - time) / introDuration;
        }
        else
        {
            introRatio = time < FromNoteModel.nextdraglinestarttime ? 1.0f : 0.0f;
        }

        var outroDuration = ToNoteModel.start_time - FromNoteModel.start_time;
        if (outroDuration > 0)
        {
            outroRatio = (time - FromNoteModel.start_time) / outroDuration;
        }
        else
        {
            outroRatio = time < FromNoteModel.start_time ? 0.0f : 1.0f;
        }

        if (introRatio > 0 && introRatio < 1)
        {
            spriteRenderer.material.SetFloat(MaterialEnd, 1.0f - introRatio);
        }
        else if (introRatio <= 0)
        {
            spriteRenderer.material.SetFloat(MaterialEnd, 1.0f);
        }
        else
        {
            spriteRenderer.material.SetFloat(MaterialEnd, 0.0f);
        }

        if (outroRatio > 0 && outroRatio < 1)
        {
            spriteRenderer.material.SetFloat(MaterialStart, outroRatio);
        }
        else if (outroRatio >= 1)
        {
            spriteRenderer.material.SetFloat(MaterialStart, 1.0f);
        }
        else
        {
            spriteRenderer.material.SetFloat(MaterialStart, 0.0f);
        }
    }
}
