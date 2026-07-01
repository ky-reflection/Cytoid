# Cytoid Lab：`ResyncPlayfieldToTime` Hold 状态问题调研

> **Date:** 2026-07-01（截图证据 + 同步系统分析更新）  
> **Branch:** `feature/cytoid-player`  
> **Symptoms:** timeline seek 后 hold **提前出现**、**进度不正确**、**进度颜色异常**  
> **Scope:** 静态代码分析 + Lab 截图对照（无编译验证环境）

> **System context:** 本文档聚焦 Hold / LongHold，是 [Timeline Resync 系统评审](2026-07-01-cytoid-lab-resync-system-review.md) 的子集问题。Preview 副作用隔离、active note set 重建、storyboard trigger replay、c2v3 event catch-up 等系统级问题以 resync 系统文档为准。

---

## 1. 问题描述

在 Cytoid Lab 中使用 timeline slider 跳转到谱面任意时间（`ResyncPlayfieldToTime` / `PreviewTimeline`）后，Hold / LongHold 音符可能出现：

1. **提前出现** — 主要是 **hold 头（Ring/Fill）** 与 **判定特效**（ProgressRing 弧、approach 缩放/透明度、Triangle 引导），**而非** hold body line
2. **进度不对** — ProgressRing 弧的填充比例与 seek 目标时间不匹配，或 seek 后「锁定」在错误比例
3. **颜色问题** — 同屏多个 hold 的 ProgressRing 颜色/弧形色相不一致（如一侧全蓝、另一侧半白半红）

### 1.1 视觉元素 ↔ 代码对照

| 玩家看到的 | 代码组件 | 渲染入口 |
|------------|----------|----------|
| Hold 头（大圆 Ring/Fill） | `ClassicNoteRenderer` | `UpdateComponentStates` / `UpdateTransformScale` / `UpdateFillScale` |
| Approach 缩放/渐入 | 同上 | `(Time - intro) / (start - intro)` 驱动 scale/opacity |
| 判定环弧（ProgressRing） | `ProgressRing` + shader | `ClassicHoldNoteRenderer` L89–119 |
| 引导三角/虚线感 | `MeshTriangle` | `Triangle.OnUpdate()` |
| Hold body（梯子横档） | `Line` / `CompletedLine` | style 1/2 分支，受 `ShouldShowHoldBody` 门控 |

**Seek 现状：** 仅 **body 进度** 有 `ApplyTimelineHoldProgress`；**head + 判定特效** 无 `FastForwardToTime` 等价逻辑（对比 drag 已有 `DragHeadNote.FastForwardToTime`）。

相关代码：

| 文件 | 职责 |
|------|------|
| `Navigation/CytoidLab/Game.CytoidLab.cs` | `ResyncPlayfieldToTime`、`SpawnActiveNotesAtTime`、`PreviewTimeline` |
| `Navigation/CytoidLab/HoldNote.CytoidLab.cs` | `ApplyTimelineHoldProgress`（仅 body 进度） |
| `Navigation/CytoidLab/DragHeadNote.CytoidLab.cs` | `FastForwardToTime`（drag 已有，hold 缺） |
| `Game/Notes/HoldNote.cs` | 按住 / Auto / timeline 模式切换 |
| `Game/Notes/Classic/ClassicHoldNoteRenderer.cs` | hold 头判定环 + body line |
| `Game/Notes/Classic/ClassicNoteRenderer.cs` | hold 头 approach 动画 |
| `Game/Elements/ProgressRing.cs` | 判定环 shader 参数（sharedMaterial 问题） |

---

## 2. 截图证据分析

**复现场景：** 谱面 *Let you DIVE!* easy Lv.13，Lab v0.1.0，**01:46 / 02:22**（约 79%），**Auto: On**，timeline seek 后 pause。

### 2.1 截图中观察到的现象

