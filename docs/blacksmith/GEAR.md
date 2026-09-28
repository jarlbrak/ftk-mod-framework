# Blacksmith gear progression

**Status: unpublished 0.2.0 development prototype.** The approved redesign has
32 equipment items, eight in each of four tiers. It depends on unreleased
framework capabilities for class affinity, Blacksmith actions and appearance
variants. The manifest's `frameworkVersion: 1.0.2` identifies the local
development baseline; it does not establish compatibility with a public 1.0.2
release. Public framework and launcher compatibility remain release gates.

The package preserves the native Blacksmith, its starting loadout and Steady.
Every class can equip these items and receive their ordinary modifiers.
Blacksmith-only passives and actions require the native class. No matching set,
piece-count threshold, new class, resource meter or class-in-party requirement
is introduced. [Combat](COMBAT.md) is the canonical action and lifecycle contract.

The unchanged [marketplace banner](../../marketplace/packages/classgear/promo/blacksmith-banner.png)
is promotional illustration for Kilnward. It does not establish the appearance
or acceptance of the other tiers. Current source pins are in the
[package provenance](../../marketplace/packages/classgear/assets.provenance.json)
and [approved art delivery](../../art-experiments/blacksmith-forge-rodin/approved-redesign/delivery/manifest.json).
Earlier native observations remain historical evidence below and in the
[release pass](FULL-RELEASE-PASS.md).

## Four complete tiers

These are item-level bands, not character levels. Each tier contains a
one-handed hammer, two-handed maul, shield, body armor, helmet, boots, trinket
and necklace. Coalmark and Bellowsworn now include early two-handed mauls.

| Tier | Item levels | Rarity | 1H damage / checks | 2H damage / checks |
|---|---|---|---:|---:|
| Coalmark | 0 | Common | 10 / 4 | 14 / 5 |
| Bellowsworn | 1-2 | Common | 17 / 4 | 22 / 5 |
| Rivetwatch | 3 | Rare | 24 / 4 | 30 / 5 |
| Kilnward | 4-6 | Rare | 29 / 4 | 33 / 5 |

Both routes use Vitality-based weapon checks (`vitality` in the declaration)
and damage gain 1. Base damage excludes character growth, action coefficients
and defenses. The themed private weapon menus retain the ordinary basic attack.
Blacksmiths additionally receive Set Hammer or Overhand. Inherited Splash,
Shockwave and Stun are removed from these weapons; shields retain native Taunt.

| Equipment action | Coalmark | Bellowsworn | Rivetwatch | Kilnward |
|---|---:|---:|---:|---:|
| Set Hammer, 75% physical damage | +2 Armor | +3 Armor | +4 Armor | +5 Armor |
| Overhand, 115% physical damage | -2 Armor | -3 Armor | -4 Armor | -6 Armor |
| Temper, full action once per combat | None | +3 Armor | +4 Armor | +5 Armor |

Set Hammer requires any valid equipped shield and grants protection only after
positive HP damage, through the start of the next scheduled own turn. Overhand
pays its Armor penalty on commitment, including a miss, through that same turn
boundary. Both retain ordinary weapon checks and Focus costs.

Temper targets a living ally or self without a roll or Focus cost. It lasts
through the target's next two completed scheduled turns; a self-cast excludes
the casting turn. Set Hammer and Temper use the stronger positive Armor bonus,
while Overhand's penalty remains additive. Equal or stronger Temper refreshes;
a weaker application does not lower or refresh the stronger one. Equipment
swaps cannot replenish Temper or erase Overhand. See [Combat](COMBAT.md) for
cancellation, shield removal, dispel, death, skipped turns and authority rules.

## Piece-specific passive budget

These additions apply only while the item is equipped by a native Blacksmith.
Other classes receive the ordinary item values in the next table.

| Piece | Blacksmith passive |
|---|---|
| 1H hammer or 2H maul | +1 Vitality |
| Shield | +1 Armor |
| Body armor | +1 Armor |
| Helmet | +1 Vitality |
| Boots | +1 Speed |
| Trinket | +1 Resistance |
| Necklace | +1 Vitality |

Only three equipped slots add affinity Vitality in either weapon path. With the
necklace's ordinary modifier, either complete route grants five Vitality early
or six late, plus one Speed. This replaces the earlier per-piece Vitality rule.

