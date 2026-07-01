# Cytoid Core Unity 审计报告（最终版）

> Storyboard Bug · 内存泄漏 · 性能优化

| 项 | 值 |
|---|---|
| **审计日期** | 2026-06-30；补充评审 2026-07-01 |
| **代码基准** | 分支 `feature/cytoid-player`，提交 `8243ca7a` |
| **审计范围** | `engines/unity/Assets/Scripts/`（~293 个 C# 文件；重点 Storyboard ~40 文件 + Game 层 + Screen/Host/Utils） |
| **审计方式** | 两份草稿报告合并 + 关键条目逐条读码核实；2026-07-01 追加 Storyboard/c2v3 兼容性走查（行号以审计时代码为准，后续提交可能漂移） |
| **验证状态** | 静态代码审计；**未经真机/Editor 运行时复测** |

本文档合并 `docs/audit-storyboard-memory-performance.md` 与 `docs/2026-06-30-storyboard-leak-perf-audit.md`，去重后以代码核实结果为准。条目编号 **SB**（Storyboard Bug）、**ML**（Memory Leak）、**PF**（Performance）便于跟踪修复 PR。

---

## 执行摘要

当前 Unity 核心在 **Storyboard 触发器销毁路径**、**事件监听生命周期**、**ObjectPool 释放** 三处存在高置信度缺陷，会在游玩/retry/切关时表现为崩溃、对象残留或 GC 抖动。PC standalone player（`CytoidPlayerHudController`）在事件订阅与 seek 路径上有额外累积风险。

**建议分三批修复：**

1. **P0 — 崩溃与功能失效**（1–2 PR）：SB-1/2/3、SB-6、SB-4
2. **P1 — 会话级泄漏**（1–2 PR）：ML-1/2/3/4/5、ML-6、ML-12
3. **P2 — 帧率与 GC**（按需）：PF-1/2/3–7 及中低优先级项

---

## 修复优先级总表

| 序 | ID | 维度 | 问题 | 影响 |
|---|---|---|---|---|
| 1 | SB-1 | Bug | `DestroyObjectsById` 字典 key 类型错误 | trigger 销毁抛 `KeyNotFoundException` |
| 2 | SB-2 | Bug | `OnNoteClear` 在 `foreach` 中 `Remove` | `InvalidOperationException`，后续 trigger 跳过 |
| 3 | SB-3 | Bug | `OnGameUpdate` 销毁记录用错组件类型 | 父对象残留 typed 列表 → 每帧 NRE |
| 4 | ML-3 | 泄漏 | `ObjectPool.Dispose` 未释放特效池 | 每局数百 `ParticleSystem` GO 孤儿 |
| 5 | ML-1 | 泄漏 | `Screen` 语言变更 lambda 不移除 | 每次屏切换钉住整个 UI 子树 |
| 6 | PF-2 | 性能/Bug | `GetScannerPositionY` 每帧 `CurrentPageId = 0` | 多页谱 O(n) + 破坏共享页状态 |
| 7 | SB-7 | Bug | `Note.Override` controller 生命周期结束后不复位 | note 余下整曲视觉错位 |
| 8 | ML-2 | 泄漏 | `CytoidPlayerHudController` 事件未对称移除 | PC player 多次游玩累积 |
| 9 | PF-1 | 性能 | `OnGameUpdate` 每帧分配数组/字典 | storyboard 每帧 GC |
| 10 | PF-3–7 | 性能 | UI 文本/DOTween 每帧重建 | 全场景字符串/tween 分配 |
| 11 | SB-4/5/6 | Bug | 监听泄漏、双重 Dispose、`async void`、坏 note 引用 | retry/load 鲁棒性 |
| 12 | ML-6 | 泄漏 | renderer 子类不调 `base.Dispose()` | 每渲染器每关一个占位 GO |
| 13 | ML-12 | 泄漏 | `Game.Start` catch 不调 `Dispose` | 加载失败时整池泄漏 |
| 14 | ML-5 | 泄漏 | `GamePlayEventRecorder.End` 仅 bridge 路径 | standalone 静态引用跨局 |
| 15 | SB-8–12, SB-13–20, SB-29–34 | Bug | 解析/状态/共享资源/c2v3 兼容等 | 见下文 |
| 16 | ML-7–15 | 泄漏 | Audio、DOTween、NLayer、Video RT 等 | 见下文 |
| 17 | PF-8–15 | 性能 | Scanner、Note 事件、FindStates 等 | 见下文 |

---

## 一、Storyboard Bug

### P0 — Critical（崩溃 / 功能整体失效）

#### SB-1. `DestroyObjectsById` 字典 key 类型不匹配

- **文件**：`Storyboard/StoryboardRenderer.cs:294-303`
- **代码**：

```csharp
TypedComponentRenderers[it.GetType()].Remove(it);  // it.GetType() → SpriteRenderer 等
```

- **根因**：`TypedComponentRenderers` 的 key 是**模型类型**（`typeof(Sprite)` 等，见 `:99-103`、`:164`），`it.GetType()` 返回**渲染器类型**。
- **失败场景**：任何 `"destroy": ["id"]` trigger → `KeyNotFoundException`；对象可能已 `Dispose` 但异常向上传播，销毁功能失效。
- **修复**：`var componentType = it.Component.GetType(); TypedComponentRenderers[componentType].Remove(it);`；spawn 时也可把 typed key 缓存在渲染器上。

