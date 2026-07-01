# c2v3 特性调研方案（Page Function + UI Animation）

> **Date:** 2026-07-01  
> **Branch context:** `feature/cytoid-player`（当前集成验证分支）  
> **Prior work:** `feat/page-function-support`（commits `7414e519`, `33a7655f`）  
> **Spec reference:** [C2 谱面格式详解 — 页面](https://cytoid.wiki/zh/reference/chart/c2-format)

---

## 1. 背景与规格摘要

### 1.1 c2v3 与当前支持的差异

| 维度 | Cytus II 官方 / Wiki c2 | Cytoid 当前 (`feature/cytoid-player`) | c2v3 目标 |
|------|-------------------------|--------------------------------------|-----------|
| 解析方式 | JSON | `JsonConvert.DeserializeObject<ChartModel>`（`Chart.cs`），无独立 Cylheim 解析器 | 同路径，扩展模型字段 |
| `format_version` | 官方取 `1`，无实际影响 | **未建模** | 可选记录，不影响运行时 |
| 页面 `PositionFunction` | `Arguments[0]` 缩放、`Arguments[1]` 偏移（线性 `y=ax+b`） | **完全未实现**；Y 坐标纯 tick 线性映射 | 页面级非线性/缩放/偏移坐标 |
| `event_list` UI 事件 (type 2–7) | 显示/隐藏/渐入渐出/动画显示隐藏，目标 UI 用 `"0,1,6"` 组合 | **仅 type 0/1**（加速/减速提示） | 完整 chart UI 动画 |
| `is_start_without_ui` | 无 UI 开局 | **未建模、未实现** | 开局隐藏全部 Gameplay UI |
| event type 8 自定义文字 | `"message,#FFFFFF"` | **未实现** | 可选后续 |
| Cytoid 扩展字段 | — | `music_offset`、`display_boundaries`、per-note `approach_rate` 等已支持 | 保持兼容 |

**结论：**「c2v3」在本仓库语境下主要指 **Cylheim 扩展 c2**——在标准 page/note/event 之上增加 `PositionFunction` 与完整 UI 事件语义；并非新的文件格式，而是 JSON 字段补全 + 运行时行为对齐 Cytus II。

### 1.2 Wiki 规格要点

#### 页面 `PositionFunction`

- `Arguments`：原则上 2 个 `double`
  - **参数 1（a）**：页面缩放倍率，以页面半高为中心缩放；默认 `1.0`；下落式谱面常见 `0`，但这不等价于“音符堆叠不动”，需要额外定义下落运动模型
  - **参数 2（b）**：纵向偏移，以「半个原始页面高度」为单位；`1.0` 上移半页，`-1.0` 下移半页
- 语义：`y = a·x + b`，其中 `x` 为页内 tick 进度

#### UI 事件（`event_order_list` → `event_list`）

| type | 含义 | args 格式 |
|------|------|-----------|
| 0/1 | 速度升/降（已有） | `"W"`/`"R"`/`"G"` |
| 2 | 显示 UI | `"0,1,6"` 等 |
| 3 | 隐藏 UI | 同上 |
| 4 | 渐入 UI | 同上 |
| 5 | 渐出 UI | 同上 |
| 6 | 动画显示 UI | 同上（可覆盖重复执行） |
| 7 | 动画隐藏 UI | 同上 |
| 8 | 自定义文字 | `"text,#RRGGBB"`（Cytoid wiki 标注暂不支持） |

**目标 UI 编号：**

| ID | 元素 |
|----|------|
| 0 | 连击数 |
| 1 | 分数 |
| 2 | 曲名和图标 |
| 3 | 难度 |
| 4 | 扫描线 |
| 5 | 边界线 |
| 6 | 声音频谱 |
| 7 | 进度条 |

**冲突语义：** 显示/隐藏/渐入/渐出按队列依次执行；动画显示/隐藏可覆盖且可重复。

---

## 2. 本地先行工作（`feat/page-function-support`）

### 2.1 Commit `7414e519` — PositionFunction 基础设施

**变更文件**（3 个，+81/−45 行）：

| 文件 | 变更摘要 |
|------|----------|
| `engines/unity/Assets/Scripts/Game/Chart/ChartModel.cs` | 新增 `PagePositionFunction`（`Type` + `Arguments`）；`Page.position_function` 带 `[JsonProperty("PositionFunction")]` |
| `engines/unity/Assets/Scripts/Game/Chart/Chart.cs` | `EvaluatePagePositionProgress()` 钩子；坐标方法改走 progress；新增 `GetPageBoundaryPosition()` |
| `engines/unity/Assets/Scripts/Game/Notes/GameRenderer.cs` | 边界线位置改按当前页调用 `GetPageBoundaryPosition` |

**关键事实：** `EvaluatePagePositionProgress` **仍是占位实现**——`type==0` 或 `null` 时返回线性 `rawProgress`，非零 type 分支存在但同样返回 `rawProgress`，**未应用 `Arguments[0/1]` 的缩放/偏移数学**。`GetPageBoundaryPosition` 同样为占位。

### 2.2 Commit `33a7655f` — WIP（与 c2v3 无直接关系）

修改 16 个文件（Storyboard、VideoRenderer、ScreenManager、EffectController 等），属于 **storyboard/内存/性能** 本地 WIP，**不含** Chart/Page Function 变更。合并时应单独评估，不宜与 `7414e519` 捆绑。

### 2.3 与 `feature/cytoid-player` 的分叉状态

- 共同祖先：`a6223bd7`
- `feature/cytoid-player` 在祖先之后有 15+ commits（Cytoid Lab 全套、timeline scrub、HUD camera bands 等）
- `feat/page-function-support` 仅有 2 commits 超前
- **当前 player 分支 `ChartModel.Page` 无 `position_function` 字段**

---

## 3. 当前代码库状态（`feature/cytoid-player`）

### 3.1 `ChartModel.Page` 定义

路径：`engines/unity/Assets/Scripts/Game/Chart/ChartModel.cs`

当前字段：`start_tick`、`end_tick`、`scan_line_direction`、`start_time`、`end_time`、`actual_start_tick`、`actual_start_time`、`Duration`。无 `PositionFunction`。

`ChartModel.Animation` 类存在但根模型无 `animation_list` 字段，全库无引用——与 chart `event_list` UI 动画 **不是同一路径**。

### 3.2 坐标管线（`Chart.cs`）

| 方法 | 作用 | 当前实现 |
|------|------|----------|
| `GetNoteScreenY` | 音符世界 Y | 页内 tick 线性 |
| `GetNoteChartY` | 音符归一化 Y | 同上线性 |
| `ConvertChartTickToScreenY` | 任意 tick → 屏幕 Y | 按 tick 定位 page，线性 |
| `GetScannerPositionY` | 扫描线 Y | 线性；**每帧 `CurrentPageId = 0` 重扫**（已知 bug，见 PF-2） |
| `GetBoundaryPosition` | 上下边界线 | 固定全屏 play area 上下沿 |
| hold `holdlength` | Hold 视觉长度 | `hold_tick / page_tick_span` 线性比例 |

### 3.3 局内 UI 元素清单

Gameplay overlay 挂在 `Game.unity` 场景 + `GameObjectProvider`：

| Wiki UI ID | 代码组件 | 现有动画能力 |
|------------|----------|--------------|
| 0 连击 | `ComboText` | DOTween punch/fade（combo 变化驱动） |
| 1 分数 | `ScoreText` | DOTween punch |
| 2 曲名 | `TitleText` | 静态文本，无显隐 API |
| 3 难度 | `DifficultyPill` | `TransitionElement` + DOTween fade |
| 4 扫描线 | `Scanner` | 入场/退场/变速 coroutine；`Mod.HideScanline` |
| 5 边界 | `GameRenderer` | `DOFade` 入场 |
| 6 频谱 | `BeatPulseVisualizer` | `DOFade` pulse（`StartPulsing` 未在游戏流程调用） |
| 7 进度条 | `GameProgressIndicator` | `DOWidth` 跟 chart 进度 |

**Storyboard canvas UI** 与 chart `event_list` 的 **内置 Gameplay HUD** 是两套系统。UI 动画实现应落在 Game overlay 层。

### 3.4 事件处理现状（`Game.cs`）

- 仅处理 type 0/1，else 分支把 **所有非 0 事件当作减速**
- 每 tick 只读 `event_list[0]`，忽略同 tick 多事件
- 无 UI 目标解析、无队列/冲突语义

### 3.5 动画基础设施

- **DOTween**：广泛用于 UI 组件
- **TransitionElement / CleanTitleTransitionElement**：Screen 进出场模式，可复用为 chart UI「动画显示/隐藏」
- **Storyboard**：独立 trigger/element 动画，与 chart UI 事件无连接

### 3.6 解析器

无 Cylheim 专用解析器；`Chart` 构造函数直接 `DeserializeObject<ChartModel>`。`PositionFunction` 的 JSON 键名须与 Cylheim 一致（`Type`/`Arguments` PascalCase）。

---

## 4. Page Function 实施调研

### 4.1 规格解读与数学模型（待验证）

建议实现模型（需用 Cylheim 导出谱面 + Cytus II 录像对比校验）：

```
rawProgress = (tick - page.start_tick) / (page.end_tick - page.start_tick)  // ∈ [0,1]
a = Arguments[0]  // default 1.0
b = Arguments[1]  // default 0.0，单位 = 半页高度

visualProgress = a * rawProgress + b  // 待与官方对齐

screenY = verticalRatio * direction * (-baseSize + 2*baseSize*visualProgress) + verticalOffset
```

**边界线：** 缩放后扫描线仅在缩放后高度范围运行 → `GetPageBoundaryPosition(page, bottom)` 应返回 **该页有效视觉带的上下沿**。

**Hold 长度：** 分支已改为 `progressDelta = endProgress - startProgress`（正确方向）；player 分支仍用 tick 线性比。

**下落式（a=0）注意：** 不能简单解释为“所有 tick 映射到同一 visualProgress → 音符堆叠”。`a=0` 只说明 page position function 的静态 tick→page-progress 项退化；真正的下落式应当让音符随时间向判定线目标位置运动。具体是 scanner 静止、note 下落，还是 note/scanner 双方共同运动，需要用 Cylheim 导出谱面和 Cytus II 行为对比确认后再定实现。

建议把下落式拆成两个模型评估：

| 模型 | 说明 | 风险 |
|------|------|------|
| 静态 PageFunction 模型 | 仅按 `visualProgress = a * rawProgress + b` 计算几何位置 | `a=0` 会退化为同 Y 带，无法表现“下落” |
| 运行时下落模型 | `PositionFunction` 给出判定线/目标带，note 根据 `start_time - Game.Time` 从 intro 位置向目标位置插值 | 需要改 note position runtime、intro/AR、drag/hold line、seek fast-forward |

**当前建议：** PageFunction MVP 不应把 `a=0` 直接实现成堆叠；应先以测试谱确认下落语义。若缺少证据，先标记 `a=0` 为 partial/experimental，避免错误兼容。

### 4.2 差距分析

| 项目 | 分支 `7414e519` | player 分支 | 完整 c2v3 |
|------|----------------|-------------|-----------|
| JSON 反序列化 | ✅ | ❌ | ✅ |
| 坐标钩子重构 | ✅ | ❌ | ✅ |
| Arguments 数学 | ❌ 占位 | ❌ | ✅ |
| `GetPageBoundaryPosition` 真实逻辑 | ❌ 占位 | ❌ | ✅ |
| `GameRenderer` 边界跟随当前页 | ✅ | ❌ | ✅ |
| `GetScannerPositionY` CurrentPageId bug | ❌ 未修 | ❌ 未修 | ✅ 应一并修 |
| 输入判定（跨页 `Page.Duration/2`） | 未改 | 现状 | ⚠️ 非线性页可能需改为位置判定 |
| Cytoid Lab timeline scrub | player 有 | — | 需验证 seek 后坐标一致 |
| Flutter Bridge 影响 | 无协议变更 | — | 坐标变化对移动端同样生效 |

### 4.3 实施阶段

**Phase 0 — 分支整合（0.5–1 天）**

- 将 `7414e519` cherry-pick/rebase 到 `feature/cytoid-player`
- **不要**合并 `33a7655f`（除非单独评审 storyboard WIP）
- 解决 `Chart.cs` 与 Lab HUD camera bands（`CytoidLabShell.GetCoordinateScreenHeightPx`）的冲突

**Phase 1 — 核心数学（2–3 天）**

- 实现 `EvaluatePagePositionProgress` 线性 type（含 a/b）
- 同步 `GetPageBoundaryPosition`
- 修复 `GetScannerPositionY`：使用局部 page 索引，禁止重置共享 `CurrentPageId`（PF-2）
- 单元/快照测试：固定 page + tick → 期望 Y 值

**Phase 2 — 下游消费者（1–2 天）**

- `GameRenderer.OnUpdate` 边界跟随当前页
- 验证 hold/drag line/intro_time 视觉
- 评估 `CalculateNoteSpeed` 是否需 PositionFunction 感知

**Phase 3 — 输入与边缘情况（1–2 天）**

- `InputController`、`DragHeadNote`、`DragChildNote` 的跨页触摸逻辑
- 非连续 page、`scan_line_direction` 非 ±1（Cytoid 扩展）
- Bridge-embedded 与 Editor Play Mode 回归

**Phase 4 — 合入 main（0.5 天）**

- 经 Cytoid Lab 人工验收后 PR 到 `main`

### 4.4 需修改文件

| 优先级 | 文件 |
|--------|------|
| P0 | `ChartModel.cs`、`Chart.cs` |
| P0 | `GameRenderer.cs` |
| P1 | `Scanner.cs` |
| P1 | `InputController.cs`、`DragHeadNote.cs`、`DragChildNote.cs` |
| P2 | `Game.CytoidLab.cs` |
| P2 | `Note.cs` 及 renderer partials |

### 4.5 向后兼容

- `position_function == null` 或 `type == 0` 且默认 Arguments → **与现有线性行为一致**
- 旧谱面无字段 → 零行为变化

### 4.6 测试策略（Cytoid Lab）

1. 准备测试谱：线性页 / `Arguments[0]=0` 下落页 / 带偏移页（Cylheim 导出）
2. Lab 导入 → 各难度试玩
3. **Timeline scrub**：拖 slider 观察音符 Y、扫描线、边界线、hold 长度是否连续无跳变
4. seek 到页中/页边界重点验证
5. 对比 Cytus II / 旧 Cytoid 线性行为截图
6. 确认 PF-2 修复后无性能回归

### 4.7 风险

| 风险 | 严重度 | 缓解 |
|------|--------|------|
| Wiki 公式与 Cylheim 实际不一致 | 高 | 用真实谱面 + 录像逐帧对比 |
| `GetScannerPositionY` 破坏 `CurrentPageId` | 高（已有） | Phase 1 必修 |
| 非线性页输入误判 | 中 | Phase 3 专项 |
| 与 Lab HUD camera bands 交互 | 中 | `GetCoordinateScreenHeightPx` 已有 Lab 分支 |
| `7414e519` 与 player 分支 merge 冲突 | 中 | 先 rebase player 再 cherry-pick |

---

## 5. UI Animation 实施调研

### 5.1 规格解读

- Chart 事件驱动 **内置 Gameplay HUD**，不是 storyboard
- 目标 UI 通过 `args` 逗号分隔 ID 字符串指定
- type 2–5：instant / fade，**顺序执行、不重复**
- type 6–7：动画显示/隐藏，**可覆盖、可重复**
- `is_start_without_ui`：开局隐藏全部 HUD

### 5.2 差距分析

| 能力 | 现状 |
|------|------|
| type 0/1 速度事件 | ✅ |
| type 2–7 UI 事件 | ❌ |
| UI 目标注册表 | ❌ |
| 事件队列/冲突 | ❌ |
| 同 tick 多 `event_list` 项 | ❌ 只读 `[0]` |
| `is_start_without_ui` | ❌ |
| seek/scrub 后 UI 状态 | ❌ 重置 event 索引但不重算 UI 稳态 |
| type 8 自定义文字 | ❌ |

### 5.3 实施阶段

**Phase 0 — 模型与注册（1 天）**

- `ChartModel` 增加 `is_start_without_ui`（可选）
- 新建 `ChartUiController`（建议 `Game/Elements/`）
- 注册表：`Dictionary<int, IChartUiTarget>` 映射 0–7 到场景组件

**Phase 1 — 事件调度器（2–3 天）**

- 替换 `Game.Update` 中 event 循环
- 解析 `args` → 目标 ID 列表
- 每目标维护队列（type 2–5）与动画槽（type 6–7）
- 同 tick 遍历完整 `event_list`

**Phase 2 — 各 UI 目标适配（2–3 天）**

| ID | 适配要点 |
|----|----------|
| 0 ComboText | `CanvasGroup` 或 `text.DOFade` |
| 1 ScoreText | 同上 |
| 2 TitleText | 需补充曲绘/icon |
| 3 DifficultyPill | 已有 `TransitionElement` |
| 4 Scanner | `lineRenderer.enabled` + enter/exit coroutine |
| 5 Boundaries | `GameRenderer` 暴露显隐 API |
| 6 BeatPulseVisualizer | 需先接 BPM；chart 控制显隐 |
| 7 GameProgressIndicator | `CanvasGroup` fade |

**Phase 3 — 开局与 seek（1–2 天）**

- `is_start_without_ui` → 开局前批量 Hide
- `Game.CytoidLab.ResyncPlayfieldToTime`：根据 `targetTime` 重算各 UI 元素**稳态**（显示/隐藏），而非重播动画

**Phase 4 — Bridge 与 Mod 交互（1 天）**

- `Mod.HideScanline` 与 chart 事件优先级
- Tier/Calibration 模式跳过 UI 事件
- Bridge-embedded：Flutter 壳已有 UI 时 chart 事件可能需 no-op（需产品决策）

### 5.4 需修改文件

| 文件 | 作用 |
|------|------|
| **新建** `ChartUiController.cs` | 事件调度 + 目标注册 |
| `ChartModel.cs` | `is_start_without_ui` |
| `Game.cs` | 事件循环、开局钩子 |
| `Game.CytoidLab.cs` | seek 后 UI 稳态 |
| `ComboText.cs`、`ScoreText.cs`、`TitleText.cs` 等 | 实现 `IChartUiTarget` |
| `GameRenderer.cs`、`Scanner.cs`、`BeatPulseVisualizer.cs` | 显隐 API |

### 5.5 测试策略

1. 构造最小 event 测试谱（每 type 各一条，单/多目标组合）
2. Cytoid Lab 播放 + **timeline scrub** 到事件前后，检查 UI 稳态
3. 冲突用例：同 UI 连续 hide → fadeOut → show
4. type 6/7 连续触发验证覆盖
5. `is_start_without_ui` 开局截图对比

### 5.6 风险

| 风险 | 说明 |
|------|------|
| 曲绘/icon（UI #2） | 场景可能无独立 icon 元素，需预制体改动 |
| BeatPulse 未接入 | 频谱 UI 可能长期不可见 |
| seek 动画重放 | 官方行为应为稳态而非重播 |
| Bridge HUD 重叠 | Flutter 壳已有 UI 时 chart 事件可能重复 |
| type 8 范围蔓延 | 建议 MVP 排除 |

---

## 6. 共享关切

### 6.1 迁移到 main 的路径

推荐顺序：

1. **Page Function**（坐标基础）
2. **修复 PF-2**（可随 Page Function 一并合入）
3. **UI Animation**
4. `33a7655f` storyboard WIP **单独分支/PR**

合并策略：`feature/cytoid-player` 作为集成分支 → 验证通过后 PR 到 `main`。

### 6.2 Cytoid Lab 作为验证平台

Lab 已具备 timeline scrub/seek、storyboard resync、hold/drag 进度恢复。详见 [cytoid-lab.md](cytoid-lab.md)。

- Page Function 重点：**scrub 过程中音符/扫描线/边界连续性**
- UI Animation 重点：**scrub 跨事件点后 HUD 稳态是否正确**

### 6.3 里程碑与工时估算

| 里程碑 | 内容 | 估算（1 人） |
|--------|------|-------------|
| M1 | cherry-pick `7414e519` + merge 冲突解决 | 0.5–1 天 |
| M2 | Page Function 数学 + 边界 + PF-2 修复 | 2–3 天 |
| M3 | 输入/边缘 + Lab 验收 + PR | 1–2 天 |
| M4 | ChartUiController + 事件调度 | 2–3 天 |
| M5 | 8 类 UI 目标适配 + 开局/seek | 2–3 天 |
| M6 | Lab 全量 UI 事件测试 + main PR | 1–2 天 |
| **合计** | Page Function + UI Animation | **约 9–14 人天** |

---

## 7. 实现评审补充（2026-07-01）

> 本节基于当前 `feature/cytoid-player` 代码复核，并再次核对 Cytoid Wiki C2 规格页。规格页确认：`PositionFunction.Type` 当前只有 `0`；`Arguments[0]` 为页面缩放倍率，`Arguments[1]` 为半页单位纵向偏移；UI event type 2–7 分别控制 Gameplay UI 显示/隐藏/渐入/渐出/动画显示/动画隐藏，且 type 2–5 与 type 6–7 位于不同层序。

### 7.1 Page Function 不能只 cherry-pick 基础设施

`7414e519` 适合作为结构参考，但不能作为可用实现直接合入：

- `ChartModel.Page` 增加 `PositionFunction` 是必要的；
- `Chart.cs` 的坐标管线改造是必要的；
- 但 `EvaluatePagePositionProgress` 在该分支仍是占位逻辑，未实际使用 `Arguments[0/1]`；
- `GetPageBoundaryPosition` 若仍是占位，会导致扫描线与边界线语义不一致；
- `GameRenderer` 边界跟随 current page 后，还必须避免 `GetScannerPositionY` 修改共享 `CurrentPageId`。

**建议：** cherry-pick 前先把 `7414e519` 拆成“模型/接口改造”和“真实数学实现”两个 review 单元。只合入模型字段没有价值；必须与数学、边界、scanner state 修复同批验证。

### 7.2 当前 `GetScannerPositionY` 是 P0 阻塞

当前 `Chart.GetScannerPositionY(time, useScannerSmoothing)` 内部会：

```csharp
CurrentPageId = 0;
while (CurrentPageId < Model.page_list.Count && time > Model.page_list[CurrentPageId].end_time)
    CurrentPageId++;
```

这使一个“查询扫描线位置”的函数修改 chart playback cursor。正常播放时，`Game.Update` 已经在 page event 循环里推进 `CurrentPageId`；scanner renderer 再调用此函数会重写 cursor。Lab seek、preview、future PageFunction boundary 都会放大这个问题。

**修复要求：**

- `GetScannerPositionY` 使用局部 `pageId`；
- 提供 `FindPageByTime(time)` / `FindPageByTick(tick)` helper；
- `Game.Update` 是唯一推进 playback cursor 的地方；
- `GameRenderer` 取边界位置时显式传 page 或 pageId，不能依赖 scanner 查询副作用。

### 7.3 PositionFunction 数学应集中在 Chart

建议新增集中 API：

```csharp
float EvaluatePageRawProgress(ChartModel.Page page, float tickOrTime);
float EvaluatePageVisualProgress(ChartModel.Page page, float rawProgress);
float ConvertPageProgressToScreenY(ChartModel.Page page, float visualProgress);
float GetPageBoundaryPosition(int pageId, bool bottom);
```

落点：

| 调用点 | 应改为 |
|--------|--------|
| `GetNoteScreenY` | tick → rawProgress → visualProgress → screenY |
| `GetNoteChartY` | 返回 visual/chart progress，避免 storyboard/note override 仍按旧线性 |
| `ConvertChartTickToScreenY` | 用目标 page 的 visual progress |
| `GetScannerPositionY` | time/tick → page visual progress，局部 pageId |
| hold `holdlength` | `abs(endVisualProgress - startVisualProgress) * page visual height` |
| `GameRenderer` boundary | `GetPageBoundaryPosition(currentPageId, bottom)` |

**注意：** Wiki 说明页面被缩放后扫描线仅在缩放后的高度范围运行，且部分页面出界时扫线停在边界。因此 `visualProgress` 映射后还需要定义 clamp 规则：边界线可反映缩放后视觉带，scanner position 应 clamp 到可见 play area。

### 7.4 输入判定不应继续依赖“半页时间”近似

当前输入层有时间窗口式跨页过滤：

- `InputController.OnFingerDown` 使用 `note.Page.Duration / 8f` 与 `CurrentPageId` 页面时间差；
- `DragHeadNote.OnTouch` 对 later page 使用 `Page.Duration / 2f` 判断。

PageFunction 引入后，视觉位置和 page 时间不再线性等价；尤其 `Arguments[0]=0` 的下落式页不能只靠静态 tick→Y 映射解释，可能需要按 `start_time - Game.Time` 计算 note 向判定线的运动。继续使用半页时间近似可能导致可见 note 被拒绝、不可见 note 被接受。

**建议：**

- MVP 可先保留时间判定，但把输入专项列为 P1 验证；
- 完整方案应改为基于 world position / collider / scanner distance 的空间判定；
- `CurrentPageId` 必须先去副作用化，否则输入过滤会被 scanner query 污染。

### 7.5 UI Animation 必须先做 event reducer，不能直接散落到 Game.Update

Wiki 的 UI 事件有两个关键语义：

- type 2–5（显示/隐藏/渐入/渐出）同层顺序执行、不重复；
- type 6–7（动画显示/隐藏）另一层，直接覆盖且可重复。

如果直接在 `Game.Update` 的 event loop 里逐事件调用 UI component，会很快失控：

- 同 tick 多 event 目前被忽略，因为代码只看 `event_list[0]`；
- timeline seek 需要 target 前 event 的稳态，而不是重播动画；
- Preview / full resync 已有系统性差异，UI event 若无 reducer 会进一步扩大差异；
- `Mod.HideScanline`、storyboard scanner override、chart UI event 都可能抢同一个 scanner。

**建议新增：** `ChartUiController`

职责：

1. 解析 event `args` 到目标 UI ID；
2. 维护 0–7 UI target registry；
3. 对 type 2–5 维护顺序队列和终态；
4. 对 type 6–7 维护动画层 slot；
5. 提供 `ApplyEvent(event, playAnimation: true)`；
6. 提供 `RebuildStateAtTime(targetTime)`，供 Lab full resync 和 preview 使用；
7. 暴露 `ResetToInitialState(isStartWithoutUi)`。

### 7.6 UI target 适配风险

当前 UI 元素不是统一组件体系：

| ID | 现状风险 | 建议 |
|----|----------|------|
| 0 ComboText | 自己在 `LateUpdate` 根据 combo 自动 fade | 加 `ChartUiVisibility` wrapper，避免业务更新覆盖 chart hide |
| 1 ScoreText | 无 fade API，只更新文字/scale | 同上 |
| 2 TitleText + icon | 当前只有 title text，icon 元素不明确 | MVP 先支持 title，icon 标记缺口 |
| 3 Difficulty | `DifficultyPill` 在 Navigation 元素下，Game 场景绑定需核实 | 先定位 Game scene 实例 |
| 4 Scanner | 受 `Mod.HideScanline`、storyboard scanner controller、speed animation 共同影响 | 定义优先级：Mod/Storyboard/Chart UI |
| 5 Boundary | `GameRenderer` 持有 boundary renderer，无显隐 API | `GameRenderer` 暴露 target adapter |
| 6 BeatPulseVisualizer | `StartPulsing` 未接 game flow，内部还用 `DateTime.Now` | MVP 可只做显隐，不做真实频谱 |
| 7 GameProgressIndicator | 每 `onGameUpdate` DOWidth | wrapper 控制 alpha，避免隐藏时仍频繁 tween |

### 7.7 与 Resync 系统的依赖关系

c2v3 UI event 与 Timeline resync 强耦合。详见 [Timeline Resync 系统评审](2026-07-01-cytoid-lab-resync-system-review.md)。

接入 UI Animation 前，至少需要：

1. `ResetChartIndicesToTime` 不只跳 cursor，还能要求 `ChartUiController.RebuildStateAtTime(targetTime)`；
2. preview 路径不触发普通 gameplay mutation；
3. UI target 能 snap 到 target 前事件的稳态；
4. transient animation 在 seek 后默认不 replay，除非设计明确要求。

### 7.8 PageFunction 与现有 Storyboard 坐标系统兼容性

本节专门评估 c2v3 PageFunction 的 **Y 轴缩放/偏移** 是否会和现有 Storyboard 系统互相干扰。结论：**会有高风险干扰，尤其集中在 note controller、`ReferenceUnit.NoteY`、scanner position override 三处。** PageFunction 不能只改 `Chart.GetNoteScreenY`，必须先定义 Storyboard 坐标语义。

#### 当前坐标链路

当前 chart note 的 Y 坐标在加载时被烘焙：

```text
Chart.GetNoteScreenY(note)
  -> note.position.y

Chart.GetNoteChartY(note)
  -> note.y
```

note 每帧位置更新：

```text
Note.OnGameUpdate
  -> Model.CalculatePosition(Game.Chart)
     -> pos = baked note.position
     -> if YMultiplier/YOffset:
          pos.y = Chart.ConvertChartYToScreenY(note.y * YMultiplier + YOffset)
     -> if Override.Y:
          pos.y = Override.Y
```

Storyboard note controller 写的是 `ChartModel.Note.Override`：

```text
Storyboard NoteController
  -> Note.Override.Y
  -> Note.Override.YMultiplier
  -> Note.Override.YOffset
```

Storyboard 通用单位转换中，`ReferenceUnit.NoteY` 也是：

```text
UnitFloat.ReferenceUnit.NoteY
  -> Chart.ConvertChartYToScreenY(Value)
```

#### 风险 1：`ConvertChartYToScreenY(float y)` 没有 page 上下文

PageFunction 是 page-level 变换：同一个 raw progress 在不同 page 上可能有不同 `a/b`、方向、视觉带。当前 `ConvertChartYToScreenY(float y)` 只有一个 `y` 参数，没有 `pageId` / `note` / `tick`。因此它无法正确应用 PageFunction。

如果直接把 `ConvertChartYToScreenY` 改成 PageFunction-aware，会遇到两个问题：

1. 无法知道应该用哪一页的 `PositionFunction`；
2. Storyboard 的 `ReferenceUnit.NoteY`、`scanline_pos`、note controller offset 都会被迫套用不明确的 page 变换。

**结论：** `ConvertChartYToScreenY(float y)` 应继续代表旧的全局 chart Y 到 screen Y 的线性映射，或者被标记为 legacy/global。PageFunction 需要新增带 page/note 上下文的 API。

#### 风险 2：`note.y * YMultiplier + YOffset` 可能发生二次缩放语义漂移

当前 `note.y` 是 `scan_line_direction * rawPageProgress`。Storyboard 的 `YMultiplier` / `YOffset` 等价于在旧线性 chart Y 空间里做仿射变换，然后再映射到 screen。

PageFunction 引入后，有两种可能语义：

| 方案 | 含义 | 风险 |
|------|------|------|
| A. `note.y` 保持 raw progress | Storyboard controller 仍控制旧线性页内坐标；PageFunction 只影响 base `note.position` | Override 后 note 会脱离 PageFunction 视觉带，`YMultiplier/YOffset` 看起来像“取消了页面缩放” |
| B. `note.y` 改为 visual progress | Storyboard controller 叠加在 PageFunction 后的视觉坐标上 | 旧 storyboard 在 c2v3 谱面里可能被二次缩放/偏移，历史语义改变 |

更糟的是 `Override.Y` 是绝对 world/screen-like 值，一旦 storyboard 使用 absolute override，它会完全绕过 PageFunction。这在旧系统中是预期能力，但在 c2v3 中可能与页面控制冲突。

**建议：** 不要复用单个 `note.y` 同时承载 raw progress 和 visual progress。新增字段或 helper：

```csharp
note.rawPageProgress
note.visualPageProgress
Chart.GetNoteBaseScreenY(note)
Chart.ApplyNoteYOverride(note, override)
```

然后明确 note controller 的 `YMultiplier/YOffset` 是作用于 raw progress 还是 visual progress。推荐 MVP：**作用于 visual progress 之后的 chart-local visual Y**，并在兼容性文档中声明 c2v3 下 storyboard note controller 会叠加在 PageFunction 结果上。

#### 风险 3：`ReferenceUnit.NoteY` 与 PageFunction 不兼容

Storyboard `UnitFloat.ReferenceUnit.NoteY` 被很多系统复用：

- note controller 的 `y`；
- controller 的 `scanline_pos`；
- line position parser 中的 note-space y；
- 可能还有 sprite/text 与 note-space 对齐。

它当前只接受一个数值，没有 page 上下文。PageFunction 后，`NoteY=0.5` 不再能唯一映射到屏幕位置：不同 page 的缩放/偏移不同。

**兼容策略：**

| 使用场景 | 建议语义 |
|----------|----------|
| `NoteController` 控制具体 note | 使用该 note 的 page context，允许 PageFunction-aware |
| `Controller.scanline_pos` | 使用当前 scanner page context；seek/preview 时取 target time 所在 page |
| `LineState.Pos` 的 `ReferenceUnit.NoteY` | 保持 legacy global mapping，除非 line 明确绑定 note/page |
| 普通 sprite/text 的 `ReferenceUnit.NoteY` | 保持 legacy global mapping，避免无 page context 的对象行为不可预测 |

也就是说，`ReferenceUnit.NoteY` 不应全局改行为。应在有上下文的调用点增加专用转换。

#### 风险 4：Storyboard scanner override 会绕过 PageFunction scanner

Storyboard controller 支持：

```text
override_scanline_pos
scanline_pos
```

`ScannerPositionEaser` 设置 `Scanner.Instance.positionOverride`，如果开启，就直接覆盖 scanner 的正常位置。PageFunction 实现后 scanner 默认位置会来自 page visual progress；但 storyboard override 仍可把它放到任意 `NoteY` 位置。

这是既有 storyboard 能力，不应禁止。但需要定义优先级：

1. `Mod.HideScanline` 仍最高，隐藏就是隐藏；
2. Storyboard `override_scanline_pos=true` 覆盖 chart PageFunction scanner position；
3. PageFunction 只负责默认 scanner path；
4. Chart UI event 只能控制 scanner visibility/opacity，不应改写 storyboard scanner position。

#### 风险 5：Storyboard note placeholder 与 spawned note 位置总体兼容

`NoteControllerRenderer` 的 placeholder 会跟踪已 spawn note GameObject 的 transform position。因此对于 target/parent 到 note 的 storyboard 对象，只要 note GameObject 的最终位置正确，placeholder 本身能跟随 PageFunction 后的位置。

但问题在于 note controller 同时也会写 `Note.Override`。因此：

- 单纯绑定 note transform 的 storyboard 对象：大概率兼容；
- 使用 note controller 改 YMultiplier/YOffset/Y 的 storyboard：高风险；
- 使用 scanner override 的 storyboard：需按优先级处理；
- 使用无上下文 `ReferenceUnit.NoteY` 的普通对象：保持 legacy 语义更安全。

#### 推荐实现策略

**MVP 不破坏旧 Storyboard：**

1. 保留 `ConvertChartYToScreenY(float y)` 的 legacy/global 语义；
2. 新增 PageFunction-aware API：

```csharp
float GetPageVisualProgress(int pageId, float rawProgress);
float ConvertPageProgressToScreenY(int pageId, float visualProgress);
float ConvertNoteProgressToScreenY(ChartModel.Note note, float progress, bool progressIsVisual);
float ConvertScannerProgressToScreenY(float time);
```

3. chart base note position、scanner、boundary、holdlength 使用新 API；
4. `NoteController` 对具体 note 的 `YMultiplier/YOffset` 使用 note page context；
5. 普通 storyboard `ReferenceUnit.NoteY` 暂时保持 legacy/global；
6. `scanline_pos` 单独使用 scanner current page context，不走普通 `ReferenceUnit.NoteY`。

**需要测试的 storyboard/c2v3 组合：**

| 场景 | 预期 |
|------|------|
| c2v3 PageFunction，无 storyboard | note/scanner/boundary/holdlength 正确 |
| c2v3 + storyboard 普通 sprite/text | 不因 PageFunction 改变无关 storyboard 坐标 |
| c2v3 + note controller `YMultiplier/YOffset` | 明确叠加在目标 note page 的 visual progress 上 |
| c2v3 + note controller `OverrideY` | 绝对 override 允许绕过 PageFunction，文档化 |
| c2v3 + `override_scanline_pos` | storyboard 覆盖 scanner 默认 PageFunction path |
| timeline seek 到不同 page | scanner override、note controller、base PageFunction 均 snap 到正确状态 |

#### 对现有审计问题的依赖

现有 storyboard 系统还有会影响该兼容性的已知问题，详见 [Storyboard 内存/性能/Bug 审计报告](storyboard-memory-performance-audit.md) 的 SB-7、SB-29、SB-30、SB-31、SB-34：

- `NoteControllerRenderer.Dispose` 不清 `Note.Override`，controller destroy 后会导致 PageFunction 测试出现残留偏移；
- `ScannerPositionEaser` 写入的 `Scanner.positionOverride` 缺 owner cleanup，可能绕过 PageFunction scanner path；
- `UnitFloat.ConvertedValue` 永久缓存动态坐标，不能承载 page/time/context-sensitive conversion；
- `NoteControllerStateParser` 的 `YOffset` 标注 `TODO: This is broken`，需要在 PageFunction 前明确修复或禁用；
- `ReferenceUnit.NoteY` 无 page context，不应被直接改为 PageFunction-aware；
- `GetScannerPositionY` 当前会重置 `CurrentPageId`，会污染 scanner/page state；
- storyboard resync 不 replay trigger history，会影响 timeline seek 下的兼容测试。

因此 PageFunction 与 Storyboard 兼容性工作不应等 UI Animation 才处理；它属于 **Slice A PageFunction MVP 的验收条件**。

### 7.9 建议最小可合入切片

**Slice A — PageFunction MVP**

- `ChartModel.Page.PositionFunction`
- `Chart` 统一 visual progress API
- note/scanner/boundary/holdlength 全部走 visual progress
- `GetScannerPositionY` 去除 `CurrentPageId` 副作用
- Storyboard note controller / scanner override / `ReferenceUnit.NoteY` 兼容策略落地
- Lab 三类测试谱：线性、`a=0` 下落式、`a!=1/b!=0`
- `a=0` 下落式若缺少对照证据，先作为 partial/experimental，不按“音符堆叠”落地

**Slice B — Resync 前置修复**

- preview gameplay mutation 隔离
- full resync 最后一帧 render 语义明确
- chart event state catch-up hook 预留

**Slice C — UI Animation MVP**

- `ChartUiController` + 0/1/4/5/7 五类目标先行
- `is_start_without_ui`
- type 2–7 reducer + `RebuildStateAtTime`
- type 8 暂不做

**Slice D — UI Animation 完整化**

- Title icon / difficulty / beat visualizer 完整适配
- Bridge-embedded 是否显示 chart UI 的产品决策
- storyboard scanner override 与 chart UI scanner visibility 的优先级验证

### 7.10 工时修正

原估算 9–14 人天仍可作为整体范围，但若把 resync 前置修复和 UI reducer 纳入首版，建议按以下拆分：

| 切片 | 估算 |
|------|------|
| Slice A PageFunction MVP（含 Storyboard 坐标兼容策略） | 4–6 天 |
| Slice B Resync 前置修复 | 1–2 天 |
| Slice C UI Animation MVP | 4–6 天 |
| Slice D UI Animation 完整化 | 2–4 天 |
| Lab 验证与回归 | 1–2 天 |
| **合计** | **11–19 人天** |

---

## 8. Cytoid Lab macOS 移植评估

> 详见独立文档：[cytoid-lab-macos-adaptation.md](cytoid-lab-macos-adaptation.md)

**难度：中（约 4–7 人天）** — Lab 核心逻辑跨平台，主要工作是平台宏扩展、macOS 文件选择器、构建脚本。

---

## 附录：关键文件索引

| 主题 | 路径 |
|------|------|
| Chart 模型 | `engines/unity/Assets/Scripts/Game/Chart/ChartModel.cs` |
| 坐标管线 | `engines/unity/Assets/Scripts/Game/Chart/Chart.cs` |
| 边界渲染 | `engines/unity/Assets/Scripts/Game/Notes/GameRenderer.cs` |
| 扫描线 | `engines/unity/Assets/Scripts/Game/Elements/Scanner.cs` |
| 事件循环 | `engines/unity/Assets/Scripts/Game/Game.cs` |
| Page Function 分支 | `feat/page-function-support` commit `7414e519` |
| Cytoid Lab 入口 | `engines/unity/Assets/Scripts/Navigation/CytoidLab/CytoidLabShell.cs` |
| Lab 菜单/导入 | `engines/unity/Assets/Scripts/Navigation/CytoidLab/CytoidLabMenuController.cs` |
| Lab HUD/scrub | `CytoidLabHudController.cs`、`Game.CytoidLab.cs` |
| Windows 构建 | `engines/unity/Assets/Scripts/Editor/CytoidCoreBuild.cs`、`build-cytoid-lab.ps1` |
| Wiki c2 规格 | https://cytoid.wiki/zh/reference/chart/c2-format |
