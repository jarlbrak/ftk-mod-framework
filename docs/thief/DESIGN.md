# Thief: complete class design

Status: design baseline, 2026-09-23. Numbers are initial balance targets, not established gameplay balance. The development package implements the class, equipment, and authored assets; [Validation](VALIDATION.md) tracks the remaining runtime and delivery gates.

## The promise

**A quick, fragile opportunist who turns a teammate's distraction into a decisive strike, then survives by choosing the right tool.** The Thief is the party's burglar, scout, and precision attacker. A patched street outfit grows into a guild professional's equipment and finally one of three master styles: Locksmith, Nightblade, or Wayfarer. These are equipment choices within one class, not subclasses or permanent specializations.

The D&D inspirations are Sneak Attack, expertise with tools, Cunning Action, and the Thief's practical ingenuity. [The official Rogue overview](https://www.dndbeyond.com/classes/2190883-rogue) supplies those archetypal touchstones. Our rules are original FTK adaptations: turn order and ally attacks establish openings; native Focus secures the strike; a defensive action costs a full turn. There is no tabletop position, advantage roll, bonus-action economy, or subclass leveling system to reproduce.

Flavor: "Every locked door is a choice, and every shadow is a way through."

## The design in one fight

The Thief moves before a bandit and sees **Opening: has not acted**. A paired-dagger attack with every check successful deals a Sneak Attack. A near miss can still prepare the next strike through Twin Feint. Spending Focus secures checks but leaves less Focus for exploration and the next fight.

Later, the armored captain has already acted. The Paladin hits it and creates an opening. The Thief can spend Focus on a strong normal attack, use a lower damage armor-piercing action, or save Focus and accept partial damage. Against a foe the party cannot distract, Feint deals a small hit and prepares the Thief's next strike. Under pressure, Slip Away spends the turn on protection and preparation.

The reward is a readable, earned damage spike. The cost is low Vitality, dependence on turn order or preparation, and Focus competing with exploration needs. Bosses obey the same opening rules. Armor, faster enemies, damage over time, and attacks from several enemies remain meaningful answers to the kit. Evasion punishes partial attacks; native perfect player attacks already bypass enemy evasion.

## Class sheet

| Stat | Starting value | Intent |
| --- | ---: | --- |
| Strength | 54 | Serviceable physical checks, no heavy weapon specialty |
| Intelligence | 46 | Weak magical checks |
| Awareness | 72 | Secondary bow path and scouting |
| Talent | 74 | Tools and utility checks |
| Speed | 78 | Primary dagger stat and initiative |
| Vitality | 50 | Principal cost of the combat and exploration kit |
| Focus | 3 | Native resource; no baseline regeneration |
| Gold | 3 | Modest start |

Total of the six main stats: **374**. The primary stat is Speed. Starting equipment is Street Twins, Patched Jack, Street Neckerchief, Softstep Shoes, and one native Lockpicks consumable. The paired starter occupies both hands. No starting shield, charm, bow, rare item, or extra consumable is added. Exact gear is specified in [Equipment](EQUIPMENT.md).

Native character appearances remain available under normal unlock rules. The class has no new Lore prerequisite. Native paired weapons retain their existing DLC/ownership gates; compatibility of original custom pairs with installations lacking that DLC must be verified before declaring base-game support. All gear is transferable and usable by other classes; class-specific effects explicitly require the Thief's Opportunist capability.

## Abilities

| Ability | Rule | Player decision |
| --- | --- | --- |
| Elite Sneak | Native skill flag, unchanged native behavior | Choose fights and routes |
| Elite Ambush | Native skill flag, unchanged native behavior | Try to isolate a dangerous enemy |
| Elite Trap Disarm | Native skill flag, unchanged native behavior | Spend checks, Focus, or lockpicks appropriately |
| Sneak Attack | A perfect eligible physical attack against an Open enemy gains +35% of current weapon damage, once per own turn | Commit Focus to the right target and moment |
| Twin Feint | A normal paired strike with exactly one failed check that damages its target grants Prepared | A near miss sets up the next turn without a second attack |
| Feint / Draw Out | Weapon action: 60% damage; positive direct damage prepares the next eligible attack | Pay damage now to make an opening for later |
| Slip Away | Once per combat, full action: halve the next direct enemy attack against self before next turn and prepare a strike | Give up damage now to survive and set up |

All class abilities are available from the start. Weapons add or improve actions through acquisition. There is no separate experience currency, level unlock tree, passive counterattack, baseline critical bonus, or resource generation. Native skills retain their actual check behavior; none makes every chest or trap free.

**Open** means an enemy has not begun its first turn, or another party member has directly damaged it since its latest turn began. A Thief's Prepared token can qualify one attack independently. [Combat rules](COMBAT.md) define exact timing, exclusions, arithmetic, and tooltips.

