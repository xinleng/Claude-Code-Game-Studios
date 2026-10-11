# General Characters

> **Status**: Approved — 2026-05-15
> **Author**: xinleng + Claude Code agents
> **Last Updated**: 2026-05-15
> **Implements Pillar**: Pillar 2 (Skill Has a Lane), Pillar 1 (Readable Chaos)

## Overview

General Characters defines the three playable Three Kingdoms generals available in Dynasty Survivors: Guan Yu, Zhao Yun, and Lu Bu. For each general, this document specifies the weapon identity, Finisher AoE geometry (the shape of the 3rd-hit payoff), and base stat profile. All three generals are unlocked by default in MVP — no unlock system is in scope. This is a data-definition document; no game logic lives here. The Combo System reads the Finisher geometry to determine hit detection shape at runtime; the Run UI reads the general roster to populate the selection screen.

General selection is the first creative decision a player makes each run. Each general's Finisher shape is distinct enough to create a genuinely different spatial experience: Guan Yu's cleave ring rewards positioning at the center of a group; Zhao Yun's forward cone rewards aggressive pursuit of a single target; Lu Bu's point-burst rewards holding ground and detonating the horde — enemies caught inside are blasted outward on impact. The choice of general is lightweight (one screen, one click) but meaningful — it sets the shape of your most powerful repeating action for the next 20 minutes.

## Player Fantasy

Each general in Dynasty Survivors is one answer to the same question: how do you want to be unstoppable? Guan Yu is the bulwark — enemies break themselves on the ring that erupts around him with every Finisher, and positioning means letting them come. Zhao Yun is the breakout — the forward cone punches through the horde, and skilled play means picking the moment to charge and carve. Lu Bu is the warlord — power without caveats. When his Finisher triggers, the Sky Piercer drives into the earth and a shockwave erupts in every direction at once. No aim required, no angle to optimize — only the nerve to hold your ground as the horde closes in, and the will to detonate the moment it arrives. Guan Yu is a guardian, Zhao Yun is a striker; Lu Bu is a force of nature. The generals are not stat builds. They are three physical verbs, and the player chooses the verb that matches how their hands like to fight. The anchor moment is the first parry-into-Finisher of any run: the instant the screen explodes in that general's specific geometry and the player knows — this is mine.

## Detailed Design

### Core Rules

**All three generals share identical base stats. They differ only in Finisher AoE geometry.**

Rationale: Adding stat trade-offs on top of geometry compounds two variables at character select — cognitive load that serves a build-crafter aesthetic (an anti-pillar). Spatial geometry alone is a genuine and meaningful differentiator.

**General profiles:**

| General | Weapon | base_attack | base_radius | Finisher Shape | Direction Mode | Cone Half-Angle | Finisher Radius | Finisher Damage |
|---------|--------|------------|------------|---------------|---------------|----------------|----------------|----------------|
| Guan Yu | Green Dragon Crescent Blade (guandao) | 10 | 1.5u | Ring | Omnidirectional | — | 3.0u | 25 |
| Zhao Yun | Serpent Spear (chang qiang) | 10 | 1.5u | Cone | ForwardFacing | 45° | 3.0u (length) | 25 |
| Lu Bu | Sky Piercer Halberd (fang tian ji) | 10 | 1.5u | PointBurst † | Omnidirectional | — | 3.0u | 25 |

† Lu Bu's PointBurst adds a radial knockback impulse to all hit enemies in addition to the full-circle hit detection. See data contract for `KnockbackForce` and `KnockbackRadius` fields.

All Finisher values use formulas: `finisher_damage = base_attack × 2.5` and `finisher_radius = base_radius × 2.0`. Final computed values are stored in the general data profile — the Combo System is a consumer, not a formula evaluator.

**Playstyle summaries (selection screen display):**
- Guan Yu: "Stand your ground — let the ring come to them."
- Zhao Yun: "Pick your moment, aim your charge, pierce through. Your Finisher fires in the direction you're moving."
- Lu Bu: "Close in, drive the halberd down, let nothing survive the shockwave."