## Ordinary values and prices

Armor/Resistance values below exclude class affinity and temporary effects.
Vitality is shown in displayed stat points; one point is `0.01` in content JSON.
The current values, ability costs and all 32 base prices are retained as the
reviewed tuning baseline for unpublished 0.2.0. Controlled native trials and
native item comparisons support the intended damage/protection choices without
showing a concrete reason to adjust this budget. This is a design decision,
not statistical campaign balance. See [validation](REDESIGN-VALIDATION.md) for
the trial setup, outcomes and limitations.

| Comparison | Blacksmith gear | Native reference |
|---|---:|---:|
| Bellowsworn maul base damage / checks | 22 / 5 | War Hammer 21 / 5 |
| Rivetwatch maul base damage / checks | 30 / 5 | Great Hammer 32 / 5 |
| Kilnward maul base damage / checks | 33 / 5 | Royal Hammer 38 / 5 |
| Kilnward complete shield route, gear-only Armor/Resistance | 38/17 | Native heavy comparator 44/9 |
| Kilnward complete shield route, total base gold value | 2,200 | Native heavy comparator 2,490 |

The native comparator combines Flanged Mace, Royal Shield, tier-five heavy
body/head/boots, Toughness amulet and defense trinket. Its Armor contributions
sum to 44, excluding native character defenses. The themed route trades six
passive Armor for eight Resistance and has a different governing stat and
ability menu. Native heavy equipment retains substantial Strength bonuses;
the themed pieces avoid its associated stat penalties. Kilnward Overhand's
115% coefficient brings its base-damage equivalent to 37.95 while imposing
six Armor exposure. Base gold values exclude shop multipliers. Individual
themed armor/shield discounts are offset by weapon and necklace premiums;
the complete route costs about 12% less than this native comparator.

| Piece | Coalmark | Bellowsworn | Rivetwatch | Kilnward |
|---|---:|---:|---:|---:|
| Body Armor/Resistance | 3/0 | 6/1 | 10/2 | 13/3 |
| Helmet Armor/Resistance | 2/0 | 4/1 | 6/2 | 8/3 |
| Boots Armor/Resistance | 2/0 | 4/1 | 6/1 | 8/2 |
| Shield Armor/Resistance | 1/0 | 2/1 | 3/2 | 4/3 |
| Trinket Armor/Resistance | 1/0 | 2/1 | 2/2 | 3/3 |
| Necklace | +2 Vitality | +2 Vitality, +1 Resistance | +3 Vitality, +1 Resistance | +3 Vitality, +2 Resistance |

Each stable item ID combines the prefix in the first column with its tier name.
For example, `blacksmith_hammer_2h_coalmark` is the new Coalmark Forge Maul.
The original 30 IDs are retained.

| ID prefix | Coalmark gold | Bellowsworn gold | Rivetwatch gold | Kilnward gold |
|---|---:|---:|---:|---:|
| `blacksmith_hammer_1h_` | 12 | 58 | 178 | 390 |
| `blacksmith_hammer_2h_` | 14 | 65 | 188 | 370 |
| `blacksmith_shield_` | 10 | 32 | 75 | 160 |
| `blacksmith_armor_` | 35 | 85 | 200 | 560 |
| `blacksmith_helmet_` | 16 | 32 | 75 | 220 |
| `blacksmith_boots_` | 12 | 30 | 80 | 220 |
| `blacksmith_trinket_` | 10 | 55 | 135 | 300 |
| `blacksmith_necklace_` | 12 | 70 | 180 | 350 |

| Blacksmith gear-only total | Coalmark | Bellowsworn | Rivetwatch | Kilnward |
|---|---:|---:|---:|---:|
| 1H + shield Armor/Resistance | 11/1 | 20/7 | 29/11 | 38/17 |
| 2H Armor/Resistance | 9/1 | 17/6 | 25/9 | 33/14 |
| Either route, Vitality / Speed | +5 / +1 | +5 / +1 | +6 / +1 | +6 / +1 |

These calculated totals include class affinity and exclude native character
stats, difficulty, sanctums and temporary actions. High physical mitigation is
an explicit tuning risk. Earlier fixture totals below use the superseded
Vitality affinity and cannot validate these new values or action balance.

## Acquisition

