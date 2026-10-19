# Cytoid Lab 0.2.4 validation

Validated on 2026-10-20 with Unity 6000.0.75f1 on Windows x64, using an IL2CPP player and Direct3D11. The automated driver launches real Game scenes, waits for natural chart completion and cleanup back to the Lab menu, and fails on runtime errors. It does not simulate a human playthrough.

## Automated tests

All **203 / 203 EditMode cases passed, including a new regression for synchronous timeline despawn unregistering drag-stack members before pooled note models are cleared.

## Complete player runs

| Run | Judged / notes | Chart duration | Result |
| --- | --- | --- | --- |
| fixture-classic | 72 / 72 | 34.00s | Completed and returned to menu |
| fixture-flat | 72 / 72 | 34.00s | Completed and returned to menu |
| fixture-public-miss | 72 / 72 | 34.00s | Completed and returned to menu |
| tera-sea-classic | 443 / 443 | 125.09s | Completed and returned to menu |

The controlled 72-note fixture includes every note type (0–9), both Drop directions, overlapping drag children, connected drags, Hold and cross-page LongHold, reversed PositionFunction travel, and storyboard Hold-direction overrides. The Miss run disables autoplay and waits for natural misses.

For each timeline run, the driver pauses during a Hold, confirms the clock stays paused, previews and commits forward/backward jumps to 12.5, 5.5, 21 and 3 seconds, checks that preview does not change judgments, verifies pooled IDs and suppression cleanup, resumes, hard-restarts and then plays the entire chart from the beginning.

## Fixes found by the regression

- Rounded-corner validation can run before its material is assigned; it now handles that state.
- The slider's unused built-in Knob.psd lookup is unavailable in Unity 6 and has been removed. The existing rounded visual and transparent hit area remain.
- HUD time reads now wait for playback to start and stop when music teardown begins.
- Timeline despawn removes live drag-stack membership before clearing a pooled note's model and resets its armed/stack state.
- A direct hard restart pauses the old game before destroying its pool and waiting for scene replacement.
- Pill-label color and scale tweens are linked to their owner's lifetime. The final player gate also fails on DOTween missing-target warnings; none occurred in the final complete runs.

## Distribution checks

The temporary player driver and its scripting define are removed before the final release build. Packaging checks the final IL2CPP metadata for absence of the driver, validates x64 executables, omits local user data and debug folders, and produces a SHA256 sidecar and build information. Existing local user levels are preserved. The final build and package checks are recorded with the release assets.

## Coverage limits

The complete chart runs and controls above are automated. Physical keyboard/mouse play, touchscreen input, and Android installation are outside this Windows Lab release validation. No performance-budget claim is made.
