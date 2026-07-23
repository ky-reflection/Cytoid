using System;
using UnityEngine;
using UnityEngine.UI;

public class AccuracyText : MonoBehaviour
{
    public Text text;
    public Game game;

    private double lastAccuracy = -1;
    private string lastFallback;

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
        if (!game.IsLoaded) return;

        if (game.State.Mode == GameMode.Calibration)
        {
            SetFallback("");
            return;
        }

        if (game.State.IsStarted && game.State.ClearCount > 0)
        {
            var accuracy = Math.Floor(game.State.Accuracy * 100 * 100) / 100;
            if (accuracy != lastAccuracy)
            {
                lastAccuracy = accuracy;
                lastFallback = null;
                text.text = accuracy.ToString("0.00") + "%";
            }
        }
        else
        {
            SetFallback("100.00%");
        }
    }

    private void SetFallback(string value)
    {
        if (lastFallback == value) return;
        lastFallback = value;
        lastAccuracy = -1;
        text.text = value;
    }
}
