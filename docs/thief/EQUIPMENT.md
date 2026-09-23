# Thief equipment progression

Status: all 42 ordinary equipment rows are authored in the local development package. All numbers are initial tuning values. Native registration, action behavior, acquisition, and balance remain live-game gates.

## Conventions and acquisition bands

Damage is **base weapon damage**, before native level growth. Every custom weapon here has damage gain 1, physical damage, no break chance, and ordinary Focus use. Native paired items remain unchanged. At player level 8, a base-25 weapon starts at 33 damage before other bonuses. All dagger sets occupy both hands and use Speed, with two checks in the first three bands and three checks from Masterwork onward; all bows use four Awareness checks and two hands. The displayed class primary stat does not change a bow's Awareness checks.

Armor and Resistance are flat points. SPD, AWR, TAL, and VIT bonuses are whole stat points, so +2 SPD corresponds to a native stat fraction of +0.02. Focus bonuses increase capacity only; equipping a charm never fills it. Listed values are the complete authored modifier set. Unlisted bonuses, inherited skills, critical chance, evade bonuses, and immunities must be cleared.

Gold values are proposed `_goldValue` inputs, not guaranteed purchase prices. Native economy and difficulty scaling still apply. Set normal price scaling consistently with verified native equipment; do not substitute these numbers directly into shop UI or sale payouts.

| Band | Native item-level eligibility | Campaign guide | Rarity | Source policy |
| --- | --- | --- | --- | --- |
| Street | 0-1 | Start and first region | Common | Town, night market, dungeon merchant, ordinary loot |
| Burglar | 1-2 | Roughly party level 2 onward | Common | Same ordinary sources |
| Guild | 2-3 | Roughly party level 4 onward | Uncommon | Same ordinary sources |
| Masterwork | 3-4 | Roughly party level 6 onward | Uncommon | Same ordinary sources |
| Locksmith / Nightblade / Wayfarer | 4-6 | Late campaign, roughly party level 8 onward | Rare | Same ordinary sources |
| Artifact | 4-6 | Optional late campaign finds | Artifact | Night market, dungeon merchant, ordinary loot; no town stock |

Item level is not player level. The observed native progression tables reach item level 4 at expected party levels 8 and 9; the ranges through 6 follow the native late-game equipment envelope. Different adventures may progress differently. Adjacent bands overlap so a slightly older piece remains obtainable. Each item has stock 1 where stocked, no new Lore/quest gate, and no class restriction. Native paired items retain their DLC/ownership gates. The custom paired-animation route requires a separate compatibility check before base-game availability is promised. Templates' unrelated rarity, drop, merchant, unlock, and endgame-filter values must be explicitly audited and replaced.

Eligibility is not a guaranteed spawn. Ordinary acquisition must be observed, and no artifact is a balance requirement. Source settings should not manufacture fixed copies, add special quest rewards, or mutate vanilla loot rows. Watch the enlarged pool in playtests: if the package overwhelms shop variety, narrow lower-band overlap or merchant eligibility before adding a new loot-weight system.

The starting grant is exactly four custom equipment items plus native Lockpicks: Street Twins, Patched Jack, Street Neckerchief, Softstep Shoes. Other Street items must be bought or found.

## Paired daggers: seven ordinary sets

This is the primary Thief weapon path. A pair is **one two-handed weapon item** with one attack result, not two independently rolled weapons. Each custom pair offers the basic Strike and exactly one authored weapon action; the Thief also has Slip Away. The custom pair replaces inherited `dualKnife` actions on its private prefab. Native `dualKnife` and `dualDagger` retain their own actions and remain usable stepping stones with Sneak Attack and Twin Feint eligibility.

| Band | ID | Name | Base damage / Speed checks | Base gold | Complete action differences |
| --- | --- | --- | --- | ---: | --- |
| Street | `thief_twins_street` | Street Twins | 10 / 2 | 14 | Strike 1.00, Feint 0.60; starting pair |
| Burglar | `thief_twins_burglar` | Windowfangs | 15 / 2 | 45 | Strike, Pierce 0.75 |
| Guild | `thief_twins_guild` | Guild Twin Daggers | 20 / 2 | 100 | Strike, Pierce 0.75 |
| Masterwork | `thief_twins_masterwork` | Velvet Fangs | 25 / 3 | 200 | Strike, Pierce 0.75; heavier commitment, larger hit |
| Locksmith | `thief_twins_locksmith` | Locksmith's Picks | 28 / 3 | 390 | Strike, Feint 0.80 |
| Nightblade | `thief_twins_nightblade` | Nightglass Twins | 30 / 3 | 430 | Strike and Feint only; trades penetration for damage |
| Wayfarer | `thief_twins_wayfarer` | Trailbreakers | 29 / 3 | 410 | Strike, Pierce 0.85 |

