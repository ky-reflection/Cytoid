# Cytoid Lab v0.1.1 — GitHub Release (review draft)

**Tag:** `cytoid-lab-v0.1.1`  
**Title:** Cytoid Lab v0.1.1  
**Attach:** `CytoidLab.zip` from `.\engines\unity\build-cytoid-lab.ps1 -Package -KeepLog`

---

## English (release body)

See [cytoid-lab-v0.1.1-en.md](cytoid-lab-v0.1.1-en.md) for the GitHub-ready English body (copy-paste).

---

## 中文（release 正文）

## Cytoid Lab v0.1.1

**Cytoid Lab** 补丁版 —— 改进时间轴 scrub、Hold seek 视觉、HUD 布局、键盘快捷键与应用内帮助。基于 Unity **6000.0.75f1** gameplay core。

### 下载

- **平台：** Windows 10/11 x64
- **附件：** `CytoidLab.zip`（内含 `CytoidLab.exe` 及数据目录）
- 运行预编译包无需单独安装 Unity

本地构建：

```powershell
.\engines\unity\build-cytoid-lab.ps1 -Package -KeepLog
```

### 更新内容

**窗口与 HUD**
- **1280×720** 游玩窗口，HUD 为 **Screen Space Overlay**（谱面占满窗口，不再裁切 camera viewport）
- 顶部/底部 HUD 指针靠边时自动显隐
- 时间轴滑条**点击区域加宽**（外观不变，更易拖拽）
- 关卡列表滚动条与菜单布局整理

**时间轴 scrub**
- **拖拽中实时预览**：音符、拖链、Hold 身体/头部视觉与滑条时间对齐（light resync）
- **松手 full resync**：分数/判定、storyboard、输入状态在目标时间重建（与 v0.1.0 语义一致）
- Seek 后 Hold / 长 Hold **body 进度与 head approach** 显示正确
- Scrub 期间抑制 Auto 与 miss/clear，避免预览污染游玩状态

**键盘与帮助**
- **Space** — 播放 / 暂停（拖 timeline 时忽略）
- **Esc** — 窗口化下播放 / 暂停，全屏时退出全屏
- **F11** — 全屏切换
- 关卡菜单右上角 **?** 打开帮助弹窗（快捷键、timeline 说明、版本号）

**Core 修复（全平台受益）**
- Hold **ProgressRing** 改用 `MaterialPropertyBlock`，修复多 Hold 同屏时判定环颜色/填充串扰

### 已知限制

- 仅支持 Windows
- 关卡数据保存在 `%USERPROFILE%\AppData\LocalLow\TigerHix\Cytoid Lab\`（Unity 持久化目录）
- **拖拽 preview 不重 sync storyboard**；松手后走 full resync（与 v0.1.0 相同）
- Seek 后 **不 replay storyboard trigger 历史**；仅重建静态时间轴对象
- **部分谱面视频** seek 后可能无法正确恢复（已知问题，单独跟踪）
- Style 2 Hold body 在 seek 后 approach 段仍可能提前显示（影响面小，以 Style 1 为主）

### 从源码构建

```powershell
git checkout feature/cytoid-player
git checkout cytoid-lab-v0.1.1   # tag 发布后
.\engines\unity\build-cytoid-lab.ps1 -Package -KeepLog
```

需要 Unity **6000.0.75f1**。

日志：`%USERPROFILE%\AppData\LocalLow\TigerHix\Cytoid Lab\Player.log`

---

## 相对 v0.1.0 的 commit 范围（维护者）

| Commit | 摘要 |
|--------|------|
| `ccbc2d68` | overlay HUD、720p 窗口、减少 core 侵入 |
| `442fc6f7` | timeline 点击区域、菜单滚动条 |
| `d629806a` | Hold timeline seek Phase A、拖拽 light resync |
| `1f6e3b21` | 键盘快捷键、帮助弹窗、Input System UI |
