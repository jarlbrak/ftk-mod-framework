# Paladin equipment progression

Status: Paladin 1.2.0 source candidate, 2026-09-24. **The source package
defines 51 equipment items, including twelve accessories.** All 51 registered
in the earlier 1.0.1 macOS trial; individual accessory views, stats, acquisition
and persistence still need live coverage. See [Accessory validation](ACCESSORY-VALIDATION.md)
and the [launch record](LAUNCH-1.0.0.md).
The [1.2.0 balance review](BALANCE-1.2.0.md) records changed values and limits.
Values are transcribed from the [package definitions](../../marketplace/packages/paladin/content.json).
Accessory values remain initial tuning targets, not measured balance.

## Conventions and acquisition bands

Damage means base weapon damage before native level growth, criticals, action
coefficients and enemy defenses. All current hammers use Vitality and damage
gain 1. The ordinary 1H path uses three checks; the ordinary 2H path uses four.
Kingsfall uses five for its normal attack. Special actions can have their own
native check rules. Normal Focus use is retained.

Armor and Resistance are flat points. VIT and SPD are whole stat points:
+2 VIT is an authored fraction of +0.02. Listed accessory bonuses are complete;
unlisted HP, Focus, damage, skills and immunities must not be inherited. The
existing typed modifier route creates a fresh item-specific modifier row rather
than copying the template's modifier row. All equipment is class unrestricted.

Gold values are base row inputs, not exact store prices or resale payouts.
Native economy and difficulty modifiers still apply.

| Family | Native item levels | Campaign guide | Rarity | Sources |
| --- | --- | --- | --- | --- |
| Novice | 0 | Starting region | Common | Ordinary loot, town, night market, dungeon merchant |
| Oathkeeper | 1-2 | Roughly party levels 2-4 | Common | Same ordinary sources |
| Highward | 3 | Roughly party level 6 | Rare | Same ordinary sources |
| Mercy / Censure / Verdict | 4-6 | Late campaign, roughly party level 8 onward | Rare | Same ordinary sources |
| Artifact | 4-6 | Optional late finds | Artifact | Ordinary loot, night market, dungeon merchant; no town stock |

Item level is not player level. These preserve Paladin's corrected bands, rather
than copying Thief's additional band or overlapping ranges. Exact progression
varies by adventure; see [Acquisition audit](ACQUISITION-AUDIT.md). Stock is one
where eligible, with no new class, Lore, quest or DLC requirement. Eligibility
does not guarantee a spawn. Use native rarity weighting and category selection;
adding another normalization system is not part of this plan.

The starting grant remains five equipment items: Novice Hammer, Aegis, Plate,
Sabatons and Helm. Novice Great Hammer and both new Novice accessories must
be found or bought. No new consumables are proposed.

## Slot coverage

The baseline is the earlier 39-item candidate. The additions below are now
authored in source; counts do not imply native registration or live acceptance.

| Equipment route | Baseline ordinary items | Existing artifacts | New accessories | Source total |
| --- | ---: | ---: | ---: | ---: |
| One-handed hammer | 6 | 1 | 0 | 7 |
| Two-handed hammer | 6 | 1 | 0 | 7 |
| Shield | 6 | 1 | 0 | 7 |
| Body | 6 | 0 | 0 | 6 |
| Head | 6 | 0 | 0 | 6 |
| Boots | 6 | 0 | 0 | 6 |
| Trinket | 0 | 0 | 6 | 6 |
| Necklace | 0 | 0 | 6 | 6 |
| **Total** | **36** | **3** | **12** | **51** |

A 2H hammer excludes a shield. Trinket and Neck each hold one wearable item.
The native Belt container holds up to three usable consumables; it is not a
wearable belt slot. Backpack is inventory storage, not another proposed apparel
family. A complete custom loadout covers the wearable slots without requiring
new rings, belts or body slots.

## One-handed hammers: six existing weapons

Normal attack: native Smash. All six hammers retain the Smith Hammer
weapon template's Splash action (`hammerSplash`). Censure appends its own
action to that list. No accessory grants extra attacks.

| Family | ID | Name | Base damage / VIT checks | Base gold | Guardian or action difference |
| --- | --- | --- | --- | ---: | --- |
| Novice | `paladin_hammer_1h_novice` | Novice Hammer | 10 / 3 | 12 | None; native template actions |
| Oathkeeper | `paladin_hammer_1h_oathkeeper` | Oathkeeper Hammer | 17 / 3 | 70 | None; native template actions |
| Highward | `paladin_hammer_1h_highward` | Highward Hammer | 24 / 3 | 200 | None; native template actions |
| Mercy | `paladin_hammer_1h_mercy` | Mercy Hammer | 28 / 3 | 360 | Focused ally heal becomes 10% |
| Censure | `paladin_hammer_1h_censure` | Censure Hammer | 28 / 3 | 360 | Censure: 0.75 damage coefficient, Armor -4 |
| Verdict | `paladin_hammer_1h_verdict` | Verdict Hammer | 31 / 3 | 360 | None; native template actions |

