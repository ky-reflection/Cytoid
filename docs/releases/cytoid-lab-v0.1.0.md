# Cytoid Lab v0.1.0 — GitHub Release (review draft)

**Tag:** `cytoid-lab-v0.1.0`  
**Title:** Cytoid Lab v0.1.0  
**Attach:** `CytoidLab.zip` from `.\engines\unity\build-cytoid-lab.ps1 -Package -KeepLog`

---

## English (release body)

## Cytoid Lab v0.1.0

First public preview of **Cytoid Lab** — a Windows desktop tool for chart authors to import, preview, and debug Cytoid levels using the Unity gameplay core.

> This is **not** the main Cytoid app. Lab is for chart inspection and playtesting.

### Download

- **Platform:** Windows 10/11 x64
- **Attach:** `CytoidLab.zip` (contains `CytoidLab.exe` and its data folder)
- No Unity install required to run the prebuilt binary

Build locally:

```powershell
.\engines\unity\build-cytoid-lab.ps1 -Package -KeepLog
```

### Highlights

**Level browser**
- Browse locally installed levels
- Import `.cytoidlevel` files (file picker, or drag onto the executable)
- Difficulty selection: Easy / Hard / Extreme

**Playtest HUD**
- Play / pause, auto mode, hitsound on/off
- Note ID overlay for chart verification
- Fullscreen toggle
- Timeline slider: click or drag to preview; full chart resync on release
- Hard reset reloads the playfield from scratch

**Chart debugging**
- Timeline seek re-syncs on-screen notes, drag chains, and hold progress to the target time
- Storyboard elements and camera state resync after scrub
- MAX/FC completion splashes disabled in Lab

### Known limitations

- Windows only in this release
- Uses the same local level storage as legacy debug builds (`%USERPROFILE%\AppData\LocalLow\TigerHix\…`)
- While the slider is held, only audio/time are previewed; chart and storyboard state commit on release
- Not a replacement for the production Cytoid client

### Build from source

```powershell
git checkout feature/cytoid-player
.\engines\unity\build-cytoid-lab.ps1 -KeepLog
```

Requires Unity **6000.0.75f1**.

---

## 中文（release 正文）

## Cytoid Lab v0.1.0

**Cytoid Lab** 首个公开预览版 —— 面向谱师与开发者的 Windows 桌面工具，用于导入、预览和调试 Cytoid 谱面（基于 Unity gameplay core）。

> 这**不是**正式版 Cytoid 客户端。Lab 用于谱面检查与试玩验证。

### 下载

- **平台：** Windows 10/11 x64
- **附件：** `CytoidLab.zip`（内含 `CytoidLab.exe` 及数据目录）
- 运行预编译包无需单独安装 Unity

本地构建：

```powershell
.\engines\unity\build-cytoid-lab.ps1 -Package -KeepLog
```

### 功能概览

**关卡浏览**
- 浏览本机已安装关卡
- 导入 `.cytoidlevel`（文件选择器，或将文件拖到 exe 上）
- 难度选择：Easy / Hard / Extreme

**试玩 HUD**
- 播放 / 暂停、Auto、打击音开关
- Note ID 叠加显示，便于对谱
- 全屏切换
- 时间轴滑条：点击或拖拽预览；松开后完整 resync
- Hard Reset 从场景重载谱面

**谱面调试**
- 时间轴跳转会将场上音符、拖链与 Hold 进度同步到目标时间点
- Storyboard 元素与镜头在 scrub 后重同步
- Lab 内禁用 MAX/FC 结算特效

### 已知限制

- 本版本仅支持 Windows
- 关卡数据路径与旧版调试构建相同（`%USERPROFILE%\AppData\LocalLow\TigerHix\…`）
- 按住滑条时仅预览音频/时间；松开时才提交谱面与 storyboard 状态
- 不能替代正式版 Cytoid 客户端

### 从源码构建

```powershell
git checkout feature/cytoid-player
.\engines\unity\build-cytoid-lab.ps1 -KeepLog
```

需要 Unity **6000.0.75f1**。
