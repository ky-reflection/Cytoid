# Cytoid Lab / Core 开发方向评审

> **Date:** 2026-07-01  
> **Branch:** `feature/cytoid-player`  
> **Scope:** 当前仓库实现、`feature/cytoid-player`、`feat/page-function-support`、`origin/fix/dragline-negative-duration`、现有 bug/audit 文档  
> **目的:** 给出 Cytoid Lab 与 Unity gameplay core 后续开发方向，避免把 Lab 专用修补、核心能力、WIP 分支和 bridge 产品化工作混在一起推进。

---

## 1. 执行结论

建议把接下来的工作分成两条主线：

1. **Cytoid Lab 作为核心验证与制谱预览工具**
   - Lab 应继续承担 timeline seek、storyboard resync、PageFunction/c2v3 验证、Windows standalone playtest 的职责。
   - Lab 代码可以保留 UI/HUD/build shell 的平台特化，但不应长期持有 gameplay 行为 fork。
   - 一旦 Lab 发现的是通用 gameplay/core 问题，应沉淀回 `Game` / `Chart` / `Note` / `Storyboard` 的共享实现。

2. **Unity core 作为嵌入式 gameplay runtime**
   - 继续围绕 Bridge v2、稳定 session lifecycle、deterministic result/telemetry、chart compatibility、resource cleanup 做核心能力。
   - Core 不应为了 Lab 快速修补而引入只在 Windows standalone 下成立的逻辑。
   - Resync、chart geometry、UI event reducer、storyboard disposal 这类能力应设计成 core-level services，Lab 只是主要调用者和验证入口。

短期优先级不是继续加新功能，而是先把 **resync / storyboard / material / object lifecycle** 这几个会影响 Lab 和 mobile core 的基础问题修稳。`feature/cytoid-player` 已经接近一个可用的 Lab v0.1.0，但它同时暴露出 core 里原本被移动端线性播放路径掩盖的问题。

---

## 2. 当前分支与实现信号

### 2.1 `feature/cytoid-player`

当前分支在 `origin/main` 之上叠加了 Cytoid Lab / Cytoid Player 系列提交：

- Windows standalone shell、menu、HUD、timeline slider；
- click-to-seek / drag-to-seek；
- active note set rebuild；
- drag chain fast-forward；
- hold progress restoration；
- storyboard renderer rebuild；
- HUD camera band 对齐；
- Lab build scripts 与 release docs。

这条分支的价值很明确：它把 Unity core 从“嵌入移动端运行一次 session”推向“可重复加载、seek、预览、调试”的开发工具场景。

风险也同样明确：

- timeline preview 和 full resync 是两套行为，preview 仍会触发普通 gameplay update；
- hold 只恢复 body progress，没有完整 visual fast-forward；
- storyboard resync 是静态对象重建，不 replay trigger history；
- build/output 层已经产品化，但部分 runtime 行为仍是临时方案。

### 2.2 `feat/page-function-support`

该分支包含两个不同性质的提交：

| 提交 | 价值 | 建议 |
|------|------|------|
| `7414e519 feat(chart): add Cylheim PositionFunction support` | PageFunction 模型和坐标管线重构雏形 | 可 cherry-pick 思路，但必须补真实数学、scanner cursor 修复和 Lab 验证 |
| `33a7655f WIP: local changes` | 混入 storyboard、screen、video、scanner、postprocess 等 WIP | 不应整提交合并，应拆成独立 bugfix/perf PR |

`7414e519` 不是完整 c2v3 实现。它提供字段和 hook，但 `EvaluatePagePositionProgress` 仍是占位，不能直接作为 feature 完成标准。

### 2.3 `origin/fix/dragline-negative-duration`

该 bugfix 已进入当前 `origin/main` 历史，对 Lab 的意义是：drag line 负 duration 已有基础 guard。后续 Lab resync 对 drag line 的修复应建立在 main 现有 guard 之上，不需要重复开分支。

### 2.4 现有文档信号