## Two-handed hammers: six existing weapons

Normal attack: native Crush. All six hammers retain Shockwave and Stun
(`bluntShockwaveSplash`, `bluntStun`). Censure appends its own action to that
list. These template actions are part of the actual loadout and must
be included in balance comparisons, even when no custom action was authored.

| Family | ID | Name | Base damage / VIT checks | Base gold | Guardian or action difference |
| --- | --- | --- | --- | ---: | --- |
| Novice | `paladin_hammer_2h_novice` | Novice Great Hammer | 15 / 4 | 12 | None; native template actions |
| Oathkeeper | `paladin_hammer_2h_oathkeeper` | Oathkeeper Great Hammer | 24 / 4 | 70 | None; native template actions |
| Highward | `paladin_hammer_2h_highward` | Highward Great Hammer | 32 / 4 | 200 | None; native template actions |
| Mercy | `paladin_hammer_2h_mercy` | Mercy Great Hammer | 34 / 4 | 360 | Focused ally heal becomes 12% |
| Censure | `paladin_hammer_2h_censure` | Censure Great Hammer | 34 / 4 | 360 | Censure: 0.75 damage coefficient, Armor -6 |
| Verdict | `paladin_hammer_2h_verdict` | Verdict Great Hammer | 37 / 4 | 360 | None; native template actions |

## Shields: six existing items

Every ordinary Paladin shield supplies **0 Armor, 0 Resistance and -2 SPD**.
Their compensation requires Guardian; conventional shield users can wear them
but get no passive defense. Two-handed weapons cannot retain these bonuses.

| Family | ID | Name | Guardian benefit | Base gold |
| --- | --- | --- | --- | ---: |
| Novice | `paladin_shield_novice` | Novice Aegis | Heal ally 2% max HP on Guard | 12 |
| Oathkeeper | `paladin_shield_oathkeeper` | Oathkeeper Aegis | Heal ally 3% max HP on Guard | 70 |
| Highward | `paladin_shield_highward` | Highward Aegis | Heal ally 4% max HP on Guard | 200 |
| Mercy | `paladin_shield_mercy` | Mercy Aegis | Heal ally 8% max HP on Guard | 360 |
| Censure | `paladin_shield_censure` | Censure Aegis | Ward Poison, Stun, Daze and Curse on guarded direct attacks | 360 |
| Verdict | `paladin_shield_verdict` | Verdict Aegis | Retaliate for 4 through guarded direct attack resolution | 360 |

Guard remains 50% in every family. Identical Guardian fields use the strongest
value, not a sum. Different fields can coexist; new accessories deliberately
carry no Guardian fields. See [Combat](COMBAT.md) for exact trigger limits.

## Body armor: six existing pieces

| Family | ID | Name | Armor | Resistance | Other bonuses | Base gold |
| --- | --- | --- | ---: | ---: | --- | ---: |
| Novice | `paladin_armor_novice` | Novice Plate | 2 | 1 | None | 12 |
| Oathkeeper | `paladin_armor_oathkeeper` | Oathkeeper Plate | 4 | 2 | None | 70 |
| Highward | `paladin_armor_highward` | Highward Plate | 6 | 3 | None | 200 |
| Mercy | `paladin_armor_mercy` | Mercy Plate | 7 | 4 | +2 VIT | 360 |
| Censure | `paladin_armor_censure` | Censure Plate | 7 | 4 | +1 SPD | 360 |
| Verdict | `paladin_armor_verdict` | Verdict Plate | 7 | 6 | None | 360 |

## Headpieces: six existing helms

| Family | ID | Name | Armor | Resistance | Other bonuses | Base gold |
| --- | --- | --- | ---: | ---: | --- | ---: |
| Novice | `paladin_helmet_novice` | Novice Helm | 1 | 1 | None | 12 |
| Oathkeeper | `paladin_helmet_oathkeeper` | Oathkeeper Helm | 2 | 1 | None | 70 |
| Highward | `paladin_helmet_highward` | Highward Helm | 3 | 2 | None | 200 |
| Mercy | `paladin_helmet_mercy` | Mercy Helm | 4 | 3 | +2 VIT | 360 |
| Censure | `paladin_helmet_censure` | Censure Helm | 4 | 3 | +1 SPD | 360 |
| Verdict | `paladin_helmet_verdict` | Verdict Helm | 4 | 5 | None | 360 |

