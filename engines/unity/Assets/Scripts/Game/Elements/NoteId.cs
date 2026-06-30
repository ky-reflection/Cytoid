using System;
using TMPro;
using UnityEngine;

public class NoteId : MonoBehaviour
{

    public bool Visible
    {
        get => visible;
        set
        {
            visible = value;
            text.color = text.color.WithAlpha(visible ? 1 : 0);
        }
    }

    public TextMeshPro text;
    private bool visible;

    public void SetModel(ChartModel.Note note)
    {
        text.text = note.id.ToString();

        var scale = 0.1f;
        var color = Color.white;
        switch (note.type)
        {
            case (int) NoteType.DragHead:
            case (int) NoteType.CDragHead:
                scale = 0.08f;
                color = Color.black;
                break;
            case (int) NoteType.DragChild:
            case (int) NoteType.CDragChild:
                scale = 0.06f;
                color = Color.black;
                break;
            case (int) NoteType.Flick:
                color = Color.black;
                break;
        }

        transform.localScale = new Vector3(scale, scale, scale);
        text.color = color.WithAlpha(visible ? 1 : 0);
    }

}