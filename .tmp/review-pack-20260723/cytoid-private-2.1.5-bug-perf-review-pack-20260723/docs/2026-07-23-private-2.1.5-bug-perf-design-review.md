# Cytoid-private 2.1.5 Bug & 性能补丁 — 方案评审候选定稿

> **审计链：** [第三方审核索引与证据导航](./2026-07-23-private-2.1.5-THIRD-PARTY-REVIEW-INDEX.md) → [独立审核回复](./2026-07-23-private-2.1.5-third-party-review-response.md)（Verdict: **Agree-with-changes**）。本稿已吸收全部事实纠错、范围补强与门禁建议；审核回复保留原文，不作为另一份实施规范。

| 字段 | 内容 |
|------|------|
| 状态 | **候选定稿 · 已吸收第三方意见 · 待 owner 批准与排期** |
| 日期 | 2026-07-23 |
| 基线 | `private/main` @ `0e11d2c3f12ed61968aac3058272748045b27f3d`（短 `0e11d2c3`；2.1.4 BUILD.1 / VersionCode 123） |
| Unity | 6000.0.58f2 |
| 远程 | `private` → `git@github.com:Cytoid/Cytoid-private.git` |
| 对照 | `upstream` → `git@github.com:Cytoid/Cytoid.git`；本地分支 `fix/correctness-hardening-unverified` |
| 目标版本 | 商店小版本 **2.1.5**（建议 VersionCode 124） |
| 方法 | 静态源码审计 + 调用链追踪 + 与 core 差异对照；二次复核 `private/main@0e11d2c3` 与补丁提交；**尚未** Profiler / 真机量化 |
| 评审目标 | 拍板：Must/Should/Defer 范围、方案取舍、验收门禁、风险与回滚 |

---

## 0. 评审结论（建议先读）

### 0.1 一句话

2.1.5 应做一次 **「崩溃防护 + 会话泄漏 + AssetMemory 卡死修复」** 补丁；定点 GC 作为低风险 Should；视频加固单列里程碑；**禁止** 把 AudioServer / Lab 时间轴 / Flutter Bridge 塞进本版本。

### 0.2 建议拍板范围

| 档 | 条目 | 人日（估） | 说明 |
|----|------|-----------|------|
| **Must** | B01 B02 B03 B05 B07 B08 B11 **B12 B16** | 2–3 | 崩溃 / 泄漏 / 卡死；合入门槛 |
| **Should** | B04（删除全量 flush） B09（完整同步版） P01–P04 | 0.5–1 | B09 不可原样移植 `900af401` |
| **Defer（默认）** | B06 B10 B13 B14 P08 Note 热路径 | — | B06/B10 仅可按 §5 的完整方案和独立门禁重新打开；不属于 2.1.5 基线 |
| **Out** | AudioServer、VFS、Lab seek、Bridge v2 | — | 架构级 |

### 0.3 建议决议基线（待 owner 一次性确认）

第三方投票与本文建议已一致。除非 owner 明确覆盖，实施与验收均按下表执行：

| # | 议题 | 建议决议 | 不变量 / 重开条件 |
|---|------|----------|-------------------|
| 1 | B03 Score 语义 | **保留现状** | Score one-shot；Uses 只管 NoteClear/Combo |
| 2 | B10 | **Defer 整项** | 禁止只合纯函数化 A；重开须同时设计显式 page sync |
| 3 | B06 | **整项进 2.1.6** | 若强制纳入，须独立 PR 并通过 Android 真机视频门禁 |
| 4 | B04 | **纳入 Should** | 删除全量 flush；scheme 只解析回调 `url` |
| 5 | B09 | **完整同步版才可纳入 Should** | 不原样移植 `900af401`；不阻塞 Must 发版 |

### 0.4 二次代码复核后的范围变化

| 发现 | 结论变化 |
|------|----------|
| `DestroyObjectsById` 用 renderer CLR 类型索引以 model 类型为 key 的字典 | B12 从 Should 升为 **Must / P0** |
| `GameRenderer.OnGameBeforeExit` 无条件 `cover.DOFade`，但无背景/全局校准不会赋值 `cover` | B11 从纯泄漏升级为 **Crash + Leak** |
| `LineStateParser` 虽可解析舞台字段，但 `LineEaser` 不读取 X/Y/旋转/缩放 | B13 单行补丁不产生宣称的视觉修复，改为 **Defer** |
| AssetMemory 的 `async void Task` 文件写入失败时永远不置 `completed` | B16 不能只加 `try/finally`，还须消除不可等待的后台任务 |
| NLayer 的 `MpegFile` 内部已同步 read/seek，但 `Dispose()` 不持内部 seek lock | B09 复用单实例可行，但外层锁必须覆盖 PCM read 与 Dispose |

### 0.5 第三方审核吸收（2026-07-23）

| 类型 | 处理 |
|------|------|
| 事实纠错 ×4 | B12 次要崩溃路径表述、B04 `PlayerAvatar` 用词、B08 池规模、P03/P04 对 `cb740083` 覆盖面——已改正文 |
| 范围补强 ×2 | B12 shared **Line** target 双毁纳入 owner 语义；B07 A′ 含 FileSystemWatcher 停用/Dispose |
| 门禁缺口 ×4 | §8 增补 #0 真机 smoke、#8 生产交错、#15 参考谱回归、#4 Line 共享用例；附录 A 澄清 Crash 用语 |
| Must 列表 | **维持 9 项不变**（第三方确认全部 code-backed） |

### 0.6 模块级审计增量（已入评审包 `04`）

全量模块审计见 [2026-07-23-private-module-code-audit.md](./2026-07-23-private-module-code-audit.md)（包内 `docs/04-module-code-audit.md`）。**不改变** §0.2 的 9 项 Must；下列为相对方案稿的增量发现，建议 owner 在 §12 勾选是否并入 2.1.5 Should / 后续：

| ID | 建议档 | 摘要 |
|----|--------|------|
| **G-SPAWN** | Should（评估）或 Defer | `note_map[CurrentNoteId]` 假定 id 连续；缺口谱可崩 |
| **B-JWT-LOG** | Should（低成本） | `Debug.Log` 打印完整 JWT |
| **B-SET-JSON** | Should（低成本） | `cdn_region` JsonProperty 双绑定 |
| **B-FONT** | Should（低成本） | FontManager `WaitUntil(() => Loaded = true)` 赋值 bug |
| **B-CTX-LM** | Defer / 策略评审 | `OnLowMemory` 有效清理前 `return` |
| **B-SEC** | Out（不挡 2.1.5） | Secure 剧场 + ClientSecret 硬编码；随壳迁移 |

