# Damage & Health

> **Status**: In Revision — 2026-05-18
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-05-15
> **Implements Pillar**: Pillar 1 (Readable Chaos), Pillar 2 (Skill Has a Lane)

## Overview

Damage & Health is the HP ledger for the player character in Dynasty Survivors. It holds one authoritative value — the player's current HP — and emits two events: `DamageTaken` when HP decreases, and `PlayerDead` when HP reaches zero. All game logic that reacts to the player being hurt or dying reads these events rather than polling HP directly. No logic about what caused the damage lives here.

The `DamageTaken` event is consumed by the Combo System (combo counter reset), the Parry System (momentum meter reset), and the Combat Feedback UI (HP bar update, visual feedback). The `PlayerDead` event is consumed by the Run Session Manager (death screen, retry prompt, full run-state reset). Damage & Health receives resolved attack damage forwarded by the Parry System — if an attack was successfully parried the Parry System absorbs it; if not, the unresolved `AttackDamage` value arrives here. Survivability upgrades (max HP increase, HP regeneration, damage reduction multiplier) interact with this system at the formula layer and are defined in the Upgrade Definition GDD, but their values are read and applied here.

Without Damage & Health, there are no stakes to positioning decisions. Every tension in the game — "push in to complete the combo or back off to safety" — flows through the cost of taking damage.

## Player Fantasy

Damage & Health is the system that makes the question "push in or hold back?" meaningful. Players don't engage with it directly — they feel it in the moment when the combo counter reads 2, one more hit away from the Finisher, and the question is: is the risk worth it? Taking a hit isn't just losing HP — it erases the combo chain you built, collapses the momentum you earned, and hands the initiative back to the horde. The legendary general fantasy isn't invincibility — it's knowing exactly when to commit, and having the reads to make that moment a calculated gamble rather than a mistake. Every death screen answers "what cost you the run?" so the next run is a more deliberate version of the same gamble.

## Detailed Design

### Core Rules

**1. Player HP value**

The player has a single HP value. `current_hp` is a float in the range `[0, max_hp]`.

| Stat | Symbol | Default | Tuning Range |
|---|---|---|---|
| Max HP | `max_hp` | 100 | 60–200 (see Tuning Knobs) |
| Starting HP per run | — | `max_hp` | — |

`current_hp` is initialised to `max_hp` at run start and after every retry.

**2. Damage application**

When an unparried attack resolves against the player, Damage & Health receives the `AttackDamage` value from the resolved AttackEvent (forwarded by the Parry System). The damage formula applies:

```
effective_damage = max(1, floor(raw_damage × (1 - damage_reduction)))
```

`damage_reduction` is 0.0 at run start (no base reduction — upgrade-gated only). `current_hp` decreases by `effective_damage`. If `current_hp ≤ 0` after the deduction, `current_hp` is clamped to 0 and `PlayerDead` is emitted in the same frame.

**3. DamageTaken event**

Emitted every time `current_hp` decreases.

```
DamageTaken {
    Amount      : float   // effective_damage as float (int value, always whole number — int→float widening)
    RawAmount   : float   // raw_damage before reduction — for feedback UI and analytics
    AttackerId  : int     // mirrors AttackEvent.AttackerId — int, not string (string causes heap allocation per hit)
    DamageType  : enum    // Standard | CounterHit | Slam | Projectile
}
```

**Implementation contract:** `DamageTaken` must be implemented as a C# `delegate` with a **value-type `struct` payload**. `UnityEvent` is forbidden for this event — reflection overhead and argument boxing at this fire frequency will cause GC pressure. `System.Action<DamageTakenData>` where `DamageTakenData` is a `readonly struct` is the correct pattern.

`DamageTaken` fires before `PlayerDead` in the same frame if both conditions are met.

**4. On DamageTaken — cross-system resets**

In the same frame `DamageTaken` fires, IF `effective_damage ≥ reset_damage_threshold` (tuning knob; default 3):
- Combo System resets `combo_counter → 0` (if `combo_reset_on_damage == true`)
- Parry System reduces `momentum_meter` by `momentum_meter × momentum_reset_multiplier` (default: full reset to 0 when multiplier = 1.0)

