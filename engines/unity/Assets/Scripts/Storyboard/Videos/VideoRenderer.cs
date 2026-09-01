using System;
using Cytoid.Storyboard.Sprites;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using static UnityEngine.Object;

namespace Cytoid.Storyboard.Videos
{
    public class VideoRenderer : StoryboardComponentRenderer<Video, VideoState>
    {
        public VideoPlayer VideoPlayer { get; private set; }

        public RawImage RawImage { get; private set; }

        public RenderTexture RenderTexture { get; private set; }

        public RectTransform RectTransform { get; private set; }

        public Canvas Canvas { get; private set; }

        private bool mediaPrepared;
        private bool timelineSyncedBeforePlayback;
        private float videoTimelineStartTime = float.NaN;
        private bool ownsVideoObjects;
        private bool hasEnded;
        private VideoRenderer sharedPlaybackOwner;

        public override Transform Transform => RectTransform;

        public override bool IsOnCanvas => true;

        public VideoRenderer(StoryboardRenderer mainRenderer, Video component) : base(mainRenderer, component)
        {
        }

        public override StoryboardRendererEaser<VideoState> CreateEaser() => new VideoEaser(this);

        private float GetVideoTimelineStartTime()
        {
            if (!float.IsNaN(videoTimelineStartTime)) return videoTimelineStartTime;

            foreach (var state in Component.States)
            {
                if (state.Time < float.MaxValue)
                {
                    videoTimelineStartTime = state.Time;
                    return videoTimelineStartTime;
                }
            }

            videoTimelineStartTime = Component.States.Count > 0 ? Component.States[0].Time : 0f;
            return videoTimelineStartTime;
        }

        private float GetTimelineElapsed()
        {
            return MainRenderer.Time - GetVideoTimelineStartTime();
        }

        private void ConfigureVideoPlayer(string url)
        {
            VideoPlayer.playOnAwake = false;
            VideoPlayer.waitForFirstFrame = true;
            VideoPlayer.skipOnDrop = false;
            VideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            VideoPlayer.source = VideoSource.Url;
            VideoPlayer.url = url;
            VideoPlayer.aspectRatio = VideoAspectRatio.FitOutside;
            VideoPlayer.renderMode = VideoRenderMode.RenderTexture;
            VideoPlayer.targetTexture = RenderTexture;
        }

        public override async UniTask Initialize()
        {
            var version = BeginInitialize();
            var targetRenderer = GetTargetRenderer<VideoRenderer>();
            if (targetRenderer != null)
            {
                ownsVideoObjects = false;
                sharedPlaybackOwner = targetRenderer.sharedPlaybackOwner ?? targetRenderer;
                VideoPlayer = targetRenderer.VideoPlayer;
                RawImage = targetRenderer.RawImage;
                RenderTexture = targetRenderer.RenderTexture;
                RectTransform = targetRenderer.RectTransform;
                Canvas = targetRenderer.Canvas;
                mediaPrepared = targetRenderer.mediaPrepared;
                timelineSyncedBeforePlayback = targetRenderer.timelineSyncedBeforePlayback;
                videoTimelineStartTime = targetRenderer.videoTimelineStartTime;
                hasEnded = targetRenderer.hasEnded;
                return;
            }

            sharedPlaybackOwner = this;
            VideoPlayer = Instantiate(Provider.VideoVideoPlayerPrefab);
            RawImage = Instantiate(Provider.VideoRawImagePrefab, Provider.Canvas.transform);
            RenderTexture = new RenderTexture(UnityEngine.Screen.width / 2, UnityEngine.Screen.height / 2, 0, RenderTextureFormat.ARGB32);
            RenderTexture.Create();
            ownsVideoObjects = true;
            RectTransform = RawImage.rectTransform;
            Canvas = RawImage.GetComponent<Canvas>();
            Canvas.overrideSorting = true;
            Canvas.sortingLayerName = "Storyboard2";

            Clear();

            var videoPath = Component.States[0].Path;
            if (videoPath == null && Component.States.Count > 1) videoPath = Component.States[1].Path;
            if (videoPath == null)
            {
                throw new InvalidOperationException("Video does not have a valid path");
            }

            VideoPlayer.gameObject.name = RawImage.gameObject.name = $"$Video[{videoPath}]";

            var levelPath = MainRenderer.Game.Level.Path;
            var videoFilePath = GameLaunchVfs.ResolveRequiredFilePath(
                levelPath,
                videoPath,
                "storyboard.video.path");

            var useRawAndroidPath = Application.platform == RuntimePlatform.Android && Context.AndroidVersionCode >= 29;
            var playerUrl = useRawAndroidPath ? videoFilePath : GameLaunchVfs.ToFileUri(videoFilePath);

            ConfigureVideoPlayer(playerUrl);
            RawImage.texture = RenderTexture;
            VideoPlayer.loopPointReached += OnLoopPointReached;

            mediaPrepared = await PrepareCurrentUrlAsync();
            if (IsInitializeStale(version)) return;
        }

