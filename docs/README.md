# Cytoid Core Documentation

Stable references tracked in git. Work-in-progress notes live in `docs/local/` (gitignored).

## Planning / Design

| Document | Purpose |
|----------|---------|
| [2026-07-27-judgment-optimization-design.md](2026-07-27-judgment-optimization-design.md) | **判定优化设计**：select note（Click/CDrag head/Flick）上 effectiveNoteTime 拍序 + 15ms 簇 + 簇内 x；Drag/Hold 不进候选（分支 `fix/judgment-optimization`） |

## Bridge / Flutter plugin

| Document | Purpose |
|----------|---------|
| [host-protocol-v2.md](host-protocol-v2.md) | Flutter ↔ Unity host protocol (current) |
| [mock-engine.md](mock-engine.md) | Mock runtime when Unity artifacts are absent |

Deprecated v1 protocol: `engines/unity/flutter_plugin/example/docs/host-protocol.md`

## Unity core / research notes

| Document | Purpose |
|----------|---------|
| [vendor.md](vendor.md) | Optional licensed vendor packages |
| [2026-06-20-play-events-anti-cheat-followup.md](2026-06-20-play-events-anti-cheat-followup.md) | Play-events / anti-cheat follow-up |
| [2026-07-02-storyboard-ir-design-investigation.md](2026-07-02-storyboard-ir-design-investigation.md) | Storyboard IR investigation |
| [2026-07-02-storyboard-external-compiler-plan.md](2026-07-02-storyboard-external-compiler-plan.md) | External storyboard compiler plan |
| [2026-07-02-storyboard-lua-value-case-study.md](2026-07-02-storyboard-lua-value-case-study.md) | Storyboard Lua value case study |

Agent-oriented overview: repository root [AGENTS.md](../AGENTS.md).
