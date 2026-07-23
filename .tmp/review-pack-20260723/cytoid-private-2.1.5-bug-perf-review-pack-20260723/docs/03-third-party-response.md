# 第三方审核意见 · Cytoid-private 2.1.5 Bug/Perf 方案

| 字段 | 内容 |
|------|------|
| 性质 | 第三方 agent 独立审核回复（对应审核索引 §7 交付格式） |
| 日期 | 2026-07-23 |
| 主审对象 | [2026-07-23-private-2.1.5-bug-perf-design-review.md](./2026-07-23-private-2.1.5-bug-perf-design-review.md) |
| 审核入口 | [2026-07-23-private-2.1.5-THIRD-PARTY-REVIEW-INDEX.md](./2026-07-23-private-2.1.5-THIRD-PARTY-REVIEW-INDEX.md) |
| 事实基线 | `private/main` @ `0e11d2c3f12ed61968aac3058272748045b27f3d`（已核对哈希一致） |
| 审核方式 | 静态审计：按方法名在基线提交逐条复现附录 C 全部证据，并核查 6 个补丁提交（`297f7536` / `cb740083` / `900af401` / `a6223bd7` / `831b64ac` / `218ddeda`）的实际内容。未改动任何生产代码。 |

---

## Verdict: Agree-with-changes

Must/Should/Cond/Defer 的划分与代码事实一致，二次复核的升降档全部站得住。但主文档有 **4 处事实性错误必须修正**、**2 处范围遗漏建议补强**、**验收矩阵 4 个缺口**，详见下文。这些不动摇结论，只影响文档准确性与门禁完备性。

## Confirmed (code-backed)

