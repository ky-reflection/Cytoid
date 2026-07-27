# 判定优化设计文档（Hit Selection by Note Time + 15ms Cluster）

| 项 | 值 |
|---|---|
| **文档状态** | Design / Implemented on branch（待手测验证） |
| **日期** | 2026-07-27（修订：弃用 `|Δt|`；成簇仅限 select / 消耗点击的 note） |
| **工作分支** | `fix/judgment-optimization`（`origin` → ky-reflection/Cytoid） |
| **基线（upstream main）** | 已含 `#187` input-consume；**尚不含**本优化 |
| **前置依赖** | [Cytoid/Cytoid#187](https://github.com/Cytoid/Cytoid/pull/187) |
| **影响面** | 主改 `InputController`：**Click/CDrag head** 成簇；**Flick 列表序对齐 `#187`**；Drag/Hold 不成簇；排除 `IsCleared`；**不改**等级窗口与计分 |
| **门禁归属** | Track B → 验证后按需 Track C backport；默认不塞进 2.1.5 A1 |

---

## 0. 一句话结论

**范围：仅对 Click / CDrag head 做成簇选择**（`FingerDown` → `OnTouch`/`TryClear`）。

**Flick：与 `#187` 原本对齐**——按 `TouchableNormalNotes` **列表序**绑定；不参与 time/x 成簇。列表序遍历时，Flick 之前积压的 Click 段先按本文成簇尝试（软续扫），全部拒绝后再绑 Flick，从而保持「列表中更早的 Click 优先于更晚的 Flick」的原优先级。

**不在成簇范围：Drag\*、Hold / LongHold。** 持续接触 / 绑定；列表序扫描（仍先于 select 查 Drag；Hold 仅 `FingerUpdate`）。

重叠 hitbox 的 **Click / CDrag head** 候选上：

1. **拍序主键** = `effectiveNoteTime` 升序（先打仍可判定的更早 note，禁止无故「跳拍」）；
2. **同拍识别** = note-to-note `NoteGap ≤ 15ms` 且 **簇跨度** `maxTime − minTime ≤ 15ms`（禁止相邻差链式扩张）；
3. **同拍内仲裁** = 触点到 **实际渲染位置 / collider 中心** 的 x 距离，再 `effectiveNoteTime`，再 `id`；
4. **簇间软续扫**：当前簇全部拒绝后，继续下一簇（**不是** 40ms Perfect 硬截断）。

`HitDelta`（触摸相对 note 的 `|Δt|`）**退出候选排序主键**，只服务等级、early/late、可否接受触摸、日志。

这是 **Click 命中选择优化**（Flick/Drag/Hold 路径对齐原行为），不是 JudgmentResolver / 计分重构（B4）。

---

## 1. 为什么弃用「`|Δt|` 最小优先」

### 1.1 对称性破坏拍序

`|Δt|` 把 early / late 当成完全对称。反例：

| Note | 状态 | HitDelta |
|------|------|----------|
| A | late 30ms（仍可打） | 30ms |
| B | early 20ms | 20ms |

按 `|Δt|` 会选 **B**，跳过仍可判定的 **A** → 跳拍，随后 A 易直接 Miss。  
谱面顺序语义应是：**除非明确视为同一拍（簇），否则后面 note 不能越过前面仍可打的 note。**

### 1.2 40ms Perfect 窗不宜兼作「同拍簇」

Perfect 带（±40ms）是 **触摸相对 note** 的等级窗，不是 **note 相对 note** 的同拍间距。用 40ms 做硬截断 / 全窗切到 x 优先，容易把 20–40ms 快速纵连、交互误认成双押。

### 1.3 两个量必须分开

| 量 | 定义 | 用途 |
|----|------|------|
| **HitDelta(n)** | `\|noteTime(n) + offset − inputTime\|` 即 `\|TimeUntilStart + JudgmentOffset\|` | 等级；early/late；note 能否接受本次触摸；日志 / 测试。**不是**排序主键 |
| **NoteGap(a,b)** | `\|noteTime(a) − noteTime(b)\|`（建议用 `effectiveNoteTime`） | 识别同 tick / 伪双押簇 |

`effectiveNoteTime(n) = start_time + JudgmentOffset`（与等级时间轴一致）。

---

## 2. 目标与非目标

### 2.1 Must

1. **候选集（成簇）= Click / CDrag head only**：Flick **不进**成簇；按列表序绑定（见 §6）。Drag / Hold **不得**进入成簇 / 簇内 x 排序。  
2. **Flick 与原本对齐**：`TouchableNormalNotes` 列表序；遇 Flick 前先冲刷此前积压的 Click 簇；reserved 检查与 `#187` 相同；Update/Up 路径不变。  
3. **单调拍序**（Click）：按 `effectiveNoteTime` 升序成簇、按簇处理；非同簇时不可跳过更早仍可接受的 note。  
4. **15ms NoteGap 簇**（note-to-note）：同 tick / `has_sibling` 真双押（间距 0）稳定覆盖；极小错位伪双可进簇；默认不把 20–40ms 纵连并成一簇。  
5. **簇内空间优先**：`|touchX − renderedNoteCenterX|` → `effectiveNoteTime` → `id`。位置取 **实际渲染 / collider 中心**，**不用** 原始 `Model.x`。  
6. **软续扫**：簇内候选全部拒绝后，继续下一簇的时间序候选；整段 Click 拒绝后再尝试列表中后续 Flick。  
7. **有效候选过滤**（Click）：排除 `IsCleared`、既有 ExtraBucketPredicate（`collidedDrag` 抑制、跨页过早等）。  
8. **同帧多指安全**：Touchable 按帧刷新 + `TryClear` 对已 clear 返回 `false`（见 §8）；Drag/Hold/Flick 扫描亦跳过 `IsCleared` / reserved。  
9. **无 Click 重叠时**与 `#187` 之后的 main 行为一致；Flick/Drag/Hold 与 `#187` 列表序语义一致。

### 2.2 Non-goals

- 改 Perfect/Great/Good/Bad/Miss 阈值或计分。  
- 抽出 JudgmentResolver（B4）。  
- Lab `JudgeFromModel` 去重（另任务；实机触摸路径以本文为准）。  
- 按刷新率动态改 15ms。  
- 首版「经典选择序」玩家开关（投诉集中再加）。  
- **对 Flick 应用本文成簇或簇内 x**（明确排除；列表序绑定，对齐 `#187`）。  
- **对 Drag / Hold 应用本文成簇或簇内 x**（明确排除；保持列表序）。  
- Drag 与 Normal **合并**成同一竞争集。

### 2.3 成功标准

- late 仍可打的 A 不被 early 的 B 抢走（非同簇）。  
- 同 tick 双押：按触点 x 落到更近的那颗。  
- ~15ms 内伪双：进同一簇，x 仲裁。  
- 20–40ms 纵连：通常 **不同簇**，按时间序逐拍处理，不因 x 抢拍。  
- 孤立 note / 宽窗 Great·Good：可打性不回归。  
- 同帧两指点不同 note：第二指不被已 clear note 吞掉。

---

## 3. As-Is（仍成立的问题）

```
GameTouchInput.Update (-100)  → 可连续 FingerDown
Game.Update                   → onGameUpdate → 重建 Touchable*
```

- Touchable 桶序 ≈ `SpawnedNotes` 键序，非拍序。  
- 选择 = 碰撞后列表序第一个 Accept。  
- `#187`：`OnTouch`/`TryClear` 返回是否 clear；无效 touch 继续扫。  
- **缺口**：已 clear 仍留在本帧列表；`TryClear` 对已 clear 返回 `IsCleared==true` → 同帧后指被吞（见 §8）。

分支 tip `11f19274` 的 `|Δt|` + 40ms cluster **已废弃**；当前分支基于 `main`（含 `#187`）按本文重写。

---

## 4. To-Be 算法（规范）

### 4.1 常量

```csharp
public const float NoteClusterGapSeconds = 0.015f; // 15ms；测试可扫 12 / 15 / 20，默认固定 15
```

- **不要** 按刷新率缩放。  
- 15ms 是保守首发值，不一定数学最优；发版前用固定默认 + 可选内部调参，不写进玩家设置（首版）。

### 4.2 收集有效碰撞候选（仅 Click / CDrag head 段）

`OnFingerDown` **按 `TouchableNormalNotes` 列表序**扫描：

- 遇 **Flick**：先对当前积压的 Click 段调用成簇 Accept；若无人接受，再按 `#187` 做 reserved 检查并 `StartFlicking`（绑定即消费 Down）。  
- 遇 **Click / CDrag head**：通过 ExtraBucketPredicate 后加入当前段 `C`（不成交，先积压）。  
- 扫描结束：对剩余 Click 段成簇 Accept。

成簇输入：

```
C = { n |
      n ∈ 当前列表段内的 Click/CDrag head
      ∧ n ≠ null
      ∧ !n.IsCleared
      ∧ DoesCollide(n, p)
      ∧ ExtraBucketPredicate(n)   // collidedDrag 抑制；跨页过早等
      ∧ CanAcceptTouch(n)         // 建议：等级窗外不进 C
    }
```

**Flick / Drag / Hold：** 不构建 `C`、不调用成簇排序。

`CanAcceptTouch`（Click）：与 `OnTouch` / `TryClear` 前「可尝试」语义对齐；开放问题见 §12。

### 4.3 成簇（禁止链式扩张）

1. 将 `C` 按 `effectiveNoteTime` **升序**排列（tie-break：`id`）。  
2. **先定序，再划簇**——**禁止**在 `Sort` comparator 里两两 `NoteGap` 比较来「排序」，那无法表达跨度约束。  
3. 划簇规则（推荐线性扫描）：

```
clusters = []
current = [sorted[0]]
for n in sorted[1..]:
    spanIfAdd = n.effectiveNoteTime - current[0].effectiveNoteTime
    if spanIfAdd <= NoteClusterGapSeconds:
        current.append(n)
    else:
        clusters.append(current)
        current = [n]
clusters.append(current)
```

等价约束：

- 簇内任意两点的时间差 ≤ 15ms（因已按时间排序，⇔ `maxTime − minTime ≤ 15ms`）。  
- **禁止**「相邻都 ≤15ms」链式并成更大簇：例如时间戳 `0, 10, 20` ms → 两个簇 `[0,10]` 与 `[20]`（或 `[0]` 与 `[10,20]`，取决于扫描；**跨度**不得变为 20ms 的单簇）。  
  上式以 **相对簇内最早 note** 的跨度截断，因此 `0,10,20` → `[0,10]` + `[20]`。

同 tick（`NoteGap=0`）/ 谱面 `has_sibling` 真双押：必然 `span=0`，同簇。

### 4.4 簇间 / 簇内次序

**簇间：** 按簇内最小 `effectiveNoteTime` 升序（与构造顺序一致）。

**簇内排序键（升序，越小越优先）：**

1. `|touchX − renderedNoteCenterX|`  
2. `effectiveNoteTime`  
3. `Model.id`

然后：

```
for cluster in clusters:
    for note in OrderWithinCluster(cluster):
        if Accept(note):   // OnTouch / 绑定等，须真正接受
            done
    // 本簇无人接受 → 软续扫下一簇
```

### 4.5 渲染位置（x）

- 使用 **实际显示位置**：优先 `Collider.bounds.center`（与 `DoesCollide`/`OverlapPoint` 一致），或等价的当前 `transform` 世界/本地 x（与 InputController 触点坐标系一致）。  
- **禁止** 仅用 chart `Model.x` / 未应用 Override·Storyboard 位移前的原始坐标。  
- 仅用于 **select** 簇内仲裁；Drag 移动中的插值位置不进入本文排序。

### 4.6 HitDelta 残留职责

| 用途 | 是否用 HitDelta |
|------|-----------------|
| 候选排序主键 | **否** |
| `CalculateGrade` Perfect/Great/… | 是（现有） |
| early/late 展示与权重 | 是 |
| `CanAcceptTouch` / 窗内可打 | 是（与等级窗一致） |
| 日志、单测断言 | 是 |

---

## 5. 场景演算（select note）

设 offset=0，触点同时命中下列 **Click（或同桶 select）** note。Drag/Hold 不适用下表。

| 场景 | Notes (effectiveTime, x) | 期望 |
|------|--------------------------|------|
| 跳拍陷阱 | A late 30ms；B early 20ms；不同簇 | **先 A**（时间序），不得因 \|Δt\| 选 B |
| 同 tick 双押 | A、B 同 time，x=0.3 / 0.7；触点偏右 | **B**（x 更近） |
| 伪双 10ms | A@0，B@10ms，hitbox 重叠 | 同簇；x 近者优先 |
| 纵连 0/10/20ms | 三音 | 簇 `[0,10]` 与 `[20]`；处理完第一簇再第二簇；**不是** 一个 20ms 大簇 |
| 纵连 30ms | A@0，B@30ms | 两簇；先 A 后 B；不因 B 的 x 更近而抢先 |
| 宽窗 only | 两音都在 Good，间距 50ms | 两簇；HitDelta 不排序，但仍可按拍序尝试 |
| 同帧双指 | 指1 clear A；指2 打 B | 指2 **不得** 因 A 仍在列表且 `IsCleared` 被吞 |

---

## 6. 与 note 类型 / 分桶

| 类型 | FingerDown | 规则 |
|------|------------|------|
| Click / CDrag head | clear | **本文成簇 + 簇内 x**（仅在列表段内，段边界为 Flick） |
| Flick | 绑定 | **`#187` 列表序**；不进成簇；Update/Up 不变 |
| Drag* / CDrag child | 持续接触 | **列表序**；仍 **先于** Normal 查 Drag |
| Hold / LongHold | Update 绑定 | **列表序**；Down 不进 Hold（`#187`） |

Click↔Flick 优先级：由 `TouchableNormalNotes` **列表序**决定（与原本相同）。成簇只重排「两个 Flick 之间（或首尾）的 Click 段」，不会把更晚的 Flick 提到更早的 Click 之前，也不会把 Flick 按 x/time 重排。

`collidedDrag`、跨页过早等对 **Click** 的 ExtraBucketPredicate 保留；Flick 仍不走这些过滤（与原本一致）。

---

## 7. 实现约束

1. **先排序再划簇**，再簇内二次排序；不要把 NoteGap 塞进 `Comparison<Note>` 当总序。  
2. 复用 `List` 缓冲，避免热路径 LINQ。  
3. `Accept` 成功条件与 `#187` 一致：真正 clear / 真正绑定；**已 `IsCleared` 不得视为成功消费**（应在收集阶段排除；防守性再在 `TryClear` 开头 `if (IsCleared) return false`）。  
4. Rebase `main` 后重写 tip，丢弃 `|Δt|`+40ms 选择逻辑；文档与分支名仍为 `fix/judgment-optimization`。  
5. 坐标系统一：触点转换用的 camera / 本地空间须与 collider 中心一致。

### 7.1 建议提交切片

| Commit | 内容 |
|--------|------|
| 1 | `fix(gameplay): skip already-cleared notes in hit selection`（§8，可单独先合） |
| 2 | `fix(gameplay): select overlapping hits by note time and 15ms clusters` |
| 3 | docs：本文定稿 |

---

## 8. 同帧多指 / IsCleared 吞指（必修）

**事实（已核实）：**

- Touchable 仅在 `onGameUpdate` 重建；`GameTouchInput`（−100）可在同一次 `Update` 连发多个 `FingerDown`。  
- `Clear` → `AwaitAndCollect` → `DelayFrame(0)` 才 `Collect`（关 collider、移出 `SpawnedNotes`）。  
- `#187` 后 `TryClear` 返回 `IsCleared`：对 **已 clear** 的 note 仍为 `true` → 后指被消费。

**Must fix（与选择算法同属本优化门禁）：**

1. 收集候选时 `!IsCleared`。  
2. `TryClear` / `OnTouch`：若已 `IsCleared`，返回 **`false`**（语义：本次触摸未新接受），避免防守路径再吞指。  
3. （可选增强）`Clear` 同步从当前 `Touchable*` 移除，或标记 reserved；非必须若 1+2 完备。

---

## 9. 备选与否决

| 方案 | 结论 |
|------|------|
| `|Δt|` 全局最小优先 | **否决**（跳拍） |
| `|Δt|` + Perfect 40ms 硬截断 | **否决**（窗语义错位；纵连误并） |
| 仅 x 距离、无视时间 | **否决** |
| 相邻 NoteGap≤15 链式并簇 | **否决**（0/10/20 会并成 20ms） |
| 簇内拒绝后硬停止、不扫后簇 | **否决**（与软续扫相反） |
| 15ms 随 fps 变化 | **否决** |
| 本阶段跨 Drag/Normal 统一竞争 | **延后** Phase 2 |
| 对 Flick 成簇或簇内 x | **否决**：Flick 列表序绑定，对齐 `#187` |
| 对 Drag / Hold 成簇或簇内 x | **否决（本轮）** |
| 先扫完全部 Flick 再扫 Click（或相反） | **否决**：会破坏列表序下 Click↔Flick 原优先级 |

---

## 10. 测试计划

### 10.1 纯函数 / 单测（建议抽出划簇 + 簇内排序）

| 用例 | 期望 |
|------|------|
| 空 / 单候选 | 平凡 |
| late30 + early20 | 先 late |
| 同 time 不同 x | x 近者先 |
| times 0,10,20 | 两簇，跨度均 ≤15 |
| times 0,30 | 两簇 |
| 含 IsCleared | 不出现在 C |

### 10.2 手测

| ID | 场景 | 期望 |
|----|------|------|
| T1 | 跳拍陷阱谱 | 先清可打的过去 note |
| T2 | 同 tick 双押 | 跟手指 x |
| T3 | ~10ms 伪双 | 同簇 x 仲裁 |
| T4 | 30ms 纵连 | 按拍，不 x 抢拍 |
| T5 | 孤立各类型 | 无回归 |
| T6 | JudgmentOffset≠0 | 拍序用 effectiveTime |
| T7 | 同帧双指两 note | 不吞第二指 |
| T8 | Storyboard 位移 note | x 跟渲染位置，非 Model.x |

调参：内部对比 **12 / 15 / 20ms**，默认锁定 **15**。

---

## 11. 分阶段

| 阶段 | 内容 | 状态 |
|------|------|------|
| Phase 0 | `#187` input-consume | main 已合 |
| Phase 1a | IsCleared / reserved 过滤 + TryClear 语义修正 | **已实现（本分支）** |
| Phase 1b | Click/CDrag head 成簇 + 15ms + 簇内 x；Flick 列表序对齐 `#187`；Drag/Hold 退出成簇 | **已实现（本分支）** |
| Phase 2 | 可选：触点距离次级键扩展；跨类型统一竞争；Drag/Hold 若另需仲裁则单独立项 | 未开始 |
| Phase 3 | JudgmentResolver（B4） | 中长期 |

---

## 12. 开放问题

1. **`CanAcceptTouch` 与 `CalculateGrade()==None` 是否完全等价？（Click）**  
   Click / CDrag head 的早期拒绝与等级窗外须列类型表，避免进簇占坑。Flick/Drag 不进成簇谓词。  

2. **簇内第二键用 `effectiveNoteTime` 还是 HitDelta？**  
   本文：同簇已近同时，用 `effectiveNoteTime` 稳定；HitDelta 不做簇内主键。  

3. **playEvents 是否记录 `hitSelectionVersion=2`？**  
   建议有 telemetry 时加上；非阻断。  

4. ~~Hold 绑定「未过期」~~ — **不适用**：Hold 不在成簇范围。

---

## 13. 附录

### 13.1 术语

| 中文 | 含义 |
|------|------|
| 判定优化 | 本文 Phase 1（Click 选择层；Flick 对齐原列表序） |
| select / Click 段 | FingerDown → TryClear 的 Click / CDrag head；成簇仅作用于这些 |
| Flick 绑定 | `#187` 列表序 `StartFlicking`；不成簇 |
| 判定重构 | B4 resolver，非本分支 |
| HitDelta | 触摸↔note 时间差绝对值 |
| NoteGap | note↔note 时间差 |
| effectiveNoteTime | `start_time + JudgmentOffset` |
| 簇 / cluster | select 候选上 NoteGap 跨度 ≤15ms 的同拍集合 |
| 软续扫 | 簇拒绝后继续下一簇 |
| 跳拍 | 后 note 越过仍可打的前 note |

### 13.2 关键文件

| 文件 | 角色 |
|------|------|
| `InputController.cs` | 收集 / 成簇 / 选择 |
| `Note.cs` | Clear / TryClear / 等级 / IsCleared |
| `GameTouchInput.cs` | 同帧多 FingerDown |
| `NoteRenderer.cs` | collider / DoesCollide |
| `ClassicNoteRenderer.cs` | hitbox 半径 |

### 13.3 口径变更记录

| 时间 | 变更 |
|------|------|
| 初稿 | `|Δt|` 主键 + Perfect 40ms cluster 硬截断 |
| 2026-07-27 17:24 | 提出 HitDelta/NoteGap 分离、15ms、簇内 x；软截断 |
| 2026-07-27 17:30 | **定稿方向**：弃用 `|Δt|` 主键；`effectiveNoteTime` 单调拍序；跨度约束成簇；簇内 x；Δt 仅等级/可接受性 |
| 2026-07-27 | **范围收窄**：成簇候选仅 Click/CDrag head；Drag/Hold 退出 |
| 2026-07-27 | **Flick 对齐原本**：列表序绑定；成簇仅作用于 Flick 之间的 Click 段 |

---

*实现以本文为准；分支已按 Phase 1a/1b（select-only）调整。合入前完成 §10 验证。*
