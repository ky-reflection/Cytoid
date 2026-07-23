# Storyboard Runtime Contract — Supply Side for cytoid-sb

> **Audience:** cytoid-sb (authoring compiler / typed IR) maintainers  
> **Source of truth on Unity side:** `engines/unity/Assets/Scripts/Storyboard/`  
> **Related plan:** cytoid-sb “typed IR → emit Unity authoring JSON today; future in-game engine consumes IR”  
> **Status:** Living contract (2026-07). Distinguishes **today’s Lab/runtime emit target** from **future engine-facing IR**.

This document is what the Unity gameplay core (Lab + Bridge-embedded) can usefully give the SB toolchain. It is **not** an authoring tutorial and **not** a promise that IR must mirror JSON forever.

---

## 1. Roles (read this first)

| Side | Owns | Does not own |
|------|------|--------------|
| **cytoid-sb** | Frontends (JSON5 / Lua / …), typed IR, provenance, validation, emit | Unity frame loop, easers, vendor shaders |
| **Unity core (this repo)** | Parse/load, state machines, easers, triggers, Lab seek, Bridge payload paths | Author UX, language design, compression contests |

**Shared agreement from the SB future-direction plan:**

1. **Semantic truth lives in IR**, not in the shape of today’s JSON.  
2. **Current emit target** for validation is still **Unity-loadable authoring JSON** (Lab / existing `Storyboard.Parse()`).  
3. **Future in-game SB engine** may consume IR (or a compiled IR profile) and **may extend/refactor semantics** — Unity will document emit vs engine contracts separately when that work starts.  
4. **No new interpreter host** on either side in the current roadmap.

What helps cytoid-sb most is a **stable, explicit consumer contract** plus an honest **capability / dead-field / seek matrix** — not a frozen field-for-field dump of `StoryboardModel.cs`.

---

## 2. What we should publish (priority order)

### P0 — Required for typed IR + safe emit

| Artifact | Why SB needs it | Where / how we supply it |
|----------|-----------------|--------------------------|
| **Authoring emit profile** | Emit must load in Lab without guessing | §3 below + parsers under `Storyboard/**/*StateParser.cs` |
| **Macro expansion semantics** | IR should lower these; runtime still expands them | §4 (templates, note selectors, time arrays, `$note`) |
| **Time / easing / color / UnitFloat rules** | Validation + source-mapped errors | §5 |
| **Throw vs silent failure table** | Diagnostics severity; avoid “compiled OK, Lab empty” | §6 |
| **Golden load loop** | Regression against real runtime | §9 |
| **Chart coupling** | Note selectors / note-relative times need chart ids | `ChartModel.Note`, `note_map`, `NoteType` |

### P1 — Required before Declarative Lua / author studies

| Artifact | Why |
|----------|-----|
| **Effect capability matrix** (vendor vs fallback) | Authors think “glitch works”; open-source builds only hint |
| **NoteController override surface** | Position/color/size work; rotation/seek/dense gaps matter |
| **Trigger + Lab seek semantics** | Trigger-only objects do not survive scrub the way authors expect |
| **Dead / partial fields list** | Stop emitting `pivot_x`, `font`, `vignette`, … as if supported |

### P2 — For future in-game engine (design only for now)

| Artifact | Why |
|----------|-----|
| **IR consumption sketch** | Versioned IR nodes Unity would prefer to ingest later |
| **Compiled profile rules** | Today’s `compiled: true` is PascalCase, **drops triggers/templates** — not a Lab-validation format |
| **Dense-note / No-GameObject notes** | Future charts may clear notes without `Note` instances; trigger API must become note-id-based |

Deliver these as **this doc + linked source paths**, updated when parsers/easers change. Prefer “contract sections” over dumping entire C# models into the SB repo.

---

## 3. Today’s emit target: authoring JSON

### Loader entry

1. Host / Lab provides storyboard text (VFS / level path / localization swap).  
2. `new Storyboard(game, content)` → `JObject.Parse`.  
3. `Storyboard.Parse()` — authoring path unless `compiled == true`.  
4. `Storyboard.Initialize()` — renderer + `Game.onNoteClear` for triggers.  
5. Parse failure is **caught in `Game.Initialize`**: game runs **without** storyboard.