| 区域 | 现象 | 与 bug 的对应 |
|------|------|---------------|
| **底部两个 hold 头** | 大蓝 fill 圆 + 外围 progress 弧 | 当前判定点上的 hold；判定环（ProgressRing）可见 |
| **左 hold 判定弧** | 整体 **蓝色** 填充弧 | 正常或单侧正确写入 |
| **右 hold 判定弧** | **上白下红** 不对称弧 | **RC-1 material 串扰** 或 cutoff 写乱；左右同屏颜色/形状不一致 |
| **中间 body** | 白色 ladder 横档（style 1） | body 相对正常；**非**主诉的「提前出现」来源 |
| **右上方小 head** | 灰圈 + 红心 + **虚线向下** | approach 态 head + Triangle/引导；若 seek 时间已过其 `start_time` 仍如此 → **缺 fast-forward** |
| **左上方小 head** | 灰圈 + 红点 | 同上，未来 note 或 seek 态未刷新 |

### 2.2 截图支持的根因优先级

1. **RC-1（高）** — 两 hold 同屏，ProgressRing 弧颜色/填充形态完全不同（蓝 vs 红白），是 sharedMaterial 串扰的典型外观。
2. **RC-9（高，新增）** — body 有 timeline 进度，head/判定环仍按 approach 公式现场算；上方 approach 小 head 可能是正确未来 note，也可能是 seek 后未 fast-forward。
3. **RC-7（中高，升级）** — **Auto: On** 时 seek 后所有已过 start 的 hold 立即 `UpdateFinger(0, true)`，多 hold 同时写 ProgressRing material，放大 RC-1。
4. **RC-5（中）** — 若问题在 drag preview 阶段出现，Preview 不 despawn 导致 head/判定特效残留。
5. **RC-2（低，对此谱面）** — 截图为 style 1 ladder body，Style 2 body 无门控不是此图主因。

---

## 3. Seek 流程摘要

### 3.1 完整 resync（slider 松手）

```
ResyncPlayfieldToTime(targetTime)
  ├─ Time / Music 跳到 targetTime
  ├─ ResetChartIndicesToTime
  ├─ ClearSpawnedObjects          // ForceDespawnForResync + ResetHoldRuntimeState
  ├─ State.ResetToTime              // 分数/判定 rewind（Auto 下会 re-judge 已过音符）
  ├─ SpawnActiveNotesAtTime
  │     ├─ DragHead → FastForwardToTime(targetTime)  ✅
  │     └─ Hold     → ApplyTimelineHoldProgress(...)  ⚠️ 仅 body
  ├─ await Storyboard.ResyncToTime
  ├─ Music.Play
  └─ onGameUpdate / onGameLateUpdate（手动触发一次）
```

### 3.2 拖拽预览（slider 拖动中）

```
PreviewTimeline(targetTime)
  ├─ Time / Music 跳到 targetTime
  ├─ RefreshSpawnedHoldProgress     // 仅更新已在场上的 hold body 进度
  └─ onGameUpdate
  // ❌ 不 ClearSpawnedObjects
  // ❌ 不 SpawnActiveNotesAtTime
  // ❌ 不 refresh head/判定环 approach 态
  // ⚠️ 仍触发普通 Note.OnGameUpdate：Auto / clear / hitsound 等 gameplay 副作用未隔离
```

**关键差异：** Drag 在 resync 时有 `FastForwardToTime`；Hold 只有 body 进度，**head + ProgressRing approach 态无 seek 专用路径**。Preview 路径 additionally 不重建音符集合。

### 3.3 整体同步系统分层

当前 Lab seek 不是单一系统，而是多套状态同步拼接：