---

## 1. 背景与约束

### 1.1 产品定位

`Cytoid-private` 是 **App Store / Google Play 完整客户端**（Navigation / Online / Character / AssetBundles），不是 Flutter 嵌入 core。发版依赖维护者本机手工流程（无 CI）。详见既有调研画布。

### 1.2 与 core 的关系

| | private | core (`upstream`) |
|--|---------|-------------------|
| 布局 | `Assets/Scripts/` | `engines/unity/Assets/Scripts/` |
| 音频 | `AudioManager` + Controller | `IAudioServer` |
| 路径 | `Level.Path + rel` | `GameLaunchVfs` |
| 作者工具 | `PlayerGame`（热重载 SB） | Cytoid Lab |
| Storyboard 正确性 | **落后** | `297f7536` 等已加固 |

共同祖先后分叉；**不可整提交 cherry-pick**，须按文件改编。

### 1.3 非目标（本方案明确不做）

- AudioServer / `PlayFrom` 重构  
- `GameLaunchVfs` / 外部内容协议  
- Lab 时间轴 seek / `ResetRuntimeStateForSeek`  
- Flutter host protocol  
- AssetMemory 架构重写（仅修 `isLoading` 死锁）  
- Note 全热路径重构（无 Profiler 数据）

---

## 2. 架构快照（补丁影响面）

### 2.1 Storyboard 生命周期

```
Game.Initialize()
  └─ File.Exists(StoryboardPath)?
       new Storyboard → Parse() → await Initialize()
            ├─ Renderer.Initialize() → SpawnObjects
            ├─ onNoteClear += OnNoteClear
            ├─ onGameDisposed += λ Dispose     ← B07：lambda 无法 Remove
            └─ onGameLateUpdate += Renderer.OnGameUpdate

Game.Update (IsLoaded && IsPlaying)
  ├─ CurrentPageId++（单调）                 ← 与 B10 交互
  ├─ onGameUpdate → Scanner / Notes / Input
  └─ onGameLateUpdate → StoryboardRenderer.OnGameUpdate

Note.Clear → onNoteClear → Storyboard.OnNoteClear → OnTrigger  ← B03

Game.Dispose (生产路径)
  ├─ onGameLateUpdate.RemoveAllListeners()
  ├─ ObjectPool.Dispose()                    ← B08 不完整
  └─ onGameDisposed.Invoke → SB Dispose      ← B07 不完整
```

**PlayerGame（作者工具）差异（高置信）：**

| 点 | 行为 |
|----|------|
| `Dispose()` | **空实现**（场景不卸 Game） |
| SB 热重载 | `ReloadStoryboard`：手动 `Storyboard.Dispose` + `Renderer.Dispose`，随后 fire-and-forget `Initialize()` |
| 销毁路径 | `Game is PlayerGame` 时走 `Clear()` 后仍从索引删除，形成不可再次显示、也无法在最终 Dispose 找回的 renderer |
| Seek | `Renderer.Clear()` 隐藏对象，不 Destroy |

→ 必须区分两种 Clear：

- **保留** PlayerGame seek / 循环播放时对仍受 Renderer 跟踪对象的 `Renderer.Clear()`。
- 对 state/trigger 的永久 destroy，既然对象随后会从索引删除，就应 `Dispose()`；继续 `Clear()` 会留下不可追踪 GameObject。
- `ReloadStoryboard` 只调用一次 `Storyboard.Dispose()`，并把 `Initialize()` 变成可等待、可合并的重载流程，禁止多次文件事件并发初始化。

### 2.2 帧循环与性能热点

| 通道 | 条件 | 相关项 |
|------|------|--------|
| `onGameUpdate` | `IsLoaded && IsPlaying` | Scanner、DragLine、Note |
| `LateUpdate`（HUD） | `IsLoaded`（含暂停） | Accuracy/Score/Combo |
| `onGameLateUpdate` | 同上 playing | StoryboardRenderer |

---

## 3. Must 项详细设计

每项按需要给出：问题、调用链、失败条件、方案取舍、推荐、影响面、验收与回滚。

---

### B01 · Template 裸索引崩溃

| | |
|--|--|
| **严重度** | P0 Crash |
| **用户影响** | 坏/未完成 SB 进关失败，「无法加载 storyboard」 |
| **置信度** | 机制 **High**；线上频率 **Med**（作者错误为主） |

**调用链：**  
`Game.Initialize` → `Storyboard.Parse` → `LoadObject` / `CreateState` → `Templates[templateId]`（无防护）

**失败条件：** JSON `template` 指向不存在的 id；templates 段解析后字典无该键。

**方案：**

| 方案 | 描述 | 利 | 弊 |
|------|------|----|----|
| **A** | `TryGetValue` + `LogWarning`，跳过 template 展开（core） | 与 core 一致；合法谱不变 | 非法引用「静默降级」 |
| B | 单对象 try/catch 跳过整个对象 | 更宽容 | 掩盖其它解析错误 |
| C | 生产 warn、PlayerGame 抛 | 作者友好 | 双行为维护成本 |

**推荐：A**

**爆破半径：** `Storyboard.cs` 两处；无序列化变更。

**验收：** 缺失 template 的谱能进关；Log 含名字；合法 template 谱视觉与 2.1.4 一致。

**回滚：** ≥2 首已知 template 重谱出现状态丢失。

---

### B02 · `note_map[id]` 裸索引崩溃

| | |
|--|--|
| **严重度** | P0 Crash |
| **用户影响** | 时间表达式引用已删 note → 进关失败 |
| **置信度** | 机制 High；频率 Med（改谱残留） |

**调用链：**  
`CreateState` → `*StateParser` → `ParseTime` → `note_map[id]`

**方案：**

| 方案 | 描述 | 利 | 弊 |
|------|------|----|----|
| **A** | `TryGetValue` → null（对象保持 `time=MaxValue` 隐藏） | 符合 SB「无时间则不显示」惯例 | 作者可能不察觉 |
| B | 回退 time=0 | 可见失败 | 语义变化大 |
| C | 仅 PlayerGame UI 提示 | 不修生产崩溃 | 不满足商店 |

**推荐：A**

**验收：** `"time":"start:99999"` 进关不崩；合法 `start:N` 时机不变。

---

### B03 · Triggers 遍历中 Remove

