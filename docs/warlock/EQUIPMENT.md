# Warlock equipment progression

Status: paper design, 2026-10-06. Nothing is authored, registered, or tested. Phase labels follow [Design](DESIGN.md): **phase 1** (Hallow's Eve playtest, six items), **phase 2** (Afflictions, 21 items), **phase 3** (Pacts, 18 items). All stats, prices, and multipliers are design targets. Native comparison values are tagged **[A]** (decoded from the installed serialized assets with the decompiled field order, same build as [Native baseline](NATIVE-BASELINE.md)) or **[V]** (read from the installed assembly). Unverified facts are listed under [Open checks](#open-checks).

## Inventory at a glance

| Family | Slot | Bands 1 to 4 | Final band (three alternatives) | Count |
| --- | --- | --- | --- | ---: |
| Grimoire | Weapon, two hands | Fire path tomes | Tuned Hexfire, steady, and swarm variants | 7 |
| Hexbook | Weapon, two hands | Shadow and affliction path tomes | Curse and affliction-tier variants | 7 |
| Hood | Helmet | Warlock hoods | Cindercall, Gloamhunt, Hollowward hoods | 7 |
| Robe | Body armor | Warlock robes | Cindercall, Gloamhunt, Hollowward robes | 7 |
| Boots | Boots | Warlock boots | Cindercall, Gloamhunt, Hollowward boots | 7 |
| Trinket | Trinket | Lanterns | Three demon binding trinkets | 7 |
| Artifact tomes | Weapon, two hands | n/a | See [Artifacts](ARTIFACTS.md) | 3 |
| **Total** | | | | **45** |

| Phase | Items | Count |
| --- | --- | ---: |
| 1 | Moth-Eaten Grimoire, Smoldering Grimoire, Cobweb Hexbook, Nightshade Hexbook, Gourdlight Lantern, Witchlight Lantern | 6 |
| 2 | Grimoire and Hexbook bands 3 and 4; hood, robe, and boots bands 1 to 4; lantern bands 3 and 4; three artifact tomes | 21 |
| 3 | Three final Grimoires, three final Hexbooks, three demon sets of four pieces | 18 |

## Conventions and bands

Band labels are shared by every family so mixed outfits read as one tradition ([Art direction](ART-DIRECTION.md)).

| Band | Label | Native item level | Campaign guide | Rarity | Source policy |
| --- | --- | --- | --- | --- | --- |
| 1 | Hedge | 0-1 | Start and first region | Common | Start grant (two items); town market and ordinary loot |
| 2 | Coven | 1-2 | Roughly party level 2 onward | Common | Town market and ordinary loot |
| 3 | Gravemoss | 2-3 | Roughly party level 4 onward | Uncommon | Town market and ordinary loot |
| 4 | Witching Hour | 3-4 | Roughly party level 6 onward | Uncommon | Town market and ordinary loot |
| Final tomes | Pact | 4-6 | Roughly party level 8 onward | Rare | Town, night market, dungeon merchant, ordinary loot |
| Final apparel and bindings | Pact | 4-6 | Roughly party level 8 onward | Rare | Equipment exchange only (see [Acquisition](#acquisition)) |
| Artifact tomes | n/a | 4-6 | Optional late finds | Artifact | Night market, dungeon merchant, ordinary loot; no town stock |

**Item level is not character level.** This is the [Paladin acquisition lesson](../paladin/ACQUISITION-AUDIT.md). [A] Native progression tiers map item levels 0, 1, 2, 3, 4, 4 to expected party levels 0, 2, 4, 6, 8, 9, and no dropable native equipment exceeds item level 6. Every band above stays inside 0 to 6. Adjacent bands overlap by one level, as in the [Thief bands](../thief/EQUIPMENT.md), so a slightly older piece stays obtainable.

- **Damage** is base weapon damage with damage gain 1, before level growth. At player level 8 a base 25 tome deals 33 before other modifiers, following the Thief convention. [A] Every native tome and staff checked uses gain 1.
- **Tomes** use magic damage, Intelligence (`SkillType.fortitude`), no break chance, ordinary Focus use, and **two hands** ([A] the native `spellbook` and every native tome decode as `twoHands`; see open check 1). Tomes add no passive stats.
- **Armor and Resistance** are flat points. VIT, SPD, AWR, and TAL are whole stat points (+2 VIT is a modifier of `0.02`). Focus bonuses use `focusCapacity`: they raise maximum Focus and never fill it.
- **No Intelligence on apparel.** The public modifier schema ([Writing content](../WRITING-CONTENT.md)) offers armor, resistance, vitality, speed, awareness, talent, focusCapacity, and reflect. Native caster robes, hats, and boots grant Intelligence; Warlock apparel instead grants Vitality (the Pact budget) and Speed, with no Strength penalty. This is a deliberate design choice, not a request to extend the schema.
- **Gold** values are proposed `_goldValue` inputs, not guaranteed purchase prices. [A] Native tomes and caster apparel have `m_PriceScale` off; economy and difficulty multipliers still apply.
- Every listed modifier set is complete. Templates' inherited modifiers, immunities, skills, critical chance, evasion, and unrelated acquisition flags must be cleared, as the Thief and Paladin packages do.
- All items are usable by any class. Warlock actions, afflictions, and demon bonuses require the Warlock class ([Combat](COMBAT.md) section 1). A non-Warlock holding a Warlock tome gets Umbral Bolt only.

### Tier mapping

Burn and affliction tiers follow [Combat](COMBAT.md) sections 4 and 8. Curse tiers are not mapped by Combat; this document applies the same mapping.

| Tome band | Burn (ticks x damage) | Blight total | Rot total | Curse of Frailty / Ruin |
| --- | --- | ---: | ---: | --- |
| 1 and 2 | 6 x 2 = 12 | 16 (T1) | n/a (Rot from band 3) | T1: -10 |
| 3 | 6 x 4 = 24 | 28 (T2) | 30 (T2) | T2: -20 |
| 4 and final | 6 x 6 = 36 | 40 (T3) | 42 (T3) | T3: -30 |

Curse of Lethargy (-0.25 speed) and Curse of Feebleness (-0.25 attack) have one native value and do not tier. Two Hexbook finals deliberately carry tier 2 afflictions (see below); that is an extension of the Combat mapping.

## Grimoires: seven fire tomes

Fire, burst, and burn. Every Grimoire carries Umbral Bolt, Hexfire, and Cinderbrand. Hollow Fright joins from band 2 and Cinderstorm from band 4.

| Band | ID | Name | Item level | Rarity | INT checks | Base dmg | Actions | Base gold | Acquisition | Phase |
| --- | --- | --- | --- | --- | ---: | ---: | --- | ---: | --- | ---: |
| 1 Hedge | `warlock_grimoire_hedge` | Moth-Eaten Grimoire | 0-1 | Common | 3 | 7 | Umbral Bolt, Hexfire, Cinderbrand (burn T1) | 15 | Start, T, L | 1 |
| 2 Coven | `warlock_grimoire_coven` | Smoldering Grimoire | 1-2 | Common | 3 | 13 | Umbral Bolt, Hexfire, Cinderbrand (burn T1), Hollow Fright | 50 | T, L | 1 |
| 3 Gravemoss | `warlock_grimoire_gravemoss` | Ashbound Grimoire | 2-3 | Uncommon | 3 | 18 | Umbral Bolt, Hexfire, Cinderbrand (burn T2), Hollow Fright | 120 | T, L | 2 |
| 4 Witching Hour | `warlock_grimoire_witching` | Hallowfire Grimoire | 3-4 | Uncommon | 3 | 22 | Umbral Bolt, Hexfire, Cinderbrand (burn T3), Hollow Fright, Cinderstorm (burn T3) | 230 | T, L | 2 |
| Final | `warlock_grimoire_ember` | Ember Psalter | 4-6 | Rare | 3 | 25 | As band 4, but **Hexfire 1.5x for 12%** (minimum 2) | 440 | T, N, D, L | 3 |
| Final | `warlock_grimoire_bargains` | Ledger of Small Bargains | 4-6 | Rare | 3 | 25 | As band 4, but **Hexfire 1.25x for 7%** (minimum 2) | 440 | T, N, D, L | 3 |
| Final | `warlock_grimoire_bonfire` | Bonfire Canticle | 4-6 | Rare | 3 | 23 | As band 4, but **Cinderbrand 4%** (minimum 1) and **Cinderstorm 0.85x** | 440 | T, N, D, L | 3 |

Unlisted action values are the [Combat](COMBAT.md) defaults: Hexfire 1.35x for 10%, Cinderbrand 0.6x for 5%, Hollow Fright 0.5x for 6% with a 50% perfect-roll Daze, Cinderstorm 0.7x Aoe for 12%.

- **Ember Psalter** (burst; echoes Cindercall). Three perfect Hexfires into a boss cost 36% of max HP for +150% of one hit over three Umbral Bolts, against +105% for 30% on an ordinary tome. It is the strongest finisher and the riskiest budget.
- **Ledger of Small Bargains** (steady; echoes Hollowward). Three Hexfires cost 21% for +75%. This is the first tuning lever [Design](DESIGN.md) names, offered as a choice instead of a nerf. Grave Bargain refunds are smaller because the cost is smaller.
- **Bonfire Canticle** (swarm; echoes Gloamhunt's pack). Lower base damage buys a stronger Cinderstorm and a cheaper Cinderbrand. Cinderstorm's cost is unchanged so the Cinder Imp's Imp's Tithe (12% to 9%, [Demons](DEMONS.md)) applies without a stacking rule.

## Hexbooks: seven shadow tomes

Curses, afflictions, and drain. Every Hexbook carries Umbral Bolt, exactly one curse, and Siphon Soul. Blight is on every Hexbook from phase 2, Hollow Fright from band 2, and Rot from band 3. Hexbook base damage sits one point below the Grimoire of the same band because its value is flat damage over time, debuffs, and healing rather than multipliers.

| Band | ID | Name | Item level | Rarity | INT checks | Base dmg | Curse | Actions | Base gold | Acquisition | Phase |
| --- | --- | --- | --- | --- | ---: | ---: | --- | --- | ---: | --- | ---: |
| 1 Hedge | `warlock_hexbook_hedge` | Cobweb Hexbook | 0-1 | Common | 3 | 6 | Feebleness | Umbral Bolt, curse, Siphon Soul; Blight T1 added in phase 2 | 18 | T, L | 1 |
| 2 Coven | `warlock_hexbook_coven` | Nightshade Hexbook | 1-2 | Common | 3 | 12 | Frailty T1 (-10) | Umbral Bolt, curse, Siphon Soul, Hollow Fright; Blight T1 added in phase 2 | 50 | T, L | 1 |
| 3 Gravemoss | `warlock_hexbook_gravemoss` | Gravemoss Hexbook | 2-3 | Uncommon | 3 | 17 | Ruin T2 (-20) | Umbral Bolt, curse, Siphon Soul, Hollow Fright, Blight T2, Rot T2 | 120 | T, L | 2 |
| 4 Witching Hour | `warlock_hexbook_witching` | Raven-Quill Hexbook | 3-4 | Uncommon | 3 | 21 | Lethargy (-0.25) | Umbral Bolt, curse, Siphon Soul, Hollow Fright, Blight T3, Rot T3 | 230 | T, L | 2 |
| Final | `warlock_hexbook_withering` | Almanac of Withering | 4-6 | Rare | 3 | 24 | Ruin T3 (-30) | As band 4 with this curse; Blight T3, Rot T3 | 440 | T, N, D, L | 3 |
| Final | `warlock_hexbook_hushwillow` | Hushwillow Hymnal | 4-6 | Rare | **2** | 21 | Feebleness | As band 4 with this curse; Blight T3, **Rot T2** | 440 | T, N, D, L | 3 |
| Final | `warlock_hexbook_bramblethorn` | Bramblethorn Bestiary | 4-6 | Rare | 3 | 25 | Frailty T3 (-30) | As band 4 with this curse; **Blight T2, Rot T2**, **Siphon Soul 0.75x** | 440 | T, N, D, L | 3 |

- **Curse order across bands.** Feebleness first (defensive, forgiving), Frailty second (helps physical allies), Ruin third (helps all magic, including the Warlock's own hits), Lethargy fourth (tempo). Because the curse is a tome choice, a player may keep an older Hexbook for its curse.
- **Phase 1 Hexbooks** ship without Blight because P2 does not exist yet. Phase 2 adds Blight T1 to the same IDs; that is an additive action-list change and needs a save and resume check (open check 9).
- **Almanac of Withering** (full affliction; echoes Gloamhunt). The standard: strongest curse tier and full affliction tiers. Feeds Gloam Hound's Feast.
- **Hushwillow Hymnal** (reliable control; echoes Hollowward). Two checks raise an unfocused perfect roll at 78 Intelligence from about 47.5% to 60.8%, so curses and Blight land more often. It pays with lower damage and a weaker Rot.
- **Bramblethorn Bestiary** (drain bruiser; echoes Cindercall). Highest Hexbook damage and a stronger Siphon Soul (heals 50% of a 0.75x hit) for weaker afflictions.

### Action variants required

Each tuned value is a separate registered proficiency row, attached through class-gated weapon proficiency groups keyed by exact tome IDs ([Combat proficiencies](../COMBAT-PROFICIENCIES.md)).

| Action | Variants |
| --- | --- |
| Hexfire | Standard 1.35x/10%; Ember 1.5x/12%; Bargains 1.25x/7%; Midnight Ledger artifact |
| Cinderbrand | T1, T2, T3; Bonfire T3 at 4%; Cinderheart artifact |
| Cinderstorm | T3; Bonfire 0.85x; Cinderheart artifact |
| Hollow Fright | Standard; Midnight Ledger artifact |
| Curses | Feebleness; Frailty T1, T3; Ruin T2, T3; Lethargy |
| Siphon Soul | 0.6x; Bramblethorn 0.75x |
| Blight | T1, T2, T3; Blightbloom artifact |
| Rot | T2, T3 |

## Hoods: seven headpieces

Faces remain visible ([Art direction](ART-DIRECTION.md)). Hoods trade native wizard hats' Intelligence and Focus for Awareness and Talent.

| Band | ID | Name | Item level | Rarity | Armor | Resistance | Other | Base gold | Acquisition | Phase |
| --- | --- | --- | --- | --- | ---: | ---: | --- | ---: | --- | ---: |
| 1 Hedge | `warlock_hood_hedge` | Patchwork Cowl | 0-1 | Common | 0 | 2 | +1 AWR | 12 | T, L | 2 |
| 2 Coven | `warlock_hood_coven` | Coven Hood | 1-2 | Common | 0 | 4 | +2 AWR | 28 | T, L | 2 |
| 3 Gravemoss | `warlock_hood_gravemoss` | Gravemoss Hood | 2-3 | Uncommon | 1 | 6 | +2 AWR, +1 TAL | 65 | T, L | 2 |
| 4 Witching Hour | `warlock_hood_witching` | Witching-Hour Hood | 3-4 | Uncommon | 1 | 8 | +3 AWR, +2 TAL | 130 | T, L | 2 |
| Final | `warlock_hood_cindercall` | Cindercall Cowl | 4-6 | Rare | 1 | 10 | +3 AWR | 275 | X | 3 |
| Final | `warlock_hood_gloamhunt` | Gloamhunt Hood | 4-6 | Rare | 1 | 9 | +4 AWR | 275 | X | 3 |
| Final | `warlock_hood_hollowward` | Hollowward Cowl | 4-6 | Rare | 2 | 8 | +3 VIT | 275 | X | 3 |

## Robes: seven body pieces

Robes match native robe Armor and Resistance band for band and keep the native robe's +1 Focus. Vitality replaces Intelligence, with no Strength penalty and no Curse immunity.

| Band | ID | Name | Item level | Rarity | Armor | Resistance | Other | Base gold | Acquisition | Phase |
| --- | --- | --- | --- | --- | ---: | ---: | --- | ---: | --- | ---: |
| 1 Hedge | `warlock_robe_hedge` | Hedgewitch Robe | 0-1 | Common | 1 | 3 | +2 VIT, +1 Focus | 27 | T, L | 2 |
| 2 Coven | `warlock_robe_coven` | Candlewax Robe | 1-2 | Common | 2 | 6 | +3 VIT, +1 Focus | 64 | T, L | 2 |
| 3 Gravemoss | `warlock_robe_gravemoss` | Gravemoss Robe | 2-3 | Uncommon | 3 | 9 | +4 VIT, +1 Focus | 135 | T, L | 2 |
| 4 Witching Hour | `warlock_robe_witching` | Witching-Hour Robe | 3-4 | Uncommon | 4 | 12 | +5 VIT, +1 Focus | 300 | T, L | 2 |
| Final | `warlock_robe_cindercall` | Cindercall Robe | 4-6 | Rare | 6 | 14 | +5 VIT, +1 Focus | 620 | X | 3 |
| Final | `warlock_robe_gloamhunt` | Gloamhunt Mantle | 4-6 | Rare | 6 | 13 | +5 VIT, +1 Focus | 620 | X | 3 |
| Final | `warlock_robe_hollowward` | Hollowward Shroud | 4-6 | Rare | 7 | 13 | +6 VIT, +1 Focus | 620 | X | 3 |

## Boots: seven pairs

Boots match native wizard boots' Armor and Resistance. Speed and Vitality replace Intelligence; Speed is the Warlock's weak stat (56).

| Band | ID | Name | Item level | Rarity | Armor | Resistance | Other | Base gold | Acquisition | Phase |
| --- | --- | --- | --- | --- | ---: | ---: | --- | ---: | --- | ---: |
| 1 Hedge | `warlock_boots_hedge` | Leafmold Slippers | 0-1 | Common | 0 | 2 | +1 SPD | 13 | T, L | 2 |
| 2 Coven | `warlock_boots_coven` | Coven Boots | 1-2 | Common | 1 | 3 | +2 SPD | 26 | T, L | 2 |
| 3 Gravemoss | `warlock_boots_gravemoss` | Gravemoss Boots | 2-3 | Uncommon | 2 | 4 | +2 SPD, +1 VIT | 60 | T, L | 2 |
| 4 Witching Hour | `warlock_boots_witching` | Witching-Hour Boots | 3-4 | Uncommon | 3 | 5 | +3 SPD, +2 VIT | 135 | T, L | 2 |
| Final | `warlock_boots_cindercall` | Cindercall Slippers | 4-6 | Rare | 3 | 6 | +4 SPD | 275 | X | 3 |
| Final | `warlock_boots_gloamhunt` | Gloamhunt Boots | 4-6 | Rare | 3 | 5 | +3 SPD, +2 AWR | 275 | X | 3 |
| Final | `warlock_boots_hollowward` | Hollowward Treads | 4-6 | Rare | 4 | 5 | +2 SPD, +2 VIT | 275 | X | 3 |

## Trinkets: four lanterns and three demon bindings

Only one trinket can be worn. Lanterns are icons plus inventory, loot, shop, and item-card display objects; no worn attachment is promised ([Art direction](ART-DIRECTION.md)). Removing a Focus capacity bonus clamps excess current Focus, and equipping never refills it, so a swap loop cannot generate Focus.

| Band | ID | Name | Item level | Rarity | Armor | Resistance | Other | Base gold | Acquisition | Phase |
| --- | --- | --- | --- | --- | ---: | ---: | --- | ---: | --- | ---: |
| 1 Hedge | `warlock_lantern_gourdlight` | Gourdlight Lantern | 0-1 | Common | 0 | 0 | +2 VIT | 10 | Start, T, L | 1 |
| 2 Coven | `warlock_lantern_witchlight` | Witchlight Lantern | 1-2 | Common | 0 | 2 | +3 VIT | 40 | T, L | 1 |
| 3 Gravemoss | `warlock_lantern_foxfire` | Foxfire Lantern | 2-3 | Uncommon | 0 | 2 | +3 VIT, +1 Focus | 120 | T, L | 2 |
| 4 Witching Hour | `warlock_lantern_hallowmoon` | Hallowmoon Lantern | 3-4 | Uncommon | 0 | 4 | +4 VIT, +1 Focus | 260 | T, L | 2 |
| Final | `warlock_binding_cindercall` | Imp-Coal Censer | 4-6 | Rare | 0 | 4 | +3 VIT | 290 | X | 3 |
| Final | `warlock_binding_gloamhunt` | Gloamhound Collar Tag | 4-6 | Rare | 0 | 3 | +2 AWR, +1 Focus | 290 | X | 3 |
| Final | `warlock_binding_hollowward` | Hollow Warden's Reliquary | 4-6 | Rare | 2 | 3 | +3 VIT | 290 | X | 3 |

The binding trinket is the fourth piece of its set. It binds nothing on its own.

## Demon set bonuses

[Demons](DEMONS.md) is authoritative for set rules, familiar triggers, and presentation; this is a summary. Bonuses require the generic set primitive P3 and familiar primitive P4 ([Gaps](GAPS.md)), both phase 3.

| Set | Pieces | 2 pieces (any wearer) | 4 pieces (Warlock only) |
| --- | --- | --- | --- |
| Cindercall Regalia | Cindercall Cowl, Cindercall Robe, Cindercall Slippers, Imp-Coal Censer | +1 Focus capacity, +5 Resistance | Binds the **Cinder Imp**: Ember Toss, Imp's Tithe |
| Gloamhunt Raiment | Gloamhunt Hood, Gloamhunt Mantle, Gloamhunt Boots, Gloamhound Collar Tag | +5 AWR, +5 Resistance | Binds the **Gloam Hound**: Devour, Feast |
| Hollowward Vestments | Hollowward Cowl, Hollowward Shroud, Hollowward Treads, Hollow Warden's Reliquary | +5 VIT, +3 Armor | Binds the **Hollow Warden**: Shared Burden, Stand Between |

Any tome works with any set. Artifacts have no set affinity. Two pieces from each of two sets grant both minor bonuses and no demon; [Demons](DEMONS.md) does not state this case, so it is a proposed P3 rule (open check 11).

## Per-band power budget

### Tomes against native equivalents

[A] native rows: base damage / checks, item level, base gold. Native tomes have three checks except the Lightning tomes (four). Staffs carry more damage behind four or five checks.

| Band | Item level | Native tomes | Native staffs | Grimoire / Hexbook | Hexfire perfect (1.35x) | Burn | Blight / Rot (flat) |
| --- | --- | --- | --- | --- | ---: | ---: | --- |
| 1 | 0-1 | `spellbook` 6/3 (start, 15g), `tomeFire` 8/3 (22g), `tomeDusty` 9/3 (20g); `tomeApprentice` 13/3 at 1 | `staffCane` 6/3; `staffApprentice` 15/4 at 1 | 7 / 6 | 9.5 | 12 | 16 / n/a |
| 2 | 1-2 | `tomeIce` 12/3, `tomeWater` 12/3, `tomeApprentice` 13/3; `tomeGilded` 15/3, `tomeMage` 16/3 at 2 (45 to 95g) | `staffFire` 20/4, `staffMage` 20/5 at 2 | 13 / 12 | 17.6 | 12 | 16 / n/a |
| 3 | 2-3 | `tomeMage` 16/3 at 2; `tomeWizard` 21/3, `tomeFireGreat` 22/3 at 3 (200 to 205g) | `staffFireGreat` 25/4 at 3 | 18 / 17 | 24.3 | 24 | 28 / 0.4x + 30 |
| 4 | 3-4 | `tomeWizard` 21/3, `tomeIron` 20/3 at 3-4 (240g), `tomeLightningGreat` 22/4 at 4 | `staffWizard` 28/5 at 3-4 | 22 / 21 | 29.7 | 36 | 40 / 0.4x + 42 |
| Final | 4-6 | `tomeChaos` 24/3 (410g), `tomeWonder` 26/3 (450g), `tomeWaterGreat` 26/3 (570g) | `staffIceGreat` 30/4, `staffChaos` 32/5 | 23-25 / 21-25 | 31.1 to 37.5 | 36 | 28-40 / 0.4x + 30-42 |

Reading the table:

- Warlock tomes sit at or just below native tomes of the same level. Pact multipliers, paid in HP, supply the burst; the base curve must not also outpace native tomes.
- At bands 1 and 2, Cinderbrand (0.6x plus 12 burn for 5%) outvalues Hexfire against a healthy, burnable target. That is intended: Hexfire is the finisher that earns Grave Bargain. Flat burn and afflictions lose relative weight as tome damage grows with level. At player level 8 a final Grimoire's base is 33, Hexfire 44.6 and Ember Psalter 49.5, while burn stays 36 and Blight stays 40.
- Hexbook value is not in its base damage: a curse, 0.6x self-healing, and flat armor-ignoring afflictions.

### Apparel and trinket totals against native caster gear

Warlock totals sum hood, robe, boots, and lantern. Native totals sum the native wizard hat, robe, wizard boots, and a Focus trinket at the matching level ([A] `helmetWizard1` to `5`, `armorRobe1` to `5`, `bootsWizard1` to `5`, `trinketFocus1` and `2`). Final rows include each set's two-piece bonus.

| Band | Warlock armor / resistance | Warlock other | Native armor / resistance | Native other |
| --- | --- | --- | --- | --- |
| 1 | 1 / 7 | +4 VIT, +1 AWR, +1 SPD, +1 Focus | 1 / 7 | +4 INT, -3 STR, +3 Focus |
| 2 | 3 / 15 | +6 VIT, +2 AWR, +2 SPD, +1 Focus | 3 / 13 | +7 INT, -5 STR, +3 Focus, Curse and Confuse immunity |
| 3 | 6 / 21 | +8 VIT, +2 AWR, +1 TAL, +2 SPD, +2 Focus | 5 / 19 | +10 INT, -7 STR, +3 Focus, immunities |
| 4 | 8 / 29 | +11 VIT, +3 AWR, +2 TAL, +3 SPD, +2 Focus | 7 / 25 | +13 INT, -9 STR, +4 Focus, immunities |
| Cindercall | 10 / 39 | +8 VIT, +3 AWR, +4 SPD, +2 Focus; Cinder Imp | 10 / 31 (levels 4-6) | +15 INT, -10 STR, +4 Focus, immunities |
| Gloamhunt | 10 / 35 | +5 VIT, +13 AWR, +3 SPD, +2 Focus; Gloam Hound | 10 / 31 | as above |
| Hollowward | 18 / 29 | +19 VIT, +2 SPD, +1 Focus; Hollow Warden | 10 / 31 | as above |

The Warlock line gives up native Intelligence (roll reliability), one or two Focus, and native Curse and Confuse immunities. It gains Vitality, which enlarges the absolute HP buffer behind each percentage-based Pact cost, plus Speed, slightly more Resistance, and no Strength penalty. Native caster gear stays a valid alternative, especially for Hexbook players who depend on perfect rolls. The finals are horizontal: Cindercall is the Resistance and Speed set, Gloamhunt the Awareness set, Hollowward the Armor and Vitality set with one less Focus.

## Acquisition

Legend: **Start** = class starting grant; **T** = town market; **N** = night market; **D** = dungeon merchant; **L** = ordinary loot (`m_Dropable`); **X** = equipment exchange only.

| Group | Flags | Stock | Notes |
| --- | --- | ---: | --- |
| Moth-Eaten Grimoire, Gourdlight Lantern | Start, T, L | 1 | Exactly these two are granted ([Design](DESIGN.md)). Shop and loot eligibility lets a lost copy be replaced. |
| Bands 1 to 4, all families | T, L | 1 | [A] Matches native tomes and caster apparel, which are town plus loot, not night market or dungeon merchant. |
| Final Grimoires and Hexbooks | T, N, D, L | 1 | Ordinary routes so the tome curve never depends on token supply. |
| Demon set pieces (12) | X | n/a | One shared equipment token each through `townExchange`; ordinary shop and drop flags off. |
| Artifact tomes | N, D, L | 1 | No town stock; see [Artifacts](ARTIFACTS.md). |

All rows declare no DLC requirement, no Lore or quest gate, no class restriction, and an empty collection-lore identifier (the [Paladin lesson](../paladin/ACQUISITION-AUDIT.md) on invalid-lore collection). Eligibility is not a guaranteed spawn; acquisition must be observed through ordinary routes without appending rewards or forcing stock.

### Demon sets through the equipment exchange

The Warlock contributes a second catalog to the shared token service that Paladin introduced ([Combat proficiencies](../COMBAT-PROFICIENCIES.md), [Paladin equipment](../paladin/EQUIPMENT.md)).

- The class entry declares `townExchange: { "token": "equipment_token", "offers": [...] }` with twelve offers. Each offer names the item, display name, family (`Cindercall Regalia`, `Gloamhunt Raiment`, or `Hollowward Vestments`), and native slot (helmet, armor, boots, trinket).
- One token per piece; a complete demon costs four tokens. Stock shows only the buyer's class offers and hides pieces already owned.
- The Warlock declares **no** `enemyDropRule`. Token supply belongs to the Equipment Exchange package (Paladin's published rule: displayed combat level at least 8, 10% ordinary, 50% named boss, guaranteed by the sixth eligible opportunity). Warlock phase 3 must declare that package as a dependency.
- Exchange purchases are solo-only until network transactions are verified. Co-op stays unverified.
- Rationale: a demon needs four specific pieces. As random rare drops among twelve candidates, a complete set would be unreliable; the exchange makes the set a deliberate goal, while the final tomes stay on ordinary routes.

### Tailored weapon rewards

[V] `GameLogic.GetTailoredWeaponItem` requests the current progression item level plus one, then filters weapon rows by non-common rarity, the class's primary stat, inclusive level bounds, and lore unlock. It does not check `m_Dropable` and indexes a random entry without an empty-list guard. The Paladin crash came from an empty pool.

| Requested item level | Native non-common Intelligence weapons [A] | Warlock rows also eligible |
| ---: | ---: | --- |
| 1 | 14 | None (bands 1 and 2 are common) |
| 2 | 13 | Ashbound Grimoire, Gravemoss Hexbook |
| 3 | 15 | Band 3 and band 4 tomes |
| 4 | 12 | Band 4 tomes, six final tomes, three artifacts |
| 5 | 8 | Six final tomes, three artifacts |

The Warlock's primary stat is Intelligence, so the native pool is never empty and no fallback is needed. Counts are before lore-unlock exclusions (open check 8). Early tailored rewards will be native wands and staffs, which a Warlock can use with their basic attack only. Making band 2 tomes uncommon would add them to the level 2 pool; this design keeps them common to match the Thief and Paladin early bands. Artifact tomes can appear as late tailored rewards, as the native `ancientStaff` (artifact, levels 3-4) already can.

### Pool share

[A] At item level 4, the ordinary weapon pool holds 46 native dropable rows with a rarity weight total of 23.8 (common 1, uncommon 0.4, rare 0.2, artifact 0.1; see the [acquisition audit](../paladin/ACQUISITION-AUDIT.md)). Two band 4 tomes (0.8), six final tomes (1.2), and three artifacts (0.3) add 2.3, about 8.8% of that weight before lore, adventure, and artifact exclusions. The demon sets add nothing to apparel loot because they are exchange-only. These are conditional pool calculations, not measured frequencies. If several class packages together crowd native variety, narrow lower-band overlap before adding a loot-weight system.

## Release-time website work

Paper design only; no website change now. Each phase release must add item cards for that phase's items with the exact published values, the `mods/warlock` page sections for tomes, apparel, lanterns, demon sets, and the exchange catalog, and acquisition wording that keeps eligibility distinct from observed drops ([Gaps](GAPS.md) notes).

## Open checks

1. **Tome handedness.** [A] The native `spellbook` and every native tome decode with `m_ObjectSlot = twoHands`, and wands decode as `oneHand`. This design therefore makes every Warlock tome two-handed. Still open: confirm a cloned custom tome keeps two hands in the live equip screen and combat pose, and whether slot overrides behave the same on books ([Native baseline](NATIVE-BASELINE.md) check 1). A one-handed tome is not designed.
2. **Action button capacity.** Band 4 and final Hexbooks show six buttons (Umbral Bolt plus five actions) and band 4 Grimoires five. Confirm the native combat action bar fits them without overlap. Fallback: drop Hollow Fright from band 4 and final Hexbooks.
3. **Vitality to maximum HP.** The native formula linking VIT points to max HP was not decoded, so the HP value of apparel Vitality is unquantified.
4. **Intelligence modifiers.** The schema has no Intelligence field. The design does not need one; record it so nobody adds INT to these rows by editing a native modifier in place.
5. **Exchange API state.** [Writing content](../WRITING-CONTENT.md) still labels `townExchange` and `enemyDropRule` unreleased while published Paladin 2.0.1 uses the exchange. Confirm the release state and minimum framework version before phase 3.
6. **Rot T2 and Blight T2 on final Hexbooks.** These extend the Combat band-to-tier mapping. Confirm the package validator (see [Validation](VALIDATION.md)) accepts declared per-tome tiers rather than deriving tier from band alone.
7. **Curse tier by band.** Combat lists curse values by tier without a band mapping; this document maps them like burn. Confirm in Combat or revise.
8. **Lore exclusions** may shrink the tailored-reward counts above; a live check should confirm the level 5 pool (8 rows) stays non-empty for a fresh profile.
9. **Phase 2 Blight on phase 1 tomes.** Adding an action to an existing weapon ID needs a save made under phase 1 to resume cleanly under phase 2.
10. **Price scaling.** [A] `m_PriceScale` decodes off on native tomes and caster apparel; confirm the same flag on clones so displayed prices track the proposed gold values.
11. **Two partial sets.** Whether 2 + 2 pieces of different sets grants both minor bonuses is a P3 rule not stated in [Demons](DEMONS.md).