| 层 | 正常播放 | Full resync | Preview |
|----|----------|-------------|---------|
| 时间基准 | `SynchronizeMusic()` 用 DSP 定期校准 `Game.Time` | 手动设置 `Music.PlaybackTime` / `MusicStartedTimestamp` / `Game.Time` | 同 full resync |
| Chart 游标 | `CurrentEventId` / `CurrentPageId` / `CurrentNoteId` 单调推进 | `ResetChartIndicesToTime` 直接跳到 target 后 | ❌ 不重置 |
| 场上音符集合 | `CurrentNoteId` 到达 intro 窗口后 spawn | `ClearSpawnedObjects` 后 `SpawnActiveNotesAtTime` 重建 active set | ❌ 不重建 |
| 判定/分数 | note clear/miss 实时写入 | `State.ResetToTime` 重算；Auto 下只直接写已完全过去的 note | ❌ 不重算 |
| Drag 视觉 | 正常 update 插值 | `FastForwardToTime` + `ResyncVisualToTime` | ❌ 不重建链 |
| Hold 视觉 | `IsHolding` / `HoldProgress` 实时驱动 | `ApplyTimelineHoldProgress`，仅 body/ring progress | 仅刷新已 spawn hold progress |
| Storyboard | `onGameLateUpdate` + triggers | trigger snapshot reset + renderer 重建非手动对象 | ❌ 不 resync |

**核心风险：** full resync 是“重建 target 时刻状态”，preview 是“在现有场景上临时改时间”。两者共享普通 `onGameUpdate`，但 preview 没有清场/重建/副作用隔离，因此它既不是纯视觉预览，也不是完整 seek。

### 3.4 Storyboard 与 chart event 的 seek 语义缺口

`Storyboard.ResyncToTime` 当前会：

1. 从初始 snapshot 还原 trigger 列表；
2. dispose 并清空 renderer registry；
3. 重建所有非 manually spawned storyboard 对象；
4. 按当前 `Game.Time` update 一次。

这能恢复**静态时间轴对象**，但不会 replay target time 之前已经发生过的 `NoteClear` / combo / score triggers。`State.ResetToTime` 在 Auto 下用 `JudgeFromModel` 重算已过音符，也不会触发 `onNoteClear`。因此由 trigger 产生的手动 spawn/destroy 副作用不会出现在 seek 后状态里。

同理，`ResetChartIndicesToTime` 只是把 `CurrentEventId` 推到 target 后，并不 replay target 前的 chart events。当前 type 0/1 只是速度提示，影响有限；但后续 c2v3 UI animation 如果落在 `event_list`，timeline seek 必须实现 event state catch-up，否则 UI 显隐/动画状态会在 seek 后错误。

---

## 4. Hold 状态机

### 4.1 两套进度来源（body only）

| 模式 | 触发 | 进度计算 |
|------|------|----------|
| **手指按住** | `UpdateFinger(isHolding: true)` | `(Game.Time - start - offset) / Duration` |
| **Timeline** | `ApplyTimelineHoldProgress(time)` | 同上，但 `time` 为 seek 目标 |

`ShouldShowHoldBody = IsHolding || UseTimelineHoldProgress` — **仅控制 body line**，不控制 head approach 动画。

### 4.2 Head / 判定特效的状态来源（无 seek 专用逻辑）

| 状态 | 驱动公式 | seek 后行为 |
|------|----------|-------------|
| Head scale/opacity | `(Time - intro) / (start - intro)` | spawn 当帧按 `Game.Time` 算；**无** fast-forward API |
| ProgressRing enabled | `Time >= intro + JudgmentOffset` | 可能 enabled 但 cutoff 未同步 |
| ProgressRing fill | `HoldProgress` via `OnUpdate` | 依赖 body timeline 模式；与 head approach 分裂 |
| Triangle 引导 | `Triangle.OnUpdate()` | 同 ProgressRing 门控 |

### 4.3 模式切换

- `UpdateFinger(true)` → `ClearTimelineHoldProgress()`，切到手指模式
- **Auto / AutoHold：** `TimeUntilStart < 0` 时 `UpdateFinger(0, true)` → 清除 timeline 模式；**截图 Auto: On 即此路径**
- Drag 对比：`FastForwardToTime` 在 spawn 后显式设置链段位置；Hold **无等价物**

