# 第三方审核索引与审计记录 · Cytoid-private 2.1.5 Bug/Perf 方案

| 字段 | 内容 |
|------|------|
| 用途 | 第三方方案/证据审核的入口、导航与审计留痕 |
| 日期 | 2026-07-23 |
| 主文档 | [2026-07-23-private-2.1.5-bug-perf-design-review.md](./2026-07-23-private-2.1.5-bug-perf-design-review.md) |
| 文档状态 | **审核已完成 · 回复已归档 · 主文档已吸收 · 待 owner 批准** |
| 审核回复 | [2026-07-23-private-2.1.5-third-party-review-response.md](./2026-07-23-private-2.1.5-third-party-review-response.md)（Verdict: **Agree-with-changes**） |

---

## 0. 审核结果

第三方已按本索引逐项复核基线代码与 6 个补丁提交，确认 9 个 Must 均有代码证据，并提出 4 项事实纠错、2 项范围补强、4 项验收补强。主文档已全部吸收；独立回复保留原文，作为审计记录。

原始审核任务是：

1. 独立核对主文档中的 Must / Should / Cond / Defer 划分及附录 C 证据。
2. 复核 B12/B11/B16 升 Must、B13/B14 降 Defer、B09 不可直打 `900af401`。
3. 核对 B10 禁止只合半套、B06 默认不进 2.1.5。
4. 查找验收矩阵缺口并对开放问题投票。

本轮只整理文档，未修改生产代码。

---

## 1. 代码仓与分支（必读）

评审工作区用**同一 monorepo** 挂多个 remote：`private` = 商店完整客户端；`upstream`/`origin` 侧为 core / fork。审核 **2.1.5 private 方案**时，**事实基线是 `private/main`，不是当前工作区检出分支。**

| 角色 | Remote URL | 分支 / 提交 | 说明 |
|------|------------|-------------|------|
| **审核基线（private）** | `git@github.com:Cytoid/Cytoid-private.git`（remote 名 `private`） | **`private/main` @ `0e11d2c3f12ed61968aac3058272748045b27f3d`** | 2.1.4 BUILD.1 / VersionCode **123**；Unity **6000.0.58f2** |
| 对照 · core 加固源 | 同仓 `upstream` → `git@github.com:Cytoid/Cytoid.git` | 补丁提交见下表；`upstream/main` 头可能已前进 | 路径在 `engines/unity/Assets/Scripts/` |
| 对照 · 本地 fork | `origin` → `git@github.com:ky-reflection/Cytoid.git` | 审核时检出 `fix/correctness-hardening-unverified` | **勿当作 private 基线** |
| 文档中提及的对照分支名 | — | `fix/correctness-hardening-unverified` | 含已移植/验证中的 core 侧修复（如 `cb740083`） |

### 1.1 检出基线（只读核对）

```bash
cd /path/to/Cytoid
git fetch private main
git rev-parse private/main   # 期望 0e11d2c3f12ed61968aac3058272748045b27f3d

# 读文件（不切换工作树也行）
git show 0e11d2c3:Assets/Scripts/Storyboard/StoryboardRenderer.cs | less
```

**布局差异（极重要）：**

| | private (`0e11d2c3`) | core（本仓工作树 / upstream） |
|--|----------------------|--------------------------------|
| Unity 工程根 | 仓库根目录 | `engines/unity/` |
| 脚本前缀 | `Assets/Scripts/...` | `engines/unity/Assets/Scripts/...` |
| 音频 | `AudioManager` + Controller | `IAudioServer` / UnityAudioServer |
| 路径 | `Level.Path + rel` | `GameLaunchVfs` 等 |

不可整提交 cherry-pick；须按文件改编（主文档 §1.2）。

### 1.2 主文档引用的补丁提交（core 侧来源）

在本仓可用 `git show <hash>` / `git log -1 <hash>` 查看（已确认本地存在）：

| Hash | 摘要 | 主文档条目 |
|------|------|------------|
| `297f7536` | storyboard lifecycle / parsing / destroy | B01 B02 B07 B12 P01；B13 候选 |
| `cb740083` | scanner Y / dispose / HUD | B08 P02 P03；B11 提示；B10 纯函数化候选 |
| `900af401` | NLayer seek 复用 | B09（**方向可用，不可原样直打**） |
| `a6223bd7` | DragLine introRatio（#170） | B05 intro；outro 仍须 private 另补 |
| `831b64ac` | video prepare / timeline / VFS | B06 |
| `218ddeda` | AssetMemory `isLoading` finally | B16 部分；private 还须去 async void |

---

## 2. 文档与辅助产物索引

| # | 路径 | 给审核 agent 的用途 |
|---|------|---------------------|
| **D1** | `docs/reviews/2026-07-23-private-2.1.5-bug-perf-design-review.md` | **主审对象**：范围、方案、验收、风险（已吸收第三方意见） |
| **D2** | `docs/reviews/2026-07-23-private-2.1.5-THIRD-PARTY-REVIEW-INDEX.md` | **本文件**：仓/分支/证据导航 |
| **D2b** | `docs/reviews/2026-07-23-private-2.1.5-third-party-review-response.md` | 第三方审核回复（Verdict: Agree-with-changes） |
| **D4** | `docs/reviews/2026-07-23-private-module-code-audit.md` | **模块级审计**：健康度、跨模块增量（G-SPAWN 等）；不改 Must 基线 |
| **D3** | `docs/local/2026-07-17-core-leak-perf-audit.md` | 早期 core 泄漏/性能审计（背景；可能 gitignored） |
| **C1** | Cursor canvas：`cytoid-private-bug-perf.canvas.tsx` | 本机早期条目表（深度不及主文档；非发版依据） |
| **C2** | Cursor canvas：`cytoid-private-research.canvas.tsx` | 本机 private 调研总览（非发版依据） |
| **C3** | Cursor canvas：`cytoid-private-module-audit.canvas.tsx` | 本机模块审计总览（非发版依据） |
| **A1** | 仓库根 `AGENTS.md` | **core** 布局说明；与 private 路径不同，勿混用 |

