using System;
using UnityEngine;
using UnityEngine.UI;

public class AccuracyText : MonoBehaviour
{
    public Text text;
    public Game game;

    private double lastAccuracy = -1;

    private void OnValidate()
    {
        this.AutoFill(ref text);
    }

    protected void Awake()
    {
        this.AutoFill(ref text);
        text.text = "";
    }

    protected void LateUpdate()
    {
        if (game.IsLoaded)
        {
            if (game.State.Mode == GameMode.Calibration)
            {
                text.text = "";
            }
            else
            {
                if (game.State.IsStarted && game.State.ClearCount > 0)
                {
                    var accuracy = Math.Floor(game.State.Accuracy * 100 * 100) / 100;
                    if (accuracy != lastAccuracy)
                    {
                        lastAccuracy = accuracy;
                        text.text = accuracy.ToString("0.00") + "%";
                    }
                }
                else
                {
                    text.text = "100.00%";
                }
            }
        }
    }
}