If `effective_damage < reset_damage_threshold`, **no combat-state resets fire** — the player takes HP damage but their combo chain and momentum are preserved. This prevents trivial peripheral hits (e.g., a 1-damage graze at max defense investment) from carrying the same combat-state cost as a 25-damage Iron Captain counter-hit.

Invincibility frames begin regardless of the reset threshold (see Rule 5).

**Reset behavior is the default baseline — it is per-general-tunable.** Individual generals defined in the General Characters GDD may override `combo_reset_on_damage` and `momentum_reset_multiplier`. Survivability upgrades in the Upgrade Definition GDD may also modify reset behavior (e.g., an upgrade that reduces momentum loss on hit from 100% to 50%). Both resets at full force is the hardest baseline — generals and upgrades can only soften, not eliminate, the reset behavior without explicit design decision.

**5. Invincibility frames (iframes)**

After any `DamageTaken` event, the player enters an invulnerable window for `invulnerability_duration_ms` (default 500ms). During this window:
- Incoming `AttackEvent` damage is discarded — `DamageTaken` does not fire again
- Parry input is **still processed** — iframes do not block parry resolution
- The iframe window does not stack or reset. A second hit during iframes is discarded; the timer continues counting down from the original start time of the first hit

**6. HP regeneration (upgrade-gated)**

`regen_rate` is 0.0 HP/s at run start. Survivability upgrades may increase it.

```
current_hp = min(current_hp + regen_rate × delta_time, max_hp)
```

Regen ticks every frame. It cannot overheal beyond `max_hp` and never triggers `DamageTaken`. If `current_hp >= max_hp` (use `>=`, not `==` — float equality is unreliable), regen has no effect. **Regen does not tick while in Dead state** (`current_hp == 0` and state locked) — this rule overrides the "every frame" clause.

**7. PlayerDead event**

Emitted when `current_hp` reaches 0.

```
PlayerDead {
    FinalHP              : float   // always 0.0f (clamped before event fires; overkill values not passed)
    TimeOfDeath          : float   // seconds since run start (run-local Time.time - runStartTime)
    KillerAttackerId     : int     // AttackerId from the killing AttackEvent
    KillerDamageType     : enum    // DamageType of the killing hit
    ComboCounterAtDeath  : int     // combo_counter value at DamageTaken time, before reset
    MomentumAtDeath      : float   // momentum_meter value at DamageTaken time, before reset
}
```

**Rationale for kill-context fields:** The Player Fantasy promises "every death screen answers 'what cost you the run?'" The Run Session Manager needs enough data to display: "Killed by Iron Captain counter-hit while holding a 2-chain." Without `KillerDamageType` and `ComboCounterAtDeath`, the death screen can only show stats (finishers, wave) but not cause — a score screen, not a lesson. Damage & Health captures all four fields at the moment of death and passes them once; no run-history tracking is added here.

Damage & Health owns no run statistics. Run Session Manager aggregates total finishers, best parry chain, wave reached, and time survived when it receives `PlayerDead`. Damage & Health does not know about these stats — it is a ledger, not a historian.

**8. Same-frame parry priority**

If a parry input arrives in the same frame as an unresolved `AttackEvent`, parry resolution takes priority. The parry is processed first; if successful, the `AttackEvent` is consumed by the Parry System and `DamageTaken` never fires. Damage & Health never sees that frame's attack. This rule is enforced by Parry System execution order — documented in the Parry System GDD.

---

### States and Transitions

Damage & Health has three states:

| State | Condition | Behaviour | Exits To |
|---|---|---|---|
| **Alive — Vulnerable** | `current_hp > 0`, iframes inactive | Accepts incoming damage; processes DamageTaken | → Invulnerable (on any damage) / → Dead (if damage reduces HP to 0) |
| **Alive — Invulnerable** | `current_hp > 0`, iframes active | Discards incoming damage; parry still processed; regen continues | → Vulnerable (after `invulnerability_duration_ms` elapses) |
| **Dead** | `current_hp == 0` | PlayerDead emitted; all damage discarded; state locked | → Vulnerable (only after Run Session Manager triggers retry and run-state reset) |

---

### Interactions with Other Systems