#### SB-2. `OnNoteClear` 在枚举期间修改 `Triggers`

- **文件**：`Storyboard/Storyboard.cs:150-184`
- **根因**：`foreach (var trigger in Triggers)` 内 Score 分支直接 `Triggers.Remove(trigger)`（`:170`）；NoteClear/Combo 经 `OnTrigger` → `:183` 在 `uses` 用尽时同样 `Remove`。
- **失败场景**：Score trigger **每次** note clear 都抛 `InvalidOperationException`；同次调用中后续 trigger 全部跳过。
- **修复**：

```csharp
for (int i = Triggers.Count - 1; i >= 0; i--)
{
    var trigger = Triggers[i];
    // ... 判定后收集 id 或倒序 RemoveAt(i)
}
```

或 `foreach (var trigger in Triggers.ToList())` 收集待删项后统一移除。

#### SB-3. `OnGameUpdate` 销毁父对象时记录错误的组件类型

- **文件**：`Storyboard/StoryboardRenderer.cs:217-252`
- **根因**：子对象（如 Sprite）带 `target_id` 指向父（如 Line）且 `fromState.Destroy == true` 时，`renderersToDestroy[parentId] = type` 的 `type` 是**外层循环变量**（子类型 Sprite）。`:252` 对父渲染器执行 `TypedComponentRenderers[type].Remove(renderer)` → Line 不在 Sprite 列表中，`Remove` 为 no-op。
- **失败场景**：父 Line 已从 `ComponentRenderers` 移除并 `Dispose`，但留在 `TypedComponentRenderers[typeof(Line)]`；此后每帧遍历已 Dispose 渲染器 → `NullReferenceException`。
- **修复**：写入 `renderersToDestroy` 时用**目标渲染器**的 `Component.GetType()`，而非外层 `type`。

---

### P1 — High（鲁棒性 / 状态污染）

#### SB-4. 事件监听泄漏 + 潜在双重 Dispose

- **文件**：
  - `Storyboard/Storyboard.cs:138-147` — `Dispose` 仅移除 `onGameLateUpdate`；`Initialize` 注册 `onNoteClear`、`onGameDisposed`
  - `Storyboard/StoryboardRenderer.cs:121-126` — 注册 `onGameDisposed/onGamePaused/onGameUnpaused`，`Dispose` 未移除
  - `Game/Game.cs:906-914` — `Dispose` 先清 update 监听再 `onGameDisposed.Invoke`
- **根因**：`Storyboard` 与 `StoryboardRenderer` 均订阅 `onGameDisposed` 并调 `Dispose`；`onNoteClear`、`onGamePaused`、`onGameUnpaused` 无对称移除。
- **修复**：保存委托引用，`Dispose` 中全部 `RemoveListener`；加 `if (disposed) return` 重入保护。

#### SB-5. `SpawnObjectById` 为 `async void`

- **文件**：`Storyboard/StoryboardRenderer.cs:277`
- **根因**：`async void` 未捕获异常可终止域；内部 `SpawnObjects` 在 parent/target 缺失时抛 `InvalidOperationException`（`:173-183`）。
- **修复**：改为 `async UniTask`，由 `OnTrigger` `await` 并 try/catch 记日志。

#### SB-6. `ParseTime` 无 `note_map` 存在性检查

- **文件**：`Storyboard/Storyboard.cs:569`

```csharp
var note = Game.Chart.Model.note_map[id];
```

- **根因**：storyboard 引用不存在 note id → `KeyNotFoundException`；被 `Game.Initialize` try/catch（`:277-281`）捕获后**整段 storyboard 被禁用**。
- **修复**：`TryGetValue` + `Debug.LogWarning`，返回 `null`。

#### SB-7. `NoteControllerEaser` 生命周期结束后未清 `ChartModel.Note.Override`

- **文件**：
  - `Storyboard/Notes/NoteControllerEaser.cs:17-158` — 每帧写 `Note.Override.X/Y/Z/...`
  - `Storyboard/Notes/NoteControllerRenderer.cs:46-54` — `Dispose` 只销毁 placeholder，不清 `Override`
  - `StoryboardRenderer.cs:79-141` — seek 前已有 `ResetRuntimeStateForSeek()` 全局复位
- **修正说明（2026-07-01）**：当前代码已经在 Lab seek/full resync 前重置全部 note override，因此问题不是“seek 一定残留”。高风险路径是普通播放中 storyboard controller 被 `destroy` trigger / destroy state 销毁，或对象 Dispose 后，没有按 controller ownership 还原被它写过的 note override。
- **失败场景**：controller 退出生命周期后，目标 note 的 `YMultiplier/YOffset/Opacity/Size/Color` 等仍保持最后一次 storyboard 写入值，后续页面或 c2v3 PageFunction 测试会出现“看似 PageFunction 算错”的残留偏移。
- **修复**：`NoteControllerRenderer.Dispose()` 清理自己负责的 note override；更稳妥的是引入 note override owner/snapshot，销毁时只还原本 controller 写过的字段，避免多个 controller 互相覆盖。

#### SB-13. `StageObject<TS>` 遮蔽基类 `TargetId`

- **文件**：`Storyboard/StoryboardModel.cs:12, 55-57`；`Storyboard/Storyboard.cs:433-437`
- **根因**：派生类重新声明 `public string TargetId`，`LoadObject` 对象初始化器写入派生字段；`GetTargetRenderer` 读基类 `Object.TargetId` 仍为 `null`。
- **修复**：删除 `StageObject<TS>.TargetId` 重复声明，或显式写基类字段。

