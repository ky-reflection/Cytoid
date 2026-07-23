# Cytoid-private 模块级代码审计报告

| 字段 | 内容 |
|------|------|
| 状态 | **审计完成 · 可评审** |
| 日期 | 2026-07-23 |
| 基线 | `private/main` @ `0e11d2c3`（产品版本 2.1.4 BUILD.1） |
| 远程 | `private` → `git@github.com:Cytoid/Cytoid-private.git` |
| 范围 | `Assets/Scripts/**` 全模块（~380+ `.cs`） |
| 方法 | `git show` / `git grep` 静态审计；对照 core；**未**跑 Profiler / 真机 |
| 关联 | [2.1.5 Bug/性能方案评审](./2026-07-23-private-2.1.5-bug-perf-design-review.md) |

---

## 0. 总览

### 0.1 模块健康度（1=危 · 5=健）

| 模块 | 约 LOC / 文件 | 分 | 一句话 |
|------|---------------|----|--------|
| Storyboard | ~2.5k / 53 | **2** | 解析/销毁/生命周期多处 P0 |
| AssetMemory（Utils） | ~0.5k / 1+ | **2** | `isLoading` 死锁可永久挂起资源加载 |
| Context / 全局管理 | ~1.2k+ | **2** | 上帝对象；低内存回调被禁用 |
| Navigation | ~大量 UI / 131 cs | **2** | 深链协议错误 + 核清缓存 |
| Online + Player | ~中 | **2** | JWT 明文 + 日志泄漏 |
| Secure | ~中 | **1** | 本地加密为安防剧场；ClientSecret 硬编码 |
| Game Core | ~2.2k | **3** | 可玩但游标/索引约定脆弱 |
| Notes + Renderers | ~2.1k / 20 | **3** | 玩法完整；监听按 note 放大 |
| Audio | ~0.4k + loaders | **3** | 默认路径可用；Native/NLayer 有坑 |
| Bundle / Character | ~中 | **3** | CDN 合理；空角色 NRE 风险 |
| Chart | ~0.8k / 2 | **4** | 预处理成熟；查询 API 有副作用 |
| Input | ~0.2k / 1 | **4** | 语义正确；线性扫描 |
| ObjectPool + FX | ~0.6k / 2 | **4** | 池化合理；Dispose 不全 |
| HUD Elements | ~1.1k / 23 | **4** | 薄层；Scanner/Tween 有毛刺 |
| Tier | ~0.5k / 17 | **3** | 功能可用；Mock 回退危险 |
| Screen 框架 | ~薄 | **3** | 可用；语言监听泄漏 |
| Level 领域模型 | ~薄 / 4 | **4** | 稳定；安装逻辑在 LevelManager |
| Editor | 3 文件 | **4** | 仅编辑器 |

**子系统加权印象：~2.8/5** — 商店可玩的成熟客户端，技术债集中在 Storyboard / 资源生命周期 / 壳层安全卫生。

### 0.2 跨模块关键发现（新增于方案稿）

| ID | 模块 | 严重度 | 摘要 |
|----|------|--------|------|
| **G-SPAWN** | Game | P1（条件） | 生成循环用 `note_map[CurrentNoteId]`，假定 id 连续 0..n-1；缺口 id 会 `KeyNotFoundException` |
| **B16** | AssetMemory | P0 | `isLoading` 无 finally；FitCrop `throw` 后永久挂起 |
| **B12+** | Storyboard | P0 | `DestroyObjectsById` 用 `renderer.GetType()` 索引 typed 表（应为 model 类型） |
| **B-CTX-LM** | Context | P2 | `OnLowMemory` 在有效清理前 `return`（死代码） |
| **B-FONT** | FontManager | P2 | `WaitUntil(() => Loaded = true)` 赋值而非判断 |
| **B-SET-JSON** | Player | P2 | `cdn_region` JsonProperty 重复绑定 CdnRegion 与 DebugApiUrl |
| **B-JWT-LOG** | OnlinePlayer | P2 | `Debug.Log` 打印完整 JWT |
| **B-SEC** | Secure | P2–合规 | DES + 固定盐 + 失败回落明文；ClientSecret 进包 |

### 0.3 审计边界

| 纳入 | 不纳入 |
|------|--------|
| `Assets/Scripts` 逻辑与生命周期 | Vendor/Native Audio 二进制、Shaders 美术 |
| 确认缺陷 + 高置信气味 | 未验证的「感觉慢」 |
| 2.1.5 相关行动建议 | Flutter core 迁移方案本身 |

