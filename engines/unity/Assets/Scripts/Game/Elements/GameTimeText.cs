using UnityEngine;
using UnityEngine.UI;

public class GameTimeText : MonoBehaviour
{
    public Text text;
    public Game game;

    private float lastTime = float.NaN;

    private void OnValidate()
    {
        this.AutoFill(ref text);
    }

    private void Awake()
    {
        this.AutoFill(ref text);
    }

    private void Update()
    {
        var roundedTime = Mathf.Round(game.Time * 10f) / 10f;
        if (!float.IsNaN(lastTime) && roundedTime == lastTime) return;
        lastTime = roundedTime;
        text.text = $"Time: {game.Time:F3}";
    }
}