#### SB-14. `LineStateParser` 未调用 `ParseStageObjectState`

- **文件**：`Storyboard/Lines/LineStateParser.cs:14-38`
- **对比**：`SpriteStateParser`/`TextStateParser`/`VideoStateParser` 均调用 `ParseStageObjectState`；Line 只调 `ParseObjectState` 并手写部分字段。
- **影响**：Line 的 `x/y/z/scale/rot/opacity`（经 `ParseStageObjectState` 解析）及与 stage 相关的 width/height/fill_width 等丢失或行为不一致。
- **修复**：`ParseStageObjectState(state, json, baseState);` 后保留 Line 专有字段（`pos`、`color` 等）解析。

#### SB-15. `Color.ToUnityColor()` 未归一化 0–255

- **文件**：`Storyboard/StoryboardModel.cs:341-351`

```csharp
return new UnityEngine.Color(R, G, B, A);  // 默认 R/G/B = 255
```

- **修复**：`new UnityEngine.Color(R / 255f, G / 255f, B / 255f, A);`

---

### P2 — Medium（逻辑 / 数据 / 生命周期）

| ID | 文件 | 问题 | 修复要点 |
|---|---|---|---|
| SB-8 | `StoryboardRenderer.cs:305-334` | `RecalculateTime` 改写 `states[0].Time` 后不重排，`FindStates` 假设升序 | 结束后 `States.Sort` 或 `OrderBy(s => s.Time)` |
| SB-9 | `Storyboard.cs:527-565` | 实例字段 `replacements` 跨对象共享，`$note` 可串味 | 改为 `ParseTime` 局部变量或显式参数 |
| SB-10 | `SpriteEaser.cs:133` 等 | `From.Layer = Clamp(...)` 永久篡改 `States` 列表中的 state 对象 | 用局部变量，不写回 `From` |
| SB-11 | `StoryboardRendererEaser.cs:34,45` | 相邻 state 同 `time` 时 `(To.Time - From.Time)` 除零 → NaN | `denom == 0` 时直接返回 `j.Value` |
| SB-12 | `StoryboardRenderer.cs:294-303` | `DestroyObjectsById` 不递归 `Children` | 与 `OnGameUpdate` 一致，Flatten 子树后逐个 Dispose |
| SB-16 | `Storyboard.cs:413,502` | `Templates[templateId]` 无存在性检查 | `TryGetValue` |
| SB-17 | `Storyboard.cs:55-60` | 编译路径直接 `(JArray)RootObject["texts"]` 强转 | 检查 `token != null && token.Type == JTokenType.Array` |
| SB-18 | `Storyboard.cs:388` | `notes` 为 JSON null 时 `.Values<int>()` 抛异常 | 增加 null 类型判断 |
| SB-19 | `SpriteEaser.cs:86-110` 等 | `FillWidth` 设 `height=10000` 后无条件 Height 块覆盖 | Height 设置移入 `else` 分支（`TextEaser`/`VideoEaser` 同理） |
| SB-20 | `SpriteRenderer.cs:82-93` 等 | `target_id` 共享 `Image`/`Text` 时 `Dispose` 仍 `Destroy` 宿主 GO | 增加 `usesTarget` 标志，仅自建资源时销毁 |
| SB-21 | 各 renderer `Dispose` | 子类 `override void Dispose()` **均未** `base.Dispose()` | 末尾调用 `base.Dispose()` 释放 `equivalentTransform` |
| SB-22 | `StoryboardModel.cs:25-27` | `IsManuallySpawned` 空 `States` 越界 | `States.Count > 0 && States[0].Time == float.MaxValue` |
| SB-23 | `SpriteRenderer.cs:52-57` 等 | 空 `States` 访问 `States[0].Path` | `Count > 0` 守卫 |
| SB-24 | `GenericStateParser.cs` / `StoryboardRenderer.cs:59-63` | `UnitFloat.Convert`、canvas 换算除零 | 分母 `Mathf.Max(v, 1e-5f)` |
| SB-25 | `StoryboardComponentRenderer.cs:146-149` | `InitializeTransformPlaceholder` 可能重复创建 | `if (equivalentTransform != null) return;` |
| SB-26 | `VideoRenderer.cs` | `prepareCompleted` 未退订；`WaitUntil` 无 CancellationToken | 保存 handler，`Dispose` 移除 |
| SB-27 | `StoryboardEffectsHost.cs` / `UnitFloat.Storyboard` | 静态 `Current` / `Storyboard` 热驻留不清理 | session 结束置 `null`（兼 ML-18） |

#### SB-28. `NoteControllerStateParser` — `YOffset` 未实现

- **文件**：`Storyboard/Notes/NoteControllerStateParser.cs:27-29` — 读 `dx` 作 `YOffset`，注释 `// TODO: This is broken`

---

### 2026-07-01 补充评审 — Storyboard / c2v3 兼容问题

本节基于 `feature/cytoid-player` 当前实现，重点检查 c2v3 PageFunction 引入页面级 Y 缩放/偏移后，会放大哪些 Storyboard bug。结论：Storyboard 和 PageFunction 的主要交叉风险不在普通 sprite/text，而在 **note controller 副作用、scanner override、`ReferenceUnit.NoteY` 动态坐标转换**。

#### SB-29. `ScannerPositionEaser` 的 scanline override 缺少 owner cleanup