---

## 3. 主文档阅读顺序（建议）

1. §0 评审结论与建议决议基线  
2. §0.4 二次复核范围变化 · **§0.6 模块审计增量**  
3. §3 Must（B01–B03, B05, B07, B08, B11, B12, B16）  
4. §4 Should（B04, B09, P01–P04）  
5. §5 默认延后项 / 条件重开项（B06, B10, B13, B14）  
6. §6 依赖矩阵 · §8 验收 · §9 风险 · **附录 C 证据索引**  
7. 需要模块全景时读 **D4**（包内 `04-module-code-audit.md`）

---

## 4. 证据文件清单（基线 `0e11d2c3`）

按条目快速跳转：

| 条目 | private 路径（`git show 0e11d2c3:<path>`） | 关键符号 / 核对点 |
|------|---------------------------------------------|-------------------|
| B01 B02 B03 B07 | `Assets/Scripts/Storyboard/Storyboard.cs` | `Templates[...]`；`note_map[...]`；`OnNoteClear`+`Triggers.Remove`；`AddListener(_ => Dispose())` |
| B12 | `Assets/Scripts/Storyboard/StoryboardRenderer.cs` | `TypedComponentRenderers` 用 model `typeof`；`DestroyObjectsById` 用 `it.GetType()` |
| B13 | `Assets/Scripts/Storyboard/Lines/LineStateParser.cs`；`…/Lines/LineEaser.cs` | Parser=`ParseObjectState`；Easer 无 X/Y/旋转/缩放消费 |
| B05 | `Assets/Scripts/Game/Notes/DragLineElement.cs` | `introRatio` / `outroRatio` 分母无保护 |
| B08 | `Assets/Scripts/Game/ObjectPool.cs` | `Dispose` 缺 `SpawnedDragLines`、`effectPoolItems` |
| B11 | `Assets/Scripts/Game/Notes/GameRenderer.cs` | 条件赋值 `cover`；`OnGameBeforeExit` 无条件 `DOFade` |
| B16 | `Assets/Scripts/Utils/AssetMemory.cs` | `isLoading`；`async void Task`+`WaitUntil`；递归 Load 漏 `useFileCacheOnly` |
| B04 | `Assets/Scripts/Navigation/NavigationBehavior.cs` | deep-link 全 tag flush；`Application.absoluteURL` 取 scheme |
| B09 | `Assets/Scripts/Utils/NLayerLoader.cs`；`Assets/Plugins/NLayer/MpegFile.cs` | seek 每次 `new MpegFile`；`_seekLock` 不含 Dispose |
| B10 | `Assets/Scripts/Game/Chart/Chart.cs`；`Assets/Scripts/Game/Elements/Scanner.cs` | `GetScannerPositionY` 内 `CurrentPageId=0`；唯一调用者 Scanner |
| B14 | `Assets/Scripts/AudioManager.cs`；`…/SettingsFactory.cs` | `androidAudioTrackCount=2`；RoundRobin 3–6；`Reserved3=2` |
| 版本 | `Assets/Scripts/Context.cs`；`ProjectSettings/ProjectVersion.txt` | Version **2.1.4** / Code **123**；Unity **6000.0.58f2** |

---

## 5. 最终建议范围快照（对照主文档 §0.2）

| 档 | ID |
|----|-----|
| **Must** | B01 B02 B03 B05 B07 B08 B11 **B12 B16** |
| **Should** | B04；B09（完整同步版）；P01–P04 |
| **Defer（默认）** | B06；B10；B13；B14；Note 热路径 P08 |
| **Out** | AudioServer / VFS / Lab seek / Bridge v2 |

B06/B10 只可按主文档 §5 的完整方案和独立门禁重新打开，不属于 2.1.5 基线。

发版门禁：主文档 §8 **#0–#9 或 #15** 任一失败 → 禁止打 2.1.5 tag；Should/条件重开项仅在实际合入时启用各自门禁。

---

## 6. 已知方法局限

- 主文档方法：**静态审计 + 调用链 + 与 core 对照**；**尚未** Profiler / 商店真机量化。  
- 频率（Med/Low）与 Alloc 估算不要当成线上度量。  
- private 发版无 CI；验收靠人肉矩阵（风险 R5）。

---

## 7. 原始交付格式（审计留痕）

第三方回复使用了如下结构：

```text
## Verdict: Agree | Agree-with-changes | Reject

## Confirmed (code-backed)
- …

## Disputed / needs fix in design doc
- [ID] claim → evidence → suggested rewrite

## Scope recommendation delta
- Must/Should/Defer 增减

## Acceptance gaps
- 门禁 #… 缺失场景

## Open questions vote
- B03 / B10 / B06 / B04 / B09 ：选项 + 理由
```

---

**索引结束。** 最终实施范围以主文档 §0.2 / §0.3 为准；独立意见以审核回复原文为准；代码证据以本索引 §4 与主文档附录 C 为准。
