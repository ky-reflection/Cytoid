# Lab upstream refresh

The 2026-10-20 refresh merges public `Cytoid/Cytoid` main at `83fe3d65`
into `codex/lab-latest`, preserving the standalone Lab shell and timeline.
It includes the current flat note design, play-record updates, drag-stack
planner, DropDrag input guards, hold-direction overrides and storyboard guards.

Integration keeps timeline suppression around autoplay and automatic misses,
pooled note-ID binding, hide-note behaviour and the Lab viewport settings.
The CDrag renderer reads the original ID pose from its prefab because Lab binds
IDs on load rather than in the renderer constructor. Renderers can receive
drag-stack visibility transitions for auxiliary graphics outside the note tree.
The video ownership regression uses Lab's `ownsVideoObjects` disposal path.

Unity 6000.0.75f1 passed all 202 EditMode cases after integration. Results and
the import/compiler log are local under
`engines/unity/.utmp/UpstreamRefreshFinal/`. Player/device validation is recorded
separately when performed; an Editor test pass alone is not a device-play claim.
