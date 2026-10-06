# Warlock validation and acceptance plan

Status: pre-implementation acceptance plan, 2026-10-06. No evidence exists yet; every gate in the [status ledger](#status-ledger) is not started. Rules come from [Combat](COMBAT.md) and [Demons](DEMONS.md), primitives from [Gaps](GAPS.md), engine facts from [Native baseline](NATIVE-BASELINE.md), and art acceptance from [Art direction](ART-DIRECTION.md). Item names, bands, and item levels come from [Equipment](EQUIPMENT.md) and [Artifacts](ARTIFACTS.md).

## Evidence tiers

Each tier is reported separately. A lower tier never satisfies a higher one, and a build or registration log never satisfies a gameplay gate.

| Tier | Code | What counts | What it cannot prove |
| --- | --- | --- | --- |
| Offline, game-free | O | Pure rule-state suites, native-boundary signature pins, package and art validators, schema and helper checks, CI runs | Native hooks firing, presentation, balance, save behavior |
| Isolated native observation | N | Read-only decompile confirmations; bridge-prepared fixtures in an isolated game copy (forced equipment, controlled enemies, supplied setup) with each fixture labeled | Ordinary acquisition, natural encounters, balance |
| Live single-player | L | Native play of an exact pinned binary in an isolated copy, actions committed through native input, outcomes observed in the game | Co-op, other platforms, long-run balance |
| Co-op | C | Host and client runs with matching content, recording roles and outcomes | Nothing is claimed until it is run |

Live rules for tiers N and L, from [AGENTS.md](../../AGENTS.md) and the [in-game smoke skill](../../.agents/skills/ingame-smoke/SKILL.md):

- Launch only with `FTK_AGENT_BRIDGE=1` and `FTK_AGENT_BACKGROUND=1`, return focus to the previous app, and never activate, raise, or refocus the game window. Add `FTK_AGENT_INPUT_ISOLATED=1` for unattended campaigns and require `inputIsolated` and `hardwareInputSuppressed` before relying on it ([Background mode](../../harness/README.md#background-mode)).
- Record the framework DLL and package hashes, game build, save namespace, party, levels, equipment, enemy composition, and every outcome including failures.
- Never touch the normal installation, other worktrees' game processes, or personal saves. Restore temporary configuration afterward.
- Bridge setup and observation are not gameplay. Distinguish fixture grants from ordinary acquisition, and supplied outcomes from native damage calculation.

## Offline gates

| Gate | Content | Phase |
| --- | --- | --- |
| P1 suite (`SelfHealthCost`) | Rounding and minimum, refusal at and below threshold at button creation and before commit, cancel costs nothing, exactly-once charge by committed-action receipt, peer replay never re-charges, refund only on own primary-target defeat, refund cap, rollback on reload, cost-modifier hook order | 1; Aoe cases in 2; modifier cases in 3 |
| P2 suite (`EnemyAfflictions`) | Tick count, interval, and totals per tier; refresh and ownership transfer; coexistence with burn, bleed, and curses; end at death and combat end; harvest once per enemy; harvest owner alive; native-boundary signature pins | 2 |
| P3 suite (`EquipmentSets`, extended) | Generic slots and 2 and 4 thresholds; class-gated capability flags; Guardian sets unchanged, proven by existing EquipmentSets and SavedSets suites | 3 |
| P4 suite (`BoundFamiliars`) | Each closed trigger and effect; per-combat caps and resets; familiar damage never a direct hit; one familiar maximum | 3 |
| Content schema | `ContentEntry` keys and loader wiring for `selfHealthCost`, `affliction`, set profiles, and familiar capabilities; bounds rejected with clear errors | Per primitive |
| Launcher helper allowlist | New keys accepted by the helper and rejected when malformed; `go test ./...` passes | Per primitive |
| Package validator | A `validate_warlock.py` beside a `build_warlock.py`, checking every entry, deterministic IDs, class-gated actions, Pact costs and multipliers against [Combat](COMBAT.md), tome band to burn and affliction tier, no native Death row, no hard-coded enum integers, asset hashes, and package-relative paths | Each phase |
| Art validators | Campaign validators for exports, source manifests, provenance, and display objects per [Art direction](ART-DIRECTION.md) | Each phase |
| CI loop inclusion | Each new suite is added to the game-free loop in `.github/workflows/ci.yml` in the same change that creates it. `EquipmentSets` is not in the loop today and must join it with P3 | Per primitive |
| Proportional checks | Release build, PlayerMods, HotReloadPackages, HotReloadResources, PackageModels, ItemApparel (phase 2 on), marketplace catalog validation, link check, `git diff --check` | Each phase |

## Native confirmations before code

These are tier N read-only checks from [Native baseline](NATIVE-BASELINE.md).

| Check | Blocks |
| --- | --- |
| 1. Spellbook handedness and slot override behavior | Phase 1 tome templates and grip stations |
| 2. Synchronized self-HP mutation and 1 HP side effects | P1 |
| 3. Defeat field on `DummyDamageInfo` | Grave Bargain |
| 4. `UpdateProficiency` ticks every category on enemies | P2 |
| 5. Enemies with `m_ImmuneFire` and `m_ImmuneBleed` | Fire-immunity fixtures, balance encounter choice |
| 6. Mounting a model on a player avatar without side effects | P5 spike |

## Phase 1 scenarios

Phase 1 covers the class, Shadow's Due, Grave Bargain, Umbral Bolt, Hexfire, Cinderbrand, Hollow Fright, curses, Siphon Soul, four tomes, and two lanterns.

### Shadow's Due and Grave Bargain

| ID | Scenario | Expected | Tiers |
| --- | --- | --- | --- |
| SD-1 | Hexfire at max HP 37, 40, and 9 | Costs `ceil(3.7)=4`, `4`, and `max(2, ceil(0.9))=2`; live check uses an ordinary max HP | O, L |
| SD-2 | Cinderbrand and Hollow Fright at small max HP | Minimum 1 applies when the percentage rounds below it | O |
| SD-3 | Current HP equal to cost, and one below | Refused with a message in both cases; no HP change, turn not consumed | O, L |
| SD-4 | Current HP one above cost | Commits; Warlock ends at 1 HP and is alive with no death or resist-death side effect | O, N, L |
| SD-5 | Target selected, then cancelled | No cost | O, L |
| SD-6 | One committed Pact action | Exactly one "Shadow's Due -N"; HP drops once; peer replay applies nothing | O, L; C later |
| SD-7 | Miss, partial roll, non-lethal hit, fire-immune target | Cost still paid | O, L |
| SD-8 | Warlock is Guarded by a Paladin, has armor, resistance, and evasion | Cost unchanged; no on-hit effects fire | O, L |
| SD-9 | Warlock healed later in the turn | No refund | O, L |
| SD-10 | Warlock poisoned | Poison and cost both apply; refusal still prevents self-defeat by cost | L |
| GB-1 | Hexfire direct hit defeats its primary target | Refund equals the charged amount; "Debt repaid +N" | O, L |
| GB-2 | Refund would exceed max HP | Capped at max HP | O |
| GB-3 | Burn tick, ally attack, or later action defeats the target | No refund | O, L |
| GB-4 | Pact action does not defeat the target | No refund | O, L |
| GB-5 | Siphon Soul or Umbral Bolt defeats the target | No refund; neither is a Pact action | O, L |
| GB-6 | Refund read source | Authoritative defeat result, never animation state; one refund per action | O |

### Grimoire actions and burn

| ID | Scenario | Expected | Tiers |
| --- | --- | --- | --- |
| GR-1 | Umbral Bolt and Hexfire perfect hits on the same enemy | Hexfire direct damage is 1.35x Umbral Bolt before native rounding and mitigation | O, L |
| GR-2 | Cinderbrand on a band 1 or 2 tome, positive damage | 0.6x hit; burn of 6 ticks of 2 at native tick rate | N, L |
| GR-3 | Cinderbrand dealing zero damage | No burn applied | N |
| GR-4 | Cinderbrand on a fire-immune enemy | Direct hit lands, no burn, cost paid | N, L |
| GR-5 | Reapply burn on a burning enemy | Record replaced, never stacked | N, L |
| GR-6 | Burn ticks | Flat, ignores armor, ends with enemy death or combat end | N, L |

### Hollow Fright, curses, and Siphon Soul

| ID | Scenario | Expected | Tiers |
| --- | --- | --- | --- |
| HF-1 | Hollow Fright on band 1 tome | Action absent (band 2 and later only) | O, L |
| HF-2 | Perfect roll, repeated trials | 0.5x hit; Daze applies in roughly half; both outcomes observed | N, L |
| HF-3 | Non-perfect roll | No Daze | N, L |
| HF-4 | Stun-immune target | No Daze; hit and cost still apply | N |
| CU-1 | Each phase 1 curse, perfect roll | Native debuff applies with no damaging hit (Harmless); native icon and arrow shown | N, L |
| CU-2 | Non-perfect roll | No curse | N, L |
| CU-3 | Same curse reapplied | Duration refreshes; no stacking | N |
| CU-4 | Two different curses on one enemy | Both coexist | N |
| CU-5 | Native Debuff strip on a cursed enemy | Curse remains | N |
| CU-6 | Curse values and durations | Match [Combat](COMBAT.md) section 6 for the tome's tier | O, N |
| SS-1 | Siphon Soul damaging hit | Heal `round(directHit * 0.5)`, capped at max HP | O, N, L |
| SS-2 | Miss or fully absorbed hit | No heal | N |

### Ownership, equipment, and persistence

| ID | Scenario | Expected | Tiers |
| --- | --- | --- | --- |
| OW-1 | New Warlock | Stats and Focus match [Design](DESIGN.md); Refocus disabled; Moth-Eaten Grimoire and Gourdlight Lantern present; native armor worn | O, L |
| OW-2 | Non-Warlock holds a Warlock tome | Umbral Bolt only; no Pact action, curse, or passive | O, L |
| OW-3 | Warlock holds a native tome | Native actions only | L |
| OW-4 | Tome swapped mid-combat to another path or a non-tome | No stale action usable; button creation and commit both re-check | O, L |
| OW-5 | Lantern equipped and unequipped | Declared stats only; native appearance unchanged | L |
| PS-1 | Save, exit, and resume between fights | Equipment retained; no Warlock state required; next fight behaves normally | L |
| PS-2 | Mid-combat save and resume | Native records restore; no duplicate cost or refund; outcome recorded either way | L |
| PS-3 | Package disabled with a saved Warlock | Behavior recorded and disclosed | L |

### Party interactions

| ID | Scenario | Expected | Tiers |
| --- | --- | --- | --- |
| IX-1 | Warlock direct hit on a Thief's target | Creates a Thief opening per Thief rules | L |
| IX-2 | Burn tick on a Thief's target | No opening | L |
| IX-3 | Paladin Guards the Warlock, then the Warlock casts a Pact action | Cost unchanged; Guard still halves the next enemy direct hit | L |
| IX-4 | Divine Intervention and Shadow's Due | Divine Intervention never triggers on the cost | O, L |
| IX-5 | Herbalist Party Heal or Paladin Mercy on the Warlock | Normal healing; no interaction with refunds | L |

## Phase 2 scenarios

Phase 2 adds Cinderstorm, Blight, Rot, Soul Harvest, enemy icons, tome bands 3 and 4, apparel, and artifact tomes.

| ID | Scenario | Expected | Tiers |
| --- | --- | --- | --- |
| CS-1 | Cinderstorm into three enemies | One 12% charge; 0.7x hit and burn on each enemy hit | O, L |
| CS-2 | Cinderstorm kills a non-primary enemy only | No refund | O, L |
| CS-3 | Cinderstorm kills the primary target | Refund of the single charge | O, L |
| BR-1 | Burn on band 3 and band 4 tomes | 6 ticks of 4, and 6 ticks of 6 | O, N |
| AF-1 | Blight perfect roll at each tier | 4 ticks every 1.25 units of 4, 7, or 10; Harmless application | O, N, L |
| AF-2 | Blight non-perfect roll | No record; cost paid | N |
| AF-3 | Rot at each tier, positive damage | 0.4x hit, 6 ticks every 2.0 units of 3, 5, or 7 | O, N, L |
| AF-4 | Rot hit dealing zero damage | No record | N |
| AF-5 | Reapply Blight or Rot | Refresh; no stacking | O, N |
| AF-6 | Second Warlock applies Blight to the same enemy | Record replaced; ownership transfers to the newer caster | O, L |
| AF-7 | Blight, Rot, burn, bleed, and a curse on one enemy | All coexist; each ticks or expires on its own | O, N |
| AF-8 | Afflictions at enemy death, combat end, and next encounter | End; nothing persists | O, L |
| AF-9 | Boss target | Affected normally | L |
| AF-10 | Affliction tick kills an enemy | Native owner-sent death path; no refund | N, L |
| SH-1 | Afflicted enemy killed by an ally | Owner regains `max(1, ceil(maxHP * 5 / 100))`; "Soul Harvest +N" | O, L |
| SH-2 | Enemy carrying both Blight and Rot dies | One payout | O, L |
| SH-3 | Owner dead or absent when the enemy dies | No payout | O, L |
| SH-4 | Payout at or near max HP | Capped | O |
| SH-5 | Enemy dies from its own affliction tick | Payout once | O, L |
| IC-1 | Enemy Blight and Rot HUD icons | Appear on application, persist on refresh, clear on expiry, death, and encounter exit; hover text correct; no duplicates after retargeting | N, L |
| IX-6 | Blight or Rot tick on a Thief's target | No opening | L |
| PS-4 | Mid-combat save and resume with live afflictions | Survival or loss of framework records recorded; no double tick or double harvest | L |
| EQ-1 | Apparel bands 1 to 4 and artifact tomes | Stats and actions match [Equipment](EQUIPMENT.md) and [Artifacts](ARTIFACTS.md); art per [Art direction](ART-DIRECTION.md) | O, L |

## Phase 3 scenarios

| ID | Scenario | Expected | Tiers |
| --- | --- | --- | --- |
| ST-1 | One, two, three, and four pieces of one set | Minor bonus at 2; demon bound at 4 only | O, L |
| ST-2 | Non-Warlock wears a complete set | Minor bonus only; no familiar visible or active | O, L |
| ST-3 | Two pieces each of two different sets | Both minor bonuses; no familiar | O, L |
| ST-5 | Warlock defeated, then revived, with a bound familiar | Familiar inactive while defeated; resumes on revival with counters unchanged | O, L |
| ST-6 | Ally commits an action while the Warlock has a familiar | No familiar trigger | O |
| ST-4 | Piece removed or swapped mid-combat | Familiar unbound immediately; per-combat counters do not refresh on re-equip | O, L |
| FM-1 | Ember Toss: positive direct hit on an enemy burning after the hit | 6 flat to the primary target; "Ember 6"; native fire hit effect | O, L |
| FM-2 | Ember Toss on Cinderstorm | Primary target only; once per action | O, L |
| FM-3 | Ember Toss on a fire-immune or non-burning enemy, or a zero-damage hit | No ember | O, N |
| FM-4 | Ember kills the target | No Grave Bargain; Soul Harvest still pays if afflicted; no Thief opening | O, L |
| FM-5 | Imp's Tithe | Cinderstorm cost uses 9% | O, L |
| FM-6 | Devour on a successful curse | Enemy positive buffs stripped via native Debuff; "Devoured" | O, L |
| FM-7 | Devour on a failed curse | Nothing | O |
| FM-8 | Feast on afflicted deaths | +1 Focus on the first two; none on the third; capacity respected; resets next combat | O, L |
| FM-9 | Shared Burden on each Pact action | `max(m, ceil(baseCost * 3 / 4))`; Grave Bargain refunds the reduced amount | O, L |
| FM-10 | Stand Between | First enemy direct attack that would leave the Warlock below 25% is halved once; later hits unaffected; resets next combat | O, L |
| FM-11 | Stand Between on a hit already reduced by Paladin Guard | Stand Between does nothing and is not spent; threshold judged after armor, resistance, and Guard | O, L |
| FM-12 | Damage over time or an above-threshold hit | Does not consume Stand Between | O |
| FM-13 | Familiar visibility | Visible only with a complete set on a Warlock; placement and motion per [Art direction](ART-DIRECTION.md) after the P5 spike | N, L |
| FM-14 | Save and resume with a bound familiar | Only equipment is saved; familiar returns; counters reset at the next combat | L |

## Balance trial plan

Compare the Warlock against Scholar and Herbalist at the same player level, item band, difficulty, and consumable budget. Phase 1 trials use levels 0 and 2 with bands 1 and 2; later phases add levels 4, 6, and 8.

| Encounter | Purpose |
| --- | --- |
| No-injury fight | Ordinary enemies with no incoming damage advantage; measures pure output per HP spent |
| Boss | Long single-target fight; the DESIGN stress case of repeated Hexfire with no refund |
| Swarm | Three weak enemies; Grave Bargain frequency, and Cinderstorm from phase 2 |
| Solo survivor | Warlock alone after allies fall; checks self-drain risk and refusal behavior |

Record per run: damage dealt by source (direct, burn, affliction, familiar), HP spent on Shadow's Due, HP refunded by Grave Bargain, HP harvested, HP siphoned, incoming damage, Focus spent and gained, turns to victory, incapacitations, and outcome. Run both paths and record encounter enemies with their fire immunity.

Guardrails from [Design](DESIGN.md): the Warlock must not exceed Thief and Paladin sustained output in the boss case, and must stay meaningfully below Scholar in utility and initiative. Treat any numeric target beyond these as proposed until approved.

| Order | Lever | Trigger |
| --- | --- | --- |
| 1 | Hexfire 1.35x at 10% becomes 1.25x at 12% | Hexfire boss output exceeds Thief or Paladin sustained output |
| 2 | Grave Bargain or Siphon Soul healing reviewed | HP spent is routinely fully recovered in swarms |
| 3 | Minimum costs reviewed | Low-level costs feel free or prohibitive |

Levers 2 and 3 are named contingencies, not current rules. Record each tuning change with its trigger evidence and rerun the affected trials.

## Acquisition checks

| ID | Check | Tiers |
| --- | --- | --- |
| AQ-1 | Each item appears in native shops and loot only at its declared item level in [Equipment](EQUIPMENT.md), and not below it | L |
| AQ-2 | Native progression tiers map item levels 0, 1, 2, 3, 4, 4 to expected party levels 0, 2, 4, 6, 8, 9; record campaign stage and shop pool, not hero level alone | N, L |
| AQ-3 | Purchase and sale of each family through the native vendor, with gold, stock, and backpack deltas | L |
| AQ-4 | Ordinary loot observed per family, distinct from forced fixtures | L |
| AQ-5 | No stale unlock, DLC gate, or stock flood; non-Warlocks can buy and use items | O, L |

## Release and website gates

| Gate | Requirement |
| --- | --- |
| Package release | Pinned framework and package hashes, marketplace catalog validation, public download verified, native activation from the public catalog |
| Website class guide | A `website/src/content/docs/mods/warlock.mdx` guide matching published behavior; playtest labeled as a playtest |
| Item cards | Generated from a `warlock.json` projection of the published archive, never hand-edited |
| Sync allowlist | `website/scripts/sync-published.mjs` admits only Paladin and Thief today; add Warlock, its reviewed versions, and its new public fields (for example `selfHealthCost`, `affliction`, set and familiar keys) |
| Library art and catalog wording | Banner in `library-art.json`; version or "In Development" chip; media provenance recorded |
| Co-op wording | Every release, guide, and catalog entry states co-op is unverified until tier C evidence exists |
| Website checks | `npm ci`, `npm run build`, `npm test`, desktop and mobile screenshots, deployed pages and links verified |

## Status ledger

| Gate | Tier | Phase | Status |
| --- | --- | --- | --- |
| Native confirmations 1 to 3 | N | 1 | not started |
| Native confirmations 4 and 5 | N | 2 | not started |
| Native confirmation 6 and P5 spike | N | 3 | not started |
| P1 suite in CI | O | 1 | not started |
| P1 Aoe extension | O | 2 | not started |
| P2 suite in CI | O | 2 | not started |
| P3 suite and EquipmentSets in CI | O | 3 | not started |
| P4 suite in CI | O | 3 | not started |
| Content schema and helper allowlist | O | 1 to 3 | not started |
| Package validator | O | 1 to 3 | not started |
| Art validators and provenance | O | 1 to 3 | not started |
| Shadow's Due and Grave Bargain (SD, GB) | L | 1 | not started |
| Grimoire and burn (GR, BR) | L | 1, 2 | not started |
| Hollow Fright, curses, Siphon Soul (HF, CU, SS) | L | 1 | not started |
| Ownership and equipment (OW) | L | 1 | not started |
| Save and resume (PS) | L | 1 to 3 | not started |
| Party interactions (IX) | L | 1, 2 | not started |
| Cinderstorm (CS) | L | 2 | not started |
| Afflictions and Soul Harvest (AF, SH) | L | 2 | not started |
| Enemy affliction icons (IC) | L | 2 | not started |
| Sets and familiars (ST, FM) | L | 3 | not started |
| Art acceptance per phase | L | 1 to 3 | not started |
| Balance trials and tuning | L | 1 to 3 | not started |
| Acquisition (AQ) | L | 1 to 3 | not started |
| Co-op parity | C | All | not started |
| Release and public install | L | Each | not started |
| Website guide, cards, sync, wording | O | Each | not started |