The early curve continues the inspected native two-handed Speed weapons: `dualKnife` is 15 damage / 2 checks at item level 1, and `dualDagger` is 20 / 2 at item level 2. The Guild set matches that raw damage/check structure while supplying the Thief's authored action choices. Native sets remain viable without enabling the old thief sample.

Guild Twin Daggers are eligible through item level 4, overriding the Guild band's usual maximum of 3. Their two-check reliability remains an intentional alternative while Masterwork introduces a third check. The UI must disclose the change rather than implying an unconditional upgrade. At 78 Speed, unfocused perfect chance falls from about 60.8% to 47.5%, and fully securing an attack costs one more Focus. Extra base damage and Twin Feint make the larger pair attractive without erasing the older style.

Twin Feint grants Prepared after a positive-damage basic strike that misses exactly one check. It is the paired path's unique Thief benefit beyond Sneak Attack. It grants no second attack or immediate extra damage. The two blade meshes and any multiple animation impacts must resolve through one logical attack budget.

One-handed dagger and buckler remains a last-resort native loadout when a preferred weapon is unavailable. It can use native attacks, shields, and Slip Away, but receives no Sneak Attack, Twin Feint, custom item line, or artifact. There is no shield alongside the paired sets.

## Shortbows: seven ordinary weapons

Each row includes Shoot and exactly one authored weapon action. The custom bow replaces inherited `bowShort` actions on its private prefab. A bow offers no positional safety or increased combat join range simply because it is ranged.

| Band | ID | Name | Base damage | Base gold | Complete action differences |
| --- | --- | --- | ---: | ---: | --- |
| Street | `thief_bow_street` | Rooftop Bow | 10 | 16 | Shoot 1.00, Draw Out 0.60 |
| Burglar | `thief_bow_burglar` | Alley Recurve | 14 | 40 | Shoot, Thread the Needle 0.75 |
| Guild | `thief_bow_guild` | Guild Shortbow | 18 | 85 | Shoot, Thread the Needle 0.75 |
| Masterwork | `thief_bow_masterwork` | Gloamwood Bow | 23 | 180 | Shoot, Thread the Needle 0.75 |
| Locksmith | `thief_bow_locksmith` | Latchspring | 29 | 390 | Shoot, Draw Out 0.80 |
| Nightblade | `thief_bow_nightblade` | Blackthorn | 32 | 440 | Shoot and Draw Out only; trades penetration for damage |
| Wayfarer | `thief_bow_wayfarer` | Farstep | 30 | 420 | Shoot, Thread the Needle 0.85 |

The early/middle bow curve follows native Long, Great, and Dragon bow base damage: 14, 18, 23. The proposed endgame 29-32 curve exceeds Royal Bow's 25 base damage and needs comparison against native actions and bonuses, especially on Hunter. It supports a viable secondary weapon path without granting Twin Feint. Reducing that endgame curve is the first correction if it replaces native bows too broadly.

## Body armor: seven coats

The coat is the main defensive piece. There are no stealth immunity, automatic dodge, damage-reflect, or class-specific passive bonuses on apparel.

| Band | ID | Name | Armor | Resistance | Other bonuses | Base gold |
| --- | --- | --- | ---: | ---: | --- | ---: |
| Street | `thief_coat_street` | Patched Jack | 1 | 0 | None | 8 |
| Burglar | `thief_coat_burglar` | Burglar's Jack | 2 | 1 | None | 26 |
| Guild | `thief_coat_guild` | Guild Leather | 3 | 1 | None | 62 |
| Masterwork | `thief_coat_masterwork` | Masterwork Jack | 4 | 2 | +1 VIT | 140 |
| Locksmith | `thief_coat_locksmith` | Locksmith's Coat | 4 | 4 | +2 VIT | 300 |
| Nightblade | `thief_coat_nightblade` | Nightblade Jack | 5 | 2 | +1 SPD | 300 |
| Wayfarer | `thief_coat_wayfarer` | Wayfarer's Coat | 4 | 3 | +2 AWR | 300 |

## Headpieces: seven caps and hoods

Faces remain visible. A hood is a native-compatible headpiece with authored fitting, not a full body replacement or a promise of cloth simulation.

| Band | ID | Name | Armor | Resistance | Other bonuses | Base gold |
| --- | --- | --- | ---: | ---: | --- | ---: |
| Street | `thief_hood_street` | Street Neckerchief | 0 | 0 | +1 TAL | 6 |
| Burglar | `thief_hood_burglar` | Burglar's Hood | 1 | 0 | +1 TAL | 18 |
| Guild | `thief_hood_guild` | Guild Hood | 1 | 1 | +1 AWR | 44 |
| Masterwork | `thief_hood_masterwork` | Masterwork Cowl | 2 | 1 | +2 TAL | 100 |
| Locksmith | `thief_hood_locksmith` | Locksmith's Hood | 1 | 3 | +3 TAL | 220 |
| Nightblade | `thief_hood_nightblade` | Nightblade Cowl | 2 | 1 | +2 SPD | 220 |
| Wayfarer | `thief_hood_wayfarer` | Wayfarer's Hood | 1 | 2 | +3 AWR | 220 |

