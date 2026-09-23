# Thief evidence, balance model, and implementation gates

Status: implementation evidence, 2026-09-23. This separates offline checks, isolated native observations, and remaining release gates.

## Evidence reviewed

The installed original FTK assembly and serialized database were read without launching or changing the game. Exact type declarations and the relevant complete damage methods were inspected with ilspycmd; serialized rows were read using those declarations. No DLL or decompiled source is included in this design.

- Assembly-CSharp SHA-256: `94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`.
- sharedassets1.assets SHA-256: `e38abff2efd7ca8ef8ede4b92754d6b3a06c6498a774ee55d0d1ddbffba4750f`.
- Framework references: current `ContentEntry`, `AliasTable`, and [Guardian/equipment authoring](../GUARDIAN-AND-EQUIPMENT.md).
- Design benchmark: [Paladin design](../paladin/DESIGN.md), [implementation](../paladin/IMPLEMENTATION.md), and [artifact concepts](../paladin/LEGENDARY-CONCEPTS.md).
- Archetype reference: [official D&D Rogue overview](https://www.dndbeyond.com/classes/2190883-rogue). The FTK mechanics are original proposals, not a transcription of tabletop rules.

## Confirmed native facts

| Anchor | Finding | Design consequence |
| --- | --- | --- |
| `FTK_playerGameStart` and its serialized DB | The six class stats, starting Focus/gold, weapon, item array, skills, and native skin choices exist | Use class registration and explicit overrides; the stat table in DESIGN is grounded in native peers |
| `CharacterSkills` and serialized class rows | Hunter: Sneak, TrapProceed, EnergyBoost, CalledShot. Trapper: Ambush, TrapDisarm, CounterAttack. Treasure Hunter: MimicWhisper, FindTreasure | Thief combines selected exploration skills and gives up their other signatures; preserve their class niches |
| `FTK_weaponStats2` | Damage, damage gain, check count, and governing stat are separate fields | Override the dagger's stat and checks deliberately |
| `CharacterStats.GetWeaponMaxDamage` | Base damage plus player level times gain precedes other flat bonuses and native multipliers, then rounding | Base damage is not the final combat tooltip value |
| `DamageCalculator.StartEngageAttack` | Full-slot failure can disable proficiency success while retaining its damage coefficient | Explicitly specify partial damage, including for piercing actions |
| `DamageCalculator._generateAttackAttempt` | Armor ignore is set when the selected proficiency succeeds | A perfect-only native proficiency can support conditional bypass, subject to authored template verification |
| `DamageCalculator._calcDamage` | Fractional slot damage is rounded; critical bonus and native Frozen behavior precede defense subtraction; ignore-armor preserves negative defenses | Enhance one native hit and avoid a second damage/armor pass |
| `DummyAttackProperties` | Perfect player attacks against enemies set enemy evasion to zero | Perfect Sneak Attacks already receive evasion bypass; do not budget a second dodge-bypass perk |
| `FTK_itemRarityLevel.ID` | Native rarity includes Artifact; there is no separate Legendary rarity | All three special weapons use Artifact |
| `FTK_progressionTier` rows | Item levels 0, 1, 2, 3, 4, 4 correspond to expected party levels 0, 2, 4, 6, 8, 9 | Equipment bands use item-level eligibility, not character-level equip requirements |

Fresh native weapon comparisons, all physical and damage gain 1. Dual Knives and Dual Daggers occupy both hands and use native DLC `Jungle` (enum value 2). Exact paired animation, item ownership, and base-game compatibility need verification; the design does not clear DLC flags blindly:

| Native item | Base damage / checks / stat | Item levels | Relevant comparison |
| --- | --- | --- | --- |
| Rusty Knife | 9 / 1 / Vitality | 0 | Starter native blade uses a different stat and check structure |
| Goblin Knife | 11 / 1 / Speed | 1-2 | Native Speed knife precedent; special acquisition flags need review |
| Dual Knives | 15 / 2 / Speed | 1 | Native two-handed progression anchor |
| Dual Daggers | 20 / 2 / Speed | 2 | Native route to extend through endgame |
| Dagger | 15 / 1 / Vitality | 2-3 | The prototype's vanilla starting dagger does not match the proposed class primary stat |
| Short Bow | 8 / 2 / Awareness | 0 | Rooftop Bow trades more checks for a larger hit |
| Long Bow | 14 / 4 / Awareness | 1 | Early bow curve anchor |
| Great Bow | 18 / 4 / Awareness | 2 | Midgame bow curve anchor |
| Assassin Bow | 20 / 3 / Awareness | 3 | Fewer checks can rival larger nominal damage |
| Dragon Bow | 23 / 4 / Awareness | 3 | Veteran bow curve anchor |
| Royal Bow | 25 / 4 / Awareness | 4-6 | Ordinary endgame raw-damage anchor |
| Ancient Blade | 28 / 1 / Strength | 3-4 | Native Artifact with different stat/check budget |
| Ancient Bow | 22 / 1 / Awareness | 3-4 | Artifact rarity does not imply the largest base number |

Ancient Blade/Bow have ordinary drop and merchant flags disabled in the inspected data. Our proposed ordinary acquisition is explicit and cannot be inferred merely from copying their rarity. These comparisons do not include every native proficiency or passive item bonus, so they constrain the proposal without proving balance.

## Arithmetic model

For a plain, unarmored single-target attack with independent per-slot success probability `p` and `n` checks, expected fractional weapon damage is `p`. If an opening is available on every attempt, a +35% bonus on perfect results adds `0.35 * p^n`. This simplified model ignores rounding, criticals, enemy turns, native status effects, and partial-hit dodge. It describes the mechanic, not expected live DPS.

| Case | Perfect chance, no Focus | Expected damage / current weapon damage with an opening |
| --- | ---: | ---: |
| Early paired daggers at unmodified 78 Speed, 2 checks | 60.84% | 0.9929 |
| Late paired daggers at unmodified 78 Speed, 3 checks | 47.46% | 0.9461 |
| Bow at unmodified 72 Awareness, 4 checks | 26.87% | 0.8141 |
| Wayfarer bow at 81 Awareness, 4 checks | 43.05% | 0.9607 |

Twin Feint's exactly-one-failure chance is `2 * 0.78 * 0.22 = 34.32%` on two checks and `3 * 0.78^2 * 0.22 = 40.1544%` on three, before requiring positive damage. It grants a future opening, not immediate extra damage. The native two-check sets and extended Guild availability preserve a reliable alternative to later three-check sets.

For the same stat and no external modifiers, securing one of three dagger checks changes perfect chance from `0.78^3` to `0.78^2 = 60.84%`; securing one of four bow checks changes it from `0.72^4` to `0.72^3 = 37.32%`. These are probability examples under the stated model; native Focus UI/check arithmetic still needs an actual authored-action test.

A perfect normal attack followed by another perfect normal attack without openings deals 2.00 times current damage before armor. Standard Feint plus a prepared Sneak Attack deals 1.95. Locksmith Feint plus prepared Sneak Attack deals 2.15, buying a modest long-fight advantage for an endgame weapon with lower base damage. Slip Away plus Sneak Attack deals 1.35 across two turns, trading damage for survival. Armor can make a Feint fail to prepare, which is why a piercing alternative matters.

At player level 8, Nightglass Twins have current damage 38 before other bonuses. Against Armor 12, a perfect Sneak Attack produces approximately `51.3 - 12 = 39.3` before native rounding. Trailbreakers' current damage is 37; their perfect 0.85 Pierce deals 31.45 and bypasses positive armor. At Armor 24, Nightglass Twins fall to approximately 27.3 while that Pierce remains 31.45. This is the intended choice, not a universal best action. Native per-hit rounding and criticals change exact integer outcomes.

Artifact arithmetic and ordinary alternatives are specified in [Artifacts](ARTIFACTS.md). Focus refunds do not create net Focus. Nevertheless, recurring resource-neutral one-Focus attacks can materially improve long fights and must be measured, especially with other classes' Focus support.

## Implementation plan and remaining gates

The following sequence records the design-to-implementation plan. Steps 1-5 are implemented in the development worktree and covered by the game-free checks below. Step 6 remains a live validation and release gate.

1. **Confirm integration boundaries.** Inspect complete native turn, skipped-turn, attack commitment/result, actor identity, revival, combat exit, and supported network recovery paths. The rule design is complete; exact safe hooks are not yet verified. Reuse existing attack/damage receipts where their semantics match.
2. **Add a bounded Opportunist capability.** Opening contributors, first-turn entitlement, one attempt per own turn, Twin Feint, Prepared, and Slip Away form one small combat state model. Expose typed class and precision-weapon/action declarations through public Content APIs. No string callback or package-supplied C# execution is needed.
3. **Add the three explicit equipment effects.** Refund one spent Focus, once-per-combat opening multiplier, and one timed Evasion modifier. Their limits are fixed by this design. Do not build a general trigger language to implement three weapons.
4. **Extend ordinary item modifiers narrowly.** Existing package modifiers expose Armor, Resistance, Vitality, Speed, and Reflect. Awareness, Talent, maximum Focus, and timed Evasion require deliberate public API/schema support plus matching helper/runtime validation and truthful item descriptions. Verify native stat recomputation and capacity clamping before choosing hooks.
5. **Author and validate the complete package.** One class, 45 equipment items, and the finite action variants. Replace template skills with the intended set, explicitly clear unwanted inherited bonuses/gates, resolve deterministic IDs, and provide original assets. Do not activate the old sample thief to supply missing behavior.
6. **Prove gameplay and delivery.** Narrow state/math tests first, then native integration, ordinary acquisition, balance, visual coverage, save/mod lifecycle, and host/client parity. Production availability follows evidence; a catalog entry is not part of this paper task.

Weapon action visibility on non-Thieves must communicate that preparation needs Opportunist. The native two-handed `dualKnife` and `dualDagger` rows are deliberately included in precision eligibility without mutating their rows. Other native weapons, including all one-handed daggers, remain usable but do not accidentally acquire Sneak Attack. Do not register broad hooks based only on Speed or Awareness.

## Required behavior cases

| Area | Cases and acceptance |
| --- | --- |
| Opening | First-turn target; target acts; skipped target turn; different ally hit; self hit; several contributors; summon/revive; two Thieves with different eligibility. Markers and bonus agree. |
| Damage | Perfect, every partial slot count, zero slots, armor block, negative armor, critical, Frozen, enemy evasion, lethal hit. One damage event and one mitigation pass; excluded actions never trigger. |
| Twin Feint | Native and custom pairs; exactly one versus two failed checks; positive versus blocked/dodged damage; consumed token replaced after a near miss; duplicate two-blade animation events. One token, no extra hit. |
| Prepared | Successful and armored-out Feint; different next target; existing Open target; item/pass; weapon swap; skip; stun/death; new encounter. No stacking or indefinite deadline. |
| Slip Away | One enemy attack versus several; AoE; multiple direct hits; DoT; zero damage; Guard overlap; status on hit; revive. One full action, strongest reduction once, no renewed charge. |
| Artifacts | Focus 0/1/3 spent, full/currently changed capacity, duplicate notifications; Last Light partial failure, healed-to-full target, copy swaps and transfer; Evasion application, replacement, expiry and rebuild. |
| Class/gear | Every item resolves and grants only listed stats/actions; both paths at every tier, native pair compatibility, two-to-three-check transition, emergency one-handed fallback without precision benefits; non-Thief wielder; mixed outfits; starter inventory; all native appearance choices. |
| Economy | Normal shops and ordinary loot in every eligible band, rarity distribution, sale/purchase, no stale unlock/DLC gates, no stock flood. Fixture grants do not establish acquisition. |
| Persistence/network | New game, save/exit/resume, equipped and backpack items, package disabled/missing handling, two Thieves, host/client results, supported reconnect/host transition. No reset of spent state inside one encounter. |

## Balance playtest and tuning decisions

Compare Hunter, Trapper, Treasure Hunter, and Thief at comparable native item tiers and player levels 0, 2, 4, 6, and 8, using the same encounter, difficulty, and consumable budget. Include a Thief/Paladin/support party, two Thieves, and a lone surviving Thief. Run short normal fights and extended armored encounters. Use both ordinary weapons and each artifact; no artifact is required for the baseline class pass.

Record turns to victory, incoming HP loss, incapacitations, Focus spent/returned, opportunities available, Sneak Attacks attempted/successful, and actual damage. Assess exploration separately: route choices, failed checks, and Focus consumed. A class that wins combat by spending every resource may not outperform over a dungeon.

Initial acceptance targets are design guardrails: a supported Thief should have roughly 10-20% more single-target damage per own action than a comparable ordinary attacker in favorable opening sequences, while solo/no-opening sustained damage should remain within roughly 10% of a native peer. It should take visibly more punishment when enemies reach it. These are desired outcomes, not measured claims or universal per-enemy ratios.

If too strong, first lower Sneak Attack from 35% toward 25% or reduce the overtuned weapon's base damage; do not obscure it with hidden proc chances. If too weak, first inspect opening frequency and survivability before increasing all stats. If Skeleton Key displaces every other dagger in long fights, cap Borrowed Fortune to the first three refunds per combat; this is a named contingency, not an additional rule in the current design. If non-Thief users invalidate native weapons, tune base stats/actions without silently restricting equipment by class. The secondary endgame bows deliberately exceed the inspected Royal Bow base damage; this is a material balance risk to check before freezing their curve. One-handed plus shield is an emergency route, not a target build to bring to parity with paired daggers.

## Current verification boundary

Implementation status, 2026-09-23. The package contains one class, 45 equipment entries, and eight actions with 207 referenced assets. The dedicated Slip Away icon was reduced to 1024 pixels to satisfy the framework icon limit. The package generator now reproduces the content JSON, including the reviewed apparel and bow palettes, and its validator checks that relationship. The class starts with three Gold before the adventure's starting grant.

Game-free checks passed: Release framework build; 58 Thief combat checks; nine item modifier checks; four hot-reload package policy checks; PlayerMods, ItemApparel, and HotReloadResources; both authored art validators; the package validator; and `git diff --check`. These checks do not establish in-game balance or every native interaction.

Live-game evidence comes from an isolated, windowed macOS game with BepInEx 1.0.2 and the authorized single-player test bridge. The full package registered 54/54 entries with zero errors and warnings. A Thief started Frost Adventure with Street Twins, Patched Jack, Street Neckerchief, Softstep Shoes, and Lockpicks equipped. A normal combat reached the Thief's turn. The native stance showed basic Strike, Feint, and Slip Away; the three actions inherited from `dualKnife` were absent after the private-prefab action replacement. Earlier combat runs observed Feint dealing damage, Slip Away consuming a turn, a Skeleton Key Sneak Attack killing a Beastman, and ordinary victory/loot resolution. In a separate Skeleton Key fixture, a native Focus input changed Focus 4 to 3 and spent Focus 0 to 1; a perfect basic attack then dealt 36 damage to a 13 HP Beastman, and Focus returned to 4. The test bridge is an observation and command aid; these are native game scenes, not simulated damage tests.

The earlier raised-hood redesign was rejected. The current Astra-led apparel and weapon passes replace it with seven complete, fitted outfit tiers and the full dagger/bow/artifact progression. The [approval board](../../art-experiments/thief/approval-board/thief-art-approval.png) contains all 77 expected tiles and byte-pinned source receipts. Its full-set renders use exported equipment on a neutral pose without native face, hair, body, or hands. A separate final Street male native capture confirms equipped gear and visible native hair. Female layouts, other body/race choices, the remaining tiers, and broad animation coverage remain visual gates.

On the final 1.0.2 development DLL, the isolated game registered all 54 entries with zero errors and warnings. A fresh Frost Adventure Thief started with 8 Gold, consistent with the class's 3 plus the adventure's 5, and carried the Street gear. In one native fight, a basic attack killed one Bee, Feint dealt 3 HP to another, and a later basic attack ended the fight; native victory completed with no Thief exception. This establishes that route only, not the complete bonus edge-case matrix. The peer-safe Unlost Road Evasion signal follows the game's native proficiency RPC; its delivery ordering and effect on a different client's enemy calculation still require two-client testing.

A separate fresh Slip Away fixture showed that selecting the ability consumed the Thief's turn. The bridge could still select the same visible button on the next turn, but the committed-use guard rejected a second activation; this does not establish whether the native UI displayed its disabled state correctly. No qualifying incoming hit occurred in that fixture, so the 50% reduction remains unverified live.

In a scratch-only Unlost Road starting-weapon fixture, four actual native Focus button presses changed Focus 4 to 0 and spent Focus 0 to 4. A read-only state probe recorded Evasion unarmed before the committed basic attack, then one pending artifact receipt, then Evasion armed after the hit killed a 10 HP Beastman Warlock. No Thief error or exception was logged. The direct bridge attack left this fixture's combat open with zero live enemies, so this run proves the single-player hit signal and state transition only; ordinary victory was verified in the separate Street gear fight. The scratch package was restored to the normal Street Twins start afterward.

Outstanding live gates: Slip Away damage reduction, full action and bonus edge cases, equipment stats and fit across tiers and supported avatars, repeat and boundary cases for artifact effects, normal acquisition, save/resume, multiplayer, and managed-generation hot reload. Balance and manual mouse playtesting are not yet complete.