---

## 5. 根因分析

### RC-1 ⭐ ProgressRing 使用 `sharedMaterial`（→ **颜色问题**，截图强证据）

```21:28:engines/unity/Assets/Scripts/Game/Elements/ProgressRing.cs
    public void OnUpdate()
    {
        spriteRenderer.sharedMaterial.SetFloat(fillCutoffId, fillCutoff);
        spriteRenderer.sharedMaterial.SetFloat(maxCutoffId, maxCutoff);
        spriteRenderer.sharedMaterial.SetColor(fillColorId, fillColor);
    }
```

同屏多 hold 共用 material asset → 最后一个 `OnUpdate` 的 hold 覆盖 `_FillColor` / `_FillCutoff` / `_MaxCutoff`。

**截图表现：** 左 hold 弧全蓝、右 hold 弧半白半红 — 典型串扰或 cutoff 写一半被下一 hold 覆盖。

**修复：** `material` 实例化或 `MaterialPropertyBlock`。

---

### RC-9 ⭐ Hold head / 判定特效 **缺 seek fast-forward**（→ **提前出现 + 进度/判定态不对**，新增）

Drag 在 `SpawnActiveNotesAtTime` 中：

```csharp
dragHead.FastForwardToTime(targetTime);
```

Hold 仅：

```csharp
holdNote.ApplyTimelineHoldProgress(targetTime);  // body only
```

**缺口：**

- Head 的 approach scale/opacity（`ClassicNoteRenderer` L165–203）在 respawn 后完全依赖 `Game.Time`，无「若已过 start 则 snap 到 full-size / 跳过 approach」的显式逻辑
- ProgressRing 的 enabled/fill 与 head approach 使用 **不同时间门控**（RC-3），seek 后可能 head 仍在 approach 外观而 body 已有进度
- 截图右上方 approach 小 head + 虚线：若该 note 在 `01:46` 已过 `start_time`，则属 **判定态未 fast-forward**；若未过 start 则是合法未来 note — 需按 note 时间轴区分

**建议：** 新增 `HoldNote.FastForwardVisualStateToTime(time)`（或扩展 `ApplyTimelineHoldProgress`），统一设置：

- `UseTimelineHoldProgress` / `HoldProgress`
- 告知 renderer 跳过 approach（或预计算 `EasedOpacity` / head scale = 1）
- ProgressRing cutoff 一次性写入

---

### RC-3 ⭐ 渲染门控阈值不一致（→ **判定环与 head/body 不同步**）

| 检查点 | 时间阈值 |
|--------|----------|
| `ApplyTimelineHoldProgress` | `start_time + JudgmentOffset` |
| Head Ring/Fill 可见 | `intro_time` |
| ProgressRing `OnUpdate` 门控 | `start_time`（无 offset） |
| Body line 填充 | `start_time + JudgmentOffset` |
| Hold 组件窗口 | `intro_time + JudgmentOffset` |

负 `JudgmentOffset` 时：timeline 认为已在 hold 内，但 ProgressRing `OnUpdate` 门控可能仍 false → enabled 但 cutoff  stale → **截图类半填充弧**。

---

### RC-7 ⭐ Auto 模式放大 material 竞争（升级，截图 Auto: On）

`Note.OnGameUpdate` 在 Auto 下对 `TimeUntilStart < 0` 的 hold 调用 `UpdateFinger(0, true)`：

1. `ClearTimelineHoldProgress()` — 清除 seek 写入的 body timeline 态
2. 所有在判定点 hold 同时 `IsHolding`
3. 每帧多个 hold 写 **同一** ProgressRing sharedMaterial

**截图条件完全满足：** Auto: On + 双 hold 同屏 → RC-1 几乎必现。

---

### RC-5 Preview 路径不一致（→ drag 期间 head/判定特效残留）