| | |
|--|--|
| **严重度** | P0 Crash |
| **用户影响** | 含 Score 触发器的 SB，达成分数时中盘崩 |
| **置信度** | 崩溃机制 **High**；谱面暴露率 **Med** |

**调用链：**  
`Note.Clear` → `onNoteClear` → `OnNoteClear` `foreach` → Score 分支 `Triggers.Remove`；且 `OnTrigger` 在 Uses 耗尽时再次 `Remove`。

**根因：** C# `List` 禁止 foreach 中修改集合。Score 路径几乎总会 Remove。

**方案：**

| 方案 | 描述 | 利 | 弊 |
|------|------|----|----|
| **A** | 倒序 `for`（core） | 最小改动止崩 | Score/Uses 双 Remove 仍丑 |
| **B** | 延后删除列表 + 统一生命周期 | 最干净 | 需定义 Score vs Uses |
| C | 删除 Score 分支的 Remove，只留 OnTrigger | 语义统一 | **可能改变「过线必删」行为** |

**推荐：2.1.5 采用 A，明确保留旧语义：**

- NoteClear / Combo 继续由 `Uses` 控制；`Uses == null` 时可重复。
- Score 达阈值后只触发一次并无条件移除；即使 JSON 写了 `uses: 2` 也保持一次。
- 同时把 `notes` / `spawn` / `destroy` 解析收窄为仅接受 `JArray`；非法形状 Warning 后用空列表，避免在载入阶段抛出。

**验收：** Auto 模打到 Score 阈值不崩；多 Score 触发器同帧可触发；Score + `uses:2` 仍仅一次；NoteClear/Combo 的 Uses 回归；非法 trigger 数组不崩且有 Warning。

**回滚：** Score 触发次数异常（多触发/不触发）。

---

### B05 · DragLine 时间分母 ≤0

| | |
|--|--|
| **严重度** | P1 Correctness |
| **用户影响** | intro 为零/负时材质 NaN 或反向；相连 note 同时刻时 outro NaN，拖线可能永不回收 |
| **置信度** | High |

**代码复核：**

- upstream `a6223bd7` 只修 `introDuration <= 0`：开始前 `introRatio = 1`，开始时及以后为 `0`。
- private 的 `outroRatio = (time - from.start_time) / (to.start_time - from.start_time)` 也无保护。若相连 note 同时刻，结果为 NaN；顶部的 `outroRatio >= 1` 永远不成立，dragline 不会 Collect。

**方案：** 精确移植 intro fallback，并对 `outroDuration <= 0` 定义为到达 from start 后立即完成；不要用任意 epsilon 掩盖坏链。与 B08 一并保证会话结束时仍能销毁活动 dragline。

**验收：** intro 为零/负时无 NaN；相连 note 同时刻/逆序的坏链不会永久驻留；正常 drag 谱视觉不变；中断退出后无活动监听残留。

---

### B07 · Storyboard Dispose / 监听不对称

| | |
|--|--|
| **严重度** | P1 Leak / Correctness |
| **用户影响** | PlayerGame 热重载后触发器重复；生产重试场景依赖新 Game，风险较低但仍不完整 |
| **置信度** | PlayerGame 路径 **High**；生产场景重载路径频率 Low |

**现状缺陷：**

1. `Initialize` 使用 `AddListener(_ => Dispose())` lambda → 无法 `RemoveListener`  
2. `Dispose` 只卸 `onGameLateUpdate`，不卸 `onNoteClear` / `onGameDisposed`  
3. `StoryboardRenderer.Initialize` 自身也注册三个 lambda，`Renderer.Dispose` 无法逐一卸载  
4. 不调用 `Renderer.Dispose()`；`UnitFloat.Storyboard` 静态未清  
5. `Lines` / `Videos` 未 Clear；无幂等 flag  
6. PlayerGame `ReloadStoryboard()` 不 await `Initialize()`；FileSystemWatcher 连续事件可让已 Dispose 的旧实例在 await 返回后重新挂监听

**方案：**

| 方案 | 描述 | 利 | 弊 |
|------|------|----|----|
| A | 命名方法 + 全量 Remove + Renderer.Dispose + disposed（core） | 修正常规路径 | **单独移植仍未解决 Initialize/Dispose 交错** |
| **A′** | A + Initialize 交错保护 + PlayerGame 重载合并/await | 覆盖生产与作者工具 | 改动稍多 |
| B | Dispose 时 RemoveAllListeners | 粗暴 | 可能误伤同事件其它订阅者（当前仅 SB） |

**推荐：A′。**

- Storyboard 与 Renderer 的事件全部改成命名方法并对称 Remove。
- Dispose 幂等，清完 Text/Sprite/Line/Video/Controller/NoteController/Trigger/Template；仅当 `UnitFloat.Storyboard == this` 时清静态。
- `ReloadStoryboard` 只调一次 `Storyboard.Dispose()`；把多次文件变化合并为“当前 reload 完成后再重载最新文件”，并 await Initialize。
- Initialize 完成异步 Spawn 后、注册监听前检查实例是否已失效；失效实例必须清理初始化期间创建的 renderer，不能因 `disposed` 早退而漏掉晚到对象。
- **FileSystemWatcher**：`ReloadAll` / 切场景时必须 `EnableRaisingEvents = false` 并 `Dispose` watcher；禁止旧 watcher 向已销毁 Game 继续排 `ReloadStoryboard`（第三方补充）。

**不变量（补丁后）：** Initialize 与 Dispose 监听一一对应；Dispose 幂等；任意时刻最多一个 PlayerGame Storyboard 在初始化/接收事件；seek Clear 保留，但永久 destroy 不再 Clear 后失联；无存活 orphan watcher。

**验收：** 热重载 10 次及 3 次连续快速写文件，每帧 `OnGameUpdate` 仅一次；旧实例无监听、无 renderer；Abort/Retry 与初始化交错无双重 Dispose 或晚挂监听；切场景后 watcher 已停且不再回调。

---

### B08 · ObjectPool.Dispose 不完整

| | |
|--|--|
| **严重度** | P1 Leak（会话级） |
| **用户影响** | 连开多关 RAM 上升；拖尾 GO / 粒子残留 |
| **置信度** | **High**（代码确认） |

**现状：**

```csharp
SpawnedNotes.Dispose…; notePoolItems.Dispose…; dragLinePoolItem.Dispose();
// 缺失：SpawnedDragLines、effectPoolItems
```

**方案：** 补两行 ForEach Dispose（core `cb740083`）。先清 Spawned* 再清池。

