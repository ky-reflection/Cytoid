# Cytoid Lab：Timeline Resync 系统评审

> **Date:** 2026-07-01  
> **Branch:** `feature/cytoid-player`  
> **Scope:** `PreviewTimeline` / `ResyncPlayfieldToTime` / chart cursor / note state / storyboard / future c2v3 event seek  
> **Related:** [Hold seek 问题调研](2026-07-01-cytoid-lab-hold-seek-investigation.md) 是本文档的子集问题。

---

## 1. 结论摘要

Cytoid Lab 当前 timeline seek 不是一个单一 resync 系统，而是三套路径拼接：

1. **正常播放同步**：`Game.Update` + `SynchronizeMusic` 单调推进时间、chart cursor、note spawn、判定和 storyboard。
2. **Full resync**：slider 松手后 `ResyncPlayfieldToTime` 硬跳到目标时间，重置 cursor、清场、重建 active notes、重算部分判定、重建 storyboard。
3. **Preview**：slider 拖动中 `PreviewTimeline` 只改 `Game.Time` / `Music.PlaybackTime`，刷新已存在 hold progress，然后调用普通 `onGameUpdate`。

最大问题是 **preview 既不是纯视觉预览，也不是完整 resync**。它没有清场、重建 active set、重算 score/judgement 或 storyboard，却会触发普通 gameplay update；因此 Auto、clear、miss、hitsound、HoldFx 等副作用可能在拖动过程中发生。

Hold 颜色/进度问题是这个系统缺口的一个可见子集：

- `ProgressRing.sharedMaterial` 导致同屏 hold 串扰；
- hold 只有 body progress seek，没有 head/ring/triangle 的完整 fast-forward；
- preview 路径会触发 Auto `UpdateFinger`，把 timeline hold state 切回 finger state。

---

## 2. 现有同步链路

### 2.1 正常播放路径

入口：`engines/unity/Assets/Scripts/Game/Game.cs`

```
Game.Update
  ├─ Renderer.OnUpdate
  ├─ if !State.IsPlaying return
  ├─ SynchronizeMusic
  │   ├─ 定期从 AudioSettings.dspTime 校准 Game.Time
  │   └─ 其他帧用 unscaledDeltaTime 累加
  ├─ 处理 Chart.CurrentEventId
  ├─ 处理 Chart.CurrentPageId
  ├─ 处理 Chart.CurrentNoteId 并 spawn notes / drag lines
  ├─ onGameUpdate
  └─ onGameLateUpdate
```

特点：

- chart cursor 单调推进，不支持回退；
- note runtime state 由 `Note.OnGameUpdate` 和 renderer 每帧更新；
- storyboard 通过 `onGameLateUpdate` 驱动，并通过 `onNoteClear` / combo / score trigger 产生动态副作用。

### 2.2 Full resync 路径

入口：`engines/unity/Assets/Scripts/Navigation/CytoidLab/Game.CytoidLab.cs`

```
ResyncPlayfieldToTime(targetTime)
  ├─ State.IsPlaying = false
  ├─ AudioListener.pause = true
  ├─ Music.Stop
  ├─ Music.PlaybackTime = targetTime
  ├─ MusicStartedTimestamp = dspTime - targetTime
  ├─ Game.Time / MusicProgress / ChartProgress = targetTime
  ├─ ResetChartIndicesToTime
  ├─ ClearSpawnedObjects
  ├─ State.ResetToTime
  ├─ SpawnActiveNotesAtTime
  │   ├─ DragHead.FastForwardToTime
  │   ├─ DragLineElement.ResyncVisualToTime
  │   └─ HoldNote.ApplyTimelineHoldProgress
  ├─ inputController.ResetTouchState
  ├─ Storyboard.ResyncToTime
  ├─ Music.Play
  ├─ optionally resume
  ├─ onGameUpdate
  └─ onGameLateUpdate
```

特点：

- 这是目前最接近“目标时间状态重建”的路径；
- 它清掉场上对象并按 target time 重建 active notes；
- Drag 有显式 fast-forward，Hold 只有 progress 写入；
- Storyboard 只重建基础时间轴对象，不 replay trigger history；
- chart events 只跳 cursor，不 catch up target 前事件状态。

### 2.3 Preview 路径 — 2026-06-30 更新（light resync）

入口：`CytoidLabHudController.OnSliderValueChanged` → `Game.PreviewTimeline`