| System | Data Flow | Interface Owner |
|---|---|---|
| **Parry System** | Forwards resolved `AttackDamage` (as `raw_damage`) to Damage & Health when attack is unparried; subscribes to `DamageTaken` to reset `momentum_meter` | Damage & Health owns both events |
| **Combo System** | Subscribes to `DamageTaken` to reset `combo_counter → 0` | Damage & Health owns the event |
| **Combat Feedback UI** | Subscribes to `DamageTaken` for HP bar animation; reads `DamageType` to select hit-flash variant (CounterHit needs distinct screen shake) | Damage & Health owns the event |
| **Run Session Manager** | Subscribes to `PlayerDead`; drives death screen, stats aggregation, and retry flow | Damage & Health owns `PlayerDead` |
| **Upgrade Definition** | Provides `max_hp_bonus` (flat HP addition), `regen_rate` (HP/s), `damage_reduction` (multiplicative multiplier, capped at 0.75) at upgrade-pick time | Upgrade Definition owns the values; Damage & Health reads and applies them |

## Formulas

### 1. Effective Damage

```
effective_damage = max(1, floor(raw_damage × (1 - damage_reduction)))
```

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Raw damage | `raw_damage` | float | 1–40 | Unresolved `AttackDamage` forwarded by Parry System |
| Damage reduction | `damage_reduction` | float | 0.0–0.75 | Multiplicative reduction from upgrades; 0.0 at run start; capped at 0.75 |
| Effective damage | `effective_damage` | int | 1–40 | Final HP subtracted; floored to int, minimum 1 enforced |

**Output range:** 1–40 (current enemy set). Formula imposes no upper cap — any new enemy with raw_damage > 40 will produce effective_damage above this range without a formula change. Document in the Enemy Definition GDD if the raw_damage ceiling changes.
**Example:** Levy Soldier (5 raw) at max reduction: `max(1, floor(5 × 0.25)) = max(1, 1) = 1`. Iron Captain counter-hit (25 raw) at max reduction: `max(1, floor(25 × 0.25)) = 6`.
**Intake guard:** If `raw_damage ≤ 0`, discard the AttackEvent and log a designer error. `DamageTaken` must not be emitted for zero-damage events — this prevents silent combo/momentum resets with no gameplay cause.
**Guard:** `damage_reduction` must be clamped to [0.0, 0.75] at upgrade-apply time. Values above 0.75 are invalid; log a designer error.
**Rounding contract:** `damage_reduction` values from the Upgrade Definition GDD must be exact binary fractions (0.0, 0.25, 0.5, 0.75) to avoid IEEE 754 `floor()` precision errors at non-power-of-2 values. The Upgrade Definition GDD must enforce this constraint. Intermediate float multiplication may otherwise produce `floor(8.9999...) = 8` instead of the expected 9.
**Type note:** `effective_damage` is an integer computed by `floor()`. It is stored in `DamageTaken.Amount` as `float` (int→float widening; always a whole number in the 1–40 range). All downstream consumers must treat `Amount` as a whole-number float.

---

### 2. HP Regeneration

```
current_hp = min(current_hp + regen_rate × delta_time, max_hp)
```

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Current HP | `current_hp` | float | 0–max_hp | Player's HP value before regen tick |
| Regen rate | `regen_rate` | float | 0.0–unbounded | HP restored per second; 0.0 at run start; set by upgrades |
| Delta time | `delta_time` | float | >0 | Seconds since last frame (Unity `Time.deltaTime`) |
| Max HP | `max_hp` | int | ≥ 60 (base range 60–200; may exceed 200 with upgrades — see Formula 3) | Player's current max HP (base + upgrade additions) |

