# Warlock gaps and delivery plan

Status: pre-implementation plan, 2026-10-06. This ledger records what the [Warlock design](DESIGN.md) needs beyond today's public framework, the contract for each missing primitive, and the delivery order. Capability claims cite the framework inventory taken on this date; engine facts cite [Native baseline](NATIVE-BASELINE.md).

## Coverage ledger

| Area | Current support | Needed | Phase |
| --- | --- | --- | --- |
| Class row, stats, skill flags | Exists: JSON `class` entry cloning `scholar`, explicit `skills` object | Content only | 1 |
| Class-gated tome actions | Exists: `weaponProficiencies` / `Content.AttachClassWeaponProficiencies`, shipped in framework 1.7.0 ([Combat proficiencies](../COMBAT-PROFICIENCIES.md)) | Content only | 1 |
| Damage multipliers, magic damage, perfect-roll procs | Exists: `damage`, `m_DmgTypeOverride`, `chancetoaffect`, `fullslots` on cloned rows | Content only | 1 |
| Burn (Cinderbrand) | Exists: clone native fire rows | Content only | 1 |
| Curses | Exists: clone native armor, resistance, speed, and attack debuff rows, Harmless | Content only; confirm Harmless application | 1 |
| Siphon Soul | Exists: clone native `magicDrain` | Content only | 1 |
| Hollow Fright Daze | Exists: clone native daze row | Content only | 1 |
| Equipment stat bonuses | Exists: `modifiers` (armor, resistance, vitality, speed, awareness, talent, focusCapacity, reflect) | Content only | 1 |
| **Shadow's Due and Grave Bargain** | Missing. Only native `m_Suicide` (all HP) | **P1** | 1 |
| Cinderstorm cost | Missing. Framework capabilities accept single-target only | **P1** Aoe extension | 2 |
| **Blight, Rot, Soul Harvest** | Missing. Native Curse and Disease do nothing to enemies; no framework enemy status | **P2** | 2 |
| Enemy affliction icons | Missing. Enemy HUD icon set is fixed | **P2** | 2 |
| **Demon set bonuses** | Partial. Guardian-only sets with a fixed six-slot layout | **P3** | 3 |
| **Bound familiars** | Missing. No companion concept in game or framework | **P4** | 3 |
| Familiar models on avatars | Unknown. Equipment model mounting exists; a free-floating familiar mount is unproven | **P5** feasibility spike | 3 |
| Apparel and accessory art | Process exists (Paladin, Thief) | 45 items of original art over three phases | 1 to 3 |

## Primitive contracts

Every primitive follows the Guardian and Thief pattern: a public `Content.*` method that validates the exact registered row, a `*Runtime.Register*` call, pure rule state with no Unity dependency, narrow Harmony patches wrapped to fail safely, hot-reload suspension, a JSON key in `ContentEntry` with loader wiring, a launcher helper allowlist entry, a package validator, and a game-free test suite added to the CI loop from day one.

### P1. Self health cost (`selfHealthCost`)

```json
"selfHealthCost": { "percentOfMax": 10, "minimum": 2, "refundOnKill": true }
```