| | PreviewTimeline | ResyncPlayfieldToTime |
|--|-----------------|----------------------|
| 清除场上对象 | ❌ | ✅ |
| 重生成音符 | ❌ | ✅ |
| body 进度 | refresh 已 spawn | spawn 时 apply |
| head/判定环 | ❌ 不 refresh | ❌ 无 fast-forward |

往回拖 slider：旧 hold 的 head + ProgressRing 残留 → 相对新时间点「提前出现」。

---

### RC-10 ⭐ Preview 触发普通 gameplay update（→ **预览阶段产生状态副作用**）

`PreviewTimeline` 最后直接调用 `onGameUpdate.Invoke(this)`。这会进入普通 `Note.OnGameUpdate`：

- Auto / AutoHold 下，`TimeUntilStart < 0` 的 hold 会执行 `UpdateFinger(0, true)`；
- 非 hold Auto note 可能直接 `Clear(NoteGrade.Perfect)`；
- hold begin hitsound、HoldFx、miss/clear 判断等 runtime 逻辑都可能被触发；
- preview 没有随后执行 `State.ResetToTime`，因此这些副作用可能污染松手前的场景状态。

**结论：** preview 目前不是纯视觉路径。它应当要么完整对齐 full resync，要么提供一条“只刷新渲染、不触发判定/Auto/音效”的 preview update path。

---

### RC-11 Storyboard resync 不 replay trigger history（→ **seek 后 storyboard 状态不完整**）

`Storyboard.ResyncToTime` 重建非手动对象，但不会重新播放 target 前发生过的 trigger：

- note-clear trigger：`State.ResetToTime` 不触发 `onNoteClear`；
- combo / score trigger：没有按重算后的 combo/score 时间线 replay；
- manually spawned storyboard object：`ResyncAsync` 的 predicate 排除 `IsManuallySpawned()`，不会重建历史 trigger spawn 产物。

这意味着 seek 后 storyboard 只能保证基础时间轴对象在目标时间正确，不能保证 trigger 派生状态正确。若谱面依赖 note-clear trigger 控制画面，Lab timeline seek 会与真实播放产生差异。

---

### RC-12 Chart event seek 只跳 cursor，不 catch up state（→ **c2v3 UI animation 风险**）

`ResetChartIndicesToTime` 把 `CurrentEventId` 移到第一个未过 target 的 event，但不会把 target 前 event 的最终状态应用到 UI。当前播放循环也只处理 `event_list[0]`，且把非 0 当作 speed down。

短期对 hold bug 影响较小；长期若接入 c2v3 的 UI show/hide/fade/animation events，timeline seek 必须引入 deterministic catch-up：

- 从开头 replay 到 target 的 event state reducer；或
- 预编译 keyframe/state snapshot，加速 seek；
- 明确哪些 transient animation 在 seek 后 snap 到终态，哪些重新播放。

---

### RC-4 CompletedLine / Line 透明度分裂（→ body 颜色，次要）

仅 `Line` 在 experimental 动画下用 progress alpha；`CompletedLine` / LongHold `CompletedLine2` 路径不同。截图主诉为 **判定弧**，此项主要影响 body。

---

### RC-2 Style 2 body 无门控（→ 次要，style 1 谱面不适用）

Style 2 在 `ShouldShowHoldBody == false` 时仍画全长 line。截图谱面为 style 1 ladder，**非此图主因**，但代码缺陷仍存在。

---

### RC-6 Spawn 窗口 vs 显示窗口（→ 边界情况）

Spawn：`intro_time - 1 < targetTime`；Head 显示：`Time >= intro_time`。1 秒窗口内对象已存在但 head 未显，一般不是主诉。

---

### RC-8 暂停时 Time 漂移（已排除）

`Game.Update` 在 `!State.IsPlaying` 时 early return；seek / preview 手动 invoke `onGameUpdate`。暂停态 Time 不漂移。

---

## 6. 症状 ↔ 根因映射（汇总）