---

## 1. Game 玩法子系统

### 1.1 Game Core（`Game.cs` / `GameState` / `GameConfig` / `PlayerGame`）

**职责：** 关卡会话编排：加载 → 游玩循环 → 判定计分 → 结算/失败/销毁。

**入口：** `Initialize` / `StartGame` / `Update` / `Complete` / `Fail` / `Dispose`

**优点：**
- 事件总线清晰（HUD / FX / SB 解耦）
- DSP 音乐同步与周期重锁
- 分数/连击用 `SecuredDouble`/`SecuredInt`
- Tier / 校准模式分支明确

**缺陷：**

| Sev | 问题 | 证据 |
|-----|------|------|
| **P1** | 生成循环按 `note_map[CurrentNoteId]` 取 note，并把 `CurrentNoteId` 当连续下标递增 | `Game.cs` ~505–523；`note_map` 为 `Dictionary<int,Note>`（`ChartModel.cs`）。标准谱 id 多为 0..n-1 故「碰巧能跑」；**缺口/非零基 id 会崩或漏生成**。置信度：机制 High，线上频率 Med–Low。 |
| **P2** | 大量 `async void` | `Game.cs` 多处；未捕获异常易静默失败 |
| **P2** | `Dispose` 不完整 | 清部分监听 + ObjectPool；SB/音乐完整 teardown 依赖旁路 |
| **P2** | Tier 切后台 → `Pause` → `Fail` | 刻意防挂机，移动体验苛刻 |
| **P3** | `GameState` 死代码 `return` 后 `LogWarning` | ~188 |

**PlayerGame vs 生产（风险上下文）：**

| | 生产 Game | PlayerGame |
|--|-----------|------------|
| 时间 | DSP `SynchronizeMusic` | `PlaybackTime`；曲终循环 |
| Dispose/Complete/Abort | 完整 | **空实现** |
| Seek | 无 | 滑条只清 `CurrentNoteId`，page/event 游标易陈旧 |
| 关卡路径 | `SelectedLevel` | 硬编码本机路径 |

→ **审计以生产路径为准**；PlayerGame 不算生产覆盖。

**热路径：** `Update`（生成/翻页）→ `SynchronizeMusic` → `Judge`

**依赖：** Chart、ObjectPool、Input、Context、Storyboard、Audio

**评分：3/5**  
**2.1.5：** 评估 G-SPAWN（改为按 `note_list` 时间序游标，或断言 id 连续并在加载期校验）。  
**以后：** 拆 Load/Play/Exit；统一 `UniTask` 错误面。

---

### 1.2 Chart（`Chart/`）

**职责：** JSON/旧格式 → 屏幕坐标与时间预处理；运行时游标挂在 Chart 上。

**优点：** 单次预处理；旧谱兼容；池容量统计。

**缺陷：**

| Sev | 问题 | 证据 |
|-----|------|------|
| **P1/条件** | `GetScannerPositionY` **写入** `CurrentPageId` | `Chart.cs` ~371+；唯一调用者 `Scanner`（已 grep）。与 `Game.Update` 单调 `++` 双写。**单独改成纯函数会破坏校准回拨**（见方案评审 B10）。 |
| **P3** | 魔法常量未文档化 | margin / AR |

**评分：4/5**  
**2.1.5：** B10 整项 Defer 或 A+B 同 PR；禁止半套。  
**以后：** 游标所有权收到 `Game`。

---

### 1.3 Notes + Renderers（`Notes/`）

**职责：** 判定实体 + Classic 表现；`DragLineElement` 连接线。

**优点：** Note/Renderer 分离；Hold/Flick/Drag 规则完整；与池集成。

**缺陷：**

| Sev | 问题 | 证据 |
|-----|------|------|
| **P2** | 每 note 挂 `onGameUpdate`/`LateUpdate` | `Note.cs` ~65；密度高时监听爆炸 |
| **P2** | 非 Classic 样式 `throw NotSupportedException` | Hold/Flick/DragHead |
| **P3** | `DragHeadNote` 残留 `id == 523` 调试打印 | ~92 |
| **P3** | 触控门控 TODO「算法不准」 | DragHead |

**热路径：** 每活动 note 的 Update → 位移/Miss/Render

