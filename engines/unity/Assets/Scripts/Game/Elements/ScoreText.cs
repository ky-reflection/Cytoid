using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ScoreText : MonoBehaviour
{
    public Text text;
    public Game game;
    public float punchScale = 1.2f;
    public float punchDuration = 0.2f;
    public Ease ease = Ease.OutCubic;

    private double lastScore = double.NaN;
    private Sequence lastSequence;

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
            if (!double.IsNaN(lastScore) || text.text != "")
            {
                lastScore = double.NaN;
                text.text = "";
            }
            return;
        }

        if (game.State.IsStarted)
        {
            var score = game.State.Score;
            if (double.IsNaN(lastScore) || score != lastScore)
            {
                if (!double.IsNaN(lastScore) && score != lastScore)
                {
                    lastSequence?.Kill();
                    transform.localScale = new Vector3((punchScale + 1) / 2f, punchScale, 1);
                    lastSequence = DOTween.Sequence()
                        .Append(transform.DOScale(1, punchDuration).SetEase(ease));
                }

                lastScore = score;
                text.text = ((int) lastScore).ToString("D6");
            }
        }
        else if (text.text != "000000")
        {
            lastScore = double.NaN;
            text.text = "000000";
        }
    }
}