- Declared on a registered custom proficiency. Bounds: percent 1 to 50, minimum 1 to 20.
- The encounter authority computes `max(minimum, ceil(maxHP * percent / 100))` from synchronized maximum HP at commit.
- Refuses commit when current HP is at or below the cost, both at native button creation and before commit.
- Charges once through the native synchronized health path, keyed to a committed-action receipt (the Thief receipt pattern). Peers replaying the outcome never re-apply it.
- Not damage: bypasses armor, resistance, evasion, reflection, Guard, Slip Away, and Divine Intervention.
- `refundOnKill` restores exactly the charged amount when the authoritative outcome defeats the primary target, capped at maximum HP.
- Phase 2 extends support to native `Aoe` actions: one charge per action; refund on primary target only.
- Exposes a cost-modifier hook for P4 (Shared Burden, Imp's Tithe), applied before the minimum.
- Game-free tests: rounding, minimum, refusal at and below threshold, cancel costs nothing, exactly-once charge, refund only on own primary-target defeat, cap at maximum HP, rollback on reload.

Estimated size: 250 to 400 lines plus tests, comparable to `resistanceDamageBonus`.

### P2. Enemy afflictions (`affliction`)

```json
"affliction": { "kind": "blight", "ticks": 4, "interval": 1.25, "damagePerTick": 4, "harvestPercent": 5 }
```

- A framework-owned `ProficiencyBase` subclass hosted through `BehaviorHost`, like `ThiefSlipAwayProficiency`, registered on a cloned row. Storage uses a native category (Curse for Blight, Disease for Rot) as the record key, so it coexists with burn, bleed, and native debuffs. No new enum integers.
- Overrides enemy application so the record is kept and ticks on enemies, ticking through the native timeline update with flat damage via the native secondary-damage path.
- Records the applying Warlock. Refresh replaces the record and transfers ownership.
- Soul Harvest: on authoritative enemy death, pays the owner once per enemy if a live affliction record exists.
- Enemy HUD: adds framework-owned Blight and Rot icons to `uiEachEnemyHud`, kept separate from native assets. This is the enemy-side counterpart of the player icon work in issue #269 and should share its approach.
- Ticks are flat and roll-free, so local ticking is deterministic; tick deaths use the native owner-sent death path.
- Game-free tests: tick count and totals, refresh and ownership transfer, coexistence with burn, harvest once per enemy, harvest owner must be alive, end at combat end. Native-boundary tests pin the hooked signatures.
- Requires decompile confirmation that `UpdateProficiency` ticks records of every category on enemies.
- Extension for the Blightbloom Folio artifact ([Artifacts](ARTIFACTS.md)): on authoritative death of a Blighted enemy, re-apply Blight to a deterministically chosen living neighbor. This is a new authority-sent application path for co-op and ships only with the artifact.

Estimated size: 500 to 800 lines plus tests, plus the enemy icon patch.

### P3. Generic equipment sets

- Extract `EquipmentSetState` from Guardian into a class-agnostic set primitive with declared slots (here hood, robe, boots, trinket) and piece thresholds (2 and 4).
- Profiles carry equipment modifiers and named capability flags. A capability flag can require a class (Warlock) so non-Warlocks get the minor bonus only.
- Guardian sets migrate onto it without behavior change, proven by the existing EquipmentSets and SavedSets suites.

### P4. Bound familiars

- A familiar is a named capability granted by a complete set to an eligible class. It has no dummy, turn, or slot.
- Triggers are a closed set: after own committed action resolves (with target, direct hit, and burning state), on own curse applied, on affliction-owned enemy death, on incoming direct attack (for Stand Between), and a cost modifier into P1.
- Effects are a closed set: flat damage to the action's primary target, native Debuff strip, Focus restore with a per-combat cap, incoming-hit halving with a per-combat cap, and P1 cost modifiers.
- Per-combat counters reset at combat end and live in pure state owned by the encounter authority.
- Familiar damage is never a direct hit.

### P5. Familiar model mount (feasibility spike first)

Prove that a static, equipment-bound original model can be mounted at the shoulder, at a root offset, and behind the back of native avatars in overworld and combat, with no change to native animation or vanilla prefabs. The result decides whether demons are visible in phase 3 or ship as effects and icons first.

## Order of work

1. Decompile confirmations in [Native baseline](NATIVE-BASELINE.md), open checks 1 to 3.
2. P1, single-target. Phase 1 content and art. Phase 1 release.
3. Decompile open checks 4 and 5. P2. P1 Aoe extension. Phase 2 content and art. Phase 2 release.
4. P5 spike, then P3, P4, demon models. Phase 3 content. Phase 3 release.

## Phase 1 schedule

| Window | Work |
| --- | --- |
| Oct 6 to 10 | Decompile checks 1 to 3; P1 with game-free suite |
| Oct 11 to 17 | Class entry, four tomes, two lanterns; icons and tome models |
| Oct 18 to 24 | Live single-player validation; balance trial against Scholar and Herbalist; tuning |
| Oct 25 to 29 | Package, validator, website class guide and item cards, release notes |
| Oct 30 to 31 | Buffer and release |

## Notes

- CI's game-free loop runs the Guardian and CombatProficiencies suites but not ThiefCombat, EquipmentSets, or ItemModifiers. Warlock suites must be added from the start.
- Website release work touches the published-data sync allowlist, item-guide mechanic text, a `mods/warlock` page, library art, and catalog wording; see [website guidance](../../website/AGENTS.md).
