# Thief equipment progression

Scope: the **unreleased** generator-backed 1.1 candidate in `marketplace/packages/thief/content.json`. The published 1.0.0 bow package and website remain separate. [Candidate status](CANDIDATE.md) and [validation](VALIDATION.md) state which native gates remain open.

Status: 55 content entries comprise one class, 45 equipment items (42 ordinary and three artifacts), and nine weapon proficiencies. The listed values are current authored declarations and provisional tuning, not broad native acquisition or balance proof.

The current Street male Patched Jack selects the V86 local skinning successor, with the user's acceptance limited to its reviewed three-quarter gait and basic attack appearance. This changes no item identity, balance, geometry or rigid display. Other wearer motion and release checks remain open; [validation](VALIDATION.md) preserves the exact source and historical review record.

## Conventions and acquisition bands

Damage is **base weapon damage**, before native level growth. Every custom weapon has damage gain 1 and physical damage. The authored pistols explicitly cannot break and allow Focus; native paired items remain unchanged. At player level 8, a base-25 weapon starts at 33 damage before other bonuses. All dagger sets occupy both hands and use Speed (`quickness`), with two checks in the first three bands and three from Masterwork onward. The seven ordinary pistols and artifact Unlost Road occupy both hands and use four Talent checks. Their retained `thief_bow_*` IDs are save-stable keys, not bow behavior or names. Old bow-ID save migration and pistol turns remain native gates.

Armor and Resistance are flat points. SPD, AWR, TAL, and VIT bonuses are whole stat points, so +2 SPD corresponds to a native stat fraction of +0.02. Focus bonuses increase capacity only; equipping a charm never fills it. Listed values are the complete authored modifier set. Unlisted bonuses, inherited skills, critical chance, evade bonuses, and immunities must be cleared.

Gold values are proposed `_goldValue` inputs, not guaranteed purchase prices. Native economy and difficulty scaling still apply. Set normal price scaling consistently with verified native equipment; do not substitute these numbers directly into shop UI or sale payouts.

| Band | Native item-level eligibility | Campaign guide | Rarity | Source policy |
| --- | --- | --- | --- | --- |
| Street | 0-1 | Start and first region | Common | Town, night market, dungeon merchant, ordinary loot |
| Burglar | 1-2 | Roughly party level 2 onward | Common | Same ordinary sources |
| Guild | 2-3 | Roughly party level 4 onward | Uncommon | Same ordinary sources |
| Masterwork | 3-4 | Roughly party level 6 onward | Uncommon | Same ordinary sources |
| Locksmith / Nightblade / Wayfarer | 4-6 | Late campaign, roughly party level 8 onward | Rare | 18 ordinary items through one-token Back Alley offers; no ordinary drop or merchant stock |
| Artifact | 4-6 | Optional late campaign finds | Artifact | Night market, dungeon merchant, ordinary loot; no town stock |

Item level is not player level. The observed native progression tables reach item level 4 at expected party levels 8 and 9; the ranges through 6 follow the native late-game equipment envelope. Different adventures may progress differently. Adjacent early bands overlap. Ordinary lower-band rows have stock 1 where stocked; the 18 final-family exchange rows have stock 0 and disabled ordinary source flags. No new Lore/quest or class restriction is declared. Native paired items retain their DLC/ownership gates. The custom paired-animation route and bow-ID-to-pistol save compatibility require separate native checks.

Eligibility is not a guaranteed spawn. The shared physical Guild Token can be earned from admitted native enemy rewards at displayed enemy level 8 or higher under the current provisional 10% ordinary, 50% reviewed named-boss, sixth-eligible-opportunity guarantee rule. Those chances, campaign supply, and full exchange availability are not native-proven. The 18 final-family pieces cost one token each in the native Back Alley, independent of Thief party membership. Artifacts remain separate optional finds, never a balance requirement. No custom rule mutates vanilla loot rows. Observe the early ordinary pool and late token pacing in campaign play before release.

The starting grant is exactly four custom equipment items plus native Lockpicks: Street Twins, Patched Jack, Street Bandana, Softstep Shoes. Other Street items must be bought or found.

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

## Flintlock pistols: seven ordinary weapons

Each pistol replaces inherited `gunTreasureHunter` actions on its private weapon row and declares Fire plus one authored shot. Fire and the special shot use native firearm ammunition. Current Core restores one round at the start of an eligible Thief's scheduled combat turn; another class uses the native reload cycle. V26 contains bounded Street pistol Fire and manual Reload evidence, not all-pistol or other-class coverage. No extra targeting range is implied by the weapon's art.

| Band | ID | Name | Base damage | Base gold | Complete action differences |
| --- | --- | --- | ---: | ---: | --- |
| Street | `thief_bow_street` | Rooftop Flintlock | 10 | 16 | Fire 1.00, Bait Shot 0.60 |
| Burglar | `thief_bow_burglar` | Windowlock | 14 | 40 | Fire, Deadeye 0.75 |
| Guild | `thief_bow_guild` | Guild Sidearm | 18 | 85 | Fire, Deadeye 0.75 |
| Masterwork | `thief_bow_masterwork` | Gloam Flintlock | 23 | 180 | Fire, Deadeye 0.75 |
| Locksmith | `thief_bow_locksmith` | Latchlock | 29 | 390 | Fire, Bait Shot 0.80 |
| Nightblade | `thief_bow_nightblade` | Blackwake | 32 | 440 | Fire, Bait Shot 0.60; trades penetration for damage |
| Wayfarer | `thief_bow_wayfarer` | Farstep Flintlock | 30 | 420 | Fire, Deadeye 0.85 |

