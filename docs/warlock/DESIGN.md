# Warlock: complete class design

Status: approved design baseline, 2026-10-06. Numbers are initial balance targets, not established gameplay balance. Nothing is implemented. [Native baseline](NATIVE-BASELINE.md) records the installed-game facts this design relies on; [Gaps](GAPS.md) records the framework work it requires and the phased delivery plan.

## The promise

**A caster who pays in their own blood for fire and shadow, withers foes with curses and creeping rot, and in the end binds a demon to serve them.** The Warlock is the party's sustained magic damage dealer. Early on, a hedge-warlock with a moth-eaten book chooses between free spells and stronger spells paid in health. By the endgame, a full set of pact regalia binds one of three demons, each changing how the Warlock fights.

The inspiration is the World of Warcraft warlock's three traditions: destruction (self-costly fire), affliction (curses and damage over time), and demonology (bound demons). Our rules are original FTK adaptations with original names. There is no mana bar, pet action bar, soul shard economy, or talent tree to reproduce. The tone stays with FTK's light tabletop fantasy: candle smoke, pumpkin lanterns, polite contracts, and impish familiars, not gore.

Flavor: "The shadow keeps a ledger, and it always says please."

## Three pillars

| Pillar | Player decision | Mechanism |
| --- | --- | --- |
| **Shadow's Due** (fire and shadow) | Is this stronger spell worth my health? | Pact spells cost a percentage of max HP instead of Focus. Focus still buys accuracy. |
| **Afflictions** (curses and diseases) | Do I spend this turn on damage now or damage over time and a weakened foe? | Curses apply native debuffs. Blight and Rot are framework-owned damage over time on enemies. |
| **Bound demons** (endgame sets) | Which demon fits this party and this path? | A complete demon set binds a familiar that acts automatically in response to the Warlock's spells. |

Recovery is part of each pillar so the HP economy is a loop, not a tax. **Grave Bargain** repays a Pact spell's cost when its own hit defeats its target. **Soul Harvest** restores health when an afflicted enemy dies. **Siphon Soul** drains life directly.

## The design in one fight

A Warlock with a band two Grimoire faces a wounded skeleton, a healthy cultist, and a bat. Umbral Bolt is free. Hexfire costs 10% of max HP and hits a third harder. A perfect Hexfire defeats the skeleton and Grave Bargain repays the cost. Next turn the cultist is healthy, so Cinderbrand is the better choice: a small hit plus a burn that keeps working while the party acts. When the bat is low, Umbral Bolt finishes it for free.

A Hexbook Warlock fights the same encounter differently. Curse of Feebleness cuts the cultist's attack, Blight starts ticking on it, and Siphon Soul refills the health spent. When the blighted cultist falls to anyone's attack, Soul Harvest returns more.

Weaknesses: no party healing, light armor, modest Speed. Self-drain stacks with enemy damage, so burst and enemy damage over time are dangerous. Fire-immune enemies blunt the Grimoire. Long fights favor the Hexbook; swarms favor Cinderstorm.

## Class sheet

| Stat | Starting value | Intent |
| --- | ---: | --- |
| Strength | 44 | Weak physical fallback |
| Intelligence | 78 | Primary tome stat |
| Awareness | 62 | Ordinary scouting |
| Talent | 56 | Limited utility |
| Speed | 56 | Usually acts after fast enemies |
| Vitality | 68 | The HP pool is the Pact budget |
| Focus | 3 | Native resource; no regeneration |
| Gold | 3 | Modest start |

Total of the six main stats: **364**. Primary weapon stat: Intelligence (native `SkillType.fortitude`). Template: clone the native `scholar` row and disable Refocus through an explicit `skills` object, as Thief and Paladin do. Luck stays at the template value. Starting equipment: **Moth-Eaten Grimoire** and **Gourdlight Lantern**. Native armor is worn until the Warlock apparel line arrives.

All gear is transferable and usable by other classes. Pact spells, afflictions, and demon bonuses require the Warlock class. A non-Warlock holding a Warlock tome gets its basic attack only.

## Two spell paths

Both paths use tomes, Intelligence, and magic damage. Like the Thief's daggers and bow, every band supplies both and the player may switch freely.

| Path | Theme | Signature actions | Feel |
| --- | --- | --- | --- |
| **Grimoire** | Fire, destruction | Hexfire, Cinderbrand, Cinderstorm | Burst, burn, and HP-for-power finishing |
| **Hexbook** | Shadow, affliction | Curses, Blight, Rot, Siphon Soul | Debuffs, damage over time, and self-sustain |

## Abilities

| Ability | Path | Cost | Effect summary | Status |
| --- | --- | --- | --- | --- |
| Umbral Bolt | Both | Free | Basic tome attack, 1.0x magic | Exists |
| Hollow Fright | Both, from band 2 | 6% max HP | 0.5x magic; perfect roll gives a 50% native Daze chance | Needs Shadow's Due |
| Hexfire | Grimoire | 10% max HP | 1.35x magic, single target | Needs Shadow's Due |
| Cinderbrand | Grimoire | 5% max HP | 0.6x magic plus the native burn | Needs Shadow's Due; burn exists |
| Cinderstorm | Grimoire, late | 12% max HP | 0.7x magic to all enemies plus the native burn | Needs Shadow's Due with Aoe support |
| Curse of Frailty, Ruin, Lethargy, Feebleness | Hexbook | Free | Native armor, resistance, speed, or attack debuff | Exists |
| Siphon Soul | Hexbook | Free | 0.6x magic; heal 50% of damage dealt | Exists (native LifeDrain) |
| Blight | Hexbook | 4% max HP | Shadow damage over time (Curse category) | Needs Afflictions |
| Rot | Hexbook, late | 6% max HP | 0.4x hit plus longer disease damage over time | Needs Afflictions |
| Grave Bargain | Passive | n/a | A Pact spell's own hit defeating its primary target repays its cost | Needs Shadow's Due |
| Soul Harvest | Passive | n/a | An enemy dying while afflicted by your Blight or Rot restores 5% max HP, once per enemy | Needs Afflictions |

