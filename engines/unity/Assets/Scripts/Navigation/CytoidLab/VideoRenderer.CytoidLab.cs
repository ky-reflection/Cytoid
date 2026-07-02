#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN

using System;
using UnityEngine;
using UnityEngine.Video;

namespace Cytoid.Storyboard.Videos
{
    /// <summary>Cytoid Lab diagnostics for Windows storyboard video playback (WMF).</summary>
    public partial class VideoRenderer
    {
        private const float LabWatchdogIntervalSeconds = 0.5f;
        private const float LabStallDetectSeconds = 1.5f;
        private const string LabVideoDiagnosticsEnv = "CYTOID_LAB_VIDEO_DIAGNOSTICS";
        private static readonly bool LabVideoDiagnosticsEnabled = ReadLabVideoDiagnosticsFlag();

        private float labLastWatchdogStoryboardTime = float.NaN;
        private long labLastWatchdogFrame = -1;
        private bool labLoggedFrameReady;
        private bool labLoggedStarted;
        private bool labReportedStall;
        private bool labPlaybackConfirmed;

        partial void LabOnPrepareSucceeded()
        {
            if (!CytoidLabShell.IsActive || VideoPlayer == null) return;
            if (!ShouldLogLabVideoDiagnostics()) return;

            VideoPlayer.frameReady += OnLabFrameReady;
            VideoPlayer.started += OnLabStarted;

            LogVideo(
                $"Lab snapshot after prepare sbTime={MainRenderer?.Time:F3} elapsed={GetTimelineElapsed():F3} " +
                $"videoTime={VideoPlayer.time:F3} clockTime={VideoPlayer.clockTime:F3} frame={VideoPlayer.frame} " +
                $"isPlaying={VideoPlayer.isPlaying} alpha={RawImage?.color.a:F3} sortLayer={Canvas?.sortingLayerName} order={Canvas?.sortingOrder}");
        }

        partial void RunLabDiagnosticsIfNeeded()
        {
            if (!CytoidLabShell.IsActive || !mediaPrepared || VideoPlayer == null || MainRenderer == null) return;
            if (!ShouldLogLabVideoDiagnostics() && labPlaybackConfirmed) return;

            var sbTime = MainRenderer.Time;
            if (!float.IsNaN(labLastWatchdogStoryboardTime) &&
                sbTime - labLastWatchdogStoryboardTime < LabWatchdogIntervalSeconds)
            {
                return;
            }

            labLastWatchdogStoryboardTime = sbTime;
            var elapsed = GetTimelineElapsed();
            if (elapsed < 0) return;

            var frame = VideoPlayer.frame;
            var frameAdvanced = frame > labLastWatchdogFrame;
            labLastWatchdogFrame = frame;

            if (ShouldLogLabVideoDiagnostics())
            {
                LogVideo(
                    $"Watchdog sbTime={sbTime:F3} elapsed={elapsed:F3} videoTime={VideoPlayer.time:F3} " +
                    $"clockTime={VideoPlayer.clockTime:F3} frame={frame} frameAdvanced={frameAdvanced} " +
                    $"length={VideoPlayer.length:F3} isPlaying={VideoPlayer.isPlaying} canSetTime={VideoPlayer.canSetTime} " +
                    $"alpha={RawImage?.color.a:F3} sortLayer={Canvas?.sortingLayerName} order={Canvas?.sortingOrder} " +
                    $"rt={RenderTexture?.width}x{RenderTexture?.height}");
            }

            DetectLabVideoStall(elapsed, frame);
        }

        private static bool ShouldLogLabVideoDiagnostics()
        {
            return LabVideoDiagnosticsEnabled;
        }

        private static bool ReadLabVideoDiagnosticsFlag()
        {
            var value = Environment.GetEnvironmentVariable(LabVideoDiagnosticsEnv);
            return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private void DetectLabVideoStall(float elapsed, long frame)
        {
            if (labReportedStall || VideoPlayer == null || !VideoPlayer.isPlaying) return;
            if (elapsed < LabStallDetectSeconds || VideoPlayer.clockTime < LabStallDetectSeconds) return;
            if (VideoPlayer.time > 0.05 || frame > 0)
            {
                labPlaybackConfirmed = true;
                return;
            }

            labReportedStall = true;
            global::CytoidLabVideoRepair.ReportStalledVideo(
                PlaybackPath,
                Component?.Id,
                $"clockTime={VideoPlayer.clockTime:F3}, videoTime={VideoPlayer.time:F3}, frame={frame}");
        }

        private void OnLabFrameReady(VideoPlayer source, long frameIdx)
        {
            if (labLoggedFrameReady) return;
            labLoggedFrameReady = true;
            LogVideo(
                $"frameReady frameIdx={frameIdx} videoTime={source.time:F3} clockTime={source.clockTime:F3} frame={source.frame}");
        }

        private void OnLabStarted(VideoPlayer source)
        {
            if (labLoggedStarted) return;
            labLoggedStarted = true;
            LogVideo(
                $"started videoTime={source.time:F3} clockTime={source.clockTime:F3} frame={source.frame} isPlaying={source.isPlaying}");
        }
    }
}

#endif