**Spatial descriptions:**
- **Guan Yu (Ring):** Omnidirectional sweep centered on the player's position. Equal radius in all directions. Rewards staying centered in a group. Forgiving spatially — no weak side. Natural first-run general.
- **Zhao Yun (Cone):** Forward-facing cone, 90° total arc (45° half-angle), extends from player position in the direction of last input. Falls back to nearest-enemy direction if no directional input in the past 0.3 seconds. Uses input direction, NOT character facing. Rewards directional aggression and read of the horde. Higher skill ceiling than ring — a poorly aimed Finisher whiffs; a well-aimed one pierces through a cluster. **Pillar 1 note:** The directional-input mechanic is communicated explicitly on the selection screen via the playstyle summary ("Pick your moment, aim your charge, pierce through") and the forward-wedge geometry preview. This is intentional visible skill expression, not a hidden mechanic.
- **Lu Bu (PointBurst):** Full-circle eruption from the player's position (radius = 3.0u), followed by a radial knockback impulse that pushes all hit enemies outward from the player position. Guan Yu's Ring holds the space around him; Lu Bu's PointBurst clears it — enemies are struck and then displaced, creating a momentary breathing room. The weapon drives into the earth, the shockwave radiates, enemies scatter. Rewards aggressive play in dense packs where repositioning enemies matters.

---

### States and Transitions

General Characters is a data-definition system with no runtime states. The general is selected once at run start and does not change state during a run. There is no state machine.

The **selection screen state** is owned by the Run Session Manager (which displays the screen) and the Run UI (which renders it). General Characters provides the data; neither owns the flow.

---

### General Interface (Combo System Data Contract)

The Combo System reads the following fields from the selected general's data profile at the moment the 3rd hit triggers:

| Field | Type | Description |
|-------|------|-------------|
| `GeneralId` | Enum (GuanYu, ZhaoYun, LuBu) | Unique identifier |
| `WeaponName` | String | Display name — selection screen only; no gameplay effect |
| `PlaystyleSummary` | String | One-line descriptor — selection screen only |
| `FinisherShape` | Enum (Ring, Cone, PointBurst) | AoE shape the Combo System uses for hit detection |
| `FinisherRadius` | Float | World-unit radius of the AoE (pre-computed: base_radius × 2.0) |
| `FinisherAngle` | Float? (nullable) | Half-angle of cone in degrees; Zhao Yun = 45.0, Guan Yu = null, Lu Bu = null. **Must not be read unless `FinisherShape == Cone`.** Never substitute 0.0 as a sentinel — a 0.0 angle produces a zero-width cone at runtime; null means "not applicable". |
| `FinisherDirectionMode` | Enum (Omnidirectional, ForwardFacing) | How the geometry is oriented at trigger time. Ring = Omnidirectional. Cone = ForwardFacing. PointBurst = Omnidirectional. |
| `FinisherDamage` | Float | Final damage value (pre-computed: base_attack × 2.5) |
| `BaseAttack` | Float | Auto-attack damage; source of truth here, consumed by Combat Core |
| `KnockbackForce` | Float? (nullable) | Radial knockback impulse magnitude (world units/second). Lu Bu = 5.0; Guan Yu = null; Zhao Yun = null. **Must not be read unless `FinisherShape == PointBurst`.** Applied by Combat Core to enemies inside the Finisher AoE, directed away from player position. **Elite gate (see enemy-definition.md):** For Iron Captain and Warlord targets, Combat Core must check `combo_counter` before applying knockback — knockback applies only when `combo_counter = 2`. At `combo_counter < 2`, suppress the knockback impulse for elite targets. Basic infantry have no knockback-resist. |
| `KnockbackRadius` | Float? (nullable) | Radius of knockback effect in world units. Lu Bu = 3.0u (matches FinisherRadius). null for others. **Must not be read unless `FinisherShape == PointBurst`.** Enemies outside this radius are not displaced even if hit. |

