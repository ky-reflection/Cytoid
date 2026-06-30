using UnityEngine;

public partial class DragLineElement
{
    internal void ResyncVisualToTime(float time)
    {
        if (IsCollected || FromNoteModel.id <= 0) return;

        UpdateTransform();

        var introDuration = FromNoteModel.nextdraglinestoptime - FromNoteModel.nextdraglinestarttime;
        if (introDuration > 0)
        {
            introRatio = (FromNoteModel.nextdraglinestoptime - time) / introDuration;
        }
        else
        {
            introRatio = time < FromNoteModel.nextdraglinestarttime ? 1.0f : 0.0f;
        }

        outroRatio = (time - FromNoteModel.start_time) / (ToNoteModel.start_time - FromNoteModel.start_time);

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
