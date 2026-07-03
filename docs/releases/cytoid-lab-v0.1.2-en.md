## Cytoid Lab v0.1.2

Windows 10/11 x64 · Unity **6000.0.75f1** core

### What's new

- **Viewport presets** — Level menu, top-right: 16:9 / 4:3, Small (1280-wide) / Large (1920-wide); applies on **Start** ([#4](https://github.com/ky-reflection/Cytoid/issues/4))
- **Skip end on chart clear** — Top bar `End: On/Off`: On = fast fade and exit after clear; Off = play out music / post-chart storyboard ([#3](https://github.com/ky-reflection/Cytoid/issues/3))
- **Level import** — `.zip` packages supported; file picker fixed for Unicode / non-ASCII paths ([#1](https://github.com/ky-reflection/Cytoid/issues/1), [#2](https://github.com/ky-reflection/Cytoid/issues/2))
- **Keyboard shortcuts** — Space play/pause, Esc (pause in windowed / exit fullscreen), F11 fullscreen; Space ignored while dragging timeline; **?** on level menu for help
- **Audio** — Core **AudioServer** refactor (Unity / Exceed7 backends); timeline seek uses `PlayFrom`
- **Storyboard video** — Improved prepare, timeline sync, and VFS paths; better recovery after seek on many charts ([#5](https://github.com/ky-reflection/Cytoid/issues/5) — partial; some charts still limited)