- **B01**：`Storyboard.cs` `LoadObject` 与 `CreateState` 两处 `Templates[templateId]` 裸索引属实；`CreateState` 索引后紧跟的 `if (templateObject != null)` 是死判空（索引器永不返回 null）。
- **B02**：链路 `CreateState` → 六个 parser → `ParseObjectState` → `Storyboard.ParseTime` → `Game.Chart.Model.note_map[id]` 裸索引，每一环均存在。
- **B03**：`Triggers` 为 `List<Trigger>`；`OnNoteClear` foreach 内 Score 分支 `Triggers.Remove`，`OnTrigger` 在 Uses 耗尽时也 Remove。`LoadTrigger` 的 `notes/spawn/destroy` 只判 null 不判形状：JObject 形状在 `Values<int>()` 时抛 `InvalidCastException`（载入期），标量/null 静默得空列表——文档「收窄为 JArray + Warning」的方案与两类现状都兼容。
- **B05**：`introRatio` / `outroRatio` 两表达式原文核实，分母均无保护；`outroRatio >= 1 → Collect()` 在 `OnGameUpdate` 顶部，NaN 恒 false，且全仓无其它回收路径（`CollectDragLine` 仅定义处与自身两处）。`a6223bd7` 确实只护 intro。
- **B07**：全部子点成立——lambda 注册 `onGameDisposed`、Dispose 只退 `onGameLateUpdate`、Renderer 三个 lambda、无幂等 flag、不清 `UnitFloat.Storyboard`；`PlayerGame.ReloadStoryboard` 为 `void` 不 await，FileSystemWatcher 一次保存触发多个 `Changed` 逐个排完整 reload，并发 Initialize 确实存在。**补充佐证**：`Game.Dispose` 只对 `onGameLateUpdate` 有 `RemoveAllListeners` 兜底，`onNoteClear` 无兜底——每次 reload 累积一个存活旧监听，B07 升级 Must 证据更硬。
- **B08**：`ObjectPool.Dispose` 缺 `SpawnedDragLines`/`effectPoolItems` 属实；特效池按谱面逐页统计计算属实。
- **B11**：`cover` 唯一赋值点（非 GlobalCalibration 且 DisplayBackground）与 `OnGameBeforeExit` 无条件 `cover.DOFade` 均核实；`Game.Abort()`/`Retry()`（`Game.cs` 627/651）直接 Dispose，`onGameBeforeExit.Invoke` 全仓仅 Complete 路径一处。升 Must（Crash+Leak）成立。
- **B12**：核心断言成立且为确定性缺陷——建表用 6 个 model 类型（`typeof(Text)` 等），`DestroyObjectsById` 用 `it.GetType()`（`TextRenderer` 等）查找，**永不匹配必抛 KeyNotFoundException**，且在 `it.Dispose()` 之后、`ComponentRenderers.Remove(id)` 之前抛出，经 `OnNoteClear` 的 foreach 外抛。不展平 Children、不脱链、PlayerGame `Clear()` 后删索引均属实。升 Must/P0 成立。
- **B13**：`LineStateParser.Parse` 用 `ParseObjectState`；`LineEaser.OnUpdate` 只消费 Pos/Width/Color/Opacity/Layer/Order。Defer 成立——且比文档所述更彻底：`LineState` 对 Width/Opacity 等字段存在无 `new` 的隐藏式重声明，单行 parser 补丁写的是基类槽位，easer 读的是隐藏字段，**补丁连状态面都不会真正改变**，纯死代码。
- **B16**：四条根因路径全部复现（两处 UWR 裸 await、无参 `ArgumentException`、两个 `async void Task()` + 无 token 的 `WaitUntil(completed)`、递归 Load 漏传 `useFileCacheOnly`）。`218ddeda` 确为部分修复：只加 try/finally，未消 async void，也未修漏参。Must 成立。
- **B04 机制**：全量 flush、`Application.absoluteURL` 取 scheme（实际在调用方 `OnDeepLinkActivated` 内）、各 Screen 定点释放，均核实（调用点比文档列的更多：还有 Event/Tier/Training/StoryboardRenderer 等）。
- **B09**：每次 seek `new MpegFile` 入 `createdFiles`、泄漏句柄数 = seek 次数 + 1；`MpegFile._seekLock` 只包 `ReadSamplesImpl`/`Position` setter，`Dispose()` 不持锁。`900af401` 核实：外层 `fileLock` 覆盖 seek+Dispose 但 **PCM read 回调完全不持锁、不判空**，Dispose 后 read 回调直接 NRE（音频线程）；且内外两把锁，Dispose 可与持 `_seekLock` 的进行中 read 并发。「不可直打」成立——另外它改的 `UnityAudioServer` 部分在 private 架构里根本不存在，cherry-pick 无从谈起。
- **B10**：`GetScannerPositionY` 内 `CurrentPageId = 0` 再扫描、唯一调用者 `Scanner.OnGameUpdate`、`Game.Update` 单调 `CurrentPageId++` 先于 `onGameUpdate`、读者集合（InputController/DragHead/DragChild/GameRenderer）、PlayerGame slider seek 与循环归零**均不重置** CurrentPageId、`SynchronizeMusic` 可致 Time 回拨跨页——全部核实。「禁止只合半套」站得住。
- **B14**：`androidAudioTrackCount = 2` + `RoundRobinStartIndex/EndIndex = 3/6` + `Reserved3 = 2` → native 路径下标 2–6 全部越界；设置开关 Android/iOS 可见、默认 false、运行时打开不触发 `NativeAudio.Initialize()` 而后续 Load 直调未初始化 native。Defer 成立。
- **版本**：`Context.cs` = `2.1.4` / `2.1.4 BUILD.1` / `VersionCode 123`；`ProjectVersion.txt` = `6000.0.58f2`。与索引一致。
- **补丁内容核对**：`297f7536` 确含 TryGetValue×2、倒序 for、命名监听对称退订、destroy graph 修复（`it.Component.GetType()`）及 B13 单行候选；`cb740083` 确含 scanner Y 纯函数化（local pageId）与 Dispose 补两行，且其 cover 处理确为「fade 启动后立即 Dispose tag 且未修 NRE」，不宜移植；`831b64ac` 确为 video prepare/timeline/VFS。

