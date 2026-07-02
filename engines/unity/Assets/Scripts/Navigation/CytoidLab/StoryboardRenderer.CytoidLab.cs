using System;
using System.Collections.Generic;
using System.Linq;
using Cytoid.Storyboard.Controllers;
using Cytoid.Storyboard.Lines;
using Cytoid.Storyboard.Notes;
using Cytoid.Storyboard.Sprites;
using Cytoid.Storyboard.Texts;
using Cytoid.Storyboard.Videos;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LineRenderer = Cytoid.Storyboard.Sprites.LineRenderer;
using SpriteRenderer = Cytoid.Storyboard.Sprites.SpriteRenderer;

namespace Cytoid.Storyboard
{
    /// <summary>
    /// Cytoid Lab storyboard renderer rebuild for timeline scrubbing (partial extension).
    /// </summary>
    public partial class StoryboardRenderer
    {
        public async UniTask ResyncAsync()
        {
            foreach (var renderer in ComponentRenderers.Values.ToList())
            {
                renderer.Dispose();
            }

            ComponentRenderers.Clear();
            foreach (var list in TypedComponentRenderers.Values)
            {
                list.Clear();
            }

            SpritePathRefCount.Clear();
            ResetRuntimeStateForSeek();

            var timer = new BenchmarkTimer("StoryboardRenderer player resync");
            bool Predicate<TO>(TO obj) where TO : Object => !obj.IsManuallySpawned();
            await SpawnObjects<NoteController, NoteControllerState, NoteControllerRenderer>(
                Storyboard.NoteControllers.Values.ToList(),
                noteController => new NoteControllerRenderer(this, noteController), Predicate);
            timer.Time("NoteController");
            await SpawnObjects<Text, TextState, TextRenderer>(
                Storyboard.Texts.Values.ToList(), text => new TextRenderer(this, text), Predicate);
            timer.Time("Text");
            await SpawnObjects<Sprite, SpriteState, SpriteRenderer>(
                Storyboard.Sprites.Values.ToList(), sprite => new SpriteRenderer(this, sprite), Predicate);
            timer.Time("Sprite");
            await SpawnObjects<Line, LineState, LineRenderer>(
                Storyboard.Lines.Values.ToList(), line => new LineRenderer(this, line), Predicate);
            timer.Time("Line");
            await SpawnObjects<Video, VideoState, VideoRenderer>(
                Storyboard.Videos.Values.ToList(), line => new VideoRenderer(this, line), Predicate);
            timer.Time("Video");
            await SpawnObjects<Controller, ControllerState, ControllerRenderer>(
                Storyboard.Controllers.Values.ToList(), controller => new ControllerRenderer(this, controller),
                Predicate);
            timer.Time("Controller");
            timer.Time();

            SyncAllVideoPlayback(forceTimelineSync: true);
        }
    }
}