**静态规模：** 运行时拖线池大小 = 谱面同页四类 drag 最大值之和 × 3（`Game` 在 `ObjectPool.Initialize()` 前调用 `UpdateNoteObjectCount`）；字段字面默认值在生产路径不生效。活动 `SpawnedDragLines` 不在池队列中。粒子池由谱面同页最大 note/hold 数计算，可远高于几十个。字节量仍须 Profiler，本文不再给无依据的 MB 粗估。

**验收：** 同谱连续 10 局；每局场景切换并等待 Destroy 生效后，`DragLineElement` / 池化 `ParticleSystem` retained count 回到首局基线，不单调增长。内存只看趋势与对象归属，不用噪声较大的“总堆 ±5%”作为唯一门禁。

---

### B11 · GameCover 空引用与未释放

| | |
|--|--|
| **严重度** | **P0 Crash** + P1 Leak |
| **用户影响** | `display_background:false` 谱面或 GlobalCalibration 正常退出可空引用；Abort/Retry/正常结束后 Cover 纹理可跨会话存活 |
| **置信度** | High |

**代码事实：**

- `OnGameBeforeStart` 仅在非 GlobalCalibration 且 `DisplayBackground` 时赋值 `cover`。
- `OnGameBeforeExit` 无条件 `cover.DOFade(...)`，因此 `cover == null` 时直接 NRE。
- `OnGameBeforeExit` 只走正常 Complete；Abort / Retry 直接 `Dispose()`，不能只在 fade 回调释放缓存。
- core `cb740083` 在启动 fade 后立即 Dispose tag，会让仍引用该 Sprite 的 Image 与 tween 交错，不宜原样移植。

**方案：**

1. `OnGameBeforeExit` 对 `cover` 判空，只负责视觉 fade。
2. 增加统一、幂等的 GameRenderer 资源清理，由 `Game.Dispose()` 在正常退出、Abort、Retry 都调用：Kill tween → `cover.sprite = null` → `DisposeTaggedCacheAssets(GameCover)` → 清引用。
3. 正常 Complete 路径应允许 0.8s fade 与现有 `UnloadUnusedAssets`/场景加载重叠，最终在 Dispose 清理；不要在 fade 开始前销毁 Sprite。

**验收：** `display_background:false` 与 GlobalCalibration 完成退出不崩；正常完成、Abort、Retry 后 GameCover tag 计数归零；连续再进关可重新加载且淡入正常。

---

### B12 · Destroy 图 / typed 列表

| | |
|--|--|
| **严重度** | **P0 Crash** + P1 Leak/Correctness |
| **用户影响** | trigger destroy 活动对象时可立即 `KeyNotFoundException`；混合类型子树销毁后 typed 列表残留 |
| **置信度** | **High（类型键已逐项核对）** |

**确定性崩溃：** `TypedComponentRenderers` 的 key 是 model 类型（`typeof(Text/Sprite/Line/Video/Controller/NoteController)`），但 `DestroyObjectsById` 用 `it.GetType()`（如 `TextRenderer`）索引。只要 trigger destroy 命中已生成对象，该索引就不可能匹配。

**其它缺陷：**

- state destroy 图把外层循环的 **child** `type` 写给父子所有节点（含 `renderersToDestroy[parent.Id] = type`）；跨类型子树会从错误 typed list 静默漏删，已 Dispose 的 renderer 仍可能残留并在下帧被 Update。
- `DestroyObjectsById` 只处理根，不展平 Children，也不从 Parent.Children 脱链。（注：该方法开头**有** `ContainsKey` 守卫；`OnGameUpdate` 销毁表是 `Dictionary<id,…>`，同帧同 id 天然去重——旧稿「无守卫二次访问」表述已撤回。）
- PlayerGame 对永久 destroy 使用 `Clear()` 后仍删索引，隐藏对象从此无法恢复，也无法被 Renderer.Dispose 找回。
- **Line 共享 target：** `Sprites.LineRenderer.Initialize` 可复用 `targetRenderer.Line`，但 `Dispose()` 无条件 `Destroy(Line.gameObject)`——与 Video 同类双毁；`297f7536` 仅给 Video 加了 `ownsVideoObjects`。

**方案：** 按实际 `renderer.Component.GetType()` 记录类型；根 + Children 展平去重；所有字典读取 TryGet；从 parent 脱链；每个节点从两张索引删除。永久 destroy 在 PlayerGame 也 Dispose，seek 的全局 `Renderer.Clear()` 另行保留。最小 shared-target ownership：**Video**（`ownsVideoObjects` + `RenderTexture.Release()`）与 **Line**（仅 owner 可 Destroy 共享 `Line` GO）均归 B12；prepare/path/timeline 等完整视频加固仍归 B06。P01 的静态 UpdateOrder / 复用临时字典可同 PR，但必须独立验证。

**验收：** trigger destroy Text/Sprite/Video/Line 各一例不崩；父 Sprite + 子 Text/Video 的 state destroy 全部从两张索引消失；同帧父子重复 destroy 幂等；PlayerGame 热重载后无被 Clear 后失联的 GameObject；共享 `target_id` 的 Video **与 Line** 底层对象只由 owner 释放一次。

---

### B16 · AssetMemory 加载异常导致永久等待

| | |
|--|--|
| **严重度** | P0 Hang / 死锁 |
| **置信度** | **High**（已追踪多个异常/不完成路径） |

**根因不止非法 options：** `isLoading.Add(path)` 后，以下路径都可能不清状态：

1. `await request.SendWebRequest()` 的 HTTP / 网络异常直接抛出；下方 `isNetworkError` 分支来不及执行。upstream `#176` 的真实复现就是首局资源 404、次局同 path 永久等待。
2. FitCrop 收到非 `SpriteAssetOptions` 时无参抛出 `ArgumentException`（无消息）。
3. `AudioClipLoader.Load`、`TextureScaler`、文件 I/O 等未来/现有异常（含 GetAudioClip 分支上另一处无保护 UWR await——外层 finally 可清 `isLoading`，验收须单测音频失败路径）。
4. 两个本地 `async void Task()` 在后台执行 `File.WriteAllBytes` / `File.Copy`；若 I/O 抛出，`completed` 永远不会置 true，外层 `WaitUntil` 本身永不结束。**仅加 finally 也救不了一个永不完成的方法。**

另有一处同路径等待后的递归调用漏传 `useFileCacheOnly`，会把“只读文件缓存”的调用悄悄变成允许联网。

**方案（均为 Must）：**

