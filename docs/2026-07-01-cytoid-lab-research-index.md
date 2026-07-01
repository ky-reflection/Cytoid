# Cytoid Lab / Core 调研文档总览

> **Date:** 2026-07-01  
> **Branch:** `cytoid-lab-research-docs-2026-07-01`  
> **Scope:** Cytoid Lab timeline resync、Storyboard、c2v3 Page Function / UI Animation、core 后续方向

本文档是 2026-07-01 这组调研材料的入口。分支内只收录可进入 review 的结论文档；早期 Storyboard 草稿保留为本地未跟踪材料，不作为提交内容。

## 阅读顺序

1. [Cytoid Lab / Core 开发方向评审](2026-07-01-cytoid-lab-core-direction.md)  
   从产品/工程边界出发，划分 Lab 验证工具、Unity core 嵌入式 runtime、Bridge 产品化之间的职责。

2. [Timeline Resync 系统评审](2026-07-01-cytoid-lab-resync-system-review.md)  
   系统分析 `PreviewTimeline` / `ResyncPlayfieldToTime` / chart cursor / note state / storyboard seek 的同步链路。

3. [Hold seek 问题调研](2026-07-01-cytoid-lab-hold-seek-investigation.md)  
   作为 resync 系统问题的子集，聚焦 timeline seek 后 hold 提前出现、进度和颜色异常。

4. [Storyboard 内存/性能/Bug 审计报告](storyboard-memory-performance-audit.md)  
   合并 Storyboard bug、内存泄漏、性能问题，并补充 2026-07-01 对 c2v3 兼容性的走查。

5. [c2v3 调研方案（Page Function + UI Animation）](2026-07-01-c2v3-research-plan.md)  
   讨论 Page Function、UI Animation、Storyboard 坐标语义兼容、扫描线覆盖优先级和测试矩阵。

6. [Cytoid Lab macOS 移植评估](cytoid-lab-macos-adaptation.md)  
   评估 Lab 从 Windows standalone 扩展到 macOS standalone 的工程量与阻塞项。

## 分支收录范围

建议在开发仓中把以下文件作为一个 docs-only review：

| 文件 | 类型 | 说明 |
|------|------|------|
| `docs/cytoid-lab.md` | 现有文档更新 | 增加调研文档入口 |
| `docs/2026-07-01-cytoid-lab-research-index.md` | 新增 | 本总览入口 |
| `docs/2026-07-01-cytoid-lab-core-direction.md` | 新增 | Lab / core 开发方向 |
| `docs/2026-07-01-cytoid-lab-resync-system-review.md` | 新增 | timeline resync 系统评审 |
| `docs/2026-07-01-cytoid-lab-hold-seek-investigation.md` | 新增 | hold seek 子问题 |
| `docs/storyboard-memory-performance-audit.md` | 新增 | Storyboard 最终合并审计 |
| `docs/2026-07-01-c2v3-research-plan.md` | 新增 | c2v3 / Page Function 调研 |
| `docs/cytoid-lab-macos-adaptation.md` | 新增 | macOS 移植评估 |

以下本地文件不建议纳入本分支：

| 文件 | 原因 |
|------|------|
| `docs/audit-storyboard-memory-performance.md` | Storyboard 审计草稿，已合并进最终版 |
| `docs/2026-06-30-storyboard-leak-perf-audit.md` | Storyboard 审计草稿，已合并进最终版 |
| `docs.zip` | 本地打包产物，不进入仓库 |

## 同步到 Windows 开发仓

推荐把本分支作为临时 docs 分支使用，不直接推到正式远端：

```bash
git format-patch feature/cytoid-player --stdout > cytoid-lab-research-docs-2026-07-01.patch
```

在 Windows 开发仓中：

```bash
git switch -c cytoid-lab-research-docs-2026-07-01
git apply cytoid-lab-research-docs-2026-07-01.patch
```

确认文档内容后，在 Windows 开发仓中提交并发起 review。