```
PreviewTimeline(targetTime)
  ├─ SuppressTimelineGameplayMutations = true
  ├─ Music.Stop / PlaybackTime / MusicStartedTimestamp / Game.Time
  ├─ ResetChartIndicesToTime
  ├─ PruneInactiveSpawnedObjects
  ├─ SpawnActiveNotesAtTime(visualPreviewOnly: true)
  ├─ RefreshSpawnedHoldProgress
  ├─ RefreshSpawnedDragVisualState
  ├─ Music.Play
  └─ onGameUpdate / onGameLateUpdate
```

特点：

- **已做：** chart cursor 对齐、active note 增量 prune/spawn、hold/drag visual fast-forward、`SuppressTimelineGameplayMutations` 隔离 Auto/miss；
- **仍不做（留待松手 full resync）：** `State.ResetToTime`、input reset、storyboard resync、chart event catch-up；
- preview 使用 `visualPreviewOnly` 忽略 `IsJudged`，以便拖回已打段落仍可见；松手后 full resync 重算判定状态。

---

## 3. 状态域评审

| 状态域 | 正常播放 | Full resync | Preview | 主要风险 |
|--------|----------|-------------|---------|----------|
| 时间基准 | DSP 校准 + delta 累加 | 手动设置 target time | 手动设置 target time | preview 后 `Music.Play` 但 `State.IsPlaying=false`，音频预览与 gameplay 状态语义混合 |
| Chart cursor | 单调推进 | 跳到 target 后 | **ResetChartIndicesToTime** | event seek 仍缺 catch-up |
| Note active set | 按 intro 窗口 spawn | 清场后重建 | **Prune + spawn（visualPreviewOnly）** | 已 clear 未 collect 的 note 边界情况待观察 |
| Note runtime state | 每帧更新 | 部分重置 + fast-forward | fast-forward + suppress | Auto/miss 已隔离 |
| Hold visual | finger / Auto 驱动 | full fast-forward | **FastForwardVisualStateToTime** | snapshot 仅在 suppress 期间读 renderer |
| Drag visual | 插值推进 | 有 fast-forward | **RefreshSpawnedDragVisualState** | 每帧 prune 后重建 line |
| Score/judgement | 实时写入 | `State.ResetToTime` 重算部分状态 | 不重算（spawn 忽略 judged 仅视觉） | 松手 resync 纠正 |
| Input | touch runtime state | `ResetTouchState` | 不 reset | 拖动 preview 后残留触摸语义 |
| Storyboard | time update + triggers | 重建非手动对象 | 不处理 | trigger 派生状态无法恢复 |
| Chart UI events | type 0/1 即时处理 | 只跳 cursor | 不处理 | c2v3 UI animation 需要 catch-up |

---

## 4. Findings

### RS-1. Preview 路径有 gameplay 副作用

`PreviewTimeline` 调用普通 `onGameUpdate`。这会进入 `Note.OnGameUpdate`：

- Auto hold 会执行 `UpdateFinger(0, true)`；
- Auto 非 hold note 可能直接 `Clear(NoteGrade.Perfect)`；
- hold begin hitsound、HoldFx、miss/clear 判断可能触发；
- renderer update 与 gameplay mutation 没有隔离。

这使 preview 无法作为安全的 scrub preview。拖动时看到的状态可能已经改变了后续 full resync 前的场景。

### RS-2. Full resync 只重建 active note set，不重放历史 side effects

`State.ResetToTime` 会清空判定并在 Auto 下直接 `JudgeFromModel` 已完全过去的 note，但不会触发 `onNoteClear`。依赖 note clear 的系统不会收到历史事件。

影响：

- storyboard note-clear trigger 不会 replay；
- hit effects / logs / telemetry 不应 replay，但需要明确；
- 如果未来 UI 或分析面板依赖 clear history，需要单独 reducer。

### RS-3. Hold seek 只有 progress，没有完整 visual state

Hold 在 full resync 里只执行 `ApplyTimelineHoldProgress`。这只设置：

- `UseTimelineHoldProgress`
- `HoldProgress`

但 renderer 同时使用 `Game.Time`、`start_time`、`JudgmentOffset`、`ShouldShowHoldBody` 判断 head、ring、triangle、line、opacity、scale。结果是 head/body/ring 不一定来自同一个 target-time state。

详见 [Hold seek 问题调研](2026-07-01-cytoid-lab-hold-seek-investigation.md)。