| 文档 | 信号 |
|------|------|
| `storyboard-memory-performance-audit.md` | Storyboard 销毁路径、事件监听、ObjectPool、GC 是 core 稳定性主风险 |
| `2026-07-01-cytoid-lab-resync-system-review.md` | Lab seek 暴露出 core 缺少 target-time state reducer |
| `2026-07-01-cytoid-lab-hold-seek-investigation.md` | Hold bug 是 resync 系统缺口的高可见症状 |
| `2026-07-01-c2v3-research-plan.md` | c2v3 不只是 chart parsing，还依赖 scanner/page geometry、UI event reducer、seek catch-up |
| `host-protocol-v2.md` | Bridge runtime 已朝 resident、typed、single-terminal-outcome 方向收敛 |
| `mock-engine.md` | 测试/Flutter UI 开发还缺 first-class mock runtime |
| `2026-06-20-play-events-anti-cheat-followup.md` | Core 未来需要 deterministic replay input，但当前 telemetry 还不是 anti-cheat |

---

## 3. 推荐产品定位

### 3.1 Cytoid Lab

Lab 应定位为：

- chart author preview tool；
- core developer verification harness；
- Windows-first standalone build；
- timeline scrub / c2v3 / storyboard / renderer regression testbed；
- 不面向普通玩家替代主 Cytoid app。

Lab 应该优先强化：

1. 导入和选择谱面；
2. timeline seek 的可信度；
3. storyboard / PageFunction / UI event 可视化验证；
4. debug overlays（note id、timing、page/function、trigger state）；
5. 一键复现与日志采集。

Lab 不应该优先做：

- 排行榜、账号、上传；
- 完整移动端 shell 行为；
- 与 Flutter 主 app 重复的玩家 UI；
- 大规模平台扩展，除非 Windows 路径稳定。

### 3.2 Unity Core

Core 应定位为：

- embeddable gameplay runtime；
- deterministic chart renderer / judge / result producer；
- Bridge v2 protocol implementation；
- Flutter shell / future engine adapter 的可替换核心。

Core 应优先强化：

1. chart compatibility（c2v3 PageFunction、UI events）；
2. session lifecycle correctness；
3. memory/resource cleanup；
4. deterministic result and optional playEvents；
5. runtime diagnostics and logs；
6. mock/runtime testability。

---

## 4. 技术方向

### 4.1 Resync 从 Lab patch 升级为 core service

当前 `Game.CytoidLab.cs` 已经包含 target-time rebuild 的雏形，但它仍是 Lab partial。建议将能力逐步抽象成 core-level service：

```text
GameTimeState
ChartCursorState
GameplayJudgementState
ActiveNoteSetState
VisualFastForwardState
StoryboardState
ChartUiEventState
```

短期不一定需要引入大框架，但应避免继续把所有逻辑堆在 `Game.CytoidLab.cs`。

建议目标：

- preview 不产生 gameplay mutation；
- full resync 能稳定继续播放；
- active notes、drag lines、hold visuals 有统一 fast-forward 语义；
- storyboard / chart UI event 明确是 snap 到稳态还是 replay 历史；
- Lab 和未来 automated tests 能直接调用 reducer。

### 4.2 Storyboard 先修 bug，再谈能力扩展

Storyboard 审计已经列出大量 P0/P1。建议不要把 `33a7655f` 这类 WIP 直接并入主线，而是拆分：

1. P0 crash fixes：
   - `DestroyObjectsById` typed key；
   - `OnNoteClear` 枚举期间移除；
   - destroy parent 后 typed registry 残留；
   - `ParseTime` missing note guard；
   - `async void SpawnObjectById`。
2. P1 lifecycle fixes：
   - `Dispose` 重入保护；
   - listener 对称移除；
   - renderer 子类调用 `base.Dispose()`；
   - static storyboard references cleanup。
3. P2 performance：
   - `FindStates` 二分或缓存；
   - 每帧 LINQ/alloc 清理；
   - video/RT/postprocess lifecycle。

只有当 P0/P1 稳定后，Lab 的 storyboard resync 才有可靠基础。

### 4.3 c2v3 先做 PageFunction，再做 UI Animation

推荐顺序：

1. PageFunction MVP：
   - `ChartModel.Page.PositionFunction`；
   - `Chart` visual progress API；
   - note/scanner/boundary/holdlength 全走 visual progress；
   - `GetScannerPositionY` 去副作用；
   - Lab 测试谱验证。
2. Resync 前置：
   - chart event state catch-up hook；
   - preview mutation isolation；
   - full resync render/update 语义明确。