- **文件**：
  - `Storyboard/Controllers/ScannerPositionEaser.cs:11-16`
  - `StoryboardRenderer.cs:94-99` — seek reset 会清 `positionOverride`
  - `StoryboardRenderer.OnGameUpdate:284-320` — destroy renderer 时只 `Dispose()`，不会还原 scanner override
- **根因**：`override_scanline_pos=true` 会直接写 `Scanner.Instance.positionOverride`。如果 controller 被 destroy 或 storyboard renderer Dispose 前没有走 full seek reset，`ControllerRenderer.Dispose()` 不知道自己曾经拥有 scanner override，因此不会把 `positionOverride` 还原为 `float.MinValue`。
- **影响**：PageFunction 接入后，默认 scanner path 来自 page visual progress；残留 `positionOverride` 会完全绕过 PageFunction，表现为扫描线卡在旧 storyboard 位置。Lab seek 能缓解一部分，但正常播放/trigger destroy 仍有残留风险。
- **修复要点**：把 scanner override 也纳入 controller side-effect ownership；controller Dispose 或 destroy path 退出时，如果该 controller 最后写入过 override，则还原默认值。`Mod.HideScanline` 仍应高于 visibility，不能和 position ownership 混在一起。

#### SB-30. `UnitFloat.ConvertedValue` 永久缓存动态坐标

- **文件**：`Storyboard/GenericStateParser.cs:128-139,154-216`
- **根因**：`UnitFloat.ConvertedValue` 第一次访问后缓存 `Convert()` 结果，但转换依赖 `Storyboard.Game.camera.orthographicSize`、`UnityEngine.Screen.width/height`、`CytoidLabShell.GetCoordinateScreenHeightPx()`、`Provider.CanvasRect`、`Chart.ConvertChartYToScreenY()` 等运行时状态。
- **当前影响**：窗口尺寸、Lab HUD camera bands、canvas rect、camera 参数变化后，已解析 storyboard 的 unit value 不会刷新；camera/controller 相关 storyboard 可能在 resize 或 resync 后使用旧坐标。
- **c2v3 影响**：如果未来把 `ReferenceUnit.NoteY` 改成 PageFunction-aware，转换还会依赖 page/time/note context，单个 `convertedValue` 缓存会直接错误。
- **修复要点**：不要缓存动态 unit，或引入 conversion context/version（screen/canvas/chart geometry/page function version）。至少 `NoteX/NoteY/CameraX/CameraY/StageX/StageY` 在 Lab/c2v3 路径应可失效；`World` 才适合永久缓存。

#### SB-31. `ReferenceUnit.NoteY` 没有 page/note 上下文

- **文件**：
  - `Storyboard/GenericStateParser.cs:173-176`
  - `Storyboard/Controllers/ControllerStateParser.cs:37`
  - `Storyboard/Notes/NoteControllerStateParser.cs:21`
  - `Storyboard/Lines/LineStateParser.cs:26`
- **根因**：`ReferenceUnit.NoteY` 只保存一个数值，转换时调用 `Chart.ConvertChartYToScreenY(Value)`。c2v3 PageFunction 是 page-level 变换，同一个 `Value` 在不同 page 上可能映射到不同视觉 Y。
- **影响**：不能全局把 `ConvertChartYToScreenY(float y)` 改成 PageFunction-aware，否则普通 storyboard 对象、line、scanline_pos、note controller 都会在无上下文时套用错误 page。反过来，如果保持旧线性映射，note controller 的 PageFunction 叠加语义也必须单独定义。
- **修复要点**：保留无上下文 `ReferenceUnit.NoteY` 的 legacy/global 语义；为有上下文的调用点新增专用 API，例如 note controller 使用目标 note page，scanner position 使用 target time 所在 page，普通 sprite/text/line 默认不受 PageFunction 影响。

#### SB-32. `NoteControllerRenderer` 在 note 未 spawn 时把 placeholder 置零

- **文件**：`Storyboard/Notes/NoteControllerRenderer.cs:56-73`
- **根因**：placeholder 只有在 `Game.SpawnedNotes` 中存在目标 note 时才跟随 note GameObject；否则 `notePlaceholderTransform.position = Vector3.zero`。
- **影响**：任何 `target_id` / parent 绑定到 note controller 的 storyboard 对象，在 note intro 前、note 被 collect 后、Lab seek 到未 spawn 区间时都会跳到世界原点。PageFunction/下落式引入后，note spawn 生命周期和视觉位置更复杂，这个跳变会更明显。
- **修复要点**：placeholder 在未 spawn 时应 snap 到 `Note.Model.CalculatePosition(Game.Chart)` 的 base position，或显式隐藏依赖 placeholder 的对象；不要用 `(0,0,0)` 作为默认可见位置。

#### SB-33. `NoteControllerRenderer` 只绑定 `States[0].Note`

- **文件**：`Storyboard/Notes/NoteControllerRenderer.cs:30-34`
- **根因**：parser 允许每个 `NoteControllerState` 继承/设置 `note`，但 renderer 初始化时只读取 `Component.States[0].Note`，后续 state 的 `Note` 变化不会重绑定。
- **影响**：如果 storyboard 用同一 controller 在不同 state 控制不同 note，运行时会一直控制第一个 note；trigger clone/recalculate time 后也不会重新解释后续 note 切换。
- **修复要点**：要么在解析期禁止/警告 controller 中途切 note，要么 renderer update 时检测 `fromState.Note` 变化并重新绑定，同时清理旧 note override。

