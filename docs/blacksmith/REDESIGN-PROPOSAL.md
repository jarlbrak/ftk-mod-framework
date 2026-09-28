# Blacksmith gear redesign proposal

Status: approved for implementation, 2026-09-26. The user authorized full Rodin
art, working in-game models, and playtesting of the new kits. Values below remain
initial tuning targets until those tests finish. The previous candidate and its
evidence remain in [Gear](GEAR.md) and [Full release pass](FULL-RELEASE-PASS.md).

## Recommended identity

**A durable forge worker who chooses between bracing behind a shield, committing
to a heavy blow, and spending a turn to reinforce a companion.**

Keep the native Blacksmith class, starting equipment, and Steady behavior.
Finding equipment supplies new tools and choices. Every item remains usable by
other classes, with ordinary stats and native actions; explicitly labeled
Blacksmith benefits require the native Blacksmith class. No matching outfit,
piece-count threshold, permanent specialization, crafting currency, or Heat
meter is required.

The initial release should cover both hammer paths at every stage: four tiers
of eight item identities, **32 ordinary items**. This adds Coalmark and
Bellowsworn two-handed mauls to the present 30. The eight identities per tier
are one-handed hammer, two-handed maul, shield, body, head, boots, trinket, and
necklace. A character equips one weapon route, not both. Further artifact or
endgame families would be a separate expansion of this proposal.

## What the existing classes teach us

- Paladin equipment changes Guardian's healing, prevention, retaliation, and
  attack choices. Its ordinary shields sacrifice personal Armor and Resistance
  for those benefits. See [Paladin equipment](../paladin/EQUIPMENT.md) and
  [combat](../paladin/COMBAT.md).
- The reviewed Thief development design has two complete weapon paths and
  preparation, penetration, and burst choices. Its low durability pays for
  conditional damage. Individual pieces can be mixed without set thresholds.
  Review anchor: `art/thief-advanced-model`, commit
  `25154f02f31272262d2178667f09b8b6b82eba36`, plus the local development files
  `docs/thief/DESIGN.md`, `EQUIPMENT.md`, `COMBAT.md`, and package definitions.
  That checkout's design and source are comparison evidence, not a claim that
  all Thief behavior is released or validated.
- Blacksmith currently repeats +1 Vitality on all 30 items and introduces no
  bespoke attack. Recorded complete outfits nearly or fully reach the native
  stat cap. This spends much of the gear budget on redundant accuracy and HP
  rather than decisions. Those recorded totals include fixture difficulty and
  must not be presented as naked native class stats.

## Campaign progression

These are native **item-level bands**, not character levels. Preserve existing
item IDs and acquisition bands. All listed numbers are initial tuning targets.
Base damage excludes character-level growth, action coefficients, and defenses.

| Tier | Item level | 1H damage / checks | 2H damage / checks | New experience |
|---|---|---:|---:|---|
| Coalmark | 0 | 10 / 4 | 14 / 5, new | Choose Set Hammer's defensive strike or Overhand's exposed heavy swing immediately. |
| Bellowsworn | 1-2 | 17 / 4 | 22 / 5, new | Stronger tools; the Fire Kit unlocks Temper for an ally or self. |
| Rivetwatch | 3 | 24 / 4 | 30 / 5 | Stronger temporary protection and a more consequential heavy-swing risk. |
| Kilnward | 4-6 | 29 / 4 | 33 / 5 | Mature shield, heavy-hammer, and support choices using interchangeable pieces. |

Both paths retain Vitality-based attacks and native damage gain 1. Four versus
five checks remains a real reliability and Focus cost distinction. No new
universal accuracy bonus or free Focus recovery is proposed.

## Three equipment-provided abilities

### Set Hammer: the shield path

Granted by a themed one-handed hammer, requiring any valid equipped shield.
Spend the normal action on a single-target physical strike at **75% of normal
damage**. A hit that causes positive HP damage grants temporary
personal Armor until the beginning of the Blacksmith's next scheduled turn.

| Tier | Temporary Armor |
|---|---:|
| Coalmark | +2 |
| Bellowsworn | +3 |
| Rivetwatch | +4 |
| Kilnward | +5 |

Use ordinary weapon checks and Focus. Partial attacks retain the reduced
damage and can grant protection if they cause HP loss. Fully blocked or dodged
attacks grant none. This trades immediate damage for surviving
physical pressure. It does not change Steady chance, heal, protect allies,
negate conditions, or create an automatic counterattack. A native shield works;
a matching Blacksmith shield is not required.

### Overhand: the maul path

Granted by a themed two-handed maul. Spend the normal action on a single-target
physical strike at **115% of normal damage**, using the normal five checks.
Committing the attack applies a temporary personal Armor penalty until the
beginning of the next scheduled own turn, including when the attack fails.