## Two complete weapon paths

| Path | Governing stat | Checks | Equipment tradeoff | Feel |
| --- | --- | ---: | --- | --- |
| Paired daggers, primary | Speed | 2 early, 3 late | Two hands; no shield | Fast precision, Twin Feint, and a choice between reliability and a larger hit |
| Shortbow, secondary | Awareness | 4 | Two hands; no shield | A different accuracy investment, penetration actions, and the scout artifact |

Every band supplies both paths. The paired-dagger progression deliberately continues the native Speed-based `dualKnife` and `dualDagger` route into late-game equipment and artifacts. Weapons use physical damage and ordinary native level growth. These are custom items with authored actions and art. The native one-handed Dagger is a one-check Vitality weapon. One-handed dagger and buckler is an emergency fallback, with native actions and defenses but no Sneak Attack, Twin Feint, or prepared-strike payoff. It receives no custom progression or artifacts. Slip Away remains available for survival with any loadout.

At the end of the campaign, **Locksmith** improves preparation and the tool/Focus budget, **Nightblade** offers raw damage and Speed at the cost of magical protection and piercing options, and **Wayfarer** supports bow accuracy and armor penetration. Mixing pieces is encouraged. There are no set bonuses or requirements to wear matching pieces.

## Full inventory and artifact weapons

The complete paper inventory contains **45 equipment items**: seven paired-dagger sets, seven bows, seven coats, seven headpieces, seven boots, seven charms, and three artifact weapons. Each ordinary family has four successive campaign bands and three alternatives in the final band. All 42 ordinary items have stats, prices, acquisition bands, and actions or bonuses in [Equipment](EQUIPMENT.md).

- **The Skeleton Key:** a burglar's matched dagger set that returns one Focus actually spent when a Sneak Attack damages its target. Lower damage buys resource efficiency.
- **Candle's End:** an assassin's paired blades whose once-per-combat opening strike can raise the Sneak Attack bonus to 75% against an uninjured foe. Lower sustained damage buys initiative burst.
- **The Unlost Road:** a scout's shortbow that grants eight Evasion points after a damaging Sneak Attack until the next turn. Lower damage buys a safer enemy response.

[Artifacts](ARTIFACTS.md) defines exact triggers, limits, appearance, lore, and acquisition. They use native Artifact rarity, the game's top ordinary equipment rarity. They are optional alternatives, not required to make the class work.

## Balance against existing classes

These native class values were read from the installed serialized class table during this design pass; see [Evidence and validation](VALIDATION.md). Totals are context, not proof that equal sums produce equal power.

| Class | STR | INT | AWR | TAL | SPD | VIT | Total | Distinction |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Proposed Thief | 54 | 46 | 72 | 74 | 78 | 50 | 374 | Conditional precision, low durability, tools |
| Hunter | 52 | 46 | 78 | 64 | 78 | 66 | 384 | Better starting bow accuracy and substantially more Vitality |
| Trapper | 48 | 42 | 76 | 72 | 74 | 60 | 372 | Better Awareness and Vitality; Thief spends its budget on Speed and precision |
| Treasure Hunter | 42 | 74 | 74 | 74 | 58 | 56 | 378 | More Intelligence and treasure utility; Thief emphasizes combat tempo |

The Thief does not exceed Hunter's starting Speed. Speed-based dagger accuracy has extra value because the same stat helps initiative and native defenses, so the paired path trades a shield and low Vitality for its reliability and Twin Feint benefit. The larger late-game sets also require an additional check. Trapper and Treasure Hunter must retain reasons to be chosen; native skill comparisons and the test plan are recorded in [Validation](VALIDATION.md).

Paladin comparison: the Thief can exploit a Paladin's attack, but Guard itself creates no opening. Both players still choose between dealing damage and spending a turn on utility. Slip Away and Guard never multiply into 75% reduction. The defensive result uses the stronger applicable reduction once.

## Presentation and delivery

[Art direction](ART-DIRECTION.md) specifies readable progression, artifact silhouettes, fitting, icons, and animation acceptance. Native bodies, faces, hair, skeletons, and combat motion remain the character foundation. Original equipment needs original geometry, icons, textures, editable sources, reproducible exports, and provenance, following the Paladin standard.

One separately enabled `com.ftkmf.thief` package owns the class, actions, equipment, and assets. Engine mechanics live behind reviewed public Content APIs. Legacy bundled Thief code and sample-data IDs are not dependencies. Package enablement, save identity, acquisition, game balance, and visual acceptance have distinct gates.

This is the design baseline. The source-backed facts, implementation evidence, remaining acceptance scenarios, and tuning rules are in [Validation](VALIDATION.md).