**Runtime geometry mapping — required implementation contract:** The `FinisherShape` enum drives the Combo System's hit detection method. The mapping is:
- `Ring` → full-circle overlap centered on player position, radius = `FinisherRadius`
- `Cone` → cone overlap in `FinisherDirectionMode` direction, half-angle = `FinisherAngle`, length = `FinisherRadius`
- `PointBurst` → full-circle overlap centered on player position, radius = `FinisherRadius` (hit detection shared with Ring) **plus** a radial knockback impulse (magnitude = `KnockbackForce`, radius = `KnockbackRadius`) applied to all hit enemies via Combat Core, directed away from the player position. PointBurst reuses Ring hit detection; the knockback post-effect is unique to PointBurst. Combat Core owns knockback application — the Combo System triggers it by passing `KnockbackForce` and `KnockbackRadius` from the general data.

**Key design principle:** `FinisherDamage` and `FinisherRadius` are pre-computed final values stored in the general data — not raw multiplier inputs. If the finisher formulas change, the change is applied at the data layer (General Characters), not in Combo System code. The Combo System is a consumer, not a formula evaluator.

**Forward dependency — Combo upgrade delta (must be defined in Combo System GDD):** This GDD assumes the following runtime formula when upgrades are active:

- `effective_finisher_damage = FinisherDamage × (1 + damage_delta_multiplier)`
- `effective_finisher_radius = FinisherRadius × (1 + radius_delta_multiplier)`

Both deltas start at 0.0 (no upgrades). The Combo System GDD must define the `FinisherUpgradeDelta` struct (or equivalent), specify how deltas accumulate across multiple upgrades, and cap their maximum values.

**Forward dependency — DirectionalInputBuffer (must be defined in Combo System GDD or a dedicated ADR):** Zhao Yun's cone direction fallback requires a named subsystem that records the player's last valid directional input with a timestamp. The Combo System GDD must define `DirectionalInputBuffer` with: (a) input recording granularity (per frame or per physics tick), (b) the 300ms window referenced here, and (c) the read interface the cone direction query calls at Finisher trigger time.

---

### Selection Screen Rules

1. One general per run. No mid-run swapping. No second selection screen once the run starts.
2. Selection is final when the player confirms and the run timer begins. To change generals, the player must quit the run.
3. All three generals available by default. No unlock gates in MVP.
4. Default pre-selection on first launch: Guan Yu (ring — most spatially forgiving). Player must still actively confirm; the pre-selection is a highlight, not a forced pick.
5. The selection screen must display a **geometry preview** for each general: a top-down silhouette showing the Finisher shape (ring / forward cone / burst circle) centered on a player icon. This is a requirement for the Run UI GDD — the shape must be communicated visually, not just through text.
6. No random or auto-selection. The game never selects a general on the player's behalf.
7. Generals do not change under any upgrade path in MVP. The Combo upgrade category enhances Finisher power and area (multipliers) but does not alter shape, direction mode, or general identity.
8. **Cancel path:** Pressing Cancel/Back (default: Escape / gamepad B-button) before confirming a general returns to the main menu. No confirmation dialog is shown. This is the only exit from the selection screen before a run starts.

**Selection screen flow:** Run start → Selection Screen (3 cards, Guan Yu highlighted) → Player navigates → Confirms → Run begins immediately (no loading screen). Selection screen inaccessible until next run start.

### Interactions with Other Systems

| System | Data In | Data Out | Interface Owner |
|--------|---------|----------|----------------|
| **Combo System** | Reads: FinisherShape, FinisherRadius, FinisherAngle, FinisherDirectionMode, FinisherDamage | None | General Characters owns the data contract |
| **Combat Core** | Reads: BaseAttack (auto-attack damage source of truth) | None | General Characters owns BaseAttack |
| **Run UI** | Reads: GeneralId, WeaponName, PlaystyleSummary + geometry preview spec | None | Run UI GDD must implement geometry preview widget per this spec |
| **Run Session Manager** | Reads: GeneralId (to initialize the correct general at run start) | None | Run Session Manager drives the selection screen flow |
| **VFX & Audio** | Reads: FinisherShape per general to use distinct VFX assets | None | VFX & Audio GDD must treat Ring and PointBurst as distinct visual effects despite identical geometry — they must not share assets |

## Formulas