### Top-level authoring keys (snake_case)

| Key | Runtime collection |
|-----|-------------------|
| `templates` | `Dictionary<string, JObject>` |
| `texts` | `Dictionary<string, Text>` |
| `sprites` | `Dictionary<string, Sprite>` |
| `videos` | `Dictionary<string, Video>` |
| `lines` | `Dictionary<string, Line>` |
| `controllers` | `Dictionary<string, Controller>` |
| `note_controllers` | `Dictionary<string, NoteController>` |
| `triggers` | `List<Trigger>` |

**Emit for Lab:** snake_case keys matching `*StateParser.cs`. Omit `compiled` or set `false`.

### Naming layers (easy to get wrong)

| Layer | Convention |
|-------|------------|
| Authoring JSON keys | **snake_case** |
| C# model public fields | **PascalCase** (no `[JsonProperty]`) |
| `Storyboard.Compile()` output | Top-level arrays snake_case; **inner objects PascalCase** via `JObject.FromObject` |
| Enums in JSON | String, case-insensitive `Enum.Parse` |

### Object skeleton (pre-expansion)

```json
{
  "id": "sprite_1",
  "parent_id": "optional",
  "target_id": "optional_alias",
  "template": "optional_template",
  "time": 0,
  "note": 42,
  "states": [ { } ]
}
```

Rules the compiler must honor (runtime throws or drops otherwise):

- `id` and `target_id` are **mutually exclusive**.  
- `target_id` and `parent_id` are **mutually exclusive**.  
- Missing `id` → random name (bad for provenance; IR should always emit stable ids).  
- Duplicate ids after expansion → logged error, **second dropped**.

---

## 4. Macros the IR should lower (or deliberately keep)

Runtime still expands these in `Storyboard.Parse()` / `PopulateJObjects`. For a typed compiler, **prefer lowering to absolute authoring JSON** so Lab sees the same graph authors intend.

| Macro | Behavior | Emit advice |
|-------|----------|-------------|
| **Templates** | Merge template states; state-level `template` + `reset` | Inline; document merge order |
| **`note` int / int[]** | Fan-out one object per id | Emit expanded objects |
| **`note` selector object** | Filter `note_list` by `start`/`end`/`direction`/`min_x`/`max_x`/`type` | Resolve against **target chart**; 0 matches → **silent omit** (match runtime) |
| **`time` / `relative_time` / `add_time` arrays** | Cartesian clone | Flatten to absolute times |
| **`$note` in id / time strings** | Substitution from expansion context | Resolve before emit |
| **Note-relative time strings** | `intro\|start\|end\|at:{noteId}:{offset}` | Resolve via `note_map` or keep string if chart-bound emit |

**Trigger-only spawn:** omitting absolute `time` yields `float.MaxValue` → not auto-spawned until trigger. IR should model “manual spawn” explicitly.

---

## 5. Shared semantic primitives

### Time

- Timeline unit: **seconds**, same as `Game.Time`.  
- States sorted by `Time` at parse; per-frame `FindStates(time)` is linear.  
- `relative_time` / `add_time` re-anchor on trigger respawn via `RecalculateTime()`.

### Easing

- Enum: `EasingFunction.Ease` in `engines/unity/Assets/Scripts/Utils/Easings.cs`.  
- Quirk: if `easing` key is **absent** on a state patch, parser sets **`Linear`** (can overwrite inherited easing after deep-copy). IR validation should treat “omit easing” as intentional Linear unless you emit the inherited value explicitly.

### Color

- Authoring: `#RRGGBB` / `#RRGGBBAA` via `ColorUtility.TryParseHtmlString`.  
- Invalid string → color **unchanged** (silent).

### UnitFloat

- Forms: bare number, or `"unit:value"` with `ReferenceUnit`: `World`, `StageX`, `StageY`, `NoteX`, `NoteY`, `CameraX`, `CameraY`.  
- Reference canvas: **800×600** (`StoryboardRenderer.ReferenceWidth/Height`).  
- Defaults differ by object type (stage vs note controller vs camera) — see `GenericStateParser` / per-type parsers.