| 用户观察 | 最可能根因 | 置信度 | 截图 |
|----------|-----------|--------|------|
| **判定弧颜色不对**（蓝 vs 红白） | RC-1 sharedMaterial | **很高** | ✅ |
| **判定弧进度/形状不对** | RC-1 + RC-3 cutoff 未写入 | 高 | ✅ |
| **hold 头 + 判定特效提前/锁态** | RC-9 缺 fast-forward | 高 | ✅ 待逐 note 确认 |
| 同上（drag 期间） | RC-5 Preview 残留 | 中–高 | 需 drag 复测 |
| Auto 下更严重 | RC-7 放大 RC-1；RC-10 preview 副作用 | 高 | ✅ Auto: On |
| 拖动中状态被污染 | RC-10 普通 gameplay update 未隔离 | 高 | 需 drag 复测 |
| storyboard seek 后与真实播放不同 | RC-11 trigger history 未 replay | 中 | 需 trigger 谱验证 |
| 后续 UI animation seek 错误 | RC-12 chart event 不 catch up | 中（未来风险） | c2v3 相关 |
| body 颜色/透明度 | RC-4 | 中 | 截图不明显 |
| style 2 body 提前 | RC-2 | 低（此谱） | ❌ style 1 |

---

## 7. 建议修复方案

### Phase A — 最高优先级（截图直接对应）

**A1. ProgressRing material 实例化**（RC-1）

- `ProgressRing.cs`：`sharedMaterial` → `material` 或 MaterialPropertyBlock
- **预期：** 双 hold 同屏弧颜色/填充独立，截图红白/蓝不对称消失

**A2. Hold seek fast-forward**（RC-9，新建）

- 在 `HoldNote.CytoidLab.cs` 新增 `FastForwardVisualStateToTime(float time)`：
  - 调用 `ApplyTimelineHoldProgress(time)`
  - 若 `time >= start + offset`：标记 head approach 完成（scale/opacity = 1，或 renderer 只读 flag）
  - 一次性写入 ProgressRing cutoff/color
- `SpawnActiveNotesAtTime` 中替换 bare `ApplyTimelineHoldProgress` 调用
- 参考：`DragHeadNote.CytoidLab.cs` 的 `FastForwardToTime` 模式

**A3. 统一 ProgressRing 时间门控**（RC-3）

- `ClassicHoldNoteRenderer` L89：`start_time` → `start_time + JudgmentOffset`，与 timeline 一致

**A4. Auto + seek 路径**

- seek 期间临时 suppress Auto `UpdateFinger` 直到 visual state 写入完成；或 Auto 下仍保留 `UseTimelineHoldProgress` 直到首帧 render 完成（RC-7）
- preview 期间不要触发 Auto/clear/hitsound 副作用（RC-10）；最小实现可加 Lab-only suppress flag，较完整实现应拆出 render-only update。

### Phase B — Seek 路径一致性

**B1. PreviewTimeline 与 resync 对齐**（RC-5）

- 方案 1：preview 时也 `ClearSpawnedObjects` + `SpawnActiveNotesAtTime`（较重）
- 方案 2：preview 时至少 despawn 不在 `IsNoteActiveAtTime` 窗口的 note
- 方案 3：preview 只改 music time，不显示 approach 态变化（轻量但不理想）
- 无论采用哪种方案，preview 都必须隔离 gameplay 副作用（RC-10）。

**B2. Renderer 单一真相源**（RC-3 / RC-9）

- Renderer 只读 `HoldNote` 上 seek 后的 explicit visual state，不再各自做时间判断

**B3. Storyboard seek 语义定稿**（RC-11）

- 若目标是“近似静态预览”，文档和 UI 应明确 trigger 派生对象不会 replay；
- 若目标是“与真实播放一致”，需要 replay `NoteClear` / combo / score trigger history，或建立 trigger side-effect snapshot。

