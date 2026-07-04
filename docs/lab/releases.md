# Cytoid Lab — Release Notes

All versions in one place. For `gh release create --notes-file`, copy the **English** subsection of the target version into a temp file (or use the subsection directly if your tooling supports it).

Build artifact: `.\engines\unity\build-cytoid-lab.ps1 -Package -KeepLog` → `engines/unity/Builds/CytoidLab.zip`

---

## v0.1.2

**Tag:** `cytoid-lab-v0.1.2` · **Title:** Cytoid Lab v0.1.2

### English

Windows 10/11 x64 · Unity **6000.0.75f1** core

#### What's new

- **Viewport presets** — Level menu, top-right: 16:9 / 4:3, Small (1280-wide) / Large (1920-wide); applies on **Start** ([#4](https://github.com/ky-reflection/Cytoid/issues/4))
- **Skip end on chart clear** — Top bar `End: On/Off`: On = fast fade and exit after clear; Off = play out music / post-chart storyboard ([#3](https://github.com/ky-reflection/Cytoid/issues/3))
- **Level import** — `.zip` packages supported; file picker fixed for Unicode / non-ASCII paths ([#1](https://github.com/ky-reflection/Cytoid/issues/1), [#2](https://github.com/ky-reflection/Cytoid/issues/2))
- **Keyboard shortcuts** — Space play/pause, Esc (pause in windowed / exit fullscreen), F11 fullscreen; Space ignored while dragging timeline; **?** on level menu for help
- **Audio** — Core **AudioServer** refactor (Unity / Exceed7 backends); timeline seek uses `PlayFrom`
- **Storyboard video** — Improved prepare, timeline sync, and VFS paths; better recovery after seek on many charts ([#5](https://github.com/ky-reflection/Cytoid/issues/5) — partial; some charts still limited)

### 中文

Windows 10/11 x64 · Unity **6000.0.75f1** core

#### 更新内容

- **Viewport 预设** — 关卡菜单右上角：16:9 / 4:3，Small（1280 宽）/ Large（1920 宽）；按 Start 后进谱生效 ([#4](https://github.com/ky-reflection/Cytoid/issues/4))
- **完谱 Skip end** — 顶栏 `End: On/Off`：开 = 清谱后快速淡出退出；关 = 音乐/storyboard 播完 ([#3](https://github.com/ky-reflection/Cytoid/issues/3))
- **关卡导入** — 支持 `.zip`；修复含 Unicode/中文路径的文件选择 ([#1](https://github.com/ky-reflection/Cytoid/issues/1), [#2](https://github.com/ky-reflection/Cytoid/issues/2))
- **快捷键** — Space 播放/暂停，Esc（窗口化暂停 / 全屏退出），F11 全屏；拖 timeline 时 Space 无效；菜单 **?** 查看说明
- **音频** — 内核 **AudioServer** 重构（Unity / Exceed7 双后端），timeline seek 走 `PlayFrom`
- **Storyboard 视频** — prepare、timeline 同步与 VFS 路径改进；部分谱面 seek 后视频恢复更好 ([#5](https://github.com/ky-reflection/Cytoid/issues/5) — 部分改善，仍有限制)

---

## v0.1.1

**Tag:** `cytoid-lab-v0.1.1` · **Title:** Cytoid Lab v0.1.1

### English

Patch release — timeline scrubbing, hold seek visuals, HUD layout, keyboard shortcuts, in-app help. Unity **6000.0.75f1**.

#### What's new

**Window & HUD**
- **1280×720** play window with **Screen Space Overlay** HUD
- Edge-reveal auto-hide for top/bottom HUD bars
- Wider timeline slider hit area
- Level menu scrollbar and layout tidy-up

**Timeline scrubbing**
- Live preview while dragging (light resync)
- Full resync on release (score/judgement, storyboard, input)
- Hold / long-hold body progress and head approach after seek
- Auto and miss/clear suppressed during scrub

**Keyboard & help**
- **Space** play/pause · **Esc** pause / exit fullscreen · **F11** fullscreen · **?** help overlay

**Core fixes (all builds)**
- Hold **ProgressRing** uses per-instance `MaterialPropertyBlock`

#### Known limitations

- Windows only
- Storyboard not resynced during drag preview; commits on release
- Storyboard trigger replay after seek not implemented
- Some chart videos may not resume after seek
- Style 2 hold body may show early on seek (minor)

Player log: `%USERPROFILE%\AppData\LocalLow\TigerHix\Cytoid Lab\Player.log`

### 中文

补丁版 — 时间轴 scrub、Hold seek 视觉、HUD、快捷键、应用内帮助。Unity **6000.0.75f1**。

#### 更新内容

**窗口与 HUD**
- **1280×720** 游玩窗口，HUD 为 **Screen Space Overlay**
- 顶部/底部 HUD 指针靠边时自动显隐
- 时间轴滑条点击区域加宽
- 关卡列表滚动条与菜单布局整理

**时间轴 scrub**
- 拖拽中实时预览（light resync）
- 松手 full resync（分数/判定、storyboard、输入）
- Seek 后 Hold / 长 Hold body 与 head approach 正确
- Scrub 期间抑制 Auto 与 miss/clear

**键盘与帮助**
- **Space** / **Esc** / **F11**；关卡菜单 **?** 帮助弹窗

**Core 修复（全平台）**
- Hold **ProgressRing** 改用 `MaterialPropertyBlock`

#### 已知限制

- 仅 Windows
- 拖拽 preview 不重 sync storyboard；松手后 full resync
- Seek 后不 replay storyboard trigger 历史
- 部分谱面视频 seek 后可能无法恢复
- Style 2 Hold body seek 后 approach 段可能提前显示

#### 相对 v0.1.0 的 commit（维护者）

| Commit | 摘要 |
|--------|------|
| `ccbc2d68` | overlay HUD、720p 窗口、减少 core 侵入 |
| `442fc6f7` | timeline 点击区域、菜单滚动条 |
| `d629806a` | Hold timeline seek Phase A、拖拽 light resync |
| `1f6e3b21` | 键盘快捷键、帮助弹窗、Input System UI |

---

## v0.1.0

**Tag:** `cytoid-lab-v0.1.0` · **Title:** Cytoid Lab v0.1.0

### English

First public preview — Windows desktop tool for chart authors to import, preview, and debug Cytoid levels.

#### Highlights

**Level browser** — local levels, `.cytoidlevel` import, Easy / Hard / Extreme

**Playtest HUD** — play/pause, auto, hitsound, note IDs, fullscreen, timeline scrub, hard reset

**Chart debugging** — timeline seek resyncs notes/drag/hold; storyboard resync after scrub; MAX/FC splashes disabled

#### Known limitations

- Windows only
- Levels under `%USERPROFILE%\AppData\LocalLow\TigerHix\`
- Slider held = audio/time preview only; chart/storyboard commit on release

### 中文

首个公开预览版 — 面向谱师与开发者的 Windows 桌面谱面预览/调试工具。

#### 功能概览

**关卡浏览** — 本机关卡、`.cytoidlevel` 导入、难度选择

**试玩 HUD** — 播放/暂停、Auto、打击音、Note ID、全屏、时间轴、Hard Reset

**谱面调试** — 时间轴跳转同步音符/拖链/Hold；storyboard 重同步；禁用 MAX/FC 特效

#### 已知限制

- 仅 Windows
- 关卡数据 `%USERPROFILE%\AppData\LocalLow\TigerHix\`
- 按住滑条仅预览音频/时间；松开提交谱面与 storyboard