## Boots: seven pairs

| Band | ID | Name | Armor | Resistance | Other bonuses | Base gold |
| --- | --- | --- | ---: | ---: | --- | ---: |
| Street | `thief_boots_street` | Softstep Shoes | 0 | 0 | +1 SPD | 6 |
| Burglar | `thief_boots_burglar` | Burglar's Boots | 0 | 1 | +1 SPD | 18 |
| Guild | `thief_boots_guild` | Guild Treads | 1 | 1 | +1 SPD | 44 |
| Masterwork | `thief_boots_masterwork` | Masterwork Treads | 1 | 2 | +2 SPD | 100 |
| Locksmith | `thief_boots_locksmith` | Locksmith's Steps | 1 | 2 | +2 SPD, +1 TAL | 220 |
| Nightblade | `thief_boots_nightblade` | Nightblade Steps | 2 | 1 | +3 SPD | 220 |
| Wayfarer | `thief_boots_wayfarer` | Wayfarer's Treads | 1 | 2 | +2 SPD, +1 AWR | 220 |

## Trinket slot: seven charms

Only one charm can occupy the normal trinket slot. These are ordinary equipment modifiers usable by any class. Changing a capacity modifier clamps excess current Focus when removed and never grants Focus when added. A swap loop cannot refill Focus.

| Band | ID | Name | Complete bonuses | Base gold |
| --- | --- | --- | --- | ---: |
| Street | `thief_charm_street` | Bent Copper | +1 TAL | 8 |
| Burglar | `thief_charm_burglar` | Brass Pick | +2 TAL | 24 |
| Guild | `thief_charm_guild` | Guild Token | +2 AWR, +1 TAL | 55 |
| Masterwork | `thief_charm_masterwork` | Silver Rook | +1 SPD, +2 AWR | 130 |
| Locksmith | `thief_charm_locksmith` | Master Keyring | +3 TAL, +1 maximum Focus | 290 |
| Nightblade | `thief_charm_nightblade` | Snuffed Wick | +2 SPD, +2 TAL | 290 |
| Wayfarer | `thief_charm_wayfarer` | Trail Compass | +3 AWR, +1 SPD | 290 |

The Master Keyring increases the size of a scarce resource pool; it does not regenerate it. The authored modifier uses the framework's `focusCapacity` declaration. Verify the native item-modifier route and Focus capacity lifecycle in game before accepting this bonus.

## Endgame loadout checks

These totals include coat, hood, boots, and matching charm. Both authored weapon paths occupy both hands. Weapons add no passive stats. Totals exclude levels, shrines, consumables, and other native modifiers.

| Loadout | Armor / Resistance from gear | SPD / AWR / TAL after gear | VIT / Focus capacity | Purpose |
| --- | --- | --- | --- | --- |
| Locksmith paired daggers | 6 / 9 | 80 / 72 / 81 | 52 / 4 | Preparation, tool checks, magical defense, larger Focus pool |
| Nightblade paired daggers | 9 / 4 | 86 / 72 / 76 | 50 / 3 | Fast precision and raw damage, weak magic defense |
| Wayfarer bow | 6 / 7 | 81 / 81 / 74 | 50 / 3 | Bow accuracy and balanced light protection |
| Wayfarer paired daggers | 6 / 7 | 81 / 81 / 74 | 50 / 3 | Penetration, with less Speed than Nightblade |

There is no hidden bonus for these combinations. For example, a Nightblade can take the Locksmith hood to improve magic defense, or a bow user can use Nightblade boots for Speed. Mixed outfits should retain a coherent visual style. A one-handed emergency weapon may gain native shield defenses but loses the weapon-dependent Thief mechanics; it is not a third supported endgame build.

## Inventory and production boundaries

There are 42 ordinary items in this document and [three artifacts](ARTIFACTS.md), totaling 45. One class and the required proficiency rows are additional content entries, not counted as equipment. IDs are stable proposed authoring keys; all custom enum identities must still be allocated by the framework.

Every equipped piece needs a verified native item type and template, explicit modifiers, an original icon, original item/display art where rendered, and save/acquisition checks. Native modifiers do not automatically transfer to a new item ID. Reusing original modular parts within a family is allowed; changing only a vanilla texture does not meet the original-art goal.

The local package now includes the complete equipment inventory, eight action variants, and the designed Street starting loadout. Weapon actions, artifact signatures, and class combat capability must pass runtime checks before it is a completed class or a production catalog package.
