# Cytoid Core Documentation

Stable references tracked in git. Work-in-progress notes live in `docs/local/` (gitignored).

## Build

| Document | Purpose |
|----------|---------|
| [build.md](build.md) | Android Unity artifacts, example/production APK boundary, and Cytoid Lab builds |

## Lab

| Document | Purpose |
|----------|---------|
| [lab/guide.md](lab/guide.md) | Runtime, build, integration boundary, code layout |
| [lab/releases.md](lab/releases.md) | All release notes (English + 中文, v0.1.0–current) |

Local notes: [local/README.md](local/README.md) (gitignored) — backlog, planning, research.

## Reviews / private 2.1.5

| Document | Purpose |
|----------|---------|
| [reviews/README.md](reviews/README.md) | Cytoid-private 2.1.5 bug & perf review pack entry |
| [reviews/01-design-review.md](reviews/01-design-review.md) | Main implementation spec (Must / Should / gates) |
| [reviews/02-audit-index.md](reviews/02-audit-index.md) | Repo / branch / evidence navigation |
| [reviews/03-third-party-response.md](reviews/03-third-party-response.md) | Third-party audit record (not a second spec) |
| [reviews/04-module-code-audit.md](reviews/04-module-code-audit.md) | Module-level static audit |

Baseline for that pack: `private/main` @ `0e11d2c3` (`git@github.com:Cytoid/Cytoid-private.git`). Core patch sources remain on this monorepo / `upstream`.

## Bridge / Flutter plugin

| Document | Purpose |
|----------|---------|
| [host-protocol-v2.md](host-protocol-v2.md) | Flutter ↔ Unity host protocol (current) |
| [mock-engine.md](mock-engine.md) | Mock runtime when Unity artifacts are absent |

Deprecated v1 protocol: `engines/unity/flutter_plugin/example/docs/host-protocol.md`

## Unity core

| Document | Purpose |
|----------|---------|
| [storyboard-runtime-contract.md](storyboard-runtime-contract.md) | Supply-side SB contract for cytoid-sb (emit profile, macros, gaps) |
| [vendor.md](vendor.md) | Optional licensed vendor packages |

Agent-oriented overview: repository root [AGENTS.md](../AGENTS.md).
