# Cytoid-private 2.1.5 · Bug & Perf 方案评审包

| 字段 | 内容 |
|------|------|
| 包日期 | 2026-07-23 |
| 状态 | 候选定稿 · 第三方审核 Agree-with-changes 已吸收 · **模块审计已并入** · **待 owner 批准** |
| 基线 | `private/main` @ `0e11d2c3f12ed61968aac3058272748045b27f3d` |
| 目标 | 商店 **2.1.5**（建议 VersionCode 124） |
| Unity | 6000.0.58f2 |

---

## 从这里开始（入口）

1. **先读本文件**（你正在看的 `README.md`）。
2. **主方案（实施规范）：** [`docs/01-design-review.md`](./docs/01-design-review.md)  
   同文件原名：`2026-07-23-private-2.1.5-bug-perf-design-review.md`
3. **仓/分支/证据导航：** [`docs/02-audit-index.md`](./docs/02-audit-index.md)
4. **第三方审核原文（审计留痕，非第二套规范）：** [`docs/03-third-party-response.md`](./docs/03-third-party-response.md)
5. **模块级代码审计（全模块健康度 + 相对方案稿的增量发现）：** [`docs/04-module-code-audit.md`](./docs/04-module-code-audit.md)

阅读顺序：**README → 01 主文档 §0（含 §0.6）→ 需要核对证据时查 02 → 需要看独立意见时查 03 → 需要模块全景时查 04**。

---

## 包内文件

```
README.md                          ← 入口（本文件）
MANIFEST.txt                       ← 清单与校验信息
docs/
  01-design-review.md              ← 主文档（编号别名，便于外发）
  02-audit-index.md                ← 审核索引
  03-third-party-response.md       ← 第三方回复
  04-module-code-audit.md          ← 模块级代码审计
  2026-07-23-private-2.1.5-*.md    ← 与仓库 docs/reviews/ 同名副本
  2026-07-23-private-module-code-audit.md
```

---

## 代码仓（核对证据时需要）

| 角色 | Remote | 提交 / 分支 |
|------|--------|-------------|
| **事实基线** | `git@github.com:Cytoid/Cytoid-private.git` | `main` @ **`0e11d2c3`** |
| core 对照 | `git@github.com:Cytoid/Cytoid.git` | 补丁：`297f7536` `cb740083` `900af401` `a6223bd7` `831b64ac` `218ddeda` |

**路径前缀差异（极易搞错）：**

- private 基线：`Assets/Scripts/...`
- core：`engines/unity/Assets/Scripts/...`

只读核对示例：

```bash
git fetch <private-remote> main
git rev-parse <private-remote>/main
# 期望：0e11d2c3f12ed61968aac3058272748045b27f3d
git show 0e11d2c3:Assets/Scripts/Storyboard/StoryboardRenderer.cs | less
```

---

## 范围快照

| 档 | ID |
|----|-----|
| **Must** | B01 B02 B03 B05 B07 B08 B11 B12 B16 |
| **Should** | B04；B09（完整同步版）；P01–P04 |
| **Defer / 2.1.6** | B10；B06；B13；B14；P08 |
| **Out** | AudioServer / VFS / Lab seek / Bridge v2 |

**模块审计增量（不改变上表 Must；见主文档 §0.6 / 包内 `04`）：** G-SPAWN（评估）、B-JWT-LOG、B-SET-JSON、B-FONT（低成本 Should 候选）；B-CTX-LM / B-SEC 默认不挡发版。

发版硬门禁见主文档 §8：**#0（Android 真机 smoke）+ #1–#9 + #15**。

开放问题默认（第三方已投票，待 owner 勾选）：B03 保留现状 · B10 Defer · B06→2.1.6 · B04 删 flush · B09 不挡 Must。

---

## 给接收方的说明

- 本包**只含文档**，不含 Unity 工程、二进制或密钥。
- 主文档已吸收第三方全部纠错与门禁建议；`03` 保留原文作审计记录，**不要当作第二份实施规范**。
- `04` 为全模块静态审计；与 `01` 冲突时以 `01` §0.2 Must 为准；增量项由 owner 按 §0.6 勾选。
- 方法局限：静态审计 + 调用链；尚未 Profiler / 全量真机量化。
- 若需在本机 monorepo 继续工作，对应路径为仓库内 `docs/reviews/`（与本包内容同步）。

---

## 期望下一步（owner）

1. 按主文档 §12 检查清单勾选批准（含 §0.6 增量是否并入）。  
2. 指定 Must PR 负责人、Android #0 smoke 验证人、目标日期。  
3. 批准后按 §7 Phase 实施（建议先 B16，再 Storyboard，再 Game pool/cover）。