#### SB-34. c2v3 接入前必须先冻结 Storyboard 坐标语义

- **关联文档**：[c2v3 调研方案 §7.8](2026-07-01-c2v3-research-plan.md#78-pagefunction-与现有-storyboard-坐标系统兼容性)
- **结论**：PageFunction 不应通过修改旧 `ConvertChartYToScreenY(float y)` 来“顺手兼容” Storyboard。必须先定义：
  - base note/scanner/boundary/holdlength 使用 PageFunction-aware API；
  - note controller 的 `Y/YMultiplier/YOffset` 作用在 raw progress 还是 visual progress；
  - absolute `Override.Y` 是否允许绕过 PageFunction；
  - storyboard scanner override 与 PageFunction scanner path 的优先级。
- **建议**：把 SB-7、SB-29、SB-30、SB-31 作为 c2v3 PageFunction 前置修复/设计项，否则 c2v3 验收时会混入 Storyboard 残留状态和坐标缓存噪声。

---

## 二、内存泄漏

### P0 — Critical

#### ML-1. `Screen.OnLanguageChanged` lambda 永不移除

- **文件**：`Screen/Screen.cs:100`（订阅）、`:105-108`（`OnDestroy` 仅 `RemoveHandler`）
- **机制**：`Context.OnLanguageChanged` 为 DDOL 静态 `UnityEvent`；lambda 捕获 `this`，Screen 销毁后 UI 子树仍被静态事件引用。
- **修复**：

```csharp
private UnityAction _onLanguageChanged;
// Awake: _onLanguageChanged = () => rebuiltLayoutGroups = false;
// OnDestroy: Context.OnLanguageChanged.RemoveListener(_onLanguageChanged);
```

#### ML-2. `CytoidPlayerHudController` 事件监听不对称

- **文件**：`Game/CytoidPlayerHudController.cs:55-76`
- **机制**：注册 7 个 `Game` 事件，仅 `onGameAborted`/`onGameDisposed` 触发 `Destroy`；其余 5 个无 `OnDestroy` 移除。`Game.Dispose`（`Game.cs:908-909`）只清 `onGameUpdate`/`onGameLateUpdate`。
- **附加**：`Start()` 为 `async void` + `WaitUntil(FindObjectOfType<Game>)`，HUD 先销毁则续体悬空。
- **修复**：`OnDestroy` 移除全部监听；`Start` 用可取消 CTS。

#### ML-3. `ObjectPool.Dispose()` 未释放特效池

- **文件**：`Game/ObjectPool.cs:109-114`

```csharp
public void Dispose()
{
    SpawnedNotes.Values.ForEach(it => it.Dispose());
    notePoolItems.Values.ForEach(it => it.Dispose());
    dragLinePoolItem.Dispose();
    // 缺少：effectPoolItems、SpawnedDragLines
}
```

- **机制**：每局 `Game.Initialize` 新建 `ObjectPool`（`Game.cs:147`），`effectPoolItems` 中数百 `ParticleSystem` 实例永不销毁；借出中特效亦无追踪。
- **修复**：`effectPoolItems.Values.ForEach(it => it.Dispose());`；考虑销毁 `EffectParentTransform` 下残留子对象；`SpawnedDragLines` 逐项 `Collect`/`Dispose`。

#### ML-4. `GameCover` 载入 `AssetMemory` 从不释放

- **文件**：`Game/Notes/GameRenderer.cs:68` — `LoadAsset<Sprite>(..., AssetTag.GameCover)`
- **机制**：全仓库无 `DisposeTaggedCacheAssets(AssetTag.GameCover)` 调用；`StoryboardRenderer.Dispose` 已对 `AssetTag.Storyboard` 正确处理（`:90`），GameCover 应对齐。
- **修复**：`OnGameBeforeExit` 或 `Game.Dispose` 调用 `Context.AssetMemory.DisposeTaggedCacheAssets(AssetTag.GameCover)`。

---

### P1 — High

| ID | 文件 | 问题 | 修复要点 |
|---|---|---|---|
| ML-5 | `GamePlayEvent.cs` + `Game.cs:363` + `GameBridge.cs:133,158` | `Begin` 每局调用，`End` 仅 bridge 路径 | `Game.Dispose` 无条件 `GamePlayEventRecorder.End()` |
| ML-6 | 各 `*Renderer.cs` `Dispose` | 不调 `base.Dispose()` → `TransformEquivalent_*` GO 泄漏 | 子类末尾 `base.Dispose()` |
| ML-7 | `AudioManager.cs:26` | `Context.AudioManager = this`，`OnDestroy` 不清静态字段 | `if (ReferenceEquals(Context.AudioManager, this)) Context.AudioManager = null` |
| ML-8 | `CleanTitleTransitionElement.cs` | DOTween `Sequence` 无 `OnDestroy` kill | `currentTween?.Kill()` |
| ML-9 | `AwaitableAnimatedElement.cs` | 每次 `Animate` 新建 CTS 不 Dispose 旧实例 | `OnDestroy` cancel+dispose；重赋前 dispose |
| ML-10 | `BeatPulseVisualizer.cs:44` | 每帧 `DOFade`，无 kill-on-destroy | `OnDisable` → `image.DOKill()`（兼 PF-6） |
| ML-11 | `NLayerLoader.cs:33-38` | seek 回调每次 `new MpegFile` 加入 `createdFiles` | 复用单个实例或 seek 前释放旧实例 |
| ML-12 | `Game.cs:114-142` catch | 加载失败导航回 Navigation 但不 `Dispose` | catch/finally 中 `Dispose()` 或 `ObjectPool?.Dispose()` |

#### ML-13. Storyboard / Game 层事件监听（汇总）

| 文件 | 订阅 | 缺失移除 |
|---|---|---|
| `Storyboard/Storyboard.cs:145-147` | `onNoteClear`, `onGameDisposed` | `Dispose` 未移除（兼 SB-4） |
| `Storyboard/StoryboardRenderer.cs:121-126` | `onGameDisposed/Paused/Unpaused` | `Dispose` 未移除 |
| `Game/Notes/GameRenderer.cs:25-28,48-49` | `onGameLoaded/Completed/BeforeExit` 等 | 类无 `Dispose` |
| `Game/InputController.cs:16-19` | `onGameUpdate`, `onGamePaused` | 无 `OnDestroy` |
| `Game/EffectController.cs:24-27` | `onGameLoaded` | 无 `OnDestroy` |
| `Game/Elements/Scanner.cs:26-39` | 多个 Game 事件 | 无 `OnDestroy` |
| `Game/GameConfig.cs:24-26` | `onGameLoaded` | 无清理 |

#### ML-18. 静态 Storyboard 引用

- **文件**：`Storyboard/Storyboard.cs:42` — `UnitFloat.Storyboard = this`，`Dispose` 不清理。
- **修复**：`if (UnitFloat.Storyboard == this) UnitFloat.Storyboard = null;`

---

### P2 — Medium

| ID | 文件 | 问题 | 修复要点 |
|---|---|---|---|
| ML-14 | `LevelManager.cs:445,659` | `Application.lowMemory` 订阅无 try/finally | `finally` 中 `-=` |
| ML-15 | `VideoRenderer.cs:105-110` | RT 只 `Destroy` 未 `Release`；引用未先清空 | `Release()` + 清 `targetTexture`/`texture` |
| ML-16 | `StartupLogger.cs:26` | `logMessageReceived` 订阅，`Dispose()` 无调用方 | `OnDestroy`/`OnApplicationQuit` 调 `Dispose` |
| ML-17 | `Game.cs:654-688` | `WillUnpause` 循环新建 CTS 不 Dispose 旧实例 | 创建前 `unpauseToken?.Dispose()` |
| ML-18 | `ScreenManager.cs:21,383-387` | `History` 栈无限增长 | session/切场景时 `Clear()` |
| ML-19 | `ScreenManager.cs:191,207,279` | 多个 CTS 只 Cancel 不 Dispose | 复用或及时 Dispose |
| ML-20 | `Game.cs:72-73` | `BeforeStartTasks`/`BeforeExitTasks` 不清理 | `Dispose` 中 `Clear()` |
| ML-21 | `LevelManager.cs:22-23` | `LoadedLocalLevels` 只增不减 | 低内存边界 `UnloadAllLevels` |
| ML-22 | `Host/GameBridge.cs:142-160` | `OnGameResultJson` 为 `async void` | `UniTask` + 生命周期 token |
| ML-23 | `Utils/SingletonMonoBehavior.cs:14-23` | 重复实例 `WaitUntil` 可能永久等待 | 直接 `Destroy` 或超时 |
| ML-24 | `Utils/UnityMainThreadDispatcher.cs` | 静态队列在 dispatcher 重建后残留 | `OnDestroy` 清空队列 |
| ML-25 | `StoryboardRendererProvider.cs` | 静态 `NullChannels` 跨 session 被修改 | 每 renderer 独立实例或只读 null backend |
| ML-26 | `StoryboardFallbackPostProcess.cs` | 资源仅在 `OnDisable` 释放 | 补 `OnDestroy`；RT `DestroyImmediate` |

---

## 三、性能优化

> 节奏游戏帧率敏感：`Update`/`LateUpdate`/`onGameLateUpdate` 路径上的分配与 LINQ 均为热点。

### P0 — Critical（每帧分配 / 高开销）

#### PF-1. `StoryboardRenderer.OnGameUpdate` 每帧分配

- **文件**：`Storyboard/StoryboardRenderer.cs:203-206,224,232`
- **问题**：每帧 `new Type[]{...}`、`new Dictionary<string,Type>()`；`Flatten`/`ForEach` LINQ 链额外分配。
- **修复**：

```csharp
private static readonly Type[] UpdateOrder = { typeof(NoteController), typeof(Text), ... };
private readonly Dictionary<string, Type> _renderersToDestroy = new();
// 每帧开头 _renderersToDestroy.Clear()
```

#### PF-2. `Chart.GetScannerPositionY` 重置 `CurrentPageId`

- **文件**：`Game/Chart/Chart.cs:372`（`Scanner.cs:204` 每帧调用）

```csharp
CurrentPageId = 0;  // 丢弃 Game.Update 已推进的页索引
while (CurrentPageId < Model.page_list.Count && time > ...)
```

- **影响**：多页谱每帧 O(pages)；兼破坏 `Chart.CurrentPageId` 共享状态（逻辑 bug）。
- **修复**：用局部变量从已知页续扫，或传入当前页，勿写回 `CurrentPageId`。

#### PF-3. `AccuracyText.LateUpdate` 每帧重建字符串

- **文件**：`Game/Elements/AccuracyText.cs:33` — 值未变也 `ToString("0.00") + "%"`。
- **修复**：缓存 `lastAccuracy`，仅变化时更新（参考 `ScoreText` 范式）。

#### PF-4. `ComboText` combo 未变也赋值 `text.text`

- **文件**：`Game/Elements/ComboText.cs:57-58` — tween 有 `combo != lastCombo` 守卫，文本赋值无。
- **修复**：`text.text = ...` 移入 `if (combo != lastCombo)` 块。

#### PF-5. `GameTimeText.Update` 每帧 `$"Time: {game.Time:F3}"`

- **文件**：`Game/Elements/GameTimeText.cs:21`
- **修复**：限速（如 10 Hz）或舍入值变化时更新。

#### PF-6. `BeatPulseVisualizer.Update` 每帧 `DOFade`

- **文件**：`Game/Elements/BeatPulseVisualizer.cs:44`（兼 ML-10）
- **修复**：直接设 `image.color`  alpha，或单 tween + `DOKill` 守卫。

#### PF-7. `GameTooltipText.Update` 每帧调 `TextFunction()`

- **文件**：`Game/Elements/GameTooltipText.cs:100`
- **修复**：消息/倒计时数值变化时缓存字符串。

---

### P1 — High

| ID | 文件 | 问题 | 修复要点 |
|---|---|---|---|
| PF-8 | `Scanner.cs:183-195` | 每帧 3 次 `GetComponent<LineRenderer>()` | `Awake` 缓存 `lineRenderer` |
| PF-9 | `Note.cs:65-66` + `Game.cs:500-501` | 每 note 两个 `UnityEvent` 监听，每帧 Invoke N 次 | 用 `SpawnedNotes` 直接遍历替代 per-note 事件 |
| PF-10 | `ClassicNoteRenderer.cs:130-135` | 每帧双调 `GetRingColorOverride`/`GetFillColorOverride` | 各调一次存局部 |
| PF-11 | `StoryboardRendererEaser.cs:48-53` | `EaseColor` 每帧 `ToUnityColor`+`Lerp` | 颜色未变时跳过；考虑 `Color` 改 struct |
| PF-12 | `StoryboardModel.cs:30-51` | `FindStates` 每渲染器每帧 O(states) | 二分查找或单调 time 缓存 index |
| PF-13 | `GlobalNoteFillColorEaser.cs:18-25` | 每帧 `Select` + `new Color[]` | 预计算 mapping，写缓存数组 |
| PF-14 | `MeshTriangle.cs:53-77` | 每帧 `new Vector3[]/Vector2[]/int[]` | `Awake` 分配，每帧改值 |
| PF-15 | `InputController.cs:102-113` | LINQ `.Where` 链 + `ContainsValue` O(n) | `for` 循环 + `HashSet` |

---

### P2 — Medium / 低影响

| ID | 文件 | 问题 | 修复要点 |
|---|---|---|---|
| PF-16 | `ClassicHoldNoteRenderer.cs:121-127` | 按住时每帧 `DOScale` | 仅 hold 开始/结束创建 tween |
| PF-17 | `GameProgressIndicator.cs:27-37` | 每帧 `DOWidth` | 值变化时才 tween |
| PF-18 | `Game.cs:601-612` | `Seek` 时 `new List<Note/DragLine>` | 直接遍历字典或可复用 List |
| PF-19 | `ObjectPool.cs:148-158` | `SpawnEffect` 每次 new provider | 结构体或对象池 |
| PF-20 | `StoryboardComponentRenderer.cs:169-200` | 每帧 canvas↔world 投影 | transform 变化时才更新 |
| PF-21 | `StoryboardRendererEaser.cs:32-34` | 每帧 `GetEasingFunction` | 缓存 easing delegate |
| PF-22 | `CytoidPlayerHudController.cs:103,136` | 每帧格式化时间 + `GetComponent<RectTransform>` | 缓存 RectTransform；秒变化时更新 |
| PF-23 | `Storyboard/Storyboard.cs:150-172` | 每 note clear O(triggers) + SB-2 | 按 note id 索引 trigger |
| PF-24 | `Host/GameBridgeRouter.cs:214` | cancel 时 `FindObjectOfType<Game>()` | 缓存 Game 引用 |
| PF-25 | `AudioManager.cs:288-398` | `Exceed7Controller.IsFinished` 每次 `print` | 移除 debug print；缓存状态 |
| PF-26 | `ProgressRing.cs:25-27` | 写 `sharedMaterial` | `MaterialPropertyBlock` |

**加载期（可接受，低优先）**：`Storyboard.Parse()` LINQ + `OrderBy`；`OverlayScreen.RefreshTransitionDefaults` 的 `GetComponentsInChildren`（有 safe-area 变化守卫）。

---

## 四、已验证为干净的项

以下经读码确认**当前无问题**，避免重复审计：

| 区域 | 结论 |
|---|---|
| `Host/GameBridge.cs` | 实例订阅 Awake/OnDestroy 对称 |
| `Host/GameLogBridge` | `logMessageReceived` OnEnable/OnDisable 对称 |
| `Utils/UnityMainThreadDispatcher` | `OnDestroy` 清 `_instance` |
| `Utils/SingletonMonoBehavior` | `OnDestroy` 清 `instance` |
| `Game/Notes/Note`、`DragLineElement` | 池化，`Collect` 移除监听器 |
| `Utils/GlobalCalibrator` | 退出路径 + Dispose 退订 |
| `Storyboard/Sprites/SpriteRenderer` | `Image.sprite` + `SpritePathRefCount`，无 `.material` |
| `Storyboard/Texts/TextRenderer` | `UnityEngine.UI.Text`，非 TMP |
| `Storyboard/Notes/NoteControllerRenderer` | 借 `noteGameObject`，不拥有 note GO |
| `Storyboard/*Easer.cs` | 手动 easing，无 DOTween |
| `AudioManager.Unload("Level")` | 四条退出路径均调用 |
| `FileGameContentProvider` / `ExternalGameContentProvider` | `Dispose` 调 `DisposeDecoder` |

---

## 五、建议修复批次与代码片段

### 批次 A — Storyboard 销毁路径（1 PR）

修复 SB-1、SB-2、SB-3、SB-12；附带 SB-4 事件移除。

```csharp
// DestroyObjectsById — 正确 type key + 递归子对象
void DestroyObjectsById(string id)
{
    if (!ComponentRenderers.TryGetValue(id, out var root)) return;
    foreach (var it in ListOf(root).Flatten(r => r.Children))
    {
        var t = it.Component.GetType();
        it.Dispose();
        ComponentRenderers.Remove(it.Component.Id);
        TypedComponentRenderers[t].Remove(it);
    }
}

// OnNoteClear — 安全移除
for (int i = Triggers.Count - 1; i >= 0; i--)
{
    var trigger = Triggers[i];
    // ... 匹配后 Triggers.RemoveAt(i) 或收集后统一删
}
```

### 批次 B — 会话泄漏（1 PR）

ML-1、ML-3、ML-4、ML-5、ML-6、ML-18；`Game.Dispose` 补 `GamePlayEventRecorder.End()`。

### 批次 C — 帧率热点（1 PR）

PF-1、PF-2、PF-3–7；PC player 加 ML-2、PF-22。

### 批次 D — 解析与状态正确性（1 PR）

SB-6、SB-7、SB-8、SB-9、SB-13、SB-14、SB-15、SB-19、SB-28、SB-32、SB-33。

### 批次 E — c2v3 / Storyboard 坐标兼容（PageFunction 前置）

SB-29、SB-30、SB-31、SB-34。该批次应与 c2v3 PageFunction 设计一起评审，不建议作为纯 Storyboard bugfix 静默合入。

---

## 六、测试建议

修复后建议在以下场景复测：

| 场景 | 验证点 |
|---|---|
| 带 `destroy`/`spawn` trigger 的谱面 | SB-1/2/3 不抛异常，对象正确销毁 |
| Note controller override 窗口结束后 | SB-7 note 位置/颜色恢复 |
| Destroy note/scanner controller 后继续播放 | SB-7/SB-29 副作用恢复，scanner 不残留 position override |
| Resize / Lab HUD bands / timeline seek 后 storyboard 坐标 | SB-30 动态 unit 不使用旧缓存 |
| c2v3 PageFunction + storyboard note controller | SB-31/SB-34 坐标语义明确，无二次缩放或误套 page |
| Note 未 spawn/已 collect 时的 note-bound storyboard | SB-32 不跳到世界原点 |
| 连续 10 局 retry（Android warm-resident + PC player） | `unityActivityInstanceCount` ≤ 1；内存不单调上升 |
| 多页谱面游玩 | PF-2 scanner 位置正确，`CurrentPageId` 不被破坏 |
| 加载失败谱面 | ML-12 无 note/特效 GO 残留 |
| Editor Profiler / Memory Profiler | PF-1/3–7 GC Alloc 下降 |

---

## 附录：关键代码锚点（审计时行号）

| 锚点 | 文件:行 |
|---|---|
| `OnNoteClear` foreach Remove | `Storyboard.cs:150-184` |
| `DestroyObjectsById` 错误 key | `StoryboardRenderer.cs:300` |
| `renderersToDestroy` 错误 type | `StoryboardRenderer.cs:223-252` |
| `ObjectPool.Dispose` 缺特效池 | `ObjectPool.cs:109-114` |
| `Screen` 语言 lambda | `Screen.cs:100,105-108` |
| `GetScannerPositionY` 重置页 | `Chart.cs:372` |
| `ParseTime` 无 TryGetValue | `Storyboard.cs:569` |
| `StageObject` 遮蔽 TargetId | `StoryboardModel.cs:55-57` |
| `LineStateParser` 缺 stage 解析 | `LineStateParser.cs:14-16` |
| `Color.ToUnityColor` | `StoryboardModel.cs:348-351` |
| `ScannerPositionEaser.positionOverride` | `ScannerPositionEaser.cs:11-16` |
| `UnitFloat.ConvertedValue` 缓存 | `GenericStateParser.cs:128-139` |
| `ReferenceUnit.NoteY` 无 page context | `GenericStateParser.cs:173-176` |
| `NoteControllerRenderer` placeholder 置零 | `NoteControllerRenderer.cs:56-73` |
| HUD 事件订阅 | `CytoidPlayerHudController.cs:69-76` |
| `GamePlayEventRecorder.End` 仅 bridge | `GameBridge.cs:133,158` vs `Game.cs:363` |

---

*最终版生成于 2026-06-30。2026-07-01 追加 Storyboard/c2v3 PageFunction 兼容补充评审。合并自 `audit-storyboard-memory-performance.md` 与 `2026-06-30-storyboard-leak-perf-audit.md`，并经 `feature/cytoid-player` 代码走查核实。*