**B4. Chart event catch-up**（RC-12 / c2v3）

- c2v3 UI event 接入前，先实现 seek 到 target 的 event state reducer；
- `ResetChartIndicesToTime` 不应只跳 cursor，还应应用 target 前 event 的最终 UI state。

### Phase C — 次要（body / style 2）

**C1. Style 2 body 门控**（RC-2）  
**C2. CompletedLine 透明度同步**（RC-4）

### Phase D — 测试矩阵

| # | 场景 | 预期 | 截图对应 |
|---|------|------|----------|
| T1 | 双 hold 同屏 + seek 到 mid-hold，Auto On | 两 ProgressRing 弧颜色/比例各自正确 | ✅ 主回归 |
| T2 | 同上，Auto Off | 与 T1 一致，无 Auto 干扰 | |
| T3 | seek 到 hold approach 段 | head 显示 approach；body 无进度 | |
| T4 | seek 越过某 hold start 后再 back | 无 preview 残留 head/弧 | RC-5 |
| T5 | 负 JudgmentOffset 谱面 | ring/head/body 同步 | RC-3 |
| T6 | Style 2 谱面 | body 不在 approach 段提前全长 | RC-2 |
| T7 | drag preview → release | 松手前后 visual 一致 | RC-5 |
| T8 | Auto On 拖动 preview，不松手 | 不触发 clear/hitsound/HoldFx，不改变判定状态 | RC-10 |
| T9 | note-clear trigger storyboard | seek 后 trigger 派生对象状态符合定义语义 | RC-11 |
| T10 | c2v3 UI event 谱面 | seek 后 UI 显隐/动画状态 catch-up 正确 | RC-12 |

---

## 8. 预估工作量

| 项 | 估算 |
|----|------|
| A1 ProgressRing material | 0.25 天 |
| A2 Hold fast-forward | 0.5–1 天 |
| A3–A4 门控 + Auto | 0.25–0.5 天 |
| B1 Preview 路径 | 0.5 天 |
| B3 Storyboard trigger seek 语义/实现 | 0.5–2 天（取决于是否 replay） |
| B4 Chart event catch-up | 1–2 天（c2v3 前置） |
| C1–C2 次要 | 0.25 天 |
| T1–T10 Lab 验证 | 0.5–1 天 |
| **合计（hold bug 最小闭环）** | **2–3 人天** |
| **合计（含 storyboard/event 完整 seek）** | **4–7 人天** |

---

## 9. 相关文档

- [cytoid-lab.md](cytoid-lab.md) — timeline scrub 功能说明
- [2026-07-01-cytoid-lab-resync-system-review.md](2026-07-01-cytoid-lab-resync-system-review.md) — Timeline resync 系统评审
- [2026-07-01-c2v3-research-plan.md](2026-07-01-c2v3-research-plan.md) — Page Function seek 验证策略

---

## 附录 A：与 Drag seek 实现的对比

| 能力 | DragHead | Hold |
|------|----------|------|
| despawn on resync | ✅ `ForceDespawnForResync` | ✅ |
| spawn at seek time | ✅ | ✅ |
| position/state fast-forward | ✅ `FastForwardToTime` | ❌ 缺失 |
| body progress at seek | N/A | ✅ `ApplyTimelineHoldProgress` |
| head/判定特效 fast-forward | N/A | ❌ 缺失（**本 bug 核心缺口**） |

---

## 附录 B：截图复现 checklist

复现 *Let you DIVE!* easy Lv.13 或类似双 hold 段：

1. 开启 **Auto: On**
2. Timeline seek 到 **01:46** 附近（或该双 hold 段 mid-hold）
3. 观察底部两 hold **ProgressRing 弧**是否颜色/形状不一致
4. 观察上方是否有多余 **approach 小 head + 虚线**
5. 往回 drag slider（不松手）→ 检查 head/弧是否残留（RC-5）
6. 松手 full resync → 对比步骤 3–4 是否改善
