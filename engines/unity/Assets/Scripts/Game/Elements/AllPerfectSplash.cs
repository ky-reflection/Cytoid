public class AllPerfectSplash : CleanTitleTransitionElement
{
    public Game game;
    
    protected override void Awake()
    {
        base.Awake();
        game.onGameCompleted.AddListener(_ => OnGameComplete());
    }

    public void OnGameComplete()
    {
        if (CytoidLabShell.IsActive) return;

        if (game.State.Mode != GameMode.Calibration && game.State.Score == 1000000 && !game.EditorImmediatelyComplete)
        {
            game.BeforeExitTasks.Add(Animate());
            Context.AudioManager.Get("LevelMax").Play();
        }
    }
}