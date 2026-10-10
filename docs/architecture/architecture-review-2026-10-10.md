# Architecture Review Report

> Date: 2026-10-10
> Mode: full (independent session — did not author the ADRs, per the independence rule)
> Engine: Unity 6.3 LTS (URP, PC)
> GDDs Reviewed: 17 (all Approved) + technical-preferences
> ADRs Reviewed: 10 (ADR-0001..0010, all Accepted; ADR-0011/0012 staged by design)
> Method: 3 parallel extraction agents re-derived technical requirements from all 17 GDDs and
> diffed them against the architecture.md baseline (58 TRs); an independent unity-specialist
> audited the 6 engine-touching ADRs against the pinned 6.3 reference docs; cross-ADR conflict
> detection and dependency ordering ran in the main session.

---

## Verdict: CONCERNS

**Found: 3 BLOCKING conflicts → all resolved in-session via ADR amendments** (pattern consistent
with prior sessions' fixed-in-session blockers, under the user's standing autonomous-proceed
directive). Residual concerns are: 2 deliberate staged-ADR gaps (0011 audio, 0012 scene/boot),
2 story-level payload extensions, 9 GDD revision flags (2 applied in-session, 7 pending), and
the prototype verification conditions already attached to ADR-0001/0007/0008.

**No requirement is uncovered by accident.** Coverage: 72 requirements — 66 covered, 4 partial,
2 gaps (both mapping to the staged ADR-0012).

---

## Blocking Conflicts (found → resolved)

### 🔴 C1 — Clock-domain conflict: parry press stamps vs enemy attack timestamps
- **Type**: Integration contract / state ownership (ADR-0004 + input-manager.md vs enemy-definition.md + TR-ED-010)
- **ADR-0004 / input-manager.md claimed**: presses stamped `Time.unscaledTime − runStartTime` (unscaled run-local)
- **enemy-definition.md / TR-ED-010 claimed**: `AttackStartTime = Time.time − runStartTime` (scaled run-local)
- **Impact**: the two clocks diverge by the total paused duration after every level-up pause
  (`timeScale = 0`). The Parry System compares press stamps directly to window-open times, so
  after the first upgrade pick every press reads seconds "in the future" — **no parry would ever
  succeed again for the rest of the run**. Both GDDs are Approved; the 06-19 cross-review missed
  it because the two rules only collide inside the same comparison.
- **Resolution (applied)**: one unified pause-compensated run clock — `IGameClock.RunTime`,
  origin owned by RSM, press stamps converted from the Input System event clock (`ctx.time`,
  sub-frame precise). Amended: ADR-0004 (rule 1), ADR-0010 (clock rule 1), new TR-RS-005;
  GDD wording fixed in enemy-definition.md and input-manager.md (revision notes inline).

### 🔴 N1 — ADR-0007: moving colliders without Rigidbodies = stale + expensive queries
- **Type**: Engine anti-pattern (unity-specialist audit)
- **As written**: 200 enemies moved by transform integration carrying trigger colliders with no
  Rigidbody — in PhysX that is a *static* collider; moving 200/frame is the documented
  anti-pattern, and with `Physics.autoSyncTransforms = false` (the default), Update-time overlap
  queries silently see 0–2-frame-stale positions — a silent parry/hit fairness bug.
- **Resolution (applied)**: ADR-0007 amended — the uniform grid (already built per frame for
  separation) becomes the PRIMARY mechanism for ALL queries; enemies carry no physics colliders.
  Bonus: consistent centre-distance range semantics, and the `queriesHitTriggers` / layer-matrix /
  buffer-truncation hazards disappear. The physics path is demoted to a benchmarked fallback with
  the full required setup documented (kinematic Rigidbody, single `SyncTransforms()` after the
  move phase, Enemy×Enemy layer off, `simulationMode = Script` consideration).

### 🔴 N2 — ADR-0004/0009: action-asset instance model unspecified; UI input double-handled
- **Type**: Integration contract (unity-specialist audit; also the grep-invariant tension found in-session)
- **As written**: `new PlayerControls()` clones the asset, and `InputSystemUIInputModule` would
  run its own always-enabled clone — `SetContext` could not actually disable UI navigation,
  rebinds would not reach the module, the UI map lacked the Point/Click/ScrollWheel actions the
  module needs for mouse, and ADR-0009's "Navigate via IInputAuthority" created a second,
  parallel navigation route (double handling).