## Boots: six existing pairs

| Family | ID | Name | Armor | Resistance | Other bonuses | Base gold |
| --- | --- | --- | ---: | ---: | --- | ---: |
| Novice | `paladin_boots_novice` | Novice Sabatons | 1 | 0 | None | 12 |
| Oathkeeper | `paladin_boots_oathkeeper` | Oathkeeper Sabatons | 2 | 1 | None | 70 |
| Highward | `paladin_boots_highward` | Highward Sabatons | 3 | 2 | None | 200 |
| Mercy | `paladin_boots_mercy` | Mercy Sabatons | 3 | 3 | +2 VIT | 360 |
| Censure | `paladin_boots_censure` | Censure Sabatons | 3 | 3 | +1 SPD | 360 |
| Verdict | `paladin_boots_verdict` | Verdict Sabatons | 3 | 5 | None | 360 |

## Trinket slot: six new keepsakes

Status: **authored; live verification pending**. One can be equipped at a time. Each supplies ordinary
stats to any class. These effects neither restore Focus nor increase its
capacity, cleanse, retaliate, add immunity or improve Guard's reduction.

| Family | ID | Name | Complete bonuses | Base gold |
| --- | --- | --- | --- | ---: |
| Novice | `paladin_trinket_novice` | Tin Oath Token | +1 VIT | 8 |
| Oathkeeper | `paladin_trinket_oathkeeper` | Keeper's Seal | +2 VIT | 35 |
| Highward | `paladin_trinket_highward` | Watchtower Reliquary | +2 VIT, +1 Resistance | 110 |
| Mercy | `paladin_trinket_mercy` | Lantern of Mercy | +1 Armor, +3 Resistance | 250 |
| Censure | `paladin_trinket_censure` | Seal of Censure | +2 SPD, +1 Armor | 250 |
| Verdict | `paladin_trinket_verdict` | Scales of Verdict | +2 Armor, +2 Resistance | 250 |

## Necklace slot: six new pendants

Status: **authored; live verification pending**. One can be equipped alongside a trinket. No matching-set
bonus applies. A Guardian may mix a necklace from one branch with another
branch's charm, apparel, weapon and shield.

| Family | ID | Name | Complete bonuses | Base gold |
| --- | --- | --- | --- | ---: |
| Novice | `paladin_necklace_novice` | Pilgrim's Pendant | +1 Resistance | 8 |
| Oathkeeper | `paladin_necklace_oathkeeper` | Oath Chain | +1 Resistance, +1 VIT | 35 |
| Highward | `paladin_necklace_highward` | Highward Gorget | +2 Resistance | 110 |
| Mercy | `paladin_necklace_mercy` | Mercy Locket | +2 Resistance, +1 SPD | 250 |
| Censure | `paladin_necklace_censure` | Censure Medallion | +2 SPD | 250 |
| Verdict | `paladin_necklace_verdict` | Judge's Collar | +3 Resistance | 250 |

Mercy accessories split defense and Speed; Censure prioritizes Speed; Verdict
prioritizes Armor and Resistance. No new endgame accessory strictly dominates
another in the same slot at the same price. Matching colors grant no extra
benefit, so swapping one piece remains a meaningful choice.

Highward Gorget and Judge's Collar are item/display identities, not a promise
of a new visible neck attachment on the character. All twelve items have
original object art and icons; their native display surfaces still need live
verification.

## Combined loadout checks

These are additive **calculated totals**, combining the authored class sheet with
body, head and boots. They exclude native difficulty bonuses, levels, sanctums,
consumables and other external effects. The accessory column adds the matching
trinket and necklace. These are source calculations, not observed stat panels.

| Family | Existing Armor / Resistance | With new accessories | Existing VIT / SPD, before shield | With accessories VIT / SPD, before shield |
| --- | --- | --- | --- | --- |
| Novice | 4 / 2 | 4 / 3 | 80 / 60 | 81 / 60 |
| Oathkeeper | 8 / 4 | 8 / 5 | 80 / 60 | 83 / 60 |
| Highward | 12 / 7 | 12 / 10 | 80 / 60 | 82 / 60 |
| Mercy | 14 / 10 | 15 / 15 | 86 / 60 | 86 / 61 |
| Censure | 14 / 10 | 15 / 10 | 80 / 63 | 80 / 67 |
| Verdict | 14 / 16 | 16 / 21 | 80 / 60 | 80 / 60 |