All generals share the same formulas. These are inherited from the game concept and stored as pre-computed values in each general's data profile.

```
finisher_damage = base_attack × finisher_damage_multiplier
finisher_radius = base_radius × finisher_radius_multiplier
```

| Variable | Symbol | Type | Range | Description |
|----------|--------|------|-------|-------------|
| Base attack | `base_attack` | float | > 0; default: **10** | General's auto-attack damage; source of truth here |
| Base radius | `base_radius` | float | > 0; default: **1.5u** | General's base melee hit radius |
| Finisher damage multiplier | `finisher_damage_multiplier` | float | 1.5×–4.0×; default: **2.5×** | Finisher damage scaling (owned by game-concept.md tuning knobs) |
| Finisher radius multiplier | `finisher_radius_multiplier` | float | 1.5×–3.0×; default: **2.0×** | Finisher AoE scaling (owned by game-concept.md tuning knobs) |
| Finisher damage | `finisher_damage` | float | > 0 | Pre-computed and stored in general data profile |
| Finisher radius | `finisher_radius` | float | > 0 | Pre-computed and stored in general data profile |

**Output at defaults:** `finisher_damage = 10 × 2.5 = 25`, `finisher_radius = 1.5 × 2.0 = 3.0u`

**Upgrade interaction:** Combo-category upgrades apply modifiers at runtime (via Combo System). `FinisherDamage` and `FinisherRadius` in the general data profile are always the un-upgraded base values. The Combo System holds the delta from upgrades and computes the final value at Finisher trigger time.

**Floor clamp (finisher_radius):** `finisher_radius` must always exceed `base_radius`. The following clamp applies when pre-computing the stored value:

`finisher_radius = max(base_radius × finisher_radius_multiplier, base_radius + 0.5u)`

This ensures the Finisher AoE extends at least 0.5u beyond the auto-attack radius at any tuning setting. The clamp is inactive at default values (3.0u >> 2.0u) and only activates at extreme low-end combinations.

**Damage ceiling:** `finisher_damage` base value is bounded by the formula's input ranges — `base_attack` max (20) × `finisher_damage_multiplier` max (4.0) = **80 max**. Post-upgrade ceiling on `effective_finisher_damage` must be specified in the Combo System GDD.

## Edge Cases

- **If Zhao Yun's Finisher fires with no directional input in the past 0.3 seconds:** Cone extends toward the nearest enemy. "Nearest" is defined as the enemy with the minimum Euclidean distance (world units, XZ plane) to the player position at the moment of Finisher trigger. Search scope: all active enemies in the scene (no radius cap). Tie-break: if two or more enemies are equidistant within floating-point tolerance (0.001u), select the one with the lower entity index (the first result returned by the spatial query). **If no enemies are present:** extend in the last known directional input recorded before the 0.3-second window. If no directional input has ever been made (first seconds of run), extend in the default screen-forward direction: `Vector3(0, 0, 1)` (world positive-Z, the camera up-vector projected onto the XZ plane). This value is canonical here; Combat Core must respect the same convention for directional consistency.

- **If the player presses parry at the same frame Zhao Yun's Finisher fires:** In a same-FixedUpdate parry + Finisher event, parry resolves first; Finisher samples direction from the `DirectionalInputBuffer` state before the parry input is processed. A parry input is not a directional input for cone orientation purposes and does not update the buffer.

- **If a Combo upgrade increases `finisher_radius_multiplier` above 2.0×:** The Combo System applies the upgrade delta at runtime on top of the general's stored `FinisherRadius`. `FinisherRadius` in the general data is always the un-upgraded base value (3.0u at defaults). Upgrades never write back to the general data profile.

- **If the player confirms a general and the run starts, then returns to the selection screen via a game crash or quit:** On retry, the selection screen re-presents all three generals with Guan Yu pre-selected (or last-played general in post-MVP). The previously selected general is not remembered within an interrupted run.

- **If all three generals are somehow unavailable (data load failure):** Undefined — this should not occur since all three are always unlocked. If it occurs, treat as a critical error and show an error state rather than defaulting to a phantom general. Document as a required error state in the Run Session Manager GDD.