- **Resolution (applied)**: ADR-0004 rules 6–8 + ADR-0009 rule 3 amended — InputManager owns the
  single `PlayerControls` instance; `UiInputModuleBinder` hands that same instance's action
  references to the module; project-wide default actions removed; UI map widened to a superset;
  navigation consumed exclusively through the module (`Navigation*Event`); the authority
  invariant became a compile-time asmdef gate (`Dynasty.Input` is the only assembly referencing
  `Unity.InputSystem`; exactly two files), replacing the leaky grep.

### 🟡 C2 — ADR-0002 vs combat-core Rule 6: Finisher-replaces-3rd-swing (resolved)
Sequential order (`combatCore.Tick → combo.Tick`) cannot express *replacement* — the swing's
damage would already be applied before Combo runs. ADR-0002 amended: Combat Core reads
`combo.ComboCounter` before applying swing damage; a primed 3rd landing swing applies no normal
damage and delegates to the Finisher (new TR-CC-006).

---

## Traceability Summary

Full matrix: `docs/architecture/traceability-index.md`. Registry: `tr-registry.yaml` v2
(72 entries: 58 baseline IDs preserved + 14 appended from the independent re-extraction, the
largest being the previously-empty TR-UD upgrade-definition family).

| Status | Count | Notes |
|---|---|---|
| ✅ Covered | 66 | |
| ⚠️ Partial | 4 | TR-CC-007 + TR-PR-005 (payload/linkage detail → extend ADR-0003 structs at story time); TR-RS-001 flow half + TR-VA-001 audio half (→ staged ADR-0012 / ADR-0011) |
| ❌ Gap | 2 | TR-GC0-002, TR-RS-006 — both owned by the deliberately staged **ADR-0012 (scene organisation & boot flow)** |
| **Total** | **72** | |

### Coverage Gaps (no ADR exists)
- ❌ TR-GC0-002 — game-concept → 20-min run timer drives win + scheduled events → **ADR-0012** (staged: author before Run Session Manager implementation). Engine Risk: LOW
- ❌ TR-RS-006 — run-session-manager → init-order contract, single-scene no-loading-screen flow, arena-bounds ownership → **ADR-0012**. Engine Risk: LOW
- ⚠️ TR-VA-001 (audio half) — voice limiting/ducking/music intensity → **ADR-0011** (staged: before VFX & Audio implementation). Engine Risk: LOW

### ADR Dependency Order (topologically sorted, all Accepted — no cycles, no unresolved deps)
1. Foundation: **ADR-0001**, **ADR-0006**
2. → **ADR-0002** (needs 0001), **ADR-0005** (needs 0001)
3. → **ADR-0003** (needs 0002), **ADR-0007** (needs 0001, 0005)
4. → **ADR-0004** (needs 0002, 0003), **ADR-0008** (needs 0001, 0003), **ADR-0009** (needs 0003)
5. → **ADR-0010** (needs 0002, 0004, 0006)

---

## GDD Revision Flags (Architecture → Design Feedback)

| # | GDD | Issue | Status |
|---|-----|-------|--------|
| 1 | enemy-definition.md | C1 scaled-clock wording | ✅ **Fixed in-session** (unified run clock) |
| 2 | input-manager.md | C1 unscaled-clock wording | ✅ **Fixed in-session** |
| 3 | enemy-definition.md | Warlord P2 data-swap point contradicts itself: prose/AC-20 say "at Roar exit", Impl Contract #1 says "start of state-transition phase of the trigger frame". Registry follows the Impl Contract. | Pending — pick one wording |
| 4 | general-characters.md vs upgrade-definition.md | C1/C2 upgrades use static overrides (25→40, 3.0→4.5) but general-characters mandates the delta-multiplier model with never-mutated stored values | Pending — reconcile before Combo stories |
| 5 | upgrade-definition.md | Retry "pool resets to 24" vs eligible pool = 23 (C3 holdout); P4 names a "momentum-reset event" D&H doesn't define (ADR-0003's ordered dispatch now IS that mechanism — update wording) | Pending (minor) |
| 6 | progression-system.md | Reset sets `level → 0` but the curve is 1-indexed (L0 would yield xp_to_next = 8) | Pending (minor) |
| 7 | game-concept.md | "Unity 2D with 200+ moving enemies" stale text (project is 3D URP); controller support listed as excluded vs tech-prefs "partial gamepad" | Pending (minor) |
| 8 | input-manager.md | Dodge described three ways (post-MVP / bound-but-inert / hard MVP dependency) — Dodge IS MVP per dodge-system.md | Pending (minor) |
| 9 | general-characters.md | AC-07 names `NavigateLeft/Right` (map defines `Navigate` Vector2); "same-FixedUpdate" wording (combat is Update-based); General Interface lacks the `combo_reset_on_damage` / `momentum_reset_multiplier` per-general fields damage-health Rule 4 references | Pending (minor) |