The endgame 29-32 base-damage curve remains a balance risk, especially for a non-Thief Talent user. A successful native Fire turn is not a matched class comparison. Pistols do not grant Twin Feint merely because their stable IDs retain `bow`.

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

## Headpieces: seven bandanas

Faces remain visible. Each bandana uses an authored rigid fit for the supported wearer profiles.

| Band | ID | Name | Armor | Resistance | Other bonuses | Base gold |
| --- | --- | --- | ---: | ---: | --- | ---: |
| Street | `thief_hood_street` | Street Bandana | 0 | 0 | +1 TAL | 6 |
| Burglar | `thief_hood_burglar` | Burglar's Bandana | 1 | 0 | +1 TAL | 18 |
| Guild | `thief_hood_guild` | Guild Bandana | 1 | 1 | +1 AWR | 44 |
| Masterwork | `thief_hood_masterwork` | Masterwork Bandana | 2 | 1 | +2 TAL | 100 |
| Locksmith | `thief_hood_locksmith` | Locksmith's Bandana | 1 | 3 | +3 TAL | 220 |
| Nightblade | `thief_hood_nightblade` | Nightblade Bandana | 2 | 1 | +2 SPD | 220 |
| Wayfarer | `thief_hood_wayfarer` | Wayfarer's Bandana | 1 | 2 | +3 AWR | 220 |

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
| Guild | `thief_charm_guild` | Guild Insignia | +2 AWR, +1 TAL | 55 |
| Masterwork | `thief_charm_masterwork` | Silver Rook | +1 SPD, +2 AWR | 130 |
| Locksmith | `thief_charm_locksmith` | Master Keyring | +3 TAL, +1 maximum Focus | 290 |
| Nightblade | `thief_charm_nightblade` | Snuffed Wick | +2 SPD, +2 TAL | 290 |
| Wayfarer | `thief_charm_wayfarer` | Trail Compass | +3 AWR, +1 SPD | 290 |

The Master Keyring increases the size of a scarce resource pool; it does not regenerate it. The authored modifier uses the framework's `focusCapacity` declaration. Verify the native item-modifier route and Focus capacity lifecycle in game before accepting this bonus.

## Visible-armor roles and endgame loadout checks

For a Thief, only registered matching **Head, Body and Feet** pieces count toward a Locksmith, Nightblade or Wayfarer role. One piece grants item stats only. Two grant a preview; all three grant the core, including its cost. At most one family profile is active. A matching dagger pair or pistol is optional completion metadata with **no extra bonus** in current Core. Charms and artifacts never fill an armor slot. Non-Thieves receive ordinary item stats and weapon actions, not Thief role effects. The current role values below are implemented candidates, not approved balance outcomes.

| Family | Two-piece preview | Three-piece core |
| --- | --- | --- |
| Locksmith | Prepared Sneak Attack +5 percentage points; other Sneak Attack -5 points | One spent Focus refunded on a Prepared perfect hit once per combat; Sneak Attack -10 points |
| Nightblade | Full-health or not-yet-acted opener +5 points; other Sneak Attack -5 points | Same opener +10 points; other Sneak Attack -10 points |
| Wayfarer | Eligible direct hit grants +2 Evasion until next turn; Sneak Attack -5 points | Eligible direct hit grants +4 Evasion until next turn; Sneak Attack -10 points |

These arithmetic totals include coat, bandana, boots, and matching charm, but exclude role effects, levels, shrines, consumables and other native modifiers. Both authored weapon paths occupy both hands. Weapons add no passive stats.

| Loadout | Armor / Resistance from gear | SPD / AWR / TAL after gear | VIT / Focus capacity | Purpose |
| --- | --- | --- | --- | --- |
| Locksmith paired daggers | 6 / 9 | 80 / 72 / 81 | 52 / 4 | Preparation, tool checks, magical defense, larger Focus pool |
| Nightblade paired daggers | 9 / 4 | 86 / 72 / 76 | 50 / 3 | Fast precision and raw damage, weak magic defense |
| Wayfarer pistol | 6 / 7 | 81 / 81 / 74 | 50 / 3 | Talent sidearm and balanced light protection |
| Wayfarer paired daggers | 6 / 7 | 81 / 81 / 74 | 50 / 3 | Penetration, with less Speed than Nightblade |

There is no hidden matching-weapon bonus. For example, a Nightblade can take the Locksmith bandana to improve magic defense, but that mixed piece can also reduce or remove the active family count. A one-handed emergency weapon may gain native shield defenses but loses weapon-dependent Thief mechanics; it is not a third supported endgame build.

## Inventory and production boundaries

There are 42 ordinary items in this document and [three artifacts](ARTIFACTS.md), totaling 45. One class and nine proficiency rows are additional content entries, not counted as equipment. The `thief_bow_*` strings remain stable item keys, while custom enum identities are allocated by the framework.

Every equipped piece needs a verified native item type and template, explicit modifiers, an original icon, original item/display art where rendered, and save/acquisition checks. Native modifiers do not automatically transfer to a new item ID. Reusing original modular parts within a family is allowed; changing only a vanilla texture does not meet the original-art goal.

The current package includes the complete equipment inventory, **nine** proficiency rows, the Street starting loadout, 18 one-token exchange offers, and seven adopted production bandana families with 56 exact worn profiles. Its 77 approved head assets and 21 selected worn coat/boot assets are integrated with separate source ledgers and bounded native evidence. The latter comprise both coat sex variants and all seven boots, including the Guild male V3 drape repair. Source-faithful final display and studio media are prepared for the unreleased candidate, including the adopted 7,998-triangle boot displays. Media preparation does not establish native motion or package release acceptance. Weapon actions, armor roles, artifact signatures, token supply, save/resume, other-class use and balance still need the stated native gates. Update the published website only from an approved immutable release package.