## Dependencies

| System | Direction | Nature | Interface |
|--------|-----------|--------|-----------|
| **Combo System** | General Characters → Combo | Hard | Combo System reads FinisherShape, FinisherRadius, FinisherAngle, FinisherDirectionMode, FinisherDamage at Finisher trigger. General Characters is the authoritative source. |
| **Combat Core** | General Characters → Combat Core | Hard | Combat Core reads `BaseAttack` as the auto-attack damage source of truth. Combat Core also owns knockback application: when `FinisherShape == PointBurst`, the Combo System passes `KnockbackForce` and `KnockbackRadius` to Combat Core, which applies the radial impulse to all enemies in radius. General Characters defines screen-forward as `Vector3(0, 0, 1)` (AC-09 absolute fallback); Combat Core must respect the same convention for directional consistency. |
| **Run UI** | General Characters → Run UI | Hard | Run UI reads GeneralId, WeaponName, PlaystyleSummary, and geometry shape for the selection screen. Must implement geometry preview widget per spec in this GDD. |
| **Run Session Manager** | General Characters → Run Session Manager | Hard | Run Session Manager reads GeneralId to initialize the correct general at run start. Drives selection screen flow. |
| **VFX & Audio** | General Characters → VFX & Audio | Hard | VFX & Audio must treat Ring (Guan Yu) and PointBurst (Lu Bu) as distinct visual effects — they must not share assets despite identical geometry at MVP. Zhao Yun's Cone has a unique directional VFX. |
| **DirectionalInputBuffer** | General Characters → Combo System (via DirectionalInputBuffer) | Hard | Zhao Yun's cone direction fallback depends on a named `DirectionalInputBuffer` subsystem. Must be defined in the Combo System GDD or a dedicated ADR. General Characters mandates its existence; it does not implement it. |

## Tuning Knobs

| Knob | Default | Safe Range | Affects |
|------|---------|------------|---------|
| `base_attack` | 10 | 5–20 | All auto-attack and Finisher damage; change this, not the multiplier, for global power level tuning |
| `base_radius` | 1.5u | 0.8–2.5u | All melee range and Finisher AoE size; below 0.8u feels like you're missing enemies at your feet |
| `finisher_damage_multiplier` | 2.5× | 1.5×–4.0× | Owned by game-concept.md tuning knobs — change there, not here |
| `finisher_radius_multiplier` | 2.0× | 1.5×–3.0× | Owned by game-concept.md tuning knobs — change there, not here |
| `zhao_yun_cone_half_angle_deg` | 45° | 20°–70° | Below 20° requires precise aim; above 70° approaches ring behavior and removes skill expression |
| `zhao_yun_input_direction_window_ms` | 300ms | 100–600ms | How recent the directional input must be to count for cone orientation; below 100ms feels broken; above 600ms feels laggy |

| `lu_bu_knockback_force` | 5.0 | 2.0–10.0 (world units/sec) | Lu Bu PointBurst knockback impulse magnitude; below 2.0 is imperceptible; above 10.0 sends enemies off camera |
| `lu_bu_knockback_radius` | 3.0u | 1.5–4.0u | Radius of Lu Bu's knockback effect; set to match `finisher_radius` by default; below 1.5u fails to displace enemies hit at ring edge |

Note: `base_attack` and `base_radius` are shared across all generals. `lu_bu_knockback_force` and `lu_bu_knockback_radius` are Lu Bu-specific. All knockback tuning should happen via playtest — initial defaults are estimates.

## Visual/Audio Requirements

*Full VFX and audio specs via `/asset-spec system:general-characters` after art bible is approved.*

**Finisher VFX requirements by general:**

