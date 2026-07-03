# Cytoid Lab v0.1.2

**Tag:** `cytoid-lab-v0.1.2`  
**附件:** `CytoidLab.zip`（`.\engines\unity\build-cytoid-lab.ps1 -Package -KeepLog`）

GitHub 英文正文见 [cytoid-lab-v0.1.2-en.md](cytoid-lab-v0.1.2-en.md)。

---

## Cytoid Lab v0.1.2

Windows 10/11 x64 · Unity **6000.0.75f1** core

### 更新内容

- **Viewport 预设** — 关卡菜单右上角：16:9 / 4:3，Small（1280 宽）/ Large（1920 宽）；按 Start 后进谱生效 ([#4](https://github.com/ky-reflection/Cytoid/issues/4))
- **完谱 Skip end** — 顶栏 `End: On/Off`：开 = 清谱后快速淡出退出；关 = 音乐/storyboard 播完 ([#3](https://github.com/ky-reflection/Cytoid/issues/3))
- **关卡导入** — 支持 `.zip`；修复含 Unicode/中文路径的文件选择 ([#1](https://github.com/ky-reflection/Cytoid/issues/1), [#2](https://github.com/ky-reflection/Cytoid/issues/2))
- **快捷键** — Space 播放/暂停，Esc（窗口化暂停 / 全屏退出），F11 全屏；拖 timeline 时 Space 无效；菜单 **?** 查看说明
- **音频** — 内核 **AudioServer** 重构（Unity / Exceed7 双后端），timeline seek 走 `PlayFrom`
- **Storyboard 视频** — prepare、timeline 同步与 VFS 路径改进；部分谱面 seek 后视频恢复更好 ([#5](https://github.com/ky-reflection/Cytoid/issues/5) — 部分改善，仍有限制)