1. `isLoading.Add(path)` 后只调用提取出的 `LoadAssetCore`，外层 `try/finally { isLoading.Remove(path); }`；删除散落的 Remove。
2. 把两个 `async void + completed + WaitUntil` 改为真正可 await 的 UniTask；后台 I/O 异常必须回到调用链，并在继续创建 Unity Sprite 前切回主线程。
3. 在下载/创建 Texture 前验证 options 类型与尺寸数组，错误信息包含 path；失败时清理已创建的 Texture/Audio loader。
4. 等待同 path 后递归重试时完整传递 `useFileCacheOnly`。
5. 不把 B04 设为 B16 的正确性依赖；B16 独立阻断所有会话重试卡死。

**验收：**

- HTTP 404、断网、非法 options、FitCrop 写入无权限/路径冲突四种失败后，同 path 第二次 Load 能结束（成功或明确失败），绝不永久 Wait。
- 两个并发同 path 请求都能结束；cache-only waiter 不发起网络请求。
- 失败前后的 `isLoading` 计数回到 0；临时 Texture/Audio loader 无 retained 增长。

**回滚：** 不可接受带 hang 发版；Must。

---

## 4. Should 项详细设计

### B04 · 深链核清理缓存

**现状：** `OnCytoidDeepLinkActivated` 成功拉关后 `foreach AssetTag`（**除 `PlayerAvatar`**）全部 `DisposeTaggedCacheAssets`。注意：`AssetTag.Avatar`（限额 100）**会被清掉**；亦会清可能正在播放的 `PreviewMusic`。

**代码复核：**

- TagLimits 静态上限合计约 210（不含无上限 Storyboard，且同一 Entry 可有多个 tag），因此原“75–450 个资源”不能从代码精确推出，删除该字节/数量估算。
- Community/LevelSelection/GamePreparation 等 Screen 已在离场时清自己的 thumbnail / Preview / GameCover；Storyboard 与 B11 修复后也有会话级 owner。调用点还包括 Event/Tier/Training/StoryboardRenderer 等。
- deep-link handler 全量清理会越权销毁仍被当前 Screen 引用的 Sprite（含 Avatar、在播 PreviewMusic）；它不是正确的资源 owner。
- 协议判断在调用方 `OnDeepLinkActivated` 使用 `Application.absoluteURL` 而不是回调参数 `url`，热启动第二次 deep link 可按旧 scheme 路由。

**方案：**

| 方案 | 描述 |
|------|------|
| **A** | 删除 deep-link handler 的 flush，让各 owner 在既有退出/Dispose 路径清理 |
| B | 仅清 GameCover / PreviewMusic | 仍可能与正在过渡的 Screen 争用同 tag；收益不明确 |
| C | 保留全量 flush | 继续破坏资源所有权，不推荐 |

**推荐：A；并把 scheme 解析改为 `Uri.TryCreate(url, ...)` 或至少 `url.Split(':')[0]`。B16 是独立 Must，不是 B04 前置依赖。**

**验收：** 冷/热启 `cytoid://levels/{id}`；社区页缩略图不永久空白。

---

### B09 · NLayerLoader seek 堆积 MpegFile（完整同步版）

| | |
|--|--|
| **严重度** | P1 Leak / Race |
| **用户影响** | **桌面/编辑器** 本地 MP3 流式 seek；Android/iOS 走 UWR，通常不受影响 |
| **置信度** | 堆积机制 High；Dispose 竞争为静态并发风险 Med–High |

**现状：** 每次 `PCMSetPositionCallback` 都 `new MpegFile` 并加入 `createdFiles`，直到 AudioClip Unload 才一次性释放；PlayerGame 拖动时间轴会线性增加文件句柄/decoder。

**对 `900af401` 的复核：**

- 复用单 `MpegFile` 是正确方向。
- NLayer `MpegFile` 自身用 `_seekLock` 同步 `ReadSamples` 与 seek，因此外层无需再为 read/seek 重复造实例。
- 但 `MpegFile.Dispose()` 不持 `_seekLock`；`900af401` 的外层 `fileLock` 只覆盖 seek 与 Dispose，PCM reader callback 没持同一把锁。`AudioClip` 的 Destroy 又是延迟的，Unload 时仍可能出现 read ↔ Dispose 竞争。

**方案：** 只保留一个 `MpegFile`；同一外层锁覆盖 PCM read、set-position 和 Dispose；Dispose 后 reader 填静音并返回。不要整提 UnityAudioServer，也不再保留 `createdFiles` 列表。

**验收：** Editor 播放中拖动 100 次，活跃 MpegFile/文件句柄恒为 1；播放时间正确；播放中退出/热重载 20 次无 `ObjectDisposedException`、音频线程异常或死锁。

**定位：** 商店移动端不受主路径影响，因此从 Must 降为 Should；若测试时间不足可延后，不阻塞 2.1.5。

---

### P01–P04 · 定点 GC

| ID | 热点 | 估 Alloc | 修复 |
|----|------|----------|------|
| P01 | SB 每帧 `new Type[]`+`Dictionary` | 每帧 1 次数组+字典 | 静态 + Clear |
| P02 | Scanner 每帧 `GetComponent<LineRenderer>`×3 | CPU | 用已有 `lineRenderer` 字段 |
| P03 | Accuracy 每帧 ToString | 未量化 | dirty-check；`cb740083` 已含 Accuracy（+ GameTime 0.1s 缓存）；移植时覆盖校准/未开始分支 |
| P04 | Score 每帧 `ToString("D6")` | 未量化 | 仅变化时写；**Score 需 private 另补**（`cb740083` 已修 Accuracy/Combo/GameTime，**未**修 Score） |

**用户可感知卡顿：置信度 Low–Med**；成本低但不伪装成已量化收益，作为独立 Should 提交，便于 Profiler 前后对照。  
**先做 P01–P04，再谈 Note 热路径（P08）。**

---

## 5. 默认延后项 / 条件重开项

### B06 · VideoRenderer（可选里程碑）

**问题复合：** prepare 回调不退订、无 errorReceived、Dispose 双毁共享 target、无 RT.Release、每帧轮询播放状态但首次/恢复 seek 被注释、Android Q path 脆弱。

**方案：**

| 方案 | 描述 | 风险 |
|------|------|------|
| **A** | 移植 core 视频加固 + `Level.Path` 适配 | 中；需真机 |
| B | 仅修 path + 退订 | 低；同步问题仍在 |
| C | prepare 失败则禁用视频 | 降级 |