3. UI Animation MVP：
   - `ChartUiController`；
   - target registry；
   - type 2–7 reducer；
   - `is_start_without_ui`；
   - seek 后 snap 稳态。

不要把 PageFunction 和 UI Animation 捆成一个大 PR。PageFunction 是 geometry 基础，UI Animation 是 state/event reducer；风险不同。

### 4.4 Bridge/Core 不要被 Lab 打断

当前 `feature/cytoid-player` 已包含最新 `origin/main` 的 Bridge v2 方向。后续 Lab 工作应遵守：

- 不改变 host protocol，除非 session/result payload 真的需要新字段；
- Lab-specific flags 不进入 Flutter bridge public API；
- Bridge runtime lifecycle、resident Unity activity、session failure synthesis 保持稳定；
- Lab build 与 plugin artifact build 分开，不交叉污染 scenes/defines。

### 4.5 Mock engine 与测试能力需要补齐

当前 mock engine 文档已经指出 native mock 与 desired pure Dart mock 的差距。建议把它作为 Flutter/Core 协作的中期工程：

- Dart scenario-driven mock；
- native Android/iOS mock 与 Dart fixtures 对齐；
- runtime failure、session result、settings apply、logs、telemetry canonical scenarios；
- Flutter UI 可以不依赖 Unity artifacts 开发。

这对 Lab 不是直接功能，但能降低 bridge/core 迭代成本。

---

## 5. 分支策略

### 5.1 `feature/cytoid-player`

建议继续作为 Lab 集成分支，但不要无限期承载所有 core fix。

下一步：

1. 修 Lab P0 bug；
2. 把通用 core fix 拆成小 PR 进 `main`；
3. Lab shell/build/docs 最后作为独立 feature PR；
4. 避免在同一个 PR 混合 storyboards、c2v3、bridge、Lab UI。

### 5.2 `feat/page-function-support`

建议废弃“按分支合并”的想法，改为抽取：

- 从 `7414e519` 抽模型字段和 API 方向；
- 重写数学和 scanner cursor 修复；
- `33a7655f` 拆成 storyboard/perf/screen 小补丁，逐个 review。

### 5.3 bugfix 分支

继续保持小而独立：

- gameplay crash；
- renderer lifecycle；
- resource leak；
- bridge runtime failure；
- artifact packaging。

每个 bugfix 应带明确验证路径：Unity Editor / Lab Windows / Flutter plugin test / native unit test。

---

## 6. 里程碑建议

### M0：文档和验证基线

目标：让团队对当前方向有共同语言。

- 保留并维护 resync / hold / c2v3 / storyboard audit 文档；
- 为 Lab 固定 2–3 个测试谱；
- 建立手工 smoke checklist；
- 确认 Windows Lab build 可重复产出。

### M1：Lab 稳定化

目标：v0.1.x 可作为开发工具使用。

优先修：

1. Preview gameplay mutation 隔离；
2. `ProgressRing` material isolation；
3. hold full visual fast-forward；
4. full resync 最后一帧 Auto 语义；
5. drag / hold / storyboard seek regression checklist。

产出：

- Cytoid Lab v0.1.1；
- resync system issues 收敛；
- Windows standalone 可用于 c2v3 验证。

### M2：Core cleanup 第一批

目标：修掉会影响多 session / retry / Lab reload 的 core 缺陷。

优先修：

1. Storyboard P0；
2. `ObjectPool.Dispose` effect pool；
3. event listener lifecycle；
4. `GetScannerPositionY` cursor 副作用；
5. `Game.Start` failure cleanup。

产出：

- 更稳定的 `main`；
- Lab 和 Flutter plugin 都受益；
- 为 PageFunction/c2v3 减少干扰变量。

### M3：PageFunction MVP

目标：支持 Cylheim / Cytus II style PageFunction 的核心几何。

范围：

- model field；
- visual progress math；
- note/scanner/boundary/holdlength；
- input smoke；
- Lab timeline scrub 验证。

不包含：

- UI Animation；
- type 8 custom message；
- unrelated storyboard WIP。

### M4：Chart UI Event MVP

目标：支持 c2v3 type 2–7 的可 seek UI state。

范围：