Each item-level band adds eight items to shared town, Night Market, Dungeon
Merchant and drop pools. Early tiers are common; late tiers are rare. There is
no lore unlock, DLC dependency or Blacksmith-in-party requirement. Declaration
flags establish eligibility only; natural acquisition and exact frequencies
need current native campaign evidence.

## Art inventory and runtime routes

The current provenance lists **162 referenced runtime assets: 106 GLBs and 56
PNG textures/icons**. Its 32 identities comprise 24 rigid equipment identities
and eight apparel identities. Each item has recorded Rodin source lineage;
Blender conversion, reuse and fitting are recorded in the
[approved redesign campaign](../../art-experiments/blacksmith-forge-rodin/approved-redesign/README.md).
A lineage record does not mean every item required a new generation or establish
visual acceptance. The unchanged Kilnward banner remains the illustration reference.

| Slot | Declared routes in the current candidate |
|---|---|
| 1H hammer | All four tiers: equipped and display roots, with two break fragments |
| 2H maul | All four tiers: equipped and display roots, with three break fragments |
| Shield | All four tiers: equipped and inventory display roots |
| Helmet | All four tiers: equipped and inventory display roots |
| Body armor | All four tiers: female/male Blacksmith bindings and inventory display; Bellowsworn, Rivetwatch and Kilnward also declare Cat/Demon variants |
| Boots | All four tiers: the shared native Blacksmith boot binding and inventory display |
| Trinket and necklace | All four tiers: inventory display roots |

Rigid geometry has an 8,000-triangle budget; complete garments and boot pairs
have a 15,000-triangle budget, with exported vertices below the runtime index
limit. Exact source hashes, route declarations and structural checks are
maintained by the delivery ledger and redesigned package validator. None of
these declarations alone proves grip, card framing, hair behavior, animation,
race-specific clearance or ordinary equipment rebuild behavior in Unity.

## Current evidence boundary

This guide records the approved 0.2.0 declarations and source inventory. It adds
no new live acceptance claim. Builds, pure combat tests and asset validation
are separate from native action, fitting, co-op and lifecycle evidence. Current
validation must identify the exact package and framework bytes exercised.

Remaining release gates include public framework/launcher compatibility;
all-tier fit and card review; native action costs and durations; cancellation;
ordinary acquisition; representative party balance; appearance and equipment
rebuilds; break behavior; supported save/lifecycle paths; co-op agreement;
Windows/Linux coverage; and applicable model export/distribution entitlement.
The prototype has no production catalog entry or published archive.

## Historical evidence for the superseded 30-item package

The following preserved measurements and observations predate 0.2.0. They used
per-piece Vitality affinity and inherited weapon actions. Their live totals,
asset revisions and combat outcomes do not describe or validate the current
32-item redesign. The [banner correction](BANNER-MODELS.md),
[release review](RELEASE-REVIEW.md) and [full release pass](FULL-RELEASE-PASS.md)
preserve their own exact historical scope.

### Earlier native game checks, before the banner correction

On 2026-09-26, the fresh package was loaded in an isolated macOS game copy. A test-only fixture granted the exact registered rows to the disposable Blacksmith, then `CharacterOverworld.ForceEquip` equipped each full set outside combat. Every result confirmed the expected native hand slots and measured level-0 stats. A second fresh native campaign then used the same test-only gear grant, equipped Kilnward 1H plus shield, and accelerated the full trio to native level 14 through each `CharacterStats.UpdateXP` threshold and `CharacterStats.Update` calculation. That campaign proceeded to a controlled high-level fight described below.

| Tier and loadout | Weapon damage | Item-row Armor / live total Armor | Live Resistance | Live raw Vitality |
|---|---:|---:|---:|---:|
| Coalmark, 1H + shield | 10 | 9 / 10 | 0 | 0.94 |
| Bellowsworn, 1H + shield | 17 | 18 / 19 | 5 | 0.94 |
| Rivetwatch, 1H + shield | 24 | 27 / 28 | 10 | 0.95 |
| Rivetwatch, 2H | 30 | 24 / 25 | 8 | 0.94 |
| Kilnward, 1H + shield | 29 | 36 / 37 | 16 | 0.95 |
| Kilnward, 2H | 33 | 32 / 33 | 13 | 0.94 |

