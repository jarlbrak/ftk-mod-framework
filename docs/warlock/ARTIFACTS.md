# Warlock artifact tomes

Status: paper design, 2026-10-06, **phase 2** ([Design](DESIGN.md)). Nothing is authored or tested. All numbers are design targets. Rules build on [Combat](COMBAT.md); primitives are named as in [Gaps](GAPS.md). Ordinary tomes and the comparison baseline are in [Equipment](EQUIPMENT.md).

## Common contract

All three use native Artifact rarity ([V] `FTK_itemRarityLevel.ID` lists common, uncommon, rare, artifact, quest, lore; there is no legendary). They are two-handed tomes with magic damage, Intelligence checks, damage gain 1, no break chance, no passive stat bonuses, and ordinary Focus use. Any class can equip them; a non-Warlock gets Umbral Bolt only. Their actions and signatures require the Warlock class. They have no demon set affinity and complete no set.

[A] The installed game has no artifact tome. Its only artifact Intelligence weapon is `ancientStaff` (23 damage, one check, item levels 3-4, not dropable). Native artifacts teach one memorable rule per item and a visible cost, as the [Paladin](../paladin/ARTIFACTS.md) and [Thief](../thief/ARTIFACTS.md) artifacts do.

All use item levels 4-6, night-market, dungeon-merchant, and ordinary loot eligibility, stock 1, no town-market stock, no DLC, no Lore or quest gate, and an empty collection-lore identifier. Artifact rarity alone does not create a loot route; acquisition must be observed. Because artifact rarity is non-common, these tomes can also appear as tailored weapon rewards at requested item levels 4 and 5 ([Equipment](EQUIPMENT.md#tailored-weapon-rewards)).

| Artifact | ID | Hands / stat / checks | Base dmg | Base gold | Actions | Primitive |
| --- | --- | --- | ---: | ---: | --- | --- |
| The Midnight Ledger | `warlock_artifact_midnight_ledger` | 2 / INT / 3 | 24 | 700 | Umbral Bolt, Hexfire, Hollow Fright, Siphon Soul | P1 |
| Cinderheart Codex | `warlock_artifact_cinderheart` | 2 / INT / 3 | 23 | 650 | Umbral Bolt, Cinderbrand, Cinderstorm, Hollow Fright | P1 with its Aoe extension |
| The Blightbloom Folio | `warlock_artifact_blightbloom` | 2 / INT / 3 | 22 | 750 | Umbral Bolt, Blight, Curse of Lethargy, Siphon Soul, Hollow Fright | P2 plus a spread-on-death extension |

Comparisons below use player level 8 (base plus 8), before rounding, criticals, armor, and resistance. They are arithmetic illustrations, not expected encounter damage.

## The Midnight Ledger

**Pact artifact: the best price on Hexfire, and nothing else to spend on.**

"Signed at midnight. Due by dawn. Interest waived, this once."

A tall, narrow account book bound in midnight-blue leather with a brass corner on every edge and a small brass clasp shaped like a crescent tick mark. The page edges are inked in alternating red and black columns. A ribbon bookmark ends in a wax seal stamped with a candle. It should read as a careful bookkeeper's ledger that happens to be enchanted, not a spooky relic. No glowing runes, skulls, or permanent particles.

### Balanced Books

The Ledger carries the Pact path's two signature actions at better rates and pairs them with Hexbook sustain.

| Action | This tome | Ordinary default |
| --- | --- | --- |
| Hexfire | **1.5x for 10%** (minimum 2) | 1.35x for 10% |
| Hollow Fright | 0.5x for 6%; perfect roll gives a **75%** Daze chance | 50% |
| Siphon Soul | 0.6x, heals 50% of damage dealt | Same |
| Umbral Bolt | 1.0x | Same |

- **Trigger:** none beyond the actions themselves. Hexfire's multiplier and Hollow Fright's Daze chance are fields on cloned proficiency rows (`damage`, `chancetoaffect`).
- **Limits:** the Shadow's Due refusal rule, Grave Bargain, Shared Burden, and every other [Combat](COMBAT.md) rule apply unchanged. Daze still requires a perfect roll and respects native stun immunity.
- **No new state.** There is no charge, counter, or per-combat budget. That is deliberate: the Ledger is the artifact that ships with only the phase 1 primitive.

**Tradeoff.** The Ledger has no Cinderbrand, Cinderstorm, curse, Blight, or Rot, so it never burns, weakens, or afflicts and never feeds Soul Harvest. At level 8 its Hexfire is 48.0 for 10%, against Ember Psalter's 49.5 for 12% and an ordinary 1.35x final Grimoire's 44.6 for 10%. Three Hexfires into a boss cost 30% for 144, against 133.7 for the same cost on an ordinary final tome. Its Umbral Bolt (32) is one point below the best final Grimoires.

**Primitive:** P1 single-target, already required by phase 1. Siphon Soul is a native LifeDrain clone and Daze a native daze clone.

Feedback: standard Pact tooltip text with this tome's numbers ("Costs 10% max HP. Defeat the target to be repaid."). No extra HUD state.

## Cinderheart Codex

**Fire artifact: fires that refuse to go out, and no finisher.**

"Its pages are warm on the coldest night, and they would like to stay that way."

A squat, thick book with a charcoal-black cover cracked like cooling embers, the cracks painted pumpkin orange in the albedo. A brass hearth-grate clasp holds it shut, and a stub of candle is set into the spine. The page edges look singed but intact. The glow is painted, never a real-time light or flame particle, and no flame geometry rises from the book.

### Everburn

Burn applied by this tome's actions lasts **9 ticks instead of 6**, at 6 damage per tick: **54 total** instead of the tier 3 total of 36.

| Action | This tome | Ordinary band 4 or final default |
| --- | --- | --- |
| Cinderbrand | 0.6x for 5%; burn 9 x 6 | burn 6 x 6 |
| Cinderstorm | 0.7x Aoe for 12%; burn 9 x 6 on each enemy hit | burn 6 x 6 |
| Hollow Fright | 0.5x for 6%; 50% perfect-roll Daze | Same |
| Umbral Bolt | 1.0x | Same |
| Hexfire | **Absent** | 1.35x for 10% |

- **Trigger:** the native burn application on a positive-damage hit, unchanged. Everburn is a cloned native fire row with `m_RepeatCount` 9 and the native tick rate (quickness 2.0), so 9 ticks span 4.5 timeline units.
- **Limits:** burn still never stacks. Reapplying replaces the record, so a second Cinderbrand on a burning enemy restarts the 9 ticks rather than adding to them. Native fire immunity blocks it. Ticks end at death or combat end, so short fights waste the extra ticks.
- Burn ticks are not direct hits: no Grave Bargain, no Thief openings ([Combat](COMBAT.md) section 11).

**Tradeoff.** No Hexfire means no burst finisher and few Grave Bargain refunds; only a Cinderbrand or Cinderstorm hit that defeats its primary target repays. At level 8 a Cinderbrand is 18.6 plus 54 burn (72.6), against 19.8 plus 36 (55.8) from Ember Psalter and 18.6 plus 36 for 4% from Bonfire Canticle. A Cinderstorm into three burnable enemies totals about 227 over time, against about 187 from Bonfire Canticle, if every enemy survives the full 4.5 units. Fire-immune enemies leave the Codex with Umbral Bolt and Hollow Fright. It pairs naturally with the Cinder Imp's Ember Toss, which rewards burning targets, but needs no set.

**Primitive:** P1 with the phase 2 Aoe extension (Cinderstorm). Everburn itself is content: a cloned native fire row with a changed repeat count (open check 1).

Feedback: native burn icon and effect. The item card states "Burn lasts 9 ticks" so the longer duration is not a hidden rule.

## The Blightbloom Folio

**Affliction artifact: blight that seeds itself when its host falls.**

"Close it gently. Something inside is still growing."

A tall, narrow folio with a bark-and-cloth cover overgrown by painted moss and tiny pale mushrooms along the spine. A tarnished pewter clasp is shaped like a curled sprig, and pressed flowers poke from between the pages. Cozy-spooky, like a forgotten herbarium. No slime, rot gore, realistic eyes, or spore particles.

### Seedfall

When an enemy dies while carrying a living Blight record applied by this Folio, a fresh Blight takes root on the **nearest living enemy**.

| Rule | Value |
| --- | --- |
| Trigger | Authoritative death of an enemy, by any cause (ally attack, tick, familiar), carrying a live Blight record marked Seedfall |
| Target | Nearest living enemy by encounter slot distance; ties go to the lower slot index. Enemies already carrying any Warlock's Blight are skipped. No eligible target means no spread. |
| New record | Full Blight tier 3: 4 ticks of 10 at 1.25-unit intervals (40 total), owned by the same Warlock, also marked Seedfall |
| Cost | None. Seedfall pays no Shadow's Due and makes no roll. |
| Chain | A seeded Blight can seed again. Each enemy dies once and both sides cap at three combatants, so one combat has at most two spreads per original Blight. |
| Owner | Must be alive and in the combat, as for Soul Harvest |
| Ends | At combat end. Nothing carries into the next encounter or dungeon room. |

- The Seedfall mark lives on the record, not the equipped tome. Swapping tomes mid-combat does not cancel a Blight already applied, and does not mark Blights applied by another tome.
- Soul Harvest pays once for each dying afflicted enemy, as normal. The spread is not a direct hit and never triggers Grave Bargain.
- Applying a Blight from another Warlock replaces the record and transfers ownership ([Combat](COMBAT.md) section 8); the replacement carries Seedfall only if that Warlock's tome is also this Folio.

| Action | This tome | Ordinary final Hexbook default |
| --- | --- | --- |
| Blight | T3 (4 x 10) for 4%, with Seedfall | T3 or T2, no spread |
| Curse of Lethargy | -0.25 speed, 4 units | One curse per tome |
| Siphon Soul | 0.6x | Same (Bestiary 0.75x) |
| Hollow Fright | 0.5x for 6% | Same |
| Rot | **Absent** | T3 or T2 |

**Tradeoff.** No Rot, a tempo curse instead of Ruin or Frailty, and the second-lowest base damage of any late tome (30 at level 8; only Hushwillow Hymnal is lower). Against one enemy Seedfall does nothing, and the Almanac of Withering, with Ruin -30 and Rot, is stronger. In a three-enemy fight, one 4% Blight can tick on all three in turn: up to 120 flat damage and three Soul Harvests, if each host dies before the next spread matters.

**Primitive:** P2 plus a **spread-on-death extension** that P2 does not yet contract. Proposed declaration on the affliction:

```json
"affliction": { "kind": "blight", "ticks": 4, "interval": 1.25, "damagePerTick": 10, "harvestPercent": 5,
  "spreadOnDeath": { "target": "nearestLiving", "skipIfAfflicted": true, "chain": true } }
```

What the extension adds beyond P2:

- A per-record flag carried on the stored affliction, surviving tome swaps.
- A hook on the same authoritative enemy-death event that P2 already needs for Soul Harvest, ordered after Soul Harvest so a spread never pays twice.
- **Authority-applied records without a hit.** P2's ticks are flat and roll-free, so local ticking is deterministic. Applying a new record to a different enemy is a new mutation: the encounter authority must choose the target and every client must apply the same record exactly once. This is the main co-op risk and needs its own game-free tests: target choice and ties, skip rule, chain bound, owner absent, combat end, tome swap, ownership transfer, and replay idempotence.

Feedback: "Seedfall" combat text on the new host and the framework Blight icon on its HUD.

## Why these remain alternatives

Each artifact gives up something an ordinary final tome keeps. The Midnight Ledger buys the cheapest strong Hexfire by abandoning burn and afflictions. Cinderheart Codex buys the longest burn by abandoning Hexfire and its refunds. The Blightbloom Folio buys a self-spreading Blight by abandoning Rot and single-target strength. Ember Psalter remains the burst ceiling, Almanac of Withering the full affliction kit, and the three demon sets work with any of them. No artifact requires a demon set, another artifact, or a specific party.

Before accepting balance, test each artifact against its closest ordinary final tome in short and long fights, single targets and full three-enemy groups, fire-immune enemies for the Codex, and solo and party kill patterns for the Folio. Test cost, refund, burn duration, and spread lifecycles separately from art and acquisition.

## Open checks

1. Whether a cloned native fire row with `m_RepeatCount` 9 ticks nine times on enemies at the native rate, and whether the burn HUD and tooltip reflect the longer duration.
2. Whether native Daze honors `chancetoaffect` 0.75 on a cloned row exactly as it does 0.5 (Midnight Ledger).
3. The authoritative enemy-death hook and the network path for applying an affliction record without a hit (Blightbloom Folio); depends on [Native baseline](NATIVE-BASELINE.md) check 4.
4. How "nearest living enemy" maps onto multipart bosses and encounters with fewer than three slots filled.
5. Whether artifact-rarity tomes appearing as tailored rewards is acceptable for pacing, as native `ancientStaff` already allows.
6. Tome handedness and action button capacity, shared with [Equipment](EQUIPMENT.md#open-checks) checks 1 and 2.