**Output cap:** `current_hp` cannot exceed `max_hp`. Regen never triggers `DamageTaken` and cannot reduce HP.
**Example (regen_rate = 5 HP/s, 60 fps):** `min(60 + 5 × 0.0167, 100) = min(60.083, 100) = 60.083`. Stored as float; displayed as `ceil(current_hp)` in HUD.
**Regen cap constraint:** At `regen_rate ≥ effective_damage_min / invulnerability_duration_s` (default: `1 / 0.5 = 2.0 HP/s`), the player becomes immortal against minimum-damage enemies (they regenerate at least as much HP between iframe windows as they lose per hit). The hard cap `regen_rate_max = 1.5 HP/s` is set here and enforced by the Upgrade Definition GDD — regen upgrades must not cumulatively exceed this ceiling. If the Upgrade Definition GDD changes `invulnerability_duration_ms`, it must recalculate this constraint.
**Note on time source:** `delta_time = Time.deltaTime` (scaled). Regen pauses when `Time.timeScale = 0`. This is intentional — regen should not tick during paused gameplay. Iframes use `Time.unscaledTime` (Contract #2) and continue draining during pause. If a pause screen is introduced on hit (e.g., upgrade selection), the player exits the pause with fewer remaining iframes but the same HP. This asymmetry is documented as a design choice; if it causes fairness issues, align both to `Time.unscaledDeltaTime` via ADR.

---

### 3. Max HP (with upgrades)

```
max_hp = base_max_hp + max_hp_bonus
```

| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| Base max HP | `base_max_hp` | int | 100 (default) | Player starting max HP; tuning knob |
| Max HP bonus | `max_hp_bonus` | int | 0–unbounded | Flat HP added by Survivability upgrades; 0 at run start |
| Max HP | `max_hp` | int | 100–unbounded | Current effective ceiling |

**Example:** Two max HP upgrades each adding +20: `max_hp = 100 + 40 = 140`.

## Edge Cases

- **If a second `AttackEvent` resolves while iframes are active:** The damage is discarded silently. `DamageTaken` is not emitted. The iframe timer resets to `invulnerability_duration_ms` from the moment of the first hit — it does not stack additional time from the discarded hit. Parry input during iframes is still processed normally.

- **If two `AttackEvent`s resolve in the same frame (both unparried):** Only the first event in frame-processing order applies damage. The second is discarded as if iframes were already active. This is consistent with the iframe rule — once `DamageTaken` fires in a frame, all subsequent damage in that frame is suppressed.

- **If `effective_damage` from a single hit exactly equals `current_hp`:** `current_hp` reaches exactly 0. `DamageTaken` fires first, then `PlayerDead` fires in the same frame. Downstream systems that reset on `DamageTaken` (Combo System, Parry System) still reset before the run ends.

- **If the player is in Dead state and a `DamageTaken` or `PlayerDead` event would otherwise fire:** Dead state discards all incoming damage. No second `PlayerDead` can fire after the first. Run Session Manager drives the state back to Vulnerable on retry.

- **If a Survivability upgrade adds `max_hp_bonus` mid-run:** `max_hp` increases immediately. `current_hp` is not changed — HP does not scale proportionally. A player at 20/100 who gains +20 max HP is now at 20/120. The upgrade is not a heal; it expands the ceiling only.

- **If `regen_rate > 0` and `current_hp = max_hp`:** Regen formula is evaluated but clamped: `min(max_hp + δ, max_hp) = max_hp`. No HP change, no event. Regen is silent when full.

- **If `regen_rate > 0` while Dead:** Dead state discards all incoming changes. Regen is not evaluated while `current_hp = 0` (Dead state is locked). Regen resumes when the run session resets to a new run.

- **If `damage_reduction = 0.75` (cap) and `raw_damage = 1`:** `max(1, floor(1 × 0.25)) = max(1, 0) = 1`. The minimum-1 floor ensures the player cannot block all damage from any attack. At `damage_reduction = 0.75`, the Levy Soldier (5 dmg) deals 1 effective damage — still fires `DamageTaken` and resets both systems.

- **If parry and unresolved damage arrive in the same frame:** Parry System resolves first per frame-execution order. If the parry succeeds, the AttackEvent is consumed; Damage & Health receives nothing. If the parry window has expired but parry input was still within that frame, the resolution follows the Parry System GDD's timing rules — this document defers to that spec.

- **On retry:** Run Session Manager calls a reset API on Damage & Health. Full reset: `max_hp_bonus → 0`, `max_hp → base_max_hp`, `current_hp → base_max_hp`, `damage_reduction → 0.0`, `regen_rate → 0.0`, `combo_reset_on_damage → general_default`, `momentum_reset_multiplier → general_default`, state → Vulnerable. MVP has no persistent upgrades — all run-granted values reset. A player who gained +40 HP from upgrades resets to 100/100, not 100/140.

## Dependencies

| System | Direction | Nature | Interface |
|---|---|---|---|
| **Parry System** | Bidirectional | Hard | Parry System forwards resolved `AttackDamage` as `raw_damage` AND the `DamageType` from the originating AttackEvent when attack goes unparried. Damage & Health reads both. **DamageType source contract:** The AttackEvent contract (enemy-definition.md) must add a `DamageType` field so the Parry System can forward it. Slam (`DamageType.Slam`) is non-parriable — it bypasses Parry System and is forwarded directly from Enemy AI to Damage & Health, carrying `DamageType.Slam` explicitly. This path must be documented in the Enemy AI GDD. Parry System also subscribes to `DamageTaken` to reset `momentum_meter`. |
| **Combo System** | Damage & Health → Combo | Hard | Subscribes to `DamageTaken` to reset `combo_counter → 0`. |
| **Run Session Manager** | Damage & Health → Run Session Manager | Hard | Subscribes to `PlayerDead`; drives death screen, stats aggregation, retry. Owns the reset API called at run start/retry. |
| **Combat Feedback UI** | Damage & Health → Combat Feedback UI | Hard | Subscribes to `DamageTaken` for HP bar animation and `DamageType`-specific hit feedback (CounterHit = distinct screen shake). |
| **Run UI** | Damage & Health → Run UI | Soft | Run UI reads `current_hp` and `max_hp` for HP bar display. Interface detail (polling vs. event notification) delegated to Run UI GDD. |
| **Upgrade Definition** | Upgrade Definition → Damage & Health | Hard | Upgrade Definition provides `max_hp_bonus`, `regen_rate`, `damage_reduction` at upgrade-pick time. This GDD reads and applies them; Upgrade Definition GDD owns their values and ranges. |

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|---|---|---|---|
| `base_max_hp` | 100 | 60–200 | Overall run difficulty; below 60 makes a Warlord slam (40 dmg) a near-instant kill; above 200 makes individual hits feel irrelevant |
| `invulnerability_duration_ms` | 500 | 300–800 | Post-damage iframe window; below 300ms risks stagger-lock in dense packs; above 800ms makes the player effectively invulnerable to follow-up hits |
| `damage_reduction_cap` | 0.75 | 0.60–0.85 | Maximum reduction Survivability upgrades can provide; above 0.85 risks a near-unkillable player even with the minimum-1 floor |
| `reset_damage_threshold` | 3 | 1–10 | Minimum effective_damage required to trigger combo_counter and momentum_meter resets on DamageTaken. Hits below this threshold deal HP damage but do not reset combat state. Default 3 means a max-defense player can survive trivial grazes without losing their chain; real hits (5+ damage from any basic infantry at 0 reduction) always reset. |
| `combo_reset_on_damage` | true | true / false | Whether combo_counter resets on DamageTaken (when effective_damage ≥ reset_damage_threshold); per-general default; General Characters GDD may override |
| `momentum_reset_multiplier` | 1.0 | 0.0–1.0 | Fraction of momentum_meter lost on DamageTaken (when effective_damage ≥ reset_damage_threshold); 1.0 = full reset; per-general default; Upgrade Definition GDD may modify |
| `regen_rate_max` | **1.5 HP/s** | — | Hard ceiling for regen_rate from all sources combined. Below the immortality threshold (2.0 HP/s at default iframes). The Upgrade Definition GDD must not allow cumulative regen upgrades to exceed this. If `invulnerability_duration_ms` changes, recalculate: `regen_rate_max = effective_damage_min / (invulnerability_duration_ms / 1000f) × 0.75`. |

Regen rate and max HP bonus per upgrade are defined and owned by the Upgrade Definition GDD. Damage & Health enforces `damage_reduction_cap` and `regen_rate_max` at upgrade-apply time — both caps are defined here.

## Visual/Audio Requirements

Damage & Health is a data/event system — it emits events but owns no VFX, animations, or audio. All visual and audio feedback is owned by downstream systems that subscribe to `DamageTaken` and `PlayerDead`.

**Requirements this system places on downstream systems:**

- **Combat Feedback UI** must implement visually distinct hit responses per `DamageType`:
  - `Standard` → standard HP bar flash and hit vignette
  - `CounterHit` → distinct (larger/different-color) screen shake — this is the most dangerous hit in the game; it must read differently from a standard poke
  - `Slam` → fullscreen impact, distinct from CounterHit (Warlord Phase 2 only; non-parriable)
  - `Projectile` → arrow-hit feedback, directional if possible
- **Run UI** must display `current_hp` and `max_hp` as a visible HP bar readable at 200 enemies on screen (Pillar 1). HP bar must update on every `DamageTaken` event.
- **Run Session Manager** must transition to the death screen when `PlayerDead` fires. Death screen must display: total finishers landed, best parry chain, wave reached, time survived.

*Full VFX and audio specs are generated by `/asset-spec system:damage-health` after the art bible is approved.*

## Implementation Contracts

Architectural requirements that must be established before any Damage & Health code is written.

**1. Entity architecture ADR required.** 200 enemies × per-frame regen + iframe countdown is a workload suited to Unity 6.3 LTS's production-ready DOTS/ECS (`ISystem` + `IJobEntity` + Burst). A MonoBehaviour-per-entity approach adds 200 managed-to-native Update() calls per frame. **An ADR must be written and accepted before implementation begins**, choosing MonoBehaviour (explicit simplicity trade-off) or DOTS (performance). This choice affects all downstream systems (Combo, Parry, UI).

**2. iframe timer: timestamp pattern with unscaled time.** The iframe timer must use `Time.unscaledTime` (not `Time.deltaTime` accumulator) to behave correctly under pause (`Time.timeScale = 0`). Implementation: `_iframeEndTime = Time.unscaledTime + (invulnerability_duration_ms / 1000f)` on iframe start; check `Time.unscaledTime >= _iframeEndTime` each frame. The designer-facing tuning knob uses milliseconds (`invulnerability_duration_ms = 500`) for readability; the implementation converts to float seconds at the call site.

**3. Script Execution Order (SEO) dependency.** Parry System must evaluate before Damage & Health within the same frame to enforce same-frame parry priority. This SEO dependency must be either: (a) explicitly set in `ProjectSettings/ProjectSettings.asset` and documented in the Parry System GDD, or (b) enforced by a `CombatFrame` orchestrator MonoBehaviour that calls `parrySystem.Evaluate()` then `damageSystem.Apply()` sequentially in one `Update()`. Option (b) is preferred — it eliminates the hidden SEO dependency. Document the chosen pattern in the Enemy AI GDD and ADR.

**4. `PlayerDead.FinalHP` clamping.** `FinalHP` must be clamped to `0.0f` before the event fires. A single hit can reduce `current_hp` below zero (overkill). `FinalHP` must always be `0.0f` — the Run Session Manager and analytics should never receive a negative HP value.

**5. Reset API call-site guard.** The retry reset API (`PlayerHealthSystem.Reset()` or equivalent) must be guarded: if called while state is NOT Dead, log a designer error and no-op — do not silently reset a live player mid-run. Only valid in Dead state. The API must also expose a `ForceState(HealthState)` method for test fixtures (sets state without triggering events). This test seam is referenced by AC-DH-06 and AC-DH-09.

**6. `AttackerId` static counter reset for tests.** The `_nextInstanceId` counter must be resettable via a `[Conditional("UNITY_EDITOR")] public static void ResetInstanceIdCounter()` method. Tests that assert specific `AttackerId` integer values must call `ResetInstanceIdCounter()` in `[SetUp]` to guarantee deterministic IDs regardless of test execution order.

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|---|---|---|
| AC-DH-01 | **GIVEN** a new run begins with `base_max_hp` at its default (100) and no Survivability upgrades applied, **WHEN** run-start initialisation executes, **THEN** `current_hp == 100`, `damage_reduction == 0.0`, `regen_rate == 0.0`. **Pass condition:** assert all three values. | Logic | BLOCKING |
| AC-DH-02 | **GIVEN** `current_hp == 100`, `damage_reduction == 0.0` and an unparried attack resolves with `raw_damage == 20`, **WHEN** the damage formula executes, **THEN** `effective_damage == 20` and `current_hp` decreases by 20. **Pass condition:** assert `effective_damage == 20`; assert `current_hp == 80`. | Logic | BLOCKING |
| AC-DH-03 | **GIVEN** `damage_reduction == 0.5` and an unparried attack resolves with `raw_damage == 25`, **WHEN** the damage formula executes (`max(1, floor(25 × 0.5))`), **THEN** `effective_damage == 12`. **Pass condition:** assert `effective_damage == 12`. | Logic | BLOCKING |
| AC-DH-04 | **GIVEN** `damage_reduction == 0.75` (the cap) and `raw_damage == 1`, **WHEN** the damage formula executes (`max(1, floor(1 × 0.25))`), **THEN** `effective_damage == 1` (minimum-1 floor overrides the floored zero) and `DamageTaken` fires. **Pass condition:** assert `effective_damage == 1`; assert `DamageTaken` event count == 1 **since start of this test case** (reset event counter before test begins). | Logic | BLOCKING |
| AC-DH-05 | **GIVEN** `damage_reduction == 0.0`, `raw_damage == 15`, `AttackerId == 1` (int — test fixture calls `ResetInstanceIdCounter()` in SetUp then spawns one enemy, guaranteeing ID == 1), `DamageType == Standard`, **WHEN** damage is applied, **THEN** the emitted `DamageTaken` event has `Amount == 15.0f`, `RawAmount == 15.0f`, `AttackerId == 1` (int comparison), `DamageType == Standard`. **Pass condition:** assert all four fields using typed comparisons — AttackerId is int. | Logic | BLOCKING |
| AC-DH-06 | **GIVEN** `combo_counter == 3`, `momentum_meter == 50`, player in Vulnerable state (set via `PlayerHealthSystem.ForceState(Vulnerable)` or equivalent test API), `combo_reset_on_damage == true`, `momentum_reset_multiplier == 1.0`, and an unparried attack with `raw_damage == 10` resolves, **WHEN** `DamageTaken` fires, **THEN** in the same frame: `combo_counter == 0` AND `momentum_meter == 0`. **Pass condition:** integration test uses a deterministic frame-step harness (manual `Tick()` pump, not `WaitForFixedUpdate()`); assert both values are 0 before the next tick call returns. Requires Combo System and Parry System to be wired to `DamageTaken` in the test setup. **ADR dependency:** the specific wiring pattern (SEO vs. CombatFrame orchestrator — Implementation Contract #3) must be resolved before this test can be written. Test fixture setup depends on the chosen pattern. | Integration | BLOCKING |
| AC-DH-07 | **GIVEN** a hit at T=0s causes `DamageTaken` (entering invulnerable window, `invulnerability_duration_ms == 500` → 0.5s at implementation layer), `current_hp == 80` after the first hit, **WHEN** a second unparried attack with `raw_damage == 10` resolves at T=0.2s (within window), **THEN** `current_hp` remains 80 and `DamageTaken` is NOT emitted. At T=0.501s, the iframe window has expired and a third attack with `raw_damage == 10` deals damage. **Pass condition:** assert `DamageTaken` count == 1 at T=0.2s; assert `current_hp == 80` at T=0.2s; assert `DamageTaken` count == 2 at T=0.501s (iframe expired, third hit registered); assert `current_hp == 70` at T=0.501s. (Use test time-control to advance unscaled time without real elapsed time.) | Logic | BLOCKING |
| AC-DH-08 | **GIVEN** `regen_rate == 5.0` HP/s, `current_hp == 95`, `max_hp == 100`, and three frames elapse each with `delta_time == 0.5s`, **WHEN** regen ticks are applied each frame, **THEN** `current_hp` reaches 100 and is clamped there; `DamageTaken` is never emitted during regen. **Pass condition:** assert `current_hp == 100.0` (to within 0.001); assert `DamageTaken` count == 0. | Logic | BLOCKING |
| AC-DH-09 | **GIVEN** `current_hp == 10` and an unparried attack with `raw_damage == 10` resolves (HP → 0), then a second attack with `raw_damage == 5` arrives while in Dead state, **WHEN** both events are processed, **THEN** (a) `DamageTaken` fires before `PlayerDead` in the first frame, (b) `PlayerDead.FinalHP == 0.0` and `PlayerDead.TimeOfDeath` is a positive float, (c) `PlayerDead` fires exactly once. **Pass condition:** using a custom event-recorder that logs events by emission order (each subscriber appends to a `List<string>` in order of callback): assert event log `[0] == "DamageTaken"` and `[1] == "PlayerDead"`; assert `PlayerDead.FinalHP == 0.0f`; assert `PlayerDead.TimeOfDeath > 0.0f`; assert `PlayerDead` count == 1; **assert `DamageTaken` count == 1** (counter is NOT reset between the two attacks — total count over the full test case must equal 1; the second attack in Dead state must NOT emit DamageTaken). | Logic | BLOCKING |
| AC-DH-10 | **GIVEN** an upgrade attempts to apply `damage_reduction == 0.90` (above `damage_reduction_cap == 0.75`), **WHEN** the value is applied at upgrade-apply time, **THEN** `damage_reduction` is clamped to 0.75 and a designer error is logged (run continues; no exception). **Boundary:** `damage_reduction == 0.75` applies without logging an error. **Pass condition:** assert `damage_reduction == 0.75`; assert error log contains designer-error marker. | Logic | BLOCKING |
| AC-DH-11 | **GIVEN** a run ended in Dead state with `current_hp == 0`, `max_hp_bonus == 40` (from upgrades, so `max_hp == 140`), `damage_reduction == 0.50`, `regen_rate == 2.0`, **WHEN** Run Session Manager triggers the retry reset API, **THEN** `max_hp_bonus == 0`, `max_hp == 100` (base), `current_hp == 100`, `damage_reduction == 0.0`, `regen_rate == 0.0`, state is Vulnerable. **Pass condition:** assert all five state values post-reset; assert a test attack with `raw_damage == 10` produces `effective_damage == 10` and `current_hp == 90` (confirms damage pipeline active and damage_reduction is 0). | Integration | BLOCKING |

| AC-DH-12 | **GIVEN** an `AttackEvent` arrives with `raw_damage == 0`, **WHEN** Damage & Health processes the event, **THEN** `DamageTaken` is NOT emitted, `current_hp` is unchanged, and a designer error is logged. **Boundary:** `raw_damage == -1` (negative) produces the same result. **Pass condition:** assert `DamageTaken` count == 0 (reset counter before test); assert `current_hp` unchanged; assert error log contains designer-error marker. | Logic | BLOCKING |
| AC-DH-13 | **GIVEN** `reset_damage_threshold == 3`, `combo_counter == 2`, `momentum_meter == 80`, player in Vulnerable state, and an unparried attack resolves with `raw_damage == 5`, `damage_reduction == 0.75` → `effective_damage == 1` (below threshold), **WHEN** `DamageTaken` fires, **THEN** `current_hp` decreases by 1 AND `combo_counter` remains 2 AND `momentum_meter` remains 80 (no resets). Iframes begin normally. **Pass condition:** assert `current_hp == prior_hp - 1`; assert `combo_counter == 2`; assert `momentum_meter == 80`. **Contrast:** repeat with `damage_reduction == 0.0` → `effective_damage == 5` (≥ threshold) → assert `combo_counter == 0` and `momentum_meter == 0`. | Logic | BLOCKING |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|---|---|---|
| OQ-01 | Does 500ms invulnerability feel correct at 200 enemies? At high density, stagger-lock from simultaneous hits is the failure mode — but 500ms may feel too generous if elite encounters are designed around tight windows. | Prototype | Verify in combat prototype. Iron Captain counter-hit windows are 600ms (5-min), 550ms (10-min), 450ms (15-min). Iframe window is 500ms. If the 15-min encounter feels timing-conflicted (450ms counter-hit vs. 500ms iframe), adjust one knob — do not adjust both simultaneously. |
| OQ-02 | Should `DamageTaken` carry the attacker's world position? The game-designer said no (player transform is globally accessible), but the Combat Feedback UI may want directional hit effects (e.g., hit vignette stronger on the side the attack came from). | Combat Feedback UI GDD | Revisit when authoring Combat Feedback UI GDD. If directional feedback is desired, add `AttackerPosition: Vector3` to `DamageTaken`. |