        private async UniTask<bool> PrepareCurrentUrlAsync()
        {
            var prepareCompleted = false;
            string prepareError = null;

            void OnPrepareCompleted(VideoPlayer _) => prepareCompleted = true;

            void OnErrorReceived(VideoPlayer _, string message) => prepareError = message;

            VideoPlayer.prepareCompleted += OnPrepareCompleted;
            VideoPlayer.errorReceived += OnErrorReceived;
            VideoPlayer.Prepare();

            var startTime = DateTimeOffset.UtcNow;
            await UniTask.WaitUntil(() => prepareCompleted || prepareError != null ||
                                          DateTimeOffset.UtcNow - startTime > TimeSpan.FromSeconds(5));

            VideoPlayer.prepareCompleted -= OnPrepareCompleted;
            VideoPlayer.errorReceived -= OnErrorReceived;

            if (!prepareCompleted)
            {
                return false;
            }

            return true;
        }

        private void OnLoopPointReached(VideoPlayer _)
        {
            if (VideoPlayer != null && !VideoPlayer.isLooping)
                hasEnded = true;
        }

        private bool HasSharedPlaybackEnded =>
            sharedPlaybackOwner != null &&
            !sharedPlaybackOwner.IsDisposed &&
            sharedPlaybackOwner.hasEnded;

        private double ClampVideoTargetTime(double elapsed)
        {
            var length = VideoPlayer.length;
            return length > 0 ? Math.Min(elapsed, Math.Max(0, length - 0.01)) : elapsed;
        }

        private bool SyncVideoTimeline()
        {
            if (VideoPlayer == null || !VideoPlayer.isPrepared || !VideoPlayer.canSetTime) return false;

            var elapsed = GetTimelineElapsed();
            if (elapsed < 0) return false;

            VideoPlayer.time = ClampVideoTargetTime(elapsed);
            return true;
        }

        public void ForceSyncTimeline()
        {
            timelineSyncedBeforePlayback |= SyncVideoTimeline();
        }

        public override void Clear()
        {
            if (ownsVideoObjects)
                hasEnded = false;

            if (VideoPlayer != null)
            {
                VideoPlayer.Stop();
            }

            timelineSyncedBeforePlayback = false;

            if (RawImage != null)
            {
                RawImage.color = UnityEngine.Color.white.WithAlpha(0);
            }

            IsTransformActive = false;
        }

        public override void Dispose()
        {
            if (IsDisposed) return;
            if (ownsVideoObjects && VideoPlayer != null)
                VideoPlayer.loopPointReached -= OnLoopPointReached;
            if (ownsVideoObjects)
            {
                if (VideoPlayer != null) Destroy(VideoPlayer.gameObject);
                if (RawImage != null) Destroy(RawImage.gameObject);
                if (RenderTexture != null)
                {
                    RenderTexture.Release();
                    Destroy(RenderTexture);
                }
            }
            VideoPlayer = null;
            RawImage = null;
            RenderTexture = null;
            RectTransform = null;
            Canvas = null;
            ownsVideoObjects = false;
            hasEnded = false;
            sharedPlaybackOwner = null;
            base.Dispose();
        }

        public override void Update(VideoState fromState, VideoState toState)
        {
            base.Update(fromState, toState);
            SyncPlaybackWithGameState();
        }

        public void SyncPlaybackWithGameState(bool forceTimelineSync = false)
        {
            if (VideoPlayer == null || MainRenderer == null || !mediaPrepared || IsDisposed) return;

            var gameState = MainRenderer.Game?.State;
            if (gameState == null) return;

            if (forceTimelineSync)
            {
                timelineSyncedBeforePlayback |= SyncVideoTimeline();
            }

            if (!IsTransformActive)
            {
                if (VideoPlayer.isPlaying)
                {
                    VideoPlayer.Pause();
                }

                return;
            }

            if (!gameState.IsPlaying)
            {
                if (VideoPlayer.isPlaying)
                {
                    VideoPlayer.Pause();
                }

                return;
            }

            if (!VideoPlayer.isPlaying)
            {
                if (HasSharedPlaybackEnded && !VideoPlayer.isLooping) return;

                if (!timelineSyncedBeforePlayback)
                {
                    timelineSyncedBeforePlayback = SyncVideoTimeline();
                }

                VideoPlayer.Play();
            }
        }
    }
}