[Combat](COMBAT.md) defines exact costs, timing, arithmetic, exclusions, and tooltips.

## Endgame: bound demons

The final band of hoods, robes, boots, and trinkets forms three demon sets. Wearing two pieces grants a minor bonus. Wearing all four binds that set's demon. Demons are original creatures, not native enemies.

| Set | Demon | Supports | Signature |
| --- | --- | --- | --- |
| Cindercall Regalia | **Cinder Imp**, a shoulder-riding fire imp | Grimoire | Hurls an ember at burning enemies the Warlock damages |
| Gloamhunt Raiment | **Gloam Hound**, a shadow hound at heel | Hexbook | Devours enemy buffs when cursed; feeds on afflicted deaths for Focus |
| Hollowward Vestments | **Hollow Warden**, a hooded shade behind the Warlock | Either | Shoulders part of every Shadow's Due; once per combat stands between the Warlock and a heavy blow |

The game has no player-side combatants or summons, and its three-slot combat limit leaves no room for a pet. A bound demon is therefore a **familiar**: it has no turn, no health bar, cannot be targeted, and acts automatically in response to the Warlock's committed actions. [Demons](DEMONS.md) records the evidence, the options considered, and the exact rules.

## Inventory and artifacts

The complete paper inventory contains **45 equipment items**: seven Grimoires, seven Hexbooks, seven hoods, seven robes, seven boots, seven trinkets, and three artifact tomes. Each family has four successive campaign bands and three alternatives in the final band. The final apparel and trinket alternatives are the three demon sets. [Equipment](EQUIPMENT.md) has stats, prices, acquisition, and actions. [Artifacts](ARTIFACTS.md) defines the artifact tomes.

## Balance against existing classes

Native rows come from the installed class table; see [Native baseline](NATIVE-BASELINE.md).

| Class | STR | INT | AWR | TAL | SPD | VIT | Total | Focus | Distinction |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Proposed Warlock | 44 | 78 | 62 | 56 | 56 | 68 | 364 | 3 | HP-for-power, damage over time, demons |
| Scholar | 42 | 78 | 66 | 70 | 70 | 60 | 386 | 4 | Refocus; better utility and initiative |
| Herbalist | 44 | 76 | 70 | 58 | 64 | 52 | 364 | 3 | Party Heal, Find Herb |
| Astronomer | 50 | 76 | 68 | 46 | 62 | 74 | 376 | 4 | Support Range, Black Hole |
| Thief | 54 | 46 | 72 | 74 | 78 | 50 | 374 | 3 | Conditional precision |
| Paladin | 70 | 40 | 60 | 50 | 60 | 80 | 360 | 3 | Guard, Censure, Smite |

The Warlock gives up Scholar's Speed, Talent, Refocus, and a Focus point. Its power comes from Pact multipliers and armor-ignoring damage over time, both paid in HP. Native damage over time is flat per tick, so affliction strength comes from tome band, not Intelligence.

Perfect-roll multipliers before mitigation: Paladin Smite 0.5x (0.75x into Censure), Thief Sneak Attack 1.2x (conditional, once per turn), Warlock Hexfire 1.35x (unconditional, paid in HP). Stress case: three Hexfires into a boss with no refund cost 30% of max HP for about +105% of one weapon hit over three Umbral Bolts. If that outperforms Thief and Paladin sustained output, the first tuning lever is 1.25x at 12%.

Initial trial targets, to be tested rather than assumed:

- Warlock damage per encounter between 0.9x and 1.15x of the stronger of Scholar and Herbalist in the same fight.
- Net HP spent on Shadow's Due (cost minus Grave Bargain and Soul Harvest) at or below 35% of maximum HP in a standard three-enemy fight.
- A solo Warlock can finish a standard band 1 fight without healing items in most trials.

## Delivery phases

| Phase | Target | Scope |
| --- | --- | --- |
| 1. Hallow's Eve playtest | 2026-10-31 | Class, Shadow's Due, Grave Bargain, Umbral Bolt, Hexfire, Cinderbrand, Hollow Fright, curses, Siphon Soul; Grimoire and Hexbook bands 1 and 2; Gourdlight and Witchlight Lanterns. Six items. Native armor. |
| 2. Afflictions | November | Blight, Rot, Soul Harvest, enemy affliction icons, Cinderstorm; bands 3 and 4 of every family; apparel bands 1 to 4; three artifact tomes |
| 3. Pacts | After phase 2 | Generic equipment sets, bound familiars, original demon models; the three demon sets and final tome alternatives |

Each phase is a separately validated package release. Phase 1 needs one framework primitive. [Gaps](GAPS.md) has the primitive contracts and order.

## Presentation and delivery

[Art direction](ART-DIRECTION.md) specifies readable progression, the three demon silhouettes, fitting, icons, and acceptance. Native bodies, faces, hair, skeletons, and combat motion remain the character foundation. Original equipment and familiars need original geometry, icons, textures, editable sources, reproducible exports, and provenance, following the Paladin standard.

One separately enabled `com.ftkmf.warlock` package owns the class, actions, equipment, and assets. Engine mechanics live behind reviewed public Content APIs. Package enablement, save identity, acquisition, balance, and visual acceptance have distinct gates in [Validation](VALIDATION.md).
