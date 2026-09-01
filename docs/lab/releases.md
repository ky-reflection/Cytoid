# Cytoid Lab — Release Notes

All versions in one place. For `gh release create --notes-file`, copy the **English** subsection of the target version into a temp file (or use the subsection directly if your tooling supports it).

Build artifact: `.\engines\unity\build-cytoid-lab.ps1 -Package -KeepLog` → `engines/unity/Builds/CytoidLab.zip`

---

## Unreleased (next after v0.2.2)

_(empty)_

---

## v0.2.2

**Tag:** `cytoid-lab-v0.2.2` · **Title:** Cytoid Lab v0.2.2

### English

Windows 10/11 x64 · Unity **6000.0.75f1** core

- **Compile SB** — in-play button writes `storyboard.compiled.json` next to the source (does not overwrite authoring JSON; triggers omitted)
- **Update check** — on launch, a red badge on **?** if a newer GitHub release exists; Help opens the release page (Lab does not self-install)
- **HUD** — wider hover zone and a short hide delay so the top bar stays clickable
- **Core sync** — Cytoid/main #204–#208 (storyboard lifecycle, static page boundaries, TMP fonts, note next-position)

### 中文

Windows 10/11 x64 · Unity **6000.0.75f1** core

- **Compile SB** — 游戏内写出 `storyboard.compiled.json`（不覆盖作者 JSON，不含 trigger）
- **更新检查** — 启动时若有新 GitHub 版本，**?** 显示红点；帮助页跳转 Release（不自动安装）
- **HUD** — 扩大悬停区并延迟收起，顶栏更好点
- **内核同步** — Cytoid/main #204–#208（storyboard 生命周期、静态判定线、TMP 字体、note next-position）

---

## v0.2.1

**Tag:** `cytoid-lab-v0.2.1` · **Title:** Cytoid Lab v0.2.1

### English

- Portable `./data` next to the exe (migrates old AppData levels)
- Open folder button; remembers level/difficulty
- Rounded Lab UI; import progress in status

### 中文

- 便携 `./data`（自动迁移旧 AppData 谱面）
- 打开文件夹；记住关卡/难度
- 圆角 UI；导入进度显示在状态栏

---

## v0.2.0

**Tag:** `cytoid-lab-v0.2.0` · **Title:** Cytoid Lab v0.2.0

### English

Windows 10/11 x64 · Unity **6000.0.75f1** core

Demo-client release: newer chart/runtime surface from Cytoid core, plus Lab workflow improvements. Judgment tweaks from upstream are included for chart fidelity but are not the focus of this Lab build.

#### What's new

- **Multi-select import** — pick several levels in one import pass
- **C2V3 / Drop charts** — page functions and DropClick / DropDrag notes play in Lab
- **C2 UI & message events** — chart UI / message presentation (including color parsing for classic C2 message suffixes)
- **Overlay resize** — level-info / mods holders keep resolved sizes when the window is resized
- **Core sync** — Lab ships the latest Cytoid core runtime used for accurate chart demo playback

#### Known limitations

- Windows only
- Storyboard video may still show as a still image on some charts ([#5](https://github.com/ky-reflection/Cytoid/issues/5))

Player log: `%USERPROFILE%\AppData\LocalLow\TigerHix\Cytoid Lab\Player.log`

### 中文

Windows 10/11 x64 · Unity **6000.0.75f1** core

演示客户端版本：同步 Cytoid 主干的谱面/运行时能力，并改进 Lab 工作流。上游判定改动已带入以保证演示贴近正式客户端，但不是本版 Lab 的重点。

#### 更新内容

- **多选导入** — 一次选择导入多个关卡
- **C2V3 / Drop 谱面** — Lab 可播 page 函数与 DropClick / DropDrag
- **C2 UI / Message 事件** — 谱面 UI 与 message 表现（含经典 C2 message 颜色后缀解析）
- **Overlay 缩放** — 窗口缩放时保留关卡信息 / Mods 布局尺寸
- **内核同步** — 带上最新 Cytoid core，便于准确演示谱面

#### 已知限制

- 仅 Windows
- 部分谱面 storyboard 视频仍可能定格 ([#5](https://github.com/ky-reflection/Cytoid/issues/5))

---

## v0.1.3

**Tag:** `cytoid-lab-v0.1.3` · **Title:** Cytoid Lab v0.1.3

### English

Windows 10/11 x64 · Unity **6000.0.75f1** core

#### What's new

- **Remember viewport** — last 16:9 / 4:3 and Small / Large choice is restored after quit
- **In-app update** — menu checks GitHub Releases; an Update button downloads `CytoidLab.zip` and restarts into the new build
- **Window size** — keep the current window size when starting a chart and when returning to the level menu
- **Storyboard** — restore cover opacity after timeline seek; safer parse / dispose / seek lifecycle
- **Performance** — dirty-checked HUD text updates, AssetMemory / NLayer seek hardening, reduced GC in gameplay overlays

#### Known limitations

- Windows only
- Storyboard video may still show as a still image on some charts ([#5](https://github.com/ky-reflection/Cytoid/issues/5))
- In-app update applies from this build onward (v0.1.2 and older need a manual download once)

Player log: `%USERPROFILE%\AppData\LocalLow\TigerHix\Cytoid Lab\Player.log`

### 中文

Windows 10/11 x64 · Unity **6000.0.75f1** core

#### 更新内容

- **记住 Viewport** — 退出后恢复上次的 16:9 / 4:3 与 Small / Large
- **应用内更新** — 菜单检查 GitHub Releases；有新版本时显示 Update，下载 `CytoidLab.zip` 后重启替换
- **窗口尺寸** — 进谱与返回菜单时保留当前窗口大小
- **Storyboard** — timeline seek 后恢复 cover 透明度；解析 / 销毁 / seek 生命周期更稳
- **性能** — HUD 文本脏检查、AssetMemory / NLayer seek 加固、减少 overlay GC

#### 已知限制

- 仅 Windows
- 部分谱面 storyboard 视频仍可能定格 ([#5](https://github.com/ky-reflection/Cytoid/issues/5))
- 应用内更新从此版本起生效（v0.1.2 及更早需先手动下载一次）

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