**推荐：** 若 2.1.5 含视频 → **A**；否则整项进 2.1.6。  
**私有适配：** 无 VFS；使用规范化的 `Level.Path + relative video path`；共享 `target_id` renderer 只有 owner 可 Destroy VideoPlayer/RawImage/RT；`RenderTexture.Release()` 后再 Destroy；prepare 的两个事件在成功/错误/超时/异常都退订。PlayerGame seek 的全局 Clear 保留，但 B12 的永久 destroy 仍走 Dispose。

**验收门禁（真机 Android 10+）：** 视频可见；暂停/恢复同步；重试 5 次 VideoPlayer 实例稳定；`target_id` 共享不双毁。

---

### B10 · GetScannerPositionY 副作用（关键）

**事实（已 grep 验证）：**

- `GetScannerPositionY` **唯一调用者** = `Scanner.OnGameUpdate`
- 方法内 `CurrentPageId = 0` 再扫描
- `Game.Update` 在 `onGameUpdate` 之前另有单调 `CurrentPageId++`；Scanner 每帧稍后执行，事实上承担了隐式回拨同步
- `CurrentPageId` 读者：InputController、DragHead/Child、GameRenderer
- 明确时间跳变点至少包括 PlayerGame slider、PlayerGame 循环归零、`StartAt`、Editor 初始位置；生产 `SynchronizeMusic` 的 DSP 校正也可能小幅向后跨 page 边界

**风险：** 若只改成局部 `pageId`（core 纯函数化），**单调前向游玩通常仍对**；但 PlayerGame 回拨与生产 DSP 向后校正会失去当前唯一的 page 修正，触控门控、drag 与边界速度会读到过期 page。

**方案：**

| 方案 | 描述 |
|------|------|
| A | 仅 local pageId（不安全单独上） |
| **A+B** | local pageId + `SyncPageToTime(time, emitBoundaryEvents)`，覆盖所有显式 seek/循环，并定义 DSP 回拨策略 |
| D | 本版本不动，文档标明耦合 |

**推荐：2.1.5 Defer。后续若做 A+B，必须分别测试“seek 只同步状态、不补发跨越的 boundary 事件”和“正常前向播放仍逐页发事件”；禁止只合 A。**

---

### B13 · LineStateParser 单行补丁无完整消费链

候选补丁把 `ParseObjectState` 改为 `ParseStageObjectState`，会开始填充 X/Y/Z、dx/dy、旋转、缩放、宽高等字段；但 private/core 当前 `LineEaser.OnUpdate()` 只读取 `Pos/Width/Color/Opacity/Layer/Order`，不会把 StageObjectState 的 X/Y/旋转/缩放应用到 `Line.transform`。

→ 单改 parser 不会让“带 x/y 的 line 突然正确”，却扩大了状态数据面且没有端到端测试。第三方进一步指出：`LineState` 对 Width/Opacity 等存在无 `new` 的字段隐藏，单行 parser 写基类槽、easer 读隐藏字段时**连状态面都不会真正改变**。**2.1.5 Defer**；后续应连同 Line transform easer、parent/target equivalent transform 和谱面截图基线一起设计。

---

### B14 · NativeAudio 初始化 / 索引双缺陷

**代码事实：**

- `androidAudioTrackCount = 2`，但 `Reserved3 = 2`，RoundRobin 使用 3–6；Native source 下标越界。
- 设置入口在 Android/iOS 可见，默认虽为 `false`，用户可运行时打开。
- 启动时 false 则根本不调用 `NativeAudio.Initialize()`；设置回调只执行 `SetUseNativeAudio(true)`，随后 Load 可能在未初始化状态调用 `NativeAudio.Load`。
- 简单 `% GetNativeSourceCount()` 会把 3–6 映射到 0/1，与音乐保留 source 冲突，hit sound 可能抢占音乐，不是可接受修复。

**推荐：** 2.1.5 保持默认关，并隐藏/禁用入口或明确“重启后生效”且仍强制安全 source 配置。完整恢复需统一 Initialize/Dispose/热切换生命周期，并保证至少 7 个 source 或重新设计 music/SFX 分区；另开版本，不用一行取模冒充修复。已知同模式遗留：`Exceed7Controller.Unload()` 本身是 `async void`，归入本完整 NativeAudio 设计一并处理，不进 2.1.5 Must。

---

## 6. 依赖与交互矩阵

```
B07 lifecycle ──共同约束──► B12 renderer destroy / PlayerGame reload
B12 destroy graph ──可同文件搭载但独立验收──► P01 临时容器复用
B08 ObjectPool ──兜底会话退出──► B05 异常 dragline
B11 GameRenderer owner cleanup ──取代──► B04 deep-link 越权清 GameCover
B16 AssetMemory ──独立 Must；与 B04 无正确性前置关系
B09 corrected lock ──独立 Should；不阻塞商店 Must
B06 ──独立里程碑；B12 仅先取最小 shared-video ownership
B10-A ──禁止单独──► 须显式 page sync 或 Defer
```

| 组合 | 约束 |
|------|------|
| B07 + B12 | Dispose/Destroy 都必须幂等；PlayerGame 只在 seek 做 Clear |
| B08 + B05 | 即使坏 drag 未 Collect，Game.Dispose 仍须清活动对象 |
| B04 + B11 | deep link 不再清 GameCover；GameRenderer 成为统一 owner |
| B12 + B06 | B12 先保证 shared target ownership；B06 再加 prepare/path/timeline |
| B16 + B04 | 可按实施顺序先 B16，但不是合入依赖 |

---

## 7. 实施计划

### Phase 1 — Must（止崩止漏）· 2–3d

顺序：`B16 → B01 → B02 → B03 → B12 → B07 → B08 → B05 → B11`  
建议 2–3 个 PR：AssetMemory；Storyboard parsing/destroy/lifecycle；Game pool/drag/cover。

### Phase 2 — Should · 0.5–1d

`B04`；修正版 `B09`；`P01–P04`。性能项独立提交并保留 Profiler 前后样本。

### Phase 3 — 默认延后项 / 发版

- 默认 `B06`、`B10` 不进 2.1.5；若评审强制纳入，各自独立 PR 和门禁。
- B13 / B14 / P08 明确不进版本。
- `Context.Version*` + `ProjectSettings` 同步（2.1.5 / 124 / Android 200124）  
- 可选 `disable_lunar_console.sh`  
- 手打 AAB / IPA  
- 无自动化 CI（已知约束）

---

## 8. 验收矩阵（合入门禁）

