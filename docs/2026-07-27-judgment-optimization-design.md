# 判定优化设计文档（Hit Selection by Note Time + 15ms Cluster）

| 项 | 值 |
|---|---|
| **文档状态** | Implemented on branch（与代码对齐；待手测验证） |
| **日期** | 2026-07-28 |
| **工作分支** | `fix/judgment-optimization`（`origin` → `ky-reflection/Cytoid`） |
| **代码 tip（评审）** | 以本分支最新 push 为准；相对 `main` 仅改 `InputController.cs` / `HoldNote.cs` / `Note.cs` |
| **基线** | `main` 已含 `#187` input-consume |
| **前置依赖** | [Cytoid/Cytoid#187](https://github.com/Cytoid/Cytoid/pull/187)：consume 语义保留；**Hold 完全退出 Down 被本文修正** |
| **影响面** | Click/CDrag head + 未持有 Hold：成簇选择并消费 Down；Flick：列表序绑定；Drag：列表序擦判；不改等级窗/计分 |
| **门禁** | Track B；验证后按需 Track C backport；默认不塞 2.1.5 A1 |

---

## 0. 一句话结论

`FingerDown` 上：

1. **Drag** 仍先列表序扫描（不成簇）；成功 clear 后记录 `acceptedDrag`，**不**独占 Down。
2. 将 **未持有 Hold** 与 **Click / CDrag head / Flick** 按 `Model.id`（≈ `SpawnedNotes` 序）**合流**成一条扫描流。
3. 流上遇 **Flick**：先冲刷此前积压的 Click+Hold 成簇段；无人接受再按 `#187` 列表序绑定 Flick。
4. 积压的 **Click / CDrag head / Hold** 按 `effectiveNoteTime` 成簇（跨度 ≤ **15ms**，禁止链式扩张）；簇内 **非 Hold > Hold**，再 **渲染中心 x**，再 time，再 id；软续扫。
5. **Hold 接受** = `UpdateFinger` 绑定并 **消费 Down**；**Click 接受** = `OnTouch`/`TryClear` 真正 clear。
6. 若本 Down 已有 `acceptedDrag`：Click / CDrag head / Hold / Flick 均走 **DragCoHit** 门（select 比 Drag **晚超过 15ms** 则阻断；更早或 ≤15ms 内继续）。被阻断的 Flick **仍先冲刷**前段。
7. `TryClear`：已 `IsCleared` 返回 `false`，避免同帧后指被吞。

`HitDelta`（`|TimeUntilStart + JudgmentOffset|`）**不做排序主键**，只用于等级 / early-late / 可打性 / 日志。

---

## 1. 问题与动机

### 1.1 旧选择序

Touchable 桶按 `SpawnedNotes` 键序填充；碰撞后列表序第一个 Accept 胜出 → 易固定偏向低 id，伪双/纵连手感差。

### 1.2 `|Δt|` 不可作主键

early/late 对称会导致跳拍（late 仍可打的 A 被 early 的 B 抢走）。拍序应用 **note 时间**，不是触摸误差绝对值。

### 1.3 `#187` 与 Hold

`#187` 把 Hold 踢出 Down，避免空 `OnTouch` 抢点击。过激：同拍应 **Click 优先于 Hold**，跨拍仍按时间；Hold 命中须 **消费 Down**。空 `OnTouch` 问题用优先级解决，而不是禁止 Hold 进 Down。

### 1.4 两个时间量

| 量 | 定义 | 用途 |
|----|------|------|
| **HitDelta(n)** | `\|start_time + JudgmentOffset − Game.Time\|` | 等级、early/late、可打性、日志 |
| **NoteGap / effectiveNoteTime** | `effectiveNoteTime = start_time + JudgmentOffset` | 成簇、拍序主键 |

---

## 2. 目标 / 非目标

### Must

- Click/CDrag/Hold：`effectiveNoteTime` 成簇 + 15ms 跨度约束 + 簇内非 Hold 优先 + x。
- Flick：不进成簇；合流列表序；绑前冲刷前段。
- Drag：不成簇，先于 select；成功后用 **DragCoHit 15ms** 门控后续 select（含 Flick）；删除 `collidedDrag + Page.Duration/8`。
- Hold Down：绑定并消费；Update 仍可滑入/多指。
- 排除 `IsCleared`；`TryClear` 已 clear → `false`。

### Non-goals

- 改 Perfect/Great/… 阈值或计分。
- JudgmentResolver（B4）、Lab `JudgeFromModel` 去重。
- Flick/Drag 成簇；按 fps 改 15ms；玩家「经典序」开关。

---

## 3. FingerDown 算法（与实现一致）

### 3.1 常量

```csharp
public const float NoteClusterGapSeconds = 0.015f; // 成簇跨度，固定 15ms
public const float DragCoHitWindowSeconds = 0.015f; // Drag 成功后允许同 Down 打 select 的晚于窗口
```

### 3.2 步骤

```
1) Drag 列表序：
   碰撞且 OnTouch 成功 → acceptedDrag = note，break
   （先 clear Drag；不 return，不独占 Down）

2) 合流扫描（TouchableHoldNotes ∪ TouchableNormalNotes，按 Model.id 升序归并）：
   跳过 null / IsCleared / 未碰撞
   if Flick:
       if TryAcceptSelectClickCluster(...): return   // 冲刷前段 Click+Hold（即使本 Flick 随后被 DragCoHit 阻断）
       reserved 检查后
       if !IsEligibleSelectAfterDrag(...): skip
       StartFlicking；return
   if Hold:
       if IsHolding 或 finger 已在 HoldingNotes: skip
       if !IsEligibleSelectAfterDrag(...): skip
       hitCandidates.Add(Hold)
   if Click/CDrag head:
       if !IsEligibleSelectAfterDrag(...): skip
       hitCandidates.Add(note)

3) TryAcceptSelectClickCluster(...)  // 冲刷尾段
```

`IsEligibleSelectAfterDrag`（Click / CDrag head / Hold / **Flick** 全走）：

| 条件 | 结果 |
|------|------|
| 无 `acceptedDrag` | 通过（再看跨页） |
| `effectiveNoteTime(select) − effectiveNoteTime(Drag) ≤ DragCoHitWindowSeconds` | 通过（含 select 更早、或晚不超过 15ms） |
| select 比 Drag **晚超过** 15ms | **阻断** |
| 跨页过早（历史门闩） | 阻断 |

已删除旧规则：`collidedDrag && |TimeUntilStart| > Page.Duration/8`。

Flick 仍不进成簇；被 DragCoHit 阻断时 **不绑、不消费 Down**，但前段冲刷已发生（段边界保留）。

### 3.3 `TryAcceptSelectClickCluster`

对 `hitCandidates` 调用 `OrderHitCandidatesByNoteTimeClusters`：

1. 按 `effectiveNoteTime`、`id` 排序。  
2. 划簇：相对簇内最早 note 的跨度 ≤ 15ms（`0,10,20` → `[0,10]` + `[20]`，禁止相邻链式并成 20ms）。  
3. 簇内排序：`SelectTypePriority`（Hold=1，其它=0）→ `|touchX − collider.bounds.center.x|` → time → id。  
4. 软续扫；Accept：
   - Hold：`!IsHolding` 且 finger 未占用 → `HoldingNotes.Add` + `UpdateFinger(true)` → **消费**（return true）
   - 其它：`OnTouch` 成功 → 消费

清空 `hitCandidates`。

### 3.4 FingerUpdate / Up

- Flick：既有 `UpdateFingerPosition`。
- Drag：列表序。
- Hold：若 Down 未绑定，列表序滑入绑定 / 多指叠持；Up 松绑。

### 3.5 `Note.TryClear`

```csharp
if (IsCleared) return false;
// ... Clear / CalculateGrade ...
return IsCleared;
```

`HoldNote.OnTouch` 仍返回 `false`（不走 TryClear）；绑定只在 InputController。

---

## 4. 类型表

| 类型 | FingerDown | 规则 |
|------|------------|------|
| Drag* / CDrag child | 擦判 | 列表序；先于 select；不成簇；成功则 `acceptedDrag` |
| Click / CDrag head | clear | 与 Hold 同成簇池；簇内优先于 Hold；走 DragCoHit |
| Hold / LongHold | 绑定并消费 | 合流进 id 流；成簇；簇内低于 Click；Update 可滑入；走 DragCoHit |
| Flick | 绑定 | 合流 id 流上列表序；不进成簇；前段先冲刷；走 DragCoHit |

**Click↔Flick：** 由合流后的 **id 序**决定（Hold 插入同序）。遇 Flick 只冲刷 **流中更早** 的积压候选，不会把更晚 Flick 提到更早 Click 之前。

---

## 5. 场景期望

| 场景 | 期望 |
|------|------|
| late 可打 A vs early B（不同簇） | 先 A |
| 同 tick 双 Click，触点偏右 | 右侧（x） |
| Click+Hold 同簇 | Click |
| Hold 早于 Click 且 >15ms，Hold 可绑 | Hold 消费，Click 不触发 |
| 0/10/20ms 三音 | 两簇，跨度 ≤15 |
| 同帧双指 | 第二指不被已 clear 吞 |
| Storyboard 位移 | x 用 collider 中心，非 `Model.x` |
| Flick 与更早 Click 段 | 先尝试更早段；拒绝后再绑 Flick |
| Drag + select ≤15ms（含更早） | 同 Down 可继续处理 select |
| Drag + select 晚 >15ms（含 Flick） | select 阻断；Flick 仍先冲刷前段 |

---

## 6. 同帧多指

- Touchable 仅 `onGameUpdate` 重建；`GameTouchInput`（−100）可同 `Update` 多 `FingerDown`。
- `Clear` → `DelayFrame(0)` 才 `Collect`。
- 收集跳过 `IsCleared`；`TryClear` 已 clear → `false`。
- Hold：`IsHolding` 现场检查，避免同帧快照陈旧导致重复绑。

---

## 7. 否决方案

| 方案 | 结论 |
|------|------|
| `|Δt|` 主键 / Perfect 40ms 硬截断 | 否决 |
| 相邻 NoteGap 链式并簇 | 否决 |
| Flick 或 Drag 成簇 | 否决 |
| Hold 完全退出 Down（`#187`） | 否决（改为簇内低于 Click + 消费） |
| 先扫完全部 Flick 再扫 Click | 否决 |

---

## 8. 测试清单

| ID | 场景 |
|----|------|
| T1 | 跳拍陷阱 |
| T2 | 同 tick 双押跟 x |
| T3 | Click+Hold 同簇 → Click |
| T4 | Hold 更早跨簇 → Hold 消费 |
| T5 | 30ms 纵连按拍 |
| T6 | Flick 与前段 Click |
| T7 | 同帧双指 |
| T8 | JudgmentOffset ≠ 0 |
| T9 | Storyboard 位移 x |
| T10 | Drag + 同窗 ≤15ms Click/Flick 可同 Down；晚 >15ms 阻断（含 Flick，前段仍冲刷） |

调参对比 12/15/20ms，默认锁 15。

---

## 9. 实现文件与提交演进

| 文件 | 角色 |
|------|------|
| `engines/unity/Assets/Scripts/Game/InputController.cs` | Down 合流 / 成簇 / Accept |
| `engines/unity/Assets/Scripts/Game/Notes/Note.cs` | `TryClear` 已 clear → false |
| `engines/unity/Assets/Scripts/Game/Notes/HoldNote.cs` | `OnTouch` 仍 false；绑定在 Controller |
| `GameTouchInput.cs` | 同帧多 FingerDown（未改逻辑，约束来源） |

相对 `main` 的典型提交链（历史）：

1. `4c7c3aca` — 成簇骨架 + TryClear  
2. `7a41ed7e` — 成簇限 select；Drag 列表序  
3. `1f9cad86` — Flick 列表序  
4. `af618df0`+ — Hold 重回 Down；簇内低于 Click；**id 合流**（与本文一致）

---

## 10. 口径变更

| 时间 | 变更 |
|------|------|
| 初稿 | `|Δt|` + 40ms |
| 07-27 17:30 | effectiveNoteTime + 15ms NoteGap；Δt 退出主键 |
| 07-27 | 曾收窄为仅 Click；Flick 列表序 |
| 07-28 | Hold 重回 Down；簇内低于 Click；消费 Down |
| 07-28 | **文档对齐实现**：Hold 与 Normal 按 id 合流后再遇 Flick 冲刷 |
| 07-28 | **DragCoHit**：`acceptedDrag` + 15ms 晚于窗阻断 Click/CDrag head/Hold/Flick；删除 `collidedDrag + Page.Duration/8`；Flick 阻断前仍冲刷前段 |

---

*本文与当前分支上 `InputController` 实现一致。合入前完成 §8 手测。*