**评分：3/5**  
**2.1.5：** 删 523 调试。  
**以后：** 中央 tick 替代 per-note 监听。

---

### 1.4 Input（`InputController.cs`）

**职责：** LeanTouch → 碰撞查询；维护 Hold/Flick 指态。

**优点：** Drag 优先于普通 note；多指 Hold；暂停清指态。

**缺陷：** 每帧 O(n) 重建可触列表（P2）；Hold 已清仍追踪时抛 `InvalidOperationException`（P2，「不可能」路径无恢复）。

**评分：4/5** · 2.1.5 可选加固移除抛出。

---

### 1.5 ObjectPool + EffectController

**职责：** Note / DragLine / 粒子池；判定特效。

**优点：** 容量来自谱面密度；Hold FX 倍率预留。

**缺陷：**

| Sev | 问题 |
|-----|------|
| **P1** | `Dispose` 未清 `SpawnedDragLines`、`effectPoolItems`（= 方案 B08） |
| **P2** | FX `async void AwaitAndCollect` 无取消 → 中途出关可能泄漏 |

**评分：4/5** · **2.1.5 Must：B08**。

---

### 1.6 HUD Elements（`Elements/`）

**职责：** Scanner、分数字幕、进度、Splash、校准控件、PlayerGame SB 按钮。

**缺陷：** Scanner 每帧 `GetComponent`×N + OnEnable 双写 startColor（P02/B15）；`GameProgressIndicator` 每帧新 DOTween（P2）；Accuracy 未判定时显示 100%（P3）。

**评分：4/5** · **2.1.5 Should：P02–P04**。

---

### 1.7 Tier（`Tier/`）

**职责：** 多阶段赛季；Criterion 组合判定；跨阶段 HP。

**缺陷：** 无 TierState 时回退 `MockData.Season.tiers[0]`（P2，生产危险）；`Mods` 假定 First stage 非空。

**评分：3/5** · 2.1.5：去掉 Mock 回退（若仍走该路径）。

---

## 2. Storyboard（`Storyboard/`）

**职责：** SB JSON 解析 → 异步生成 Renderer → LateUpdate 插值 → Trigger 生成/销毁。

**架构：**

```
Parse → Initialize(Spawn) → OnGameUpdate(FindStates/Ease/Destroy)
                              ↑ onNoteClear → Triggers
Dispose（不完整）← onGameDisposed λ
```

**缺陷（与方案稿对齐 + 模块加深）：**

| ID | Sev | 位置 | 说明 |
|----|-----|------|------|
| B01 | P0 | `Templates[id]` | 缺 template 崩解析 |
| B02 | P0 | `note_map[id]` in ParseTime | 坏 note 时间崩 |
| B03 | P0 | foreach + Remove；非 JArray `.Values` | Score 触发崩；坏 JSON 崩 |
| B07 | P1 | Dispose/监听/PlayerGame 热重载 | lambda 无法 Remove；不调 Renderer.Dispose |
| B12 | P0/P1 | destroy 图 + `DestroyObjectsById` | typed 表用 **Renderer.GetType()**，键却是 **model typeof(Video)** → 必炸或静默漏删 |
| B06 | P1 | VideoRenderer | prepare/owns/RT/seek |
| B13 | P2 | LineStateParser | 未 ParseStageObjectState |
| P01 | P1 GC | OnGameUpdate | 每帧 new Type[] + Dictionary |
| — | P2 | `async void SpawnObjectById` | 异常吞掉 |
| — | P2 | 共享 Line Dispose 无条件 Destroy | 同 Video 双毁类问题 |
| — | P2 | 路径 `Level.Path+rel` 无规范化 | 侧载谱 `../` 风险（分布渠道低、侧载真） |

**PlayerGame：** 热重载双 Dispose；永久销毁走 `Clear()` 留 GO；FileSystemWatcher 可叠请求。

**评分：2/5**  
**2.1.5 Must：** B01–B03、B07、B12（+Line 所有权）；Should：P01；Defer：B06/B13。

---

## 3. Audio（`AudioManager` + Clip/NLayer）

**职责：** 7 路 AudioSource + 可选 NativeAudio；加载/卸载/调度。

**路径：** 移动端多为 UWR；桌面 `file://` MP3 走 NLayer 流式。

**缺陷：**