### RS-4. Drag 有局部 fast-forward，但 preview 不使用

Full resync 会对 drag head 调 `FastForwardToTime`，对 drag line 调 `ResyncVisualToTime`。Preview 不清场也不重建 drag chain，因此拖动 slider 时 drag 的位置/line 可能只是当前场景对象在新 `Game.Time` 下的混合状态。

### RS-5. Storyboard resync 是静态重建，不是历史重放

`Storyboard.ResyncToTime`：

- reset trigger snapshot；
- dispose 当前 renderers；
- 清空 registry；
- 重建所有非 manually spawned objects；
- 调一次 `Renderer.OnGameUpdate(Game)`。

它不会 replay target 前的 note-clear/combo/score triggers。由 trigger spawn/destroy 的对象不会恢复到真实播放状态。

### RS-6. Chart event seek 目前只跳 cursor

`ResetChartIndicesToTime` 把 `CurrentEventId` 移到 target 之后，但不会应用 target 前 event 的最终状态。

当前只处理 type 0/1 速度提示，问题较小；但 c2v3 UI show/hide/fade/animation event 接入后，这会成为 timeline seek 的系统性错误。

### RS-7. Full resync 结束后手动触发普通 update 仍可能产生副作用

`ResyncPlayfieldToTime` 在重建 state 后调用 `onGameUpdate` / `onGameLateUpdate`。这有必要刷新视觉，但仍会走普通 gameplay update。Auto On 时，刚 spawn 的 active hold 会立刻进入 finger mode；非 hold note 若已过 start，可能 clear。

这可能符合 Auto preview 预期，也可能与 timeline seek 的“只显示 target state”目标冲突。需要明确设计语义。

---

## 5. 建议架构

### 5.1 明确两种 seek 语义

建议把现有行为拆成两个明确概念：

| API | 目标 | 是否允许 gameplay mutation |
|-----|------|----------------------------|
| `PreviewTimeline(targetTime)` | 拖动期间轻量视觉预览 | 不允许 |
| `ResyncPlayfieldToTime(targetTime)` | 松手后重建可继续播放的 runtime state | 允许受控 mutation |

Preview 不应调用普通 `onGameUpdate`。如果必须刷新 renderer，应提供 Lab-only render pass 或 suppress flag。

### 5.2 为 target-time state 建立 reducer

Full resync 应分层执行：

1. `ApplyClock(targetTime)`
2. `ApplyChartCursor(targetTime)`
3. `ApplyGameplayState(targetTime)`
4. `ApplyActiveNotes(targetTime)`
5. `ApplyVisualFastForward(targetTime)`
6. `ApplyStoryboardState(targetTime)`
7. `ApplyChartUiEventState(targetTime)`
8. `RenderOneFrame(targetTime, mode)`

这样每层都能定义是否 replay history、是否 snap 到终态、是否允许副作用。

### 5.3 Renderer fast-forward 统一入口

每类需要 seek 的视觉对象应有同一语义的 fast-forward：

| 对象 | 当前状态 | 建议 |
|------|----------|------|
| DragHead | 有 `FastForwardToTime` | 保留 |
| DragLine | 有 `ResyncVisualToTime` | 保留，纳入统一接口 |
| Hold | 只有 `ApplyTimelineHoldProgress` | 增加完整 `FastForwardVisualStateToTime` |
| Storyboard | renderer rebuild | 明确是否 replay trigger |
| Chart UI | 无 | c2v3 前实现 event reducer |

### 5.4 Preview 副作用隔离

最小方案：

- 增加 Lab-only `IsTimelinePreviewing` / `SuppressGameplayMutation` flag；
- `Note.OnGameUpdate` 在该 flag 下跳过 Auto、clear/miss、hitsound/HoldFx 等 mutation；
- renderer 可以继续刷新。

更完整方案：

- 拆出 `RenderAtTime(targetTime)`，只执行 transform/renderer update；
- gameplay state update 和 renderer update 不再共用 `onGameUpdate`。

---

## 6. 修复优先级

### P0：Hold bug 最小闭环 — ✅ 2026-06-30

1. ✅ `ProgressRing` → `MaterialPropertyBlock`（core）。
2. ✅ Preview 隔离 Auto/miss（`SuppressTimelineGameplayMutations`）。
3. ✅ Hold `FastForwardVisualStateToTime` + RC-9 snapshot 生命周期。
4. ✅ ProgressRing 门控含 `JudgmentOffset`（RC-3）。