An ordinary shield subtracts 2 Speed from either column; Last Bastion subtracts
4. A 2H build pays neither penalty. Accessories leave authored Focus at 3;
actual maximum Focus also includes native difficulty and other modifiers.

| Authored endgame loadout | Base normal hit / checks | Armor / Resistance | VIT / SPD | Distinct reason to choose it |
| --- | --- | --- | --- | --- |
| Mercy 1H + Mercy Aegis | 28 / 3 | 15 / 15 | 86 / 59 | 8% on-Guard heal and 10% focused-hit heal |
| Mercy 2H | 34 / 4 | 15 / 15 | 86 / 61 | 12% focused-hit healing and larger attacks, no shield perk |
| Censure 1H + Censure Aegis | 28 / 3 | 15 / 10 | 80 / 65 | Debuff prevention, initiative and control choices |
| Censure 2H | 34 / 4 | 15 / 10 | 80 / 67 | Stronger Censure Armor reduction, no shield prevention |
| Verdict 1H + Verdict Aegis | 31 / 3 | 16 / 21 | 80 / 58 | Personal magic defense, harder normal hits and retaliation |
| Verdict 2H | 37 / 4 | 16 / 21 | 80 / 60 | Highest ordinary base hit, no shield retaliation |

Mixed-build check: Mercy apparel with the Censure trinket and necklace totals
15 Armor, 10 Resistance, 86 VIT and 64 SPD before a shield. It trades five
Resistance relative to full authored Mercy for three Speed, keeping Armor and
Vitality unchanged. The choice should remain readable without equipping a full set.

Mercy apparel reaches 86 Vitality before outside bonuses. Apprentice adds five
points, reaching 91, below the native 95-point cap. Its accessories retain
defense and Speed instead of adding further Vitality. The ceiling and
penalty ordering are recorded in [Native baseline](NATIVE-BASELINE.md). Verdict's 21 Resistance is another explicit playtest concern; compare
physical and magical enemies and native accessory alternatives.

## Native comparison and implementation constraints

Fresh installed-data anchors for the accessory budget:

| Native item key | Native slot and modifiers | Rarity / item levels | Base gold |
| --- | --- | --- | ---: |
| `trinketDefense1` | Trinket; +2 Armor | Common / 0-2 | 24 |
| `trinketDefense2` | Trinket; +4 Armor | Uncommon / 3-6 | 255 |
| `trinketMagic1` | Trinket; +2 Resistance | Common / 0-2 | 20 |
| `trinketMagic2` | Trinket; +4 Resistance | Uncommon / 2-6 | 180 |
| `amuletVitality1` | Necklace; +3 VIT, +4 maximum HP | Common / 0-2 | 12 |
| `amuletVitality2` | Necklace; +4 VIT, +8 maximum HP | Uncommon / 3-6 | 170 |

These are comparison points, not cloned bonus promises. The new accessories
are deliberately modest, but their prices and split-stat value still need
playtesting against native finds. A native Vitality necklace can remain a
reasonable alternative; class-themed equipment need not replace every native
item. Slot coverage does not establish that an item is worth buying.

`trinketDefense1` and `amuletVitality1` are native template candidates for the two
routes. The current typed Armor/Resistance/Vitality/Speed fields can express all
authored modifiers; no new trigger language, stat or resource is required.
Explicitly declare the intended modifier row so the native amulet's HP bonus is
not accidentally retained. Inspect each chosen template's renderer paths,
acquisition gates and other inherited fields before authoring the rows.

## Artifacts and inventory boundary

| Existing ID | Name | Slot | Base damage / checks or complete modifiers |
| --- | --- | --- | --- |
| `paladin_hammer_1h_last_vigil` | The Last Vigil | 1H | 30 / 3 VIT; Unbroken Watch |
| `paladin_hammer_2h_kingsfall` | Kingsfall | 2H | 42 / 5 VIT; Reckoning |
| `paladin_shield_last_bastion` | The Last Bastion | Shield | 0 Armor, 0 Resistance, -4 SPD; Stand Firm |

[Artifacts](ARTIFACTS.md) is authoritative for their effects and tradeoffs.
These are optional existing choices, not three additional planned accessories.

The source inventory is **48 ordinary items plus 3 artifacts = 51 equipment
items**. Keeping one class and the existing two Censure proficiency rows yields
**54 content entries**, compared with the previous 42. Accessory IDs are stable
strings; enum identities still come from the framework allocator.

Implementation and acceptance are tracked in [the gap plan](GAPS.md). Source
counts and offline validation do not establish registration, acquisition, stat
deltas, display fit or save behavior. Those live gates remain open.
