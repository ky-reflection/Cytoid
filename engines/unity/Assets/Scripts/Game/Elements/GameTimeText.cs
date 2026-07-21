using UnityEngine;
using UnityEngine.UI;

public class GameTimeText : MonoBehaviour
{
    public Text text;
    public Game game;

    private float lastTime = -1f;

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
        if (roundedTime == lastTime) return;
        lastTime = roundedTime;
        text.text = $"Time: {game.Time:F3}";
    }
}