## Disputed / needs fix in design doc

- **[B12]** §3 断言「`ComponentRenderers[id]` 无 TryGet；同帧父子重复加入销毁表时可二次访问已删除 id」→ 证据：`DestroyObjectsById` 开头**有** `if (!ComponentRenderers.ContainsKey(id)) return;` 守卫；`OnGameUpdate` 的销毁表是 `Dictionary<id,…>`，同帧同 id 天然去重，该崩溃路径不存在。真正的第二缺陷是 `renderersToDestroy[renderer.Parent.Component.Id] = type`（外层循环的 **child** type）导致跨类型子树从错误 typed list 静默漏删、已 Dispose renderer 残留并被下帧继续 Update。→ 建议改写为：「`DestroyObjectsById` 有 id 守卫但 state destroy 图把子节点 type 误记给父 id，跨类型子树 typed 列表残留」。主断言（类型键不匹配必崩）不受影响。
- **[B04]** 「foreach AssetTag（**除 Avatar**）全部 flush」→ 证据：代码 `if (tag == AssetTag.PlayerAvatar) continue;`，排除的是 `PlayerAvatar`；`AssetTag.Avatar`（限额 100）**会被清掉**。另发现 flush 连带清 `PreviewMusic`，若 GamePreparation 预览正在播放，其 clip 被越权销毁。→ 建议改写为「除 PlayerAvatar 外全量 flush（含 Avatar 与可能正在播放的 PreviewMusic）」——这只让删除 flush 的理由更充分。
- **[B08]** 「初始化拖线池不是常量 48，而是四类 drag 初始池之和（**当前默认 120**）」→ 证据：`Game.cs` 在 `ObjectPool.Initialize()` 之前对每种 note 类型调 `UpdateNoteObjectCount(type, Chart.MaxSamePageNoteCountByType[type] * 3)`，运行时拖线池 = 3 × 该谱同页四类 drag 最大数之和，随谱面变化；字段字面默认 120 在生产路径从不生效。→ 建议改写为「运行时池大小 = 谱面同页最大值 × 3，随谱面缩放」。
- **[P04]** 「Score 需 private follow-up，`cb740083` **只修 Combo**」→ 证据：`cb740083` 实际 dirty-check 了三处：`AccuracyText`（新增 lastAccuracy）、`ComboText`、`GameTimeText`（0.1s 取整缓存）；`ScoreText` 确实未动。→ 建议改写为「`cb740083` 已修 Accuracy/Combo/GameTime，Score 每帧 `ToString("D6")` 需 private 另补」。结论不变，但 P03 的移植工作量比文档暗示的小。
- **[B16]** 措辞：`throw ArgumentException()` 实为**无参**抛出（仅影响文档准确性，不影响方案）。

## Scope recommendation delta

- **Must 维持 9 项不变**（B01 B02 B03 B05 B07 B08 B11 B12 B16），全部 code-backed，无增项。
- **B12 建议扩一句范围说明**：`Sprites.LineRenderer.Initialize` 对 `TargetId` 复用 `targetRenderer.Line`，但 `Dispose()` 无条件 `Destroy(Line.gameObject)`——与 Video 共享 target 同类的双毁问题存在于 **Line**，`297f7536` 只给 Video 加了 `ownsVideoObjects`。建议 B12 的 owner 语义覆盖 Line（或在文档显式记录为已知遗留并说明理由）。
- **B07 A′ 建议补一点**：`PlayerGame` 的 FileSystemWatcher 从不 `EnableRaisingEvents = false`/`Dispose`，`ReloadAll` 切场景后旧 watcher 仍向 dispatcher 排 `ReloadStoryboard`（作用于已销毁 Game）。纳入 A′ 的 reload 合并设计成本极低，建议加入范围。
- Should/Cond/Defer/Out 维持文档划分；B16 附加证据：`AudioClipLoader.Load` 的 GetAudioClip 分支还有一处无保护 UWR await，结构上被 B16 的外层 try/finally 覆盖，但验收应补一条音频路径故障用例。
- 已知但同意不进版本：`Exceed7Controller.Unload()` 本身是 `async void`（与 B16 同类模式），归入 B14 的 NativeAudio 完整设计一并处理，建议在 B14 段落注明。