| # | 场景 | 覆盖 | 通过标准 | 环境 |
|---|------|------|----------|------|
| **0** | 实际提交平台真机 smoke：进关→打完→结算→再进关；另 Abort/Retry 各一次 | Must 总集 | 无未捕获异常阻断游玩；可再进关 | **Android 必测；若提交 App Store，iOS 同测** |
| 1 | 缺失 template SB | B01 | 进关；仅 Warning | Editor |
| 2 | 坏 note 时间表达式 | B02 | 进关不崩；Warning 含 note id | Editor |
| 3 | Score/Uses + 非法 trigger 数组 | B03 | 无 InvalidOperation；Score 一次；Note/Combo Uses 正确；坏数组降级 | Editor |
| 4 | trigger/state destroy 混合父子树 + shared Video/**Line** | B12 | 无 KeyNotFound；两张索引无残留；重复 destroy 幂等；Video/Line 底层对象释放一次 | Editor |
| 5 | intro≤0 + 同时刻/逆序 drag 链 | B05 | 无 NaN；坏 drag 可 Collect 或会话结束销毁 | Editor |
| 6 | 404/断网/options/写盘失败/音频 UWR 失败后重试同 path | B16 | 所有请求有限时间结束；`isLoading=0`；cache-only 不联网 | Editor + 可控故障注入 |
| 7 | 无背景谱 / GlobalCalibration 完成；Abort/Retry | B11 | 不空引用；三类退出后 GameCover tag=0 | Editor |
| 8 | PlayerGame 热重载 ×10 + 连续快速写 ×3；**生产 Abort/Retry × 异步 Initialize 交错** | B07 | 每帧更新一次；仅一个实例；无旧监听/renderer/orphan watcher | Editor |
| 9 | 连开同谱 10 局 | B08 B11 B07 | 等待 Destroy 后 DragLine/Particle/Cover retained count 不单调涨 | Editor Profiler |
| 10 | 冷启 + 两次不同 scheme 热启 deep link | B04 | 使用本次 url 路由；当前 Screen 资源不被越权销毁 | Android |
| 11 | 播放中 seek ×100 + 退出 ×20 | B09 | 单 MpegFile；无句柄增长、音频线程异常或死锁 | Editor/Desktop |
| 12 | GC Alloc 前后对照 | P01–P04 | 对应调用点 Alloc 降低；HUD 文本仍首次正确显示 | Editor Profiler |
| 13 | （若含 B06）普通/共享 target 视频 | B06 B12 | prepare 错误/超时可退订；播/暂停/重试稳定 | **Android 10+ 真机** |
| 14 | （若含 B10）PlayerGame 回拨 + DSP 边界回拨 | B10 | CurrentPageId 正确；不补发 seek 跨越的 boundary 事件 | Editor |
| **15** | 至少 3 首已知良好参考谱 Auto 模打 | B01 B03 B05 B12 | 分数/连击/SB 关键视觉与 2.1.4 基线一致；任何差异均有明确归因和批准 | Editor |

**未通过 #0–#9 或 #15 任一项 → 禁止打 2.1.5 tag。** Should / 条件重开项只有实际合入时才启用对应 #10–#14 门禁。

**#15 参考谱集要求：** 在发版分支冻结谱面 id / 文件哈希和 2.1.4 基线证据；整组至少覆盖普通无 SB 对照、合法 template/trigger/父子对象的复杂 SB、drag-heavy 谱三类。证据至少包含结果页分数/连击和 SB 关键时点截图；若现有单谱不能覆盖全部特征，可增加谱数，不得降低类别覆盖。

> **术语：** 本文「P0 Crash」在 Unity 下多为**主循环未捕获异常**（Console 红错 + 当帧逻辑中断），未必进程退出；用户影响列（进关失败 / 中盘异常 / 退出异常）为准。

---

## 9. 风险登记

| ID | 风险 | 等级 | 缓解 |
|----|------|------|------|
| R1 | B06 视频回归 | 中 | 独立 PR；真机门禁；可砍出版本 |
| R2 | B10 只合半套破坏校准 | **高** | 评审禁止；Defer 或配套 Sync |
| R3 | B13 parser-only 被误认为已修 Line transform | 中 | 2.1.5 Defer；以后做完整消费链与截图基线 |
| R4 | B03 改语义导致触发次数变 | 中 | 2.1.5 明确保留 Score one-shot；语义变更另案 |
| R5 | 无 CI，合入靠人肉 | 中 | 验收矩阵强制勾选；**#0 真机 smoke 硬门禁** |
| R6 | 版本号双文件不同步 | 中 | 发版 checklist |
| R7 | 静态分析高估/低估频率 | 中 | 置信度已标注；上线后看 Sentry |
| R8 | B07 已 Dispose 实例在异步 Initialize 返回后复活 | **高** | 合并 PlayerGame reload；初始化交错故障测试 |
| R9 | B16 后台文件任务永不完成，finally 无机会执行 | **高** | 移除 async void/polling；I/O 故障注入 |
| R10 | B09 Dispose 与 PCM reader 竞争 | 中 | 外层同锁覆盖 read/seek/dispose；播放中退出测试 |
| R11 | B14 一行取模让 SFX 抢占音乐 | 中 | 隐藏入口并 Defer 完整 NativeAudio 修复 |
| R12 | Editor 全绿但 IL2CPP 行为差异 | 中 | #0 覆盖每个实际提交平台；关键异常路径优先真机复现 |

---

## 10. 回滚策略

| 粒度 | 动作 |
|------|------|
| 单 PR | `git revert`；商店未上架则重新出包 |
| 已上架严重崩 | 热修 2.1.5.1：只保留 Must 中已验证子集 |
| B06 问题 | 关闭视频关卡运营侧规避（若有）或 revert 视频 PR |

---

## 11. 补丁来源索引

| 条目 | 来源 | 私有适配 |
|------|------|----------|
| B01 B02 B07 B12 P01 | `297f7536` | B07 加初始化交错 + watcher 生命周期；B12 含 Line/Video shared-target ownership；永久 destroy 不保留 PlayerGame Clear |
| B08 P02 P03 HUD dirty-check | `cb740083` | 已含 Accuracy/Combo/GameTime；**Score 需 private 另补**；去掉 Embed/recorder |
| B11 | `cb740083` 提示 | 不直打立即释放；补 null guard + 统一 Dispose owner |
| B09 | `900af401` 方向 | **不可直打**；外层锁须覆盖 PCM read/seek/dispose |
| B05 | upstream `#170` | intro 精确移植；private 另补 outro≤0 |
| B06 | `831b64ac` + video dispose | `Level.Path`；无 VFS |
| B13 | `297f7536` 候选单行 | 消费链不完整，2.1.5 不移植 |
| B16 | upstream `#176` + private 新审计 | finally + 消除 async void 文件任务 + 参数透传 |
| B03 B04 | 本评审新确认 | 建议回馈 core |

---

## 12. 评审检查清单（会议用）

- [ ] 确认 Must 列表包含 B11/B12/B16；B06 不属于 Must  
- [ ] 确认 B03 保留 Score one-shot、Uses 仅控制 NoteClear/Combo（第三方已投保留）  
- [ ] 拍板 B10：**Defer**（第三方已投 Defer）  
- [ ] 拍板 B06：**2.1.6**（第三方已投延后）  
- [ ] 确认 B13/B14 在 2.1.5 Defer（含 Exceed7Controller.Unload async void 记入 B14）  
- [ ] 拍板 B04 删除 deep-link 全量 flush + scheme 用回调 url  
- [ ] 确认 PlayerGame 仅 seek 保留 Clear，永久 destroy 走 Dispose；B12 含 Line shared-target ownership  
- [ ] 确认 B07 A′ 含 FileSystemWatcher 停用/Dispose  
- [ ] 确认 B09 若合入必须采用完整同步版；可不阻塞 Must 发版  
- [ ] 确认验收矩阵 **#0–#9 与 #15** 为 Must 发版门禁  
- [ ] 指定负责人与目标日期  
- [ ] 冻结 #15 参考谱 id / 文件哈希与 2.1.4 基线证据  
- [ ] 指定各提交平台真机 smoke 验证人（#0；Android 必测，提交 iOS 时 iOS 同测）

---

## 附录 A · 置信度图例

| 级别 | 含义 |
|------|------|
| High | 代码路径已读通 / grep 验证 / 与 core 对照一致 |
| Med | 逻辑成立，频率或字节数为估算 |
| Low | 需 Profiler 或线上数据 |

**Crash 用语：** P0「崩溃」= Unity 主循环未捕获异常导致功能失败（进关失败 / 中盘中断 / 退出异常），不保证进程退出。

## 附录 B · 相关产物

- **第三方审核索引：** [2026-07-23-private-2.1.5-THIRD-PARTY-REVIEW-INDEX.md](./2026-07-23-private-2.1.5-THIRD-PARTY-REVIEW-INDEX.md)
- **第三方审核回复：** [2026-07-23-private-2.1.5-third-party-review-response.md](./2026-07-23-private-2.1.5-third-party-review-response.md)（Verdict: Agree-with-changes；已吸收）
- **模块级代码审计：** [2026-07-23-private-module-code-audit.md](./2026-07-23-private-module-code-audit.md)（包内 `04`；§0.6 增量摘要）
- 交互总览 canvas：`cytoid-private-research.canvas.tsx`（本机 Cursor 产物；非发版依据）
- 早期条目表 canvas：`cytoid-private-bug-perf.canvas.tsx`（本机 Cursor 产物；深度不及本文，非发版依据）
- 模块审计 canvas：`cytoid-private-module-audit.canvas.tsx`（本机 Cursor 产物；非发版依据）
- 远程：`private`（Cytoid-private）/ `upstream`（Cytoid core）/ `origin`（ky-reflection fork）
- 早期 core 审计（可选）：`docs/local/2026-07-17-core-leak-perf-audit.md`

## 附录 C · 二次复核代码证据索引

以下均以 `private/main@0e11d2c3` 为准；方法名比行号稳定：

| 结论 | private 证据 | 对照 |
|------|--------------|------|
| B01/B02/B03 | `Assets/Scripts/Storyboard/Storyboard.cs`：`LoadObject`、`CreateState`、`ParseTime`、`OnNoteClear`、`LoadTrigger` | `297f7536` |
| B07 初始化交错 | `Storyboard.Initialize` 先 await Renderer；`PlayerGame.ReloadStoryboard` 不 await 且重复 Dispose；watcher 未停用/Dispose | `297f7536` 只覆盖常规幂等/退订，需 private follow-up |
| B12 类型键/共享对象 | `StoryboardRenderer.Initialize` 以 model Type 建表；`DestroyObjectsById` 以 renderer `GetType()` 取表；Video/Line renderer 复用 target 后无 owner 保护 | `297f7536` 仅覆盖 Video owner，Line 需 private follow-up |
| B13 无消费链 | `LineStateParser.Parse`、`LineState` 隐藏字段、`LineEaser.OnUpdate`、`StoryboardComponentRenderer.Update` | `297f7536` 单行 parser 候选 |
| B05 两个分母 | `Assets/Scripts/Game/Notes/DragLineElement.cs`：`introRatio`、`outroRatio` | `a6223bd7` 仅覆盖 intro |
| B08 池规模/遗漏 | `Assets/Scripts/Game/ObjectPool.cs`：`Initialize`、`Dispose` | `cb740083` |
| B11 空 cover | `Assets/Scripts/Game/Notes/GameRenderer.cs`：条件赋值 `cover`、无条件 `OnGameBeforeExit` | `cb740083` 仅提示释放点 |
| B16 异常与永不完成 | `Assets/Scripts/Utils/AssetMemory.cs`：`isLoading.Add` 后两个 UWR await（含 AudioClip）、两个 `async void Task`、递归 Load | `218ddeda` / upstream `#176` |
| B04 owner 越界 | `Assets/Scripts/Navigation/NavigationBehavior.cs` 全 tag flush；各 Screen 的 `OnScreenChangeFinished` 定点释放 | 无可直打补丁 |
| B09 read/dispose | `Assets/Scripts/Utils/NLayerLoader.cs`；`Assets/Plugins/NLayer/MpegFile.cs` 的 `_seekLock` 只包 read/seek | `900af401` 方向不完整 |
| B10 隐式 page sync | `Chart.GetScannerPositionY`、`Game.Update`、`Scanner.OnGameUpdate`、`PlayerGame.OnSliderSeek/SynchronizeMusic` | `cb740083` 纯函数化候选 |
| B14 双缺陷 | `AudioManager.Initialize/SetUseNativeAudio/GetAvailableIndex`；`SettingsFactory` NativeAudio 开关 | 无安全的一行补丁 |

---

**文档结束 · owner 按 §0.3 / §12 确认范围、门禁、负责人和排期后，状态可升格为「已批准」。**
