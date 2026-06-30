using System;

/// <summary>
/// Cytoid Player score/judgement rewind for timeline scrubbing (partial GameState extension).
/// </summary>
public partial class GameState
{
    public void ResetToTime(Game game, float targetTime)
    {
        ClearCount = 0;
        Combo = 0;
        MaxCombo = 0;
        Score = 0;
        Accuracy = 0;
        accumulatedAccuracy = 0;
        NoteScoreMultiplier = 1.0;
        ShouldFail = false;
        IsCompleted = false;
        IsFailed = false;
        isFullScorePossible = true;
        Health = MaxHealth;

        foreach (var note in game.Chart.Model.note_list)
        {
            Judgements[note.id] = new NoteJudgement();
        }

        var judgmentOffset = Context.Player.Settings.JudgmentOffset;
        foreach (var note in game.Chart.Model.note_list)
        {
            if (!IsNoteFullyPassed(note, game.Chart.Model, targetTime, judgmentOffset)) continue;

            if (Mods.Contains(Mod.Auto))
            {
                JudgeFromModel(note, NoteGrade.Perfect, 0);
            }
        }
    }

    public void JudgeFromModel(ChartModel.Note model, NoteGrade grade, double error, double greatGradeWeight = 1.0)
    {
        if (IsCompleted || IsFailed) return;
        if (Judgements[model.id].IsJudged) return;

        ClearCount++;
        Judgements[model.id].Apply(it =>
        {
            it.IsJudged = true;
            it.Grade = grade;
            it.Error = error;
        });

        if (Mode == GameMode.Practice)
        {
            if (grade != NoteGrade.Perfect && grade != NoteGrade.Great) isFullScorePossible = false;
        }
        else if (grade != NoteGrade.Perfect)
        {
            isFullScorePossible = false;
        }

        var miss = grade == NoteGrade.Bad || grade == NoteGrade.Miss;
        if (miss) Combo = 0;
        else Combo++;
        if (Combo > MaxCombo) MaxCombo = Combo;

        if (Mode == GameMode.Tier)
        {
            var session = game.TierPlaySession;
            if (session != null)
            {
                if (miss) session.OnMiss();
                else session.OnNonMissHit();
            }
        }

        if (Mode != GameMode.Practice)
        {
            switch (grade)
            {
                case NoteGrade.Perfect:
                    NoteScoreMultiplier += 0.004D * noteScoreMultiplierFactor;
                    break;
                case NoteGrade.Great:
                    NoteScoreMultiplier += 0.002D * noteScoreMultiplierFactor;
                    break;
                case NoteGrade.Good:
                    NoteScoreMultiplier += 0.001D * noteScoreMultiplierFactor;
                    break;
                case NoteGrade.Bad:
                    NoteScoreMultiplier -= 0.025D * noteScoreMultiplierFactor;
                    break;
                case NoteGrade.Miss:
                    NoteScoreMultiplier -= 0.05D * noteScoreMultiplierFactor;
                    break;
            }

            if (NoteScoreMultiplier > 1) NoteScoreMultiplier = 1;
            if (NoteScoreMultiplier < 0) NoteScoreMultiplier = 0;
        }

        if (Mode == GameMode.Practice)
        {
            Score += 900000.0 / NoteCount * grade.GetScoreWeight(false) +
                     100000.0 / (NoteCount * (long) (NoteCount + 1) / 2.0) * Combo;
        }
        else
        {
            var maxNoteScore = 1000000.0 / NoteCount;
            double noteScore;
            if (grade == NoteGrade.Great)
            {
                noteScore = maxNoteScore * (NoteGrade.Great.GetScoreWeight(true) +
                                            (NoteGrade.Perfect.GetScoreWeight(true) -
                                             NoteGrade.Great.GetScoreWeight(true)) *
                                            greatGradeWeight);
            }
            else
            {
                noteScore = maxNoteScore * grade.GetScoreWeight(true);
            }

            noteScore *= NoteScoreMultiplier;
            Score += noteScore;
        }

        if (Score > 999500)
        {
            if (ClearCount == NoteCount && isFullScorePossible)
            {
                Score = 1000000;
            }
        }

        if (Score > 1000000) Score = 1000000;
        if (Score == 1000000 && !isFullScorePossible) Score = 999999;

        if (Mode == GameMode.Practice || grade != NoteGrade.Great)
        {
            accumulatedAccuracy += 1.0 * grade.GetAccuracyWeight();
        }
        else
        {
            accumulatedAccuracy += 1.0 * (NoteGrade.Great.GetAccuracyWeight() +
                                          (NoteGrade.Perfect.GetAccuracyWeight() -
                                           NoteGrade.Great.GetAccuracyWeight()) *
                                          greatGradeWeight);
        }

        Accuracy = accumulatedAccuracy / ClearCount;

        if (UseHealthSystem)
        {
            var mods = Mods.Contains(Mod.ExHard) ? exHardHpMods : hardHpMods;
            if (Mode == GameMode.Tier) mods = tierHpMods;

            var mod = mods
                .Select[(NoteType) model.type]
                .Select[Mode == GameMode.Practice ? unrankedGradingIndex[grade] : rankedGradingIndex[grade]];

            double change = 0;
            switch (mod.Type)
            {
                case HpModType.Absolute:
                    change = mod.Value;
                    break;
                case HpModType.Percentage:
                    change = mod.Value / 100f * MaxHealth;
                    break;
                case HpModType.DivideByNoteCount:
                    change = mod.Value / NoteCount / 100f * MaxHealth;
                    break;
            }

            if (change < 0 && mod.UseHealthBuffer)
            {
                double a;
                if (HealthPercentage > 0.3) a = 1;
                else a = 0.25 + 2.5 * HealthPercentage;
                change *= a;
            }

            Health += change;
            Health = Math.Min(Math.Max(Health, 0), MaxHealth);
            if (Health <= 0) ShouldFail = true;
        }

        if (
            Mods.Contains(Mod.AP) && grade != NoteGrade.Perfect
            ||
            Mods.Contains(Mod.FC) && (grade == NoteGrade.Bad || grade == NoteGrade.Miss)
        )
        {
            ShouldFail = true;
        }
    }

    private static bool IsNoteFullyPassed(ChartModel.Note note, ChartModel chart, float targetTime, float judgmentOffset)
    {
        var type = (NoteType) note.type;
        float endTime;
        float missThresh;

        if (type == NoteType.DragHead || type == NoteType.CDragHead)
        {
            endTime = note.GetDragEndNote(chart).end_time;
            missThresh = (type == NoteType.CDragHead ? NoteType.CDragChild : NoteType.DragChild)
                .GetDefaultMissThreshold();
        }
        else
        {
            endTime = note.end_time;
            missThresh = type.GetDefaultMissThreshold();
        }

        return targetTime > endTime + missThresh + judgmentOffset;
    }
}
