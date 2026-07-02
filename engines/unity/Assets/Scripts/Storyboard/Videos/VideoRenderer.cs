using System;
using System.IO;
using Cytoid.Storyboard.Sprites;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using static UnityEngine.Object;

namespace Cytoid.Storyboard.Videos
{
    public partial class VideoRenderer : StoryboardComponentRenderer<Video, VideoState>
    {
        private const string LogTag = "[StoryboardVideo]";

        public VideoPlayer VideoPlayer { get; private set; }

        public RawImage RawImage { get; private set; }

        public RenderTexture RenderTexture { get; private set; }

        public RectTransform RectTransform { get; private set; }

        public Canvas Canvas { get; private set; }

        public string PlaybackPath { get; private set; }

        private bool mediaPrepared;
        private float videoTimelineStartTime = float.NaN;

        public override Transform Transform => RectTransform;

        public override bool IsOnCanvas => true;

        public VideoRenderer(StoryboardRenderer mainRenderer, Video component) : base(mainRenderer, component)
        {
        }

        public override StoryboardRendererEaser<VideoState> CreateEaser() => new VideoEaser(this);

        private void LogVideo(string message)
        {
            Debug.Log($"{LogTag} id={Component?.Id ?? "?"} {message}");
        }

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
            VideoPlayer.source = VideoSource.Url;
            VideoPlayer.url = url;
            VideoPlayer.aspectRatio = VideoAspectRatio.FitOutside;
            VideoPlayer.renderMode = VideoRenderMode.RenderTexture;
            VideoPlayer.targetTexture = RenderTexture;
        }

        public override async UniTask Initialize()
        {
            var targetRenderer = GetTargetRenderer<VideoRenderer>();
            if (targetRenderer != null)
            {
                VideoPlayer = targetRenderer.VideoPlayer;
                RawImage = targetRenderer.RawImage;
                RenderTexture = targetRenderer.RenderTexture;
                RectTransform = targetRenderer.RectTransform;
                Canvas = targetRenderer.Canvas;
                PlaybackPath = targetRenderer.PlaybackPath;
                mediaPrepared = targetRenderer.mediaPrepared;
                videoTimelineStartTime = targetRenderer.videoTimelineStartTime;
                return;
            }

            try
            {
                VideoPlayer = Instantiate(Provider.VideoVideoPlayerPrefab);
                RawImage = Instantiate(Provider.VideoRawImagePrefab, Provider.Canvas.transform);
                RenderTexture = new RenderTexture(UnityEngine.Screen.width / 2, UnityEngine.Screen.height / 2, 0, RenderTextureFormat.ARGB32);
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
                PlaybackPath = videoFilePath;

                var useRawAndroidPath = Application.platform == RuntimePlatform.Android && Context.AndroidVersionCode >= 29;
                var playerUrl = useRawAndroidPath ? videoFilePath : GameLaunchVfs.ToFileUri(videoFilePath);

                if (!File.Exists(videoFilePath))
                {
                    Debug.LogError($"[StoryboardVideo] Video file missing: {videoFilePath}");
                }

                ConfigureVideoPlayer(playerUrl);
                RawImage.texture = RenderTexture;

                mediaPrepared = await PrepareCurrentUrlAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"[StoryboardVideo] Initialize failed: {e.Message}");
                throw;
            }
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
                Debug.LogError($"[StoryboardVideo] Prepare failed: {prepareError ?? "timeout"} url={VideoPlayer.url}");
                return false;
            }

            LabOnPrepareSucceeded();
            return true;
        }

        private double ClampVideoTargetTime(double elapsed)
        {
            var length = VideoPlayer.length;
            return length > 0 ? Math.Min(elapsed, Math.Max(0, length - 0.01)) : elapsed;
        }

        private void SyncVideoTimeline()
        {
            if (VideoPlayer == null || !VideoPlayer.isPrepared || !VideoPlayer.canSetTime) return;

            var elapsed = GetTimelineElapsed();
            if (elapsed < 0) return;

            VideoPlayer.time = ClampVideoTargetTime(elapsed);
        }

        public void ForceSyncTimeline()
        {
            SyncVideoTimeline();
        }

        public override void Clear()
        {
            if (VideoPlayer != null)
            {
                VideoPlayer.Stop();
            }

            if (RawImage != null)
            {
                RawImage.color = UnityEngine.Color.white.WithAlpha(0);
            }

            IsTransformActive = false;
        }

        public override void Dispose()
        {
            if (VideoPlayer != null) Destroy(VideoPlayer.gameObject);
            if (RawImage != null) Destroy(RawImage.gameObject);
            if (RenderTexture != null) Destroy(RenderTexture);
        }

        public override void Update(VideoState fromState, VideoState toState)
        {
            base.Update(fromState, toState);
            SyncPlaybackWithGameState();
            RunLabDiagnosticsIfNeeded();
        }

        partial void LabOnPrepareSucceeded();
        partial void RunLabDiagnosticsIfNeeded();

        public void SyncPlaybackWithGameState(bool forceTimelineSync = false)
        {
            if (VideoPlayer == null || MainRenderer == null || !mediaPrepared) return;

            var gameState = MainRenderer.Game?.State;
            if (gameState == null) return;

            if (forceTimelineSync)
            {
                SyncVideoTimeline();
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
                VideoPlayer.Play();
            }
        }
    }
}