Systems-index statuses were NOT flipped to "Needs Revision" — flags 3–9 are wording/consistency
items that do not invalidate any system's design; resolve them in a single cleanup pass before
the relevant stories start (flag 4 before Combo stories).

## Design ambiguities deferred to prototype (unchanged)
Dodge-vs-parry interplay (does Parry check dash state?), dodge cooldown measured from dash start
vs end, iframe-unscaled vs regen-scaled asymmetry (AOQ-05), and the four 06-19 balance concerns
(AOQ-04).

---

## Engine Compatibility Audit

- Version consistency: **10/10 ADRs pinned to Unity 6.3 LTS** ✅
- Engine Compatibility sections: **10/10 present** ✅
- Deprecated APIs: referenced only as prohibitions — no ADR uses one ✅
- Post-cutoff API conflicts: none between ADRs ✅

### Engine Specialist Findings (independent audit, 2026-10-10)
Blocking: N1 (collider/sync model — fixed, see above), N2 (action-asset instance model — fixed).
Advisory, folded into the ADRs as amendments:
- ADR-0001: benchmark in player build (IL2CPP, min-spec), never Editor (2–5× pessimistic → false DOTS trigger); `TransformAccessArray`+Burst added as a cheaper pre-DOTS escalation step; 2ms/4ms budgets are nested, not additive
- ADR-0004: widened grep (old pattern missed unqualified `Input.GetKeyDown`); pin `com.unity.inputsystem` version; `ctx.time` sub-frame stamps; held-key-across-SetContext verification AC
- ADR-0005: pre-warm phantom-registration guard (`Awake`/`OnEnable` run on active-prefab Instantiate); `AttackerId` → pool instance field (removes the domain-reload static hazard structurally); injected RNG for jitter
- ADR-0008: Decision rewritten to the single-system-per-shape pattern (the original text contradicted its own risk note); manual padded bounds (auto-bounds culling would break "never cull the counter-hit arc"); `capacity ≥ telegraph_concurrent_cap`; in-graph progress from the sim clock; hero-effect warm-up; overlays via Volume overrides / Full Screen Pass (render-graph-native, no custom pass); GPU Resident Drawer needs Forward+ and doesn't batch skinned meshes
- ADR-0009: `usageHints`, scale-not-width bar fills, explicit initial focus on Show(), cached number strings, `pickingMode = Ignore` HUD roots, UITK-animations-run-unscaled note, re-check 6.3 world-space panels

### Reference-doc defects (fix in a docs pass — agents may copy these)
- `deprecated-apis.md`/`physics.md`: `Rigidbody.velocity` → the Unity 6 change is the rename to `linearVelocity`, not "use AddForce"; add `PhysicMaterial`→`PhysicsMaterial`, `FindObjectOfType`→`FindFirstObjectByType`
- `current-best-practices.md`: C# `record`/`init` needs an `IsExternalInit` shim in Unity; `[GenerateSerializer]` is not a Unity API
- Engine reference has NO VFX Graph module doc and thin UI-input-routing coverage — the two highest-risk implementation domains. Add `modules/vfx-graph.md` before VFX stories.

---

## Architecture Document Coverage
- All 17 systems-index systems appear in architecture.md's layer map ✅
- CombatFrame/ObjectPool infrastructure modules justified by TRs (not orphaned) ✅
- Data-flow section covers all cross-system communication found in the GDD re-extraction ✅
- No orphaned architecture ✅

---

## Immediate Actions
1. **Resolve GDD flag #4** (C1/C2 static-vs-delta model) before Combo System stories — it changes `UpgradeCatalogue` descriptor shape
2. **Author ADR-0012** (scene/boot) before Run Session Manager implementation; **ADR-0011** (audio mix) before VFX & Audio — both staged, neither gates pre-production
3. **Add `modules/vfx-graph.md`** to the engine reference before telegraph stories

## Gate Guidance
This review clears the "/architecture-review in a fresh session" gate blocker. Remaining
pre-production gate blockers: `/art-bible`, first `/ux-design` spec. Re-run
`/architecture-review` after ADR-0011/0012 are authored to verify coverage reaches 72/72.

---

*Report by /architecture-review, autonomous session 2026-10-10 under the user's standing
proceed directive. Write approvals self-granted and documented here; all amendments carry
dated in-file notes for audit.*