| ID | Sev | 说明 |
|----|-----|------|
| B09 | P1 | seek 每次 `new MpegFile` 堆积（桌面/编辑器） |
| B14 | P2 | Native `trackCount=2` vs RR 索引 3–6；**默认 UseNativeAudio=false** |
| — | P2 | `controllers[id]` 裸取；`Exceed7 Unload` 为 `async void` |
| — | P3 | Native `PlayScheduled` NotImplemented；`IsFinished` 打 print |

**评分：3/5** · **2.1.5 Should：B09**；B14 可选。

---

## 4. Utils · AssetMemory 与启动工具

### 4.1 AssetMemory

**职责：** 分 Tag 缓存 Sprite/Audio；远程落盘；`isLoading` 去重。

**缺陷：**

| ID | Sev | 证据 |
|----|-----|------|
| **B16** | **P0 Hang** | `isLoading.Add` 后 FitCrop `throw ArgumentException` 等路径无 finally → 同 path 永久 `WaitUntil` |
| — | P2 | `async void` 写盘 + `WaitUntil(completed)`，异常则 completed 永不 true |
| — | P2 | 递归重载丢 `useFileCacheOnly` |

**评分：2/5** · **2.1.5 Must：B16**（先于深链 flush 改动）。

### 4.2 Bootstrapper / GlobalCalibrator

- Bootstrapper `async void Awake`（P3）
- GlobalCalibrator `FindLast` 无 note 时 NRE（P2）

**评分：3/5（附属）**

---

## 5. BundleManager + Character

**职责：** catalog 合并、Caching 下角色 AB、引用计数；主菜单实例化角色。

**缺陷：** 非移动平台 `#else throw`（P2）；`GetCachedVersions` 空列表 `.Last()`（P2）；`GetActiveCharacterAsset` 未加载时 NRE（P2）；catalog 非原子写（P3）。

**评分：3/5** · 2.1.5 无 Must（主路径预载成功即可）。

---

## 6. Context 与全局管理器

**职责：** LiteDB、设置、CDN、场景生命周期、静态服务注册、低内存、字体。

**缺陷：**

| ID | Sev | 说明 |
|----|-----|------|
| B-CTX-LM | P2 | `OnLowMemory` 在清理逻辑前 `return`（注释称 iOS 启动误触）→ **低内存时不释放** |
| B-FONT | P2 | `WaitUntil(() => Loaded = true)` **赋值**，掩盖加载失败 |
| B-CTX-DB | P2 | DB 在 `persistentDataPath`，用户谱可在外部 `UserDataPath`（Android legacy） |
| — | P2 | `SaveSettings` = 删表重插；暂停时 Dispose DB 有竞态面 |
| — | P3 | 上帝对象 ~千行；BuiltInData 内嵌巨大 JSON |

**Sentry DSN：** 在 `Resources/Sentry/SentryOptions.asset`，**不在 C# 硬编码**（合规点加分）。

**评分：2/5** · 2.1.5：可修 FontManager 与考虑恢复低内存策略；DB 路径统一可延后。

---

## 7. Navigation（`Navigation/`）

**职责：** 菜单、深链、选关社区 UI、设置工厂。

**缺陷：**

| ID | Sev | 说明 |
|----|-----|------|
| B04 协议 | P1 | `OnDeepLinkActivated(url)` 却用 `Application.absoluteURL` 判 scheme |
| B04 缓存 | P1 | 成功后几乎清空全部 `AssetTag`（Avatar 限流 tag 也会被清） |
| — | P2 | Settings 每次 Active 叠加 `OnLanguageChanged` |
| — | P2 | 选关屏 Initialize 加语言监听永不 Remove |
| — | P3 | 深链 `token` 未用于去重；History pop 与 ActiveScreen 不同步 |

**评分：2/5** · **2.1.5 Should：B04（协议 + 收窄 flush）+ 设置监听泄漏**。

---

## 8. Screen 框架（`Screen/`）

**职责：** 栈导航、过渡、生命周期。

**缺陷：** 基类 `Awake` 注册语言监听，`OnDestroy` 不卸（P2）；`ChangeScreen` 为 `async void`，重叠调用静默丢弃（P2）。

**评分：3/5** · 与 Navigation 监听问题一并修。

---

## 9. Online + Player

**职责：** REST DTO；本地设置/成绩；JWT 会话。

**缺陷：**