The native total Armor column is one point above the sum of custom equipment-row Armor in every tested loadout; this is the Blacksmith's existing base Armor. Resistance matches the package equipment-row sum, including the necklace's ordinary modifier. These results validate item registration, native equip behavior, affinity calculation, and stat arithmetic. The max-level fixture results below are separate measurements, not natural campaign progression.

The package was equipped in the game, but item acquisition was not tested: the fixture grants items directly to the disposable Backpack. Shared shop/drop eligibility and weights remain source-validated configuration only. The player-studio offscreen capture did not render a useful avatar image and is not fit evidence; the fit review is the native in-game inventory preview and combat scene.

### Maximum-level loadout measurements

The first Armor value is the static sum of this package's Kilnward item rows; the second is the live level-0 total after the Blacksmith's native +1 base Armor. Resistance includes both equipment-row Resistance and the necklace's ordinary modifier. Vitality includes the necklace's ordinary modifier and the Blacksmith's per-piece affinity. Each item's affinity is +0.01 raw Vitality, equivalent to one displayed Vitality point on the game's 0.01 stat scale.

| Kilnward loadout on a Blacksmith | Pieces | Weapon damage, sockets | Item Armor / live Armor | Resistance | Vitality from gear / live raw Vitality |
|---|---:|---:|---:|---:|---:|
| One-handed hammer, shield, armor, boots, helmet, necklace, trinket | 7 | 29, 4 | 36 / 37 | 16 | +0.10 / 0.95 |
| Two-handed maul, armor, boots, helmet, necklace, trinket | 6 | 33, 5 | 32 / 33 | 13 | +0.09 / 0.94 |

Another class gets the same ordinary item stats but not the affinity: +0.03 Vitality from the Kilnward necklace. The Blacksmith affinity accounts for +0.07 or +0.06 Vitality in the seven- or six-piece configurations. The live values agree with those item modifiers and the native class baseline.

The `blacksmith-max-level-loadout-summary.json` record re-equipped six configurations on a native level-14 Blacksmith. It measured these rows after all native level-up calculations. The test accelerated XP in a disposable campaign; it does not claim a normal progression route.

| Level-14 loadout | Weapon row damage | Total Armor | Resistance | Maximum health | Raw Vitality |
|---|---:|---:|---:|---:|---:|
| Coalmark, 1H + shield | 10 | 10 | 0 | 184 | 0.94 |
| Bellowsworn, 1H + shield | 17 | 19 | 5 | 184 | 0.94 |
| Rivetwatch, 1H + shield | 24 | 28 | 10 | 186 | 0.95 |
| Rivetwatch, 2H | 30 | 25 | 8 | 184 | 0.94 |
| Kilnward, 1H + shield | 29 | 37 | 16 | 186 | 0.95 |
| Kilnward, 2H | 33 | 33 | 13 | 184 | 0.94 |

### Controlled end-game combat stress test

The fresh level-14 campaign entered the nearby Guardian Forest Cave, level 0, room 1. The test fixture staged Harazuel High Guard (`harazuelBoss1`, level 10, 81 HP) and Corrupted Mystic (`harazuelMinionD`, level 9, 63 HP). This was a controlled high-level encounter in a low-level dungeon, not a naturally rolled final-dungeon room. It avoids treating a prior mismatched-region FireCave attempt as evidence.

The Blacksmith wore the full Kilnward one-hand-and-shield set: 186 max HP, 37 Armor, 16 Resistance, and 29 weapon-row damage. In native combat, hammerSplash reduced the High Guard from 81 to 49 HP (32 damage). A later focused Smash reduced it from 49 to 6 HP (43 damage). The Mystic survived at 33 HP. Both companions had level-14 character stats but retained their low-tier starting gear; the Hunter and Scholar died before the Blacksmith, and the Blacksmith was then defeated. The native run ended with the party at 0 HP and enemies remaining at 6/33 HP. No kill shortcut was used.

The result confirms that the Kilnward 1H-plus-shield set registers, renders, and deals damage in a real level-14 combat scene. It does not establish that the set is balanced: the party was undergeared, the encounter was staged, the run was lost, and there was no native-gear control group or 2H combat run. A full party equipped for its level and naturally reached final-dungeon encounters are still needed before calling end-game balance verified.