## Acceptance gaps

- **G1（最重要）**：#1–#9 全部 Editor。Must 集发版前**零真机门禁**——商店目标是 Android/iOS IL2CPP，而 R5 已声明无 CI。建议加一条人工硬门禁：Android 真机 smoke（进关→完整打→结算→再进关，外加一次 Abort/Retry），否则「静态审计 + Editor 验证」对 IL2CPP 行为差异零覆盖。
- **G2**：B07 的生产路径交错（Abort/Retry 与异步 Initialize 交错）写进了 B07 自己的验收文字，但矩阵 #8 只覆盖 PlayerGame 热重载。建议把「生产 Abort/Retry × 初始化交错」单列或并入 #8。
- **G3**：缺「已知良好谱面回归基线」行。各项都声称「合法谱不变」，但矩阵没有一条用 N 首参考谱 Auto 模打对比分数/连击/SB 视觉的行——这是 B01/B03/B12/B05 语义保持声明的唯一成规模验证手段。
- **G4**：#4（B12）未覆盖上述 Line 共享 target 双毁用例；若采纳范围建议，#4 应加「共享 target_id 的 Line 只由 owner 释放一次」。
- 另注：B03/B11/B12 的「P0 Crash」在 Unity 语义下多为主循环未捕获异常（红错误 + 当帧中断），未必终止进程；文档的用户影响列描述（进关失败/中盘异常/退出异常）本身是准的，仅建议在附录 A 或术语处澄清「Crash = 未捕获异常致功能失败」，避免评审会对严重度措辞扯皮。

## Open questions vote

- **B03**：选「保留现状语义」（Score 过线一次性并无条件移除，Uses 只管 NoteClear/Combo）。理由：现状行为代码核实无误；语义变更会静默改变既有谱面表现且 2.1.5 没有对谱作者的沟通渠道；JArray 收窄 + Warning 与现状两类行为兼容，风险可控。
- **B10**：选 **Defer 整项**（方案 D）。理由：副作用当前是 PlayerGame seek/循环与 DSP 回拨唯一的 page 纠正机制（Scanner 注册早于 note，同帧内重置恰好先于读者）；只合 A 会静默破坏触控门控/drag/边界速度且无任何测试能立即暴露；A+B 需要 boundary 事件语义设计，超出小版本。
- **B06**：选「整项进 2.1.6」。理由：验收门禁要求 Android 10+ 真机，小版本不该背这个里程碑；B12 先取最小 shared-target ownership（建议含 Line）是正确切片。
- **B04**：选 **A（删除全量 flush）+ scheme 改用回调 `url` 解析**。理由：各 Screen/Game/Storyboard 的 owner 路径已核实存在；flush 连 Avatar 和可能在播的 PreviewMusic 都杀，越权证据比文档所写更硬；删除后无未认领 tag（Storyboard 无 TagLimits 上限但有显式 owner）。
- **B09**：选「仅以完整同步版纳入 Should，不阻塞发版」。理由：`900af401` 不可直打已双重确认（锁不覆盖 read、Dispose 后 read NRE、UnityAudioServer 部分无对应架构）；移动端主路径走 UWR 不受影响，降级为桌面/作者工具修复合理；测试资源不足时可再延后不碍 2.1.5。

---

**方法局限声明**：本审核与主文档同为静态审计 + 调用链 + 补丁对照，未做 Profiler/真机量化；频率与 Alloc 估算未独立验证，沿用了主文档的置信度标注。