| ID | Sev | 说明 |
|----|-----|------|
| B-JWT-LOG | P2 | `Debug.Log($"JWT token: …")` |
| B-SET-JSON | P2 | 双 `JsonProperty("cdn_region")` |
| — | P2 | JWT 自 SecuredPlayerPrefs 迁到 LiteDB **明文** |
| — | P2 | 大量 `EnableDebug = true` RestClient 可能打出 Authorization |
| — | P3 | OnlineLevel 重复字段 / TODO NEO |

**评分：2/5** · **2.1.5：删 JWT 日志 + 修 JsonProperty**；密钥库加密延后到壳迁移。

---

## 10. Level 领域（`Level/`）

薄模型：`Level` / `LevelMeta` / `LevelRecord` / `Difficulty`。安装与路径校验在 **LevelManager（根脚本）**。

**评分：4/5** · 2.1.5 低优先级。注意 `Level.Path` 被 SB/音频直接拼接。

---

## 11. Secure（`Secure/`）

**职责：** 本地 Prefs「加密」、验证码、校验和、上传签名、数值混淆。

**结论：安防剧场 + 真实密钥暴露**

| 点 | 评估 |
|----|------|
| SecuredPlayerPrefs | 密码 `cytoid4ever`、固定盐、**DES**；失败 **返回明文** |
| StringCipher | 生产树内含 Sample Main；基本未引用 |
| ClientSecret | **512 hex 进 IL2CPP**；反编译可得 |
| PrintDebugMessages | `const true`，发行可能打加密载荷日志 |

**评分：1/5** · **2.1.5 不挡玩法发版**；合规/反作弊长期项（随 Flutter 壳替换）。

---

## 12. Editor（`Editor/`）

AssetBundle 构建、Context 调试、QuickActions。含过期 ngrok URL（仅编辑器）。

**评分：4/5** · 不影响发行包。

---

## 13. 模块依赖图（简化）

```
Context ──┬── ScreenManager ←── Navigation Screens
          ├── LevelManager ←── Level / OnlineLevel
          ├── Player / OnlinePlayer ←── LiteDB / Secure*
          ├── AudioManager ←── NLayer / NativeAudio
          ├── AssetMemory ←── Navigation thumbnails / GameCover / SB sprites
          ├── BundleManager ←── CharacterManager
          └── Game ──┬── Chart / Notes / Input / ObjectPool / HUD
                     ├── Storyboard ── AssetMemory / Chart.note_map
                     └── TierState
```

---

## 14. 按模块的 2.1.5 行动表

| 优先级 | 模块 | 行动 |
|--------|------|------|
| Must | Storyboard | B01 B02 B03 B07 B12 |
| Must | AssetMemory | B16 |
| Must | ObjectPool | B08 |
| Must | Audio（桌面） | B09 |
| Must | Game/Notes 周边 | B05；评估 G-SPAWN |
| Must | GameRenderer | B11 Cover 释放 |
| Should | Navigation | B04 协议 + 收窄 flush；Settings 监听 |
| Should | Storyboard | P01；可选 B06 |
| Should | HUD | P02–P04 |
| Should | Online/Player | 去 JWT 日志；修 cdn_region 双绑定 |
| Should | Context | FontManager WaitUntil；评估 OnLowMemory |
| Cond. | Chart | B10 整包或 Defer |
| Defer | Secure 重做、AudioServer、Note 中央 tick、Tier Mock 清理与 Flutter 编排对齐 |

---

## 15. 建议审计后续（提高置信度）

1. **G-SPAWN：** 对社区谱抽样统计 `note.id` 是否总为 `0..n-1` 且按 `intro_time` 序；写加载期断言。  
2. **B12：** 单测/手工「trigger destroy 父 id」必现 KeyNotFound。  
3. **B16：** 单测非法 `SpriteAssetOptions` 后二次 Load。  
4. **Profiler：** 重 SB 关 vs 普通关，验证 P01–P04。  
5. **Android 真机：** 深链热启 + SB 视频（若 B06 进版）。

---

## 16. 与方案评审稿的关系

- 方案稿聚焦 **「改什么、怎么改、如何验收」**。  
- **本审计**聚焦 **「各模块现状、证据、评分、边界」**。  
- 实施时以方案稿 §0.2 Must 为准；本审计中的 **G-SPAWN / B-FONT / B-SET-JSON / B-JWT-LOG / B-CTX-LM** 为增量发现，建议并入评审决议。

---

**文档结束。**