| General | Finisher Shape | VFX Description | Must Not Share Assets With |
|---------|---------------|-----------------|--------------------------|
| Guan Yu | Ring (spinning sweep) | Wide horizontal sweep radiating outward from player. Read: a spinning blade ring. Warm gold/red palette per Three Kingdoms general visual identity. | Lu Bu's PointBurst |
| Zhao Yun | Cone (forward thrust) | Directional burst extending forward from player. Read: a lance/spear thrust piercing through. Cool silver/blue palette to differentiate from Guan Yu. Cone shape must remain legible at 200 enemies. | — |
| Lu Bu | PointBurst (ground slam + knockback) | Rising ground-slam shockwave from player's feet, followed by enemies visibly displaced outward. Read: the weapon drives into the earth, shockwave radiates, enemies scatter. Earth/dark red palette. The axis of origin is vertical (downward impact), not horizontal. The VFX must communicate displacement — enemies should appear pushed, not just damaged. | Guan Yu's Ring |

**Critical:** Guan Yu's Ring and Lu Bu's PointBurst have identical AoE geometry but MUST use different VFX assets and different audio. They must feel like different moves. If a player sees both in the same session and cannot tell them apart, the VFX pass has failed.

**Selection screen visual:**
- Each general card must include a top-down silhouette showing the Finisher shape centered on a player icon
- Ring: full circle (solid border, clean sweep). Cone: forward-facing wedge (90° arc). PointBurst: circle with radiating outward arrows — solid border with outward-pointing displacement marks to communicate the knockback push effect, visually distinct from Ring's clean sweep.
- All three previews at the same scale for fair comparison.

**Audio:**
- Each general must have a distinct Finisher audio signature. Three Kingdoms flavor: Guan Yu = thunderous sweep (resonant low frequency); Zhao Yun = sharp piercing rush; Lu Bu = ground impact with deep shockwave rumble.
- Selection screen: general-specific ambient sound or weapon idle sound when card is highlighted (short, loopable).

## Acceptance Criteria

