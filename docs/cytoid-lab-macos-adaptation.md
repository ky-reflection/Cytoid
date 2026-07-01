# Cytoid Lab macOS 移植评估

> **Date:** 2026-07-01  
> **Related:** [cytoid-lab.md](cytoid-lab.md)、[2026-07-01-c2v3-research-plan.md](2026-07-01-c2v3-research-plan.md)

---

## 1. 难度评级：**中**

**理由：**

- **有利：** Lab 核心逻辑（runtime 构建 uGUI 菜单/HUD、timeline scrub、Game partial 扩展）均为 **纯 C#/Unity API**，约 24 个 `CytoidLab/*.cs` 文件无 Windows 专有业务逻辑
- **不利：** 平台门控集中在 `UNITY_STANDALONE_WIN` / `WindowsPlayer` 判断；**唯一重原生依赖** 是 `comdlg32.dll` 文件选择对话框；构建链仅有 Windows x64；仓库 **无** `StandaloneOSX` 构建先例

不属于「高」：无 Win32 窗口管理、注册表、进程枚举等深度耦合。  
不属于「低」：需系统性改 ifdef、新 build target、macOS 文件选择器、测试与文档。

**估算：约 4–7 人天**（含可选 CI 则上限）

---

## 2. 阻塞项 vs 直接项

### 2.1 阻塞项（必须解决）

| 项 | 现状 | 位置 |
|----|------|------|
| 平台激活门控 | `CytoidLabShell.IsActive` 仅 `WindowsPlayer`/`WindowsEditor` | `CytoidLabShell.cs` |
| HUD 显示门控 | `CytoidLabHudController.ShouldShowHud()` 同上 | `CytoidLabHudController.cs` |
| Context 注入 | 4 处 `#if UNITY_STANDALONE_WIN` 初始化 Lab | `Context.cs` |
| 文件导入 | `PickCytoidLevelFile()` 非 Windows 返回 `null`；Windows 用 `comdlg32 GetOpenFileName` | `CytoidLabMenuController.cs` |
| 构建入口 | 仅 `BuildCytoidLabWindows64` + `build-cytoid-lab.ps1` | `CytoidCoreBuild.cs`、`build-cytoid-lab.ps1` |
| Debug 导航互斥 | `DebugNavigationController` 在 WIN standalone 禁用自身 | `DebugNavigationController.cs` |

### 2.2 直接项（改动小或已跨平台）

| 项 | 说明 |
|----|------|
| Timeline scrub / seek | `Game.CytoidLab.cs` 纯 Unity |
| Storyboard resync | `Storyboard.CytoidLab.cs` |
| Hold/Drag partials | `HoldNote.CytoidLab.cs` 等 |
| 命令行导入 | `ProcessCommandLineImport()` 用 `Environment.GetCommandLineArgs()`，macOS 可用 |
| 关卡列表刷新 | `LoadLevelsOfType` 路径逻辑跨平台 |
| 输入 | `GameInputCompat` 基于 Unity Input System |
| 窗口尺寸 | `Screen.SetResolution` / `cam.rect` HUD bands — Unity 跨平台 |
| IL2CPP link.xml | 已有 `CytoidLab*` preserve |

---

## 3. 分项工作量

| 工作项 | 内容 | 估算 |
|--------|------|------|
| **平台宏重构** | `UNITY_STANDALONE_WIN` → `(WIN \|\| OSX)`；`IsActive`/`ShouldShowHud` 加入 `OSXPlayer`/`OSXEditor` | 0.5–1 天 |
| **文件选择器** | macOS `NSOpenPanel`（原生 plugin / UPM `StandaloneFileBrowser` / Editor 用 `EditorUtility.OpenFilePanel`） | 1–2 天 |
| **路径** | `Path.Combine` 已用；`LevelManager` 已有 `__MACOSX` zip 过滤；Player.log 文档改为 `~/Library/Logs/...` | 0.25 天 |
| **构建脚本** | `BuildCytoidLabMacOS` + `build-cytoid-lab.sh`；`SwitchActiveBuildTarget(StandaloneOSX)`；输出 `.app` | 1 天 |
| **图形/分辨率** | `CytoidLabShell.ApplyGraphicsQualityIfNeeded` 已用 `Screen.currentResolution` 钳制 — macOS 可直接复用 | 0.25 天 |
| **输入** | 无额外工作（Input System） | 0 |
| **CI** | 可选 `workflow_dispatch` macOS runner 构建 `.app` zip | 1–2 天 |
| **文档** | `cytoid-lab.md`、AGENTS.md、日志路径 | 0.25 天 |
| **QA** | 导入/删除/全屏/scrub/Retina 缩放 | 1 天 |

---

## 4. 与 Windows 实现对比

| 维度 | Windows（已完成） | macOS（待做） |
|------|-------------------|---------------|
| 首次搭建 | ~15 commits 引入全套 Lab | 主要是 **门控扩展 + 构建 + 文件对话框**，不需重写 Lab 逻辑 |
| 构建时间 | IL2CPP 5–15+ 分钟 | 类似 |
| 原生 API | `comdlg32.dll` | 需 `NSOpenPanel` 或第三方 |
| 编辑器内测试 | `WindowsEditor` 激活 Lab | 需加 `OSXEditor` |
| 分发 | `.exe` + zip | `.app` + zip（或 dmg，非必须） |

macOS 工作量 **显著低于** Windows 从零实现（约 30–40%），但高于「只改编译目标」——文件选择器与条件编译是主要增量。

---

## 5. 建议实施顺序

1. 扩展 `IsActive` / `ShouldShowHud` / `Context` 门控 → **macOS Editor 可跑 Lab**
2. Editor 下文件选择用 `EditorUtility.OpenFilePanel`（快速验证）
3. Player 构建 + macOS 原生/UPM 文件对话框
4. `BuildCytoidLabMacOS` + shell 脚本
5. 更新 [cytoid-lab.md](cytoid-lab.md) + 可选 CI

---

## 6. 日志路径（macOS）

Windows Player log：

`%USERPROFILE%\AppData\LocalLow\TigerHix\Cytoid Lab\Player.log`

macOS Player log（待验证 bundle identifier `org.cytoid.lab`）：

`~/Library/Logs/TigerHix/Cytoid Lab/Player.log`

（Unity 默认路径取决于 `CompanyName` / `ProductName`；构建时需与 Windows 侧 `PlayerSettings` 对齐。）

---

## 7. 关键文件索引

| 文件 | 作用 |
|------|------|
| `engines/unity/Assets/Scripts/Navigation/CytoidLab/CytoidLabShell.cs` | 窗口 chrome、平台门控 |
| `engines/unity/Assets/Scripts/Navigation/CytoidLab/CytoidLabMenuController.cs` | 菜单、文件导入（comdlg32） |
| `engines/unity/Assets/Scripts/Navigation/CytoidLab/CytoidLabHudController.cs` | HUD、timeline |
| `engines/unity/Assets/Scripts/Context.cs` | Lab 初始化 `#if UNITY_STANDALONE_WIN` |
| `engines/unity/Assets/Scripts/Editor/CytoidCoreBuild.cs` | `BuildCytoidLabWindows64` |
| `engines/unity/build-cytoid-lab.ps1` | Windows 构建脚本 |
