using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace Cytoid.Storyboard
{
    /// <summary>
    /// Cytoid Player storyboard rewind for timeline scrubbing (partial Storyboard extension).
    /// </summary>
    public partial class Storyboard
    {
        private List<Trigger> cytoidPlayerTriggerSnapshot;

        public async UniTask ResyncToTime(float targetTime)
        {
            EnsureTriggerSnapshot();
            ResetTriggersFromSnapshot();
            await Renderer.ResyncAsync();
            Renderer.OnGameUpdate(Game);
        }

        private void EnsureTriggerSnapshot()
        {
            if (cytoidPlayerTriggerSnapshot != null) return;
            cytoidPlayerTriggerSnapshot = Triggers.Select(CloneTriggerForPlayer).ToList();
        }

        private void ResetTriggersFromSnapshot()
        {
            if (cytoidPlayerTriggerSnapshot == null) return;
            Triggers.Clear();
            Triggers.AddRange(cytoidPlayerTriggerSnapshot.Select(CloneTriggerForPlayer));
        }

        private static Trigger CloneTriggerForPlayer(Trigger trigger)
        {
            return new Trigger
            {
                Type = trigger.Type,
                Uses = trigger.Uses,
                Notes = trigger.Notes != null ? new List<int>(trigger.Notes) : new List<int>(),
                Spawn = trigger.Spawn != null ? new List<string>(trigger.Spawn) : new List<string>(),
                Destroy = trigger.Destroy != null ? new List<string>(trigger.Destroy) : new List<string>(),
                Combo = trigger.Combo,
                Score = trigger.Score,
                CurrentUses = 0
            };
        }
    }
}