| # | Criterion | Story Type | Gate |
|---|-----------|------------|------|
| AC-01 | **GIVEN** the run starts, **WHEN** the selection screen appears, **THEN** the screen displays exactly three `GeneralCard` UI elements (GuanYu, ZhaoYun, LuBu) and the GuanYu card's `IsSelected` state is `true`. **Pass condition:** assert card count == 3; `GeneralCard[GuanYu].IsSelected == true`; `GeneralCard[ZhaoYun].IsSelected == false`; `GeneralCard[LuBu].IsSelected == false`. | Logic | BLOCKING |
| AC-02 | **GIVEN** Guan Yu is selected and the 3rd combo hit fires, **WHEN** the Finisher resolves, **THEN** hit detection uses a full-circle overlap with radius 3.0u centered on the player position. | Logic | BLOCKING |
| AC-03 | **GIVEN** Zhao Yun is selected and the player recorded a directional input of Vector2(1, 0) (right, XZ plane) at t=0 and no input since, **WHEN** a Finisher fires at t=0.25s (within the 0.3s window), **THEN** the cone overlap query uses: origin = player world position, forward axis = `Vector3(1, 0, 0)`, half-angle = 45°, length = 3.0u. **Pass condition:** all four parameters match to within 0.001 tolerance; Y component of forward axis must equal 0. | Logic | BLOCKING |
| AC-04 | **GIVEN** Zhao Yun is selected, no directional input received in the past 0.3 seconds, player at `(0, 0, 0)`, exactly one enemy at `(4, 0, 0)`, **WHEN** a Finisher fires, **THEN** cone forward axis = `Vector3(1, 0, 0)`. **Pass condition:** cone forward axis matches `(1, 0, 0)` to within 0.001 tolerance. **Tie-break sub-case:** GIVEN player at `(0, 0, 0)`, enemy A (entity index=0) at `(3, 0, 4)` [XZ distance = 5.0u], enemy B (entity index=1) at `(-3, 0, 4)` [XZ distance = 5.0u], **THEN** cone forward axis = `Vector3(0.6, 0, 0.8)` (normalize toward enemy A). **Pass condition:** cone forward axis matches `Vector3(0.6, 0, 0.8)` to within 0.001 tolerance. | Logic | BLOCKING |
| AC-05 | **GIVEN** Lu Bu is selected and the 3rd combo hit fires, **WHEN** the Finisher resolves, **THEN** (1) hit detection uses a full-circle overlap with radius 3.0u centered on the player position, AND (2) a radial knockback impulse (magnitude = `KnockbackForce` = 5.0) is applied to all enemies inside `KnockbackRadius` = 3.0u, directed away from the player position. **Pass condition:** for a test enemy at `(2, 0, 0)` with player at origin — after Finisher, enemy velocity delta = `Vector3(5.0, 0, 0)` (normalized direction × KnockbackForce) to within 0.001 tolerance. | Logic | BLOCKING |
| AC-06 | **GIVEN** a `GeneralData` profile initialized directly (no Combo System required) with `damage_delta_multiplier = 0.0` and `radius_delta_multiplier = 0.0`, **WHEN** a Finisher fires, **THEN** `effective_finisher_damage = 25` and `effective_finisher_radius = 3.0u`. **Pass condition:** assert both values to within 0.001 tolerance. Test setup must not depend on Combo System state — initialize the delta values in the test fixture. | Logic | BLOCKING |
| AC-07 | **GIVEN** a general is confirmed and the run begins, **WHEN** the player fires any of the selection screen input actions (`[Confirm]`, `[NavigateLeft]`, `[NavigateRight]`, `[Cancel/Back]` as bound in the Input Action Asset), **THEN** the selection screen root `GameObject.activeSelf == false` and no scene load is triggered. **Pass condition:** for each input action listed — assert `activeSelf == false` before and after firing the action; assert no `SceneManager.LoadScene` call is made. | Logic | BLOCKING |
| AC-08 | **GIVEN** Guan Yu and Lu Bu both fire Finishers in the same session, **WHEN** each Finisher VFX plays, **THEN** the effects use separate VFX prefab assets (distinct asset GUIDs — not shared). **Pass condition:** assert that the VFX asset reference on Guan Yu's `FinisherVFX` component and Lu Bu's `FinisherVFX` component point to different asset GUIDs. Lead visual sign-off also required. | Visual | BLOCKING |
| AC-09 | **GIVEN** Zhao Yun is selected, no enemies are present in the scene, and no directional input has ever been made, **WHEN** a Finisher fires, **THEN** the cone extends in the absolute fallback direction `Vector3(0, 0, 1)` (world positive-Z). **Pass condition:** cone forward axis matches `(0, 0, 1)` to within 0.001 tolerance. **Secondary case (expired input + no enemies):** GIVEN the last directional input was `Vector2(−1, 0)` at session t=0, no enemies present, and Finisher fires at session t=0.5s (0.5s > 0.3s window — input is expired), **THEN** cone forward axis = `Vector3(−1, 0, 0)` (last known pre-window input). **Pass condition:** cone forward axis matches `(−1, 0, 0)` to within 0.001 tolerance. Note: t=0 is session start (scene load). | Logic | BLOCKING |

## Open Questions

| # | Question | Owner | Resolution Path |
|---|----------|-------|----------------|
| OQ-01 | ~~Lu Bu and Guan Yu have identical AoE geometry at MVP. Does Lu Bu feel meaningfully different in play, or does he feel like a reskin?~~ | **Resolved 2026-05-15** | Knockback promoted to MVP scope. Lu Bu's PointBurst now applies radial knockback to all hit enemies. Decision made before playtest based on design-review finding that identical mechanics + distinct Player Fantasy = false affordance. Validate knockback force feel in prototype. |
| OQ-02 | Should Zhao Yun's cone direction use the player's character facing OR the last directional input? | **Resolved 2026-05-15** | Last directional input (not character facing). Validate in prototype — if character facing effectively tracks nearest enemy and overrides player intent, revisit. |
| OQ-03 | Is a 45° half-angle (90° total arc) the right width for Zhao Yun's cone? | Prototype | Prototype default. Test: can a skilled player consistently hit a targeted group? If whiffing too much, widen to 55°. If too easy, narrow to 35°. |
| OQ-04 | ~~Post-MVP mechanical distinguisher for Lu Bu.~~ | **Resolved 2026-05-15** | Knockback (option B) promoted to MVP. Post-MVP evolution: consider (A) armor-break modifier stacking with knockback, or (B) knockback-based set-up combos (knock enemies into walls/each other). Decide after prototype validates knockback feel. |