### Triggers

| `TriggerType` | Fire condition |
|---------------|----------------|
| `NoteClear` | Cleared note id ∈ `notes` |
| `Combo` | `Game.State.Combo == combo` (exact) |
| `Score` | `Game.State.Score >= score` (trigger removed on fire) |

Actions: `spawn` / `destroy` by object id. `uses` caps fires; omitted uses → never removed by count.

**Critical:** `OnNoteClear` currently receives a **`Note` GameObject**. Dense Click simulation (heavy charts) may clear notes **without** invoking `onNoteClear` — see §8. IR/triggers that assume every clear fires SB must know this.

---

## 6. Capability matrix (emit honesty)

### Parsed but ineffective (do not document as supported)

| Field / feature | Status |
|-----------------|--------|
| `pivot_x` / `pivot_y` | On stage state model; **not parsed** |
| Text `font` | Parsed; **not applied** (only `font_weight`) |
| Controller `vignette*` | Parsed; **no easer** |
| Controller `chromatic*` | Parsed; **no easer** (≠ `chromatical`) |
| Controller `size` | Parsed; camera orthographic size **not applied** |
| NoteController `dy` | Documented broken / wrong `YOffset` mapping in parser |

### Vendor vs open-source fallback

See `docs/vendor.md`. Without `Assets/Vendor/StoryboardFilters/`, many effects (glitch, artifact, arcade, tape, chromatical, dream, fisheye, shockwave, focus, radial blur, …) degrade to **fallback / hint** passes.  

**Supply-side obligation:** expose a machine-readable **effect tier list** (full / fallback / dead) so cytoid-sb diagnostics can warn at compile time.

### Lab seek / scrub (authors will hit this)

| Behavior | Reality |
|----------|---------|
| Drag scrub | Storyboard **not** resynced |
| Release scrub | Rebuild renderers, reset filters/overrides, one update |
| Trigger history | **Not replayed** after seek |
| Trigger-only objects | Stay unspawned until re-triggered after seek |
| Video after seek | Partially fixed; some charts still fail |

IR should model **state-at-t** vs **trigger spawn graph** as different validation modes.

---

## 7. `compiled: true` (optional internal profile)

| | Authoring emit (Lab default) | Compiled emit |
|--|------------------------------|---------------|
| Purpose | Validation, triggers, templates | Fast load after expansion |
| Keys | snake_case | Inner objects **PascalCase** matching C# fields |
| Templates | Yes | **Not loaded** |
| Triggers | Yes | **Not loaded** |
| Expansion | Runtime macros | Must be pre-expanded |

**Recommendation for cytoid-sb:** default golden path = **authoring JSON**. Use compiled only if both ends agree and the chart needs no triggers/templates at load.

`Storyboard.Compile()` exists as a Unity-side expander → PascalCase blob; it is **not** the primary Lab authoring format.

---

## 8. Chart / note runtime coupling (supply-side changes SB must track)

| Topic | Contract impact |
|-------|-----------------|
| Note ids | Must exist in `Game.Chart.Model.note_map` for note-relative times and NoteControllers |
| `NoteType` | Selector `type` ints: Click=0 … CDragChild=7 |
| NoteController follow | Needs live spawn / visual handle; before spawn, placeholder at origin |
| Overrides | Written to `ChartModel.Note.Override` (and override store paths on newer branches) |
| Dense Click path | Heavy click-only charts may bypass GameObjects; **NoteClear triggers may not fire**; rotation overrides may not apply in batch visuals — document when that branch merges |

When Unity changes note simulation, we owe SB a short **compatibility note**: which triggers still fire, which overrides still apply, which seek rules change.

---

## 9. Golden validation loop (what we ask SB to run)

Against a **known chart + storyboard** pair in Cytoid Lab:

1. IR → **authoring** JSON (snake_case).  
2. Load level; confirm parse succeeds (no silent empty board).  
3. Play through for **trigger-fired** spawns.  
4. Scrub → release: check **state-at-t** objects only (triggers won’t replay).  
5. Spot-check vendor-tier effects on a build **with** and **without** vendor filters.  
6. Keep a corpus of intentional error cases (bad parent_id, missing template, bad note id) and assert Unity throw / Lab log behavior matches SB diagnostics severity.