- `ChartUiController`；
- event reducer；
- target registry；
- `is_start_without_ui`；
- seek rebuild steady state；
- 先支持 0/1/4/5/7，其他目标可标记 partial。

### M5：Bridge/Core testing maturity

目标：降低 Unity artifact 和设备依赖。

范围：

- scenario-driven mock；
- Flutter plugin test fixtures；
- runtime failure cases；
- result/session schema tests；
- optional playEvents replay metadata follow-up。

---

## 7. 推荐优先级矩阵

| 优先级 | 工作 | 理由 |
|--------|------|------|
| P0 | Lab preview mutation isolation | 当前 seek 不是可信预览，会污染状态 |
| P0 | ProgressRing material isolation | 高可见 bug，影响 hold/long hold |
| P0 | Storyboard P0 crash fixes | 影响 trigger、retry、resync，可能刷 NRE |
| P0 | `GetScannerPositionY` cursor 副作用 | PageFunction、input、boundary、Lab seek 的共同 blocker |
| P1 | Hold visual fast-forward | 修复 Lab 当前核心体验 |
| P1 | ObjectPool / listener lifecycle | 多局、retry、Lab reload 稳定性 |
| P1 | PageFunction MVP | c2v3 geometry 基础 |
| P2 | Chart UI event reducer | c2v3 完整体验，但依赖 resync/event steady state |
| P2 | Pure Dart scenario mock | 提升 Flutter/core 测试效率 |
| P3 | macOS Lab | 有价值，但应等 Windows Lab 稳定后做 |

---

## 8. 不建议的方向

- 不建议把 `feat/page-function-support` 整体 merge 到 Lab 分支。
- 不建议先做 UI Animation，再修 scanner cursor 和 resync steady state。
- 不建议把 Lab preview 的临时 flag 暴露到 Bridge API。
- 不建议为了 Lab 复制一套 gameplay judge/renderer 逻辑。
- 不建议在 storyboard P0 未修前继续加复杂 storyboard resync 功能。
- 不建议把 anti-cheat/client signing 当作近期重点；当前有价值的是 replay metadata 和 deterministic input。

---

## 9. 需要的决策

| 决策 | 推荐 |
|------|------|
| Lab 是否进入 main | 是，但先拆 core fixes，再合 Lab shell |
| Lab 定位 | 开发/制谱工具，不是玩家客户端 |
| PageFunction 合入方式 | 不整分支合并，按 Slice A 重写/补全 |
| UI Animation seek 语义 | seek 后 snap 到稳态，不 replay 动画 |
| Storyboard trigger seek | 先文档化“静态重建”，再决定是否 replay history |
| Bridge 中是否暴露 Lab 能力 | 否 |
| macOS Lab | P3，等 Windows 稳定后做 |

---

## 11. 实现备注：GameplayHostContext（2026-07-01）

为减少 Lab 对 core 的直接侵入，已引入 `GameplayHostContext`（`engines/unity/Assets/Scripts/Game/GameplayHostContext.cs`）：

- **坐标系**：`CoordinateScreenHeightPx` — Lab 注册游玩区高度；默认 `Screen.height`
- **预览行为**：`ShouldSuppressResultSplashes` — Lab 跳过 MAX/FC 动画
- **布局钩子**：`OnCoverLoaded` / `OnStoryboardSpawnComplete` — Lab 刷新 play viewport
- **启动**：`EnsureHostInitialized` / `EnsureDebugNavigationHost` / `TryApplyGraphicsQuality`

Lab 侧统一在 `CytoidLabHostRegistration.cs` 注册；core 文件仅保留带注释的 hook 调用点。Timeline resync 等仍用 `*.CytoidLab.cs` partial 扩展。

---

## 11. 相关文档

- [Cytoid Lab](cytoid-lab.md)
- [Timeline Resync 系统评审](2026-07-01-cytoid-lab-resync-system-review.md)
- [Hold seek 问题调研](2026-07-01-cytoid-lab-hold-seek-investigation.md)
- [c2v3 特性调研方案](2026-07-01-c2v3-research-plan.md)
- [Storyboard / Memory / Performance 审计](storyboard-memory-performance-audit.md)
- [Mock Engine 设计说明](mock-engine.md)
- [Play Events & Anti-Cheat Follow-up](2026-06-20-play-events-anti-cheat-followup.md)