| Tier | Armor penalty |
|---|---:|
| Coalmark | -2 |
| Bellowsworn | -3 |
| Rivetwatch | -4 |
| Kilnward | -6 |

The choice is a normal safe swing or additional damage while exposed. It grants
no free turn, armor bypass, guaranteed critical, stun, or interrupt. A miss
still pays the risk. At Kilnward, the base-damage equivalent is about 38 before
level growth, close to the existing Royal Hammer comparison of 38, but with
the temporary defensive cost. Native comparison rows require a fresh data
audit before implementation.

### Temper: the practical support tool

Granted by Bellowsworn or later Fire Kit trinkets. **Once per combat, spend a
full action** to reinforce one living ally or self, granting +3 / +4 / +5 Armor
for Bellowsworn / Rivetwatch / Kilnward. No roll or Focus cost. Protection expires
after the target completes its next two scheduled turns; skipped turns count.

It supplies temporary physical protection without restoring HP or Focus,
reviving, cleansing, granting Resistance, or changing maximum health. The
Coalmark kit remains a simple stat item. Different trinkets remain attractive
when the player prefers damage every turn or concentrated passive defense.

Temper and Set Hammer use the strongest applicable positive Armor increase,
not their sum. An equal or stronger Temper replaces the existing Temper and
starts its two-turn duration; a weaker application neither lowers nor refreshes
the stronger effect. Multiple Blacksmiths
cannot multiply it on one target. Overhand's penalty remains additive and
cannot be erased by taking off the weapon or equipping a second copy.

Temper can offset Overhand's exposure for a limited window. This is an intended
preparation option that costs a full action and a per-combat use. Compare the
whole sequence with simply attacking, including allied Blacksmith preparation;
it must not become a cost-free sustained damage bonus.

## Piece-specific stat budget

Replace the repeated Vitality affinity with one small, readable benefit per
slot. Values apply only while that piece is equipped by a Blacksmith.

| Piece | Blacksmith-only passive | Active benefit |
|---|---|---|
| 1H hammer | +1 Vitality | Set Hammer with a shield |
| 2H maul | +1 Vitality | Overhand |
| Shield | +1 Armor | Retain its ordinary native Taunt |
| Body | +1 Armor | None |
| Head | +1 Vitality | None |
| Boots | +1 Speed | None |
| Fire Kit trinket | +1 Resistance | Temper from Bellowsworn onward |
| Necklace | +1 Vitality | None |

Only three equipped slots add affinity Vitality in either weapon path. Including
the necklace's ordinary bonus, complete outfits add five Vitality early and six
late. Current shield outfits add nine or ten; current maul outfits add eight
or nine. Actual final stats still depend on
native class, difficulty, sanctums, and other modifiers.

Retain the present ordinary defensive curves as a prototype baseline, with one
change: Bellowsworn necklace gains +1 Resistance so its price increase buys an
actual improvement over Coalmark. Numbers below are complete ordinary modifiers,
before the conditional affinities above. A/R means Armor/Resistance.

| Slot | Coalmark | Bellowsworn | Rivetwatch | Kilnward |
|---|---|---|---|---|
| Body A/R | 3/0 | 6/1 | 10/2 | 13/3 |
| Head A/R | 2/0 | 4/1 | 6/2 | 8/3 |
| Boots A/R | 2/0 | 4/1 | 6/1 | 8/2 |
| Shield A/R | 1/0 | 2/1 | 3/2 | 4/3 |
| Trinket A/R | 1/0 | 2/1 | 2/2 | 3/3 |
| Necklace | +2 VIT | +2 VIT, +1 R | +3 VIT, +1 R | +3 VIT, +2 R |

Calculated gear-only totals, including affinities, are 11/1, 20/7, 29/11,
and 38/17 A/R for the shield path; 9/1, 17/6, 25/9, and 33/14 for the maul
path. Both receive +1 Speed. These exclude native character defenses and all
temporary actions. High physical mitigation remains an explicit balance risk;
these values are a starting budget, not evidence of finished balance.

The present comparison ledger puts native late heavy armor at 16/4 versus
Kilnward's 13/3, Royal Shield at 5/5 versus 4/3, and Royal Hammer at base 38
versus the maul's 33. Native finds retain stronger passive or basic-attack
specialties. Test those alternatives and mixed outfits at representative
campaign levels before retaining the proposed numbers or current prices.

## Native Steady boundary