Unity can help by keeping **minimal fixtures** under Lab levels / example plugin assets (e.g. controllers-only sample) and by not changing parser key names without a revision note here.

---

## 10. What we will *not* freeze for SB

Aligned with the SB plan’s “early rewrite OK / future engine may extend semantics”:

- Exact JSON key layout as the long-term IR schema.  
- Guaranteeing every dead field becomes implemented.  
- Treating private chart compression ratios as language evidence.  
- Providing a multi-interpreter plugin ABI.  
- Promising seek-perfect trigger replay without an explicit Unity feature.

We **will** freeze (version + changelog) the **authoring emit profile** used for Lab validation until a new profile id is published.

---

## 11. Suggested handoff package (checklist)

When starting cytoid-sb typed IR work, Unity side should hand over:

- [ ] This document (current revision)  
- [ ] Pointer list in §12 (parsers = field contract)  
- [ ] Effect tier CSV/table (full / fallback / dead)  
- [ ] 3+ authorized corpus charts + storyboards for `compare_sb` / Lab load  
- [ ] Known Lab seek limitations (copy from `docs/lab/releases.md`)  
- [ ] Note on dense-path / `onNoteClear` if/when that branch is default  
- [ ] ADR stub: “Emit profile v1 = authoring snake_case; IR may diverge; compiled PascalCase optional”

cytoid-sb side should hand back:

- [ ] Emit profile version they target  
- [ ] List of IR nodes that do **not** yet lower to Unity fields (extension_fields)  
- [ ] Diagnostics that map to §6 failures  
- [ ] Questions about semantic changes needed for the **future in-game engine** (so Unity can plan consumption, not just emit)

---

## 12. Key Unity paths

| Area | Path |
|------|------|
| Parse / compile / triggers / expansion | `engines/unity/Assets/Scripts/Storyboard/Storyboard.cs` |
| DTOs / enums | `engines/unity/Assets/Scripts/Storyboard/StoryboardModel.cs` |
| Shared parse (`UnitFloat`, easing) | `engines/unity/Assets/Scripts/Storyboard/GenericStateParser.cs` |
| Runtime spawn / update | `engines/unity/Assets/Scripts/Storyboard/StoryboardRenderer.cs` |
| Per-type parsers | `engines/unity/Assets/Scripts/Storyboard/{Texts,Sprites,Videos,Lines,Controllers,Notes}/*StateParser.cs` |
| Note overrides | `engines/unity/Assets/Scripts/Storyboard/Notes/NoteControllerEaser.cs` |
| Easing enum | `engines/unity/Assets/Scripts/Utils/Easings.cs` |
| Vendor / fallback | `docs/vendor.md`, `engines/unity/Assets/Scripts/Storyboard/PostProcess/` |
| Lab seek | `engines/unity/Assets/Scripts/Navigation/CytoidLab/Storyboard*.CytoidLab.cs`, `docs/lab/releases.md` |
| Boot / load | `engines/unity/Assets/Scripts/Game/Game.cs`, `FileGameContentProvider.cs` |
| Host path fields | `docs/host-protocol-v2.md` (`StoryboardSection`) |

---

## 13. One-page summary for SB ADR

> Unity Lab/runtime today consumes **snake_case authoring JSON** with parse-time macros (templates, note selectors, time arrays) and per-frame easers. cytoid-sb should treat that JSON as **Emit Profile v1** for validation, keep **semantic truth in typed IR**, and validate against the throw/silent/Lab-seek/effect-tier matrices in this doc. `compiled: true` is an optional PascalCase fast-path that **drops triggers**. Future in-game SB engine may consume versioned IR directly; Unity will publish a separate consumption sketch when scheduled. Dead fields and seek/trigger gaps are **documented limitations**, not language bugs to paper over in the frontend.

---

## Revision history

| Date | Change |
|------|--------|
| 2026-07-12 | Initial supply-side contract for cytoid-sb typed IR / authoring emit |