### P1：Resync 系统一致性 — 部分完成

1. ✅ Preview 策略：**light resync**（visual-only spawn + fast-forward，无 score/storyboard）。
2. ⬜ Full resync 最后一帧 render 与 gameplay mutation 进一步分离（可选）。
3. ✅ Drag preview 纳入 `RefreshSpawnedDragVisualState`。

### P2：Storyboard / c2v3 完整 seek

1. 定义 storyboard trigger seek 语义：静态预览或历史重放。
2. c2v3 UI event 接入前实现 chart event state catch-up。
3. 为 event/storyboard 建 snapshot 或 reducer，避免每次 seek 全量 replay 过重。

---

## 7. 测试矩阵

| # | 场景 | 预期 | 覆盖 |
|---|------|------|------|
| T1 | 拖动 preview，Auto Off | 不清场但无 gameplay mutation；松手后 full resync 正确 | RS-1 |
| T2 | 拖动 preview，Auto On | 不触发 clear/hitsound/HoldFx；不改变判定状态 | RS-1 / RS-7 |
| T3 | 双 hold mid-hold seek | 两个 ProgressRing 独立颜色/比例正确 | RS-3 |
| T4 | hold approach 段 seek | head approach、body、ring 状态一致 | RS-3 |
| T5 | drag chain mid-drag seek | drag head/line 与 target time 一致 | RS-4 |
| T6 | 往回拖 slider | 不出现旧 note/ring/line 残留 | RS-1 / RS-4 |
| T7 | note-clear trigger storyboard | seek 后 storyboard trigger 状态符合定义语义 | RS-5 |
| T8 | manually spawned storyboard object | seek 后行为符合文档定义 | RS-5 |
| T9 | c2v3 UI show/hide/fade event | seek 后 UI state catch-up 正确 | RS-6 |
| T10 | seek 后继续播放 | cursor、active notes、score、storyboard 不重复/不漏触发 | 全链路 |

---

## 8. 相关文件

| 领域 | 文件 |
|------|------|
| Lab seek 主入口 | `engines/unity/Assets/Scripts/Navigation/CytoidLab/Game.CytoidLab.cs` |
| HUD slider | `engines/unity/Assets/Scripts/Navigation/CytoidLab/CytoidLabHudController.cs` |
| Score/judgement rewind | `engines/unity/Assets/Scripts/Navigation/CytoidLab/GameState.CytoidLab.cs` |
| Hold timeline progress | `engines/unity/Assets/Scripts/Navigation/CytoidLab/HoldNote.CytoidLab.cs` |
| Drag fast-forward | `engines/unity/Assets/Scripts/Navigation/CytoidLab/DragHeadNote.CytoidLab.cs` |
| Drag line resync | `engines/unity/Assets/Scripts/Navigation/CytoidLab/DragLineElement.CytoidLab.cs` |
| Storyboard resync | `engines/unity/Assets/Scripts/Navigation/CytoidLab/Storyboard.CytoidLab.cs` |
| Storyboard renderer rebuild | `engines/unity/Assets/Scripts/Navigation/CytoidLab/StoryboardRenderer.CytoidLab.cs` |
| Normal gameplay update | `engines/unity/Assets/Scripts/Game/Game.cs` |
| Note update / Auto | `engines/unity/Assets/Scripts/Game/Notes/Note.cs` |
| Hold runtime state | `engines/unity/Assets/Scripts/Game/Notes/HoldNote.cs` |
| Hold renderer | `engines/unity/Assets/Scripts/Game/Notes/Classic/ClassicHoldNoteRenderer.cs` |
| Progress ring shader params | `engines/unity/Assets/Scripts/Game/Elements/ProgressRing.cs` |

---

## 9. 和 Hold 调研的关系

Hold seek bug 是 resync 系统的第一个高可见度症状。它横跨：

- Preview 副作用隔离；
- active note set 重建；
- renderer fast-forward；
- Auto 状态切换；
- material state 隔离。

因此修 hold 时不建议只改 `HoldNote.CytoidLab.cs`。最小闭环至少应同时处理：

1. `ProgressRing` material 隔离；
2. preview gameplay mutation 隔离；
3. hold visual fast-forward；
4. full resync 末尾一帧 update 的 Auto 语义。