Installed-assembly review of `DamageCalculator._calcDamage` and
`CharacterSkills.m_SteadFast` confirms that Steady requires a shield and an
eligible nonlethal damaging hit with no calculated magical damage. Its chance
is the post-armor damage fraction of current pre-hit HP, bounded from 7% to 22%.
It is not directly multiplied by Vitality. It cannot prevent an otherwise
lethal hit. Preserve this rule; do not describe gear Vitality as increasing
Steady probability. Shieldless mauls give up access to that native defense.

## Art for the entire progression

The current selected 30-item package contains 18 Rodin-derived item identities
and 12 original Blender-authored identities. The latest banner-faithful
correction only replaced major Kilnward pieces. Earlier Rodin jobs therefore
do not mean the complete progression received that correction.

| Current selected geometry | Coverage |
|---|---|
| Lower three tiers, Rodin-derived | All three 1H hammers, Rivetwatch maul, all three headpieces and necklaces, Coalmark and Rivetwatch trinkets |
| Lower three tiers, Blender-authored | All three armor sets, boot pairs and shields, plus Bellowsworn trinket |
| Kilnward, Rodin-derived | Armor, boots, 1H hammer and derived 2H maul, shield, retained necklace |
| Kilnward, Blender-authored | Circlet and retained trinket |

Prioritize fresh concept-faithful Rodin sources for the **nine lower-tier armor,
boot, and shield identities**, after checking reusable original attempts. Review
the two Blender trinkets and circlet against their concepts individually.
Existing Rodin-derived items still need visual acceptance and may need fitting
or redesign; their provenance alone does not establish quality. The two proposed
early mauls also need designed art. The paid-job count must follow that review,
not an assumption that 32 items require 32 new generations.

Every tier needs a reviewed visual identity through geometry, materials,
equipped models, item cards, and icons. The banner represents Kilnward only.
Acceptance of that tier cannot accept the earlier tiers.

| Tier | Required silhouette and finish |
|---|---|
| Coalmark | Patched leather apron, wrapped grips, plain square hammer, small iron shield, practical capped boots; soot and dull iron. |
| Bellowsworn | Fitted riveted leather, partial forearm plates, reinforced shield rim, better-balanced hammer and articulated toe caps; aged brass accents. |
| Rivetwatch | Layered steel work coat, deliberate anvil-shaped shoulder protection, heavier hammer and fitted greaves; dark steel with restrained copper. |
| Kilnward | The existing banner's master-smith silhouette, warm leather, heavy fitted steel, clear hammer faces and coherent brass detail. |

Use Rodin for original major forms that need replacement, then Blender for
cleanup, distinct tier construction, gender/race fit, grips, and separate card
exports. Existing usable original generated geometry should not be discarded
solely to create another paid job. A second weapon path needs a designed head,
shaft, and grip treatment, not merely uniform stretching.

Before generating anything, approve a contact sheet of every tier and weapon
path and prepare a per-asset reuse/regenerate ledger. Image generation and 3D
work remain assigned to Astra High agents. Keep explicit generation budgets:
8,000 triangles for rigid pieces and 15,000 for a complete garment or boot pair,
with exported vertex counts below the runtime index limit. Confirm the current
skill and quote before each paid batch. The user separately authorized the production batch after approving this design.
Record each paid generation and its actual settings in the art ledger.

## Implementation and acceptance requirements

Current `classAffinity` supports bounded passive stats only. Set Hammer,
Overhand, Temper, class-conditioned actions, timed modifiers, and their visible
status text require reusable public Content capabilities and framework work.
Do not claim they already exist because Paladin and Thief have their own hooks.

Before authoring, inspect complete native template action lists and choose an
explicit small menu for each weapon. Include every retained Splash, Shockwave,
and Stun action in the power budget. None of these inherited actions is a new
Blacksmith feature. Avoid shipping an unnoticed collection of template actions.

The implementation uses native owner-authored committed outcomes and host-authored
scheduled-turn identities, as detailed in [Combat](COMBAT.md). Canceling
targeting spends nothing; repeat animation callbacks cannot duplicate effects.
Expire Set Hammer protection when its required equipment is removed; committed
Temper persists independently of the caster's equipment,
and preserve committed penalties and per-combat uses across swaps. Death,
incapacity, skipped turns, revive, combat exit, and supported resume paths need
explicit rules. Temper's use belongs to the character and encounter, not an
individual trinket copy. Buff removal and nonstacking must be tested with other
mods' effects.

After design approval: audit exact native comparisons and action support;
finalize the 32-item ledger and price budget; review all-tier art concepts;
generate only the assets that need new sources; fit and inspect every card and
grip; then resume native tests. Validate both weapon paths, mixed gear,
non-Blacksmith wearers, ordinary acquisition, all claimed appearances, and
representative late-game fights. The current overlevelled Fire Cave smoke is
not an endgame balance baseline.
