# Paladin 1.2.0 balance review

Status: unpublished tuning candidate, 2026-09-24. This is a native-data-backed
balance pass with calculated tradeoffs, not a completed campaign comparison.
The [source package](../../marketplace/packages/paladin/content.json) is the
runtime authority. The published catalog and earlier archive bytes are unchanged.

## Role and decision

Paladin remains a slow protector: spend an attack to Guard another hero, or
attack and spend Focus to heal the designated ally. It cannot Guard or heal
itself through the baseline kit. Strength 70 offers fallback weapons; Intelligence
40 and Talent 50 remain exploration weaknesses. Guard does not redirect enemy
attacks, and protection expires at the next own turn or on incapacity. Those
conditions matter more than adding up the class's stat points.

The balance invariant is that Vitality's combined accuracy and health benefit
stays within the native Blacksmith starting value. Four-check great hammers
retain a reliability advantage, but support variants do not exceed comparable
native five-check hammers in base damage. Verdict buys damage by giving up the
Mercy heal improvement or Censure action. Neither artifacts nor a matching set
are required for the role to function.

## Changes

| Value | 1.1.0 source | 1.2.0 source | Reason |
| --- | ---: | ---: | --- |
| Base Vitality | 84 | 80 | Match Blacksmith's Vitality; avoid a stronger starting accuracy/health stat |
| Highward Great Hammer | 33 | 32 | Match native Great Hammer damage at item tier 3 |
| Mercy Great Hammer | 36 | 34 | Match Ice Hammer damage while retaining focused healing |
| Censure Great Hammer | 36 | 34 | Match Ice Hammer damage while retaining its extra control action |
| Verdict Great Hammer | 39 | 37 | Stay below Royal Hammer damage while retaining four checks and earlier eligibility |

Damage values are base values. Damage gain remains 1 per hero level. One-handed
hammers, the early great hammers, shields, apparel, accessories, prices,
acquisition bands, Focus capacity, starting items and three artifacts retain
their existing values. Guard's 50%, baseline focused healing's 8%, the once-per-
combat rescue and Cleansing March retain their existing rules. Stable mod and
content identities, models and icons are unchanged; no framework code changes
or new capabilities are required. Minimum framework version remains 1.0.3.

## Fresh native comparisons

The installed original game's `Assembly-CSharp.dll` has SHA-256
`94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`.
The 2026-09-24 read-only audit used fresh ILSpy declarations and methods plus
installed `sharedassets1.assets` class and weapon tables decoded with matching
assembly type trees. The asset SHA-256 was
`e38abff2efd7ca8ef8ede4b92754d6b3a06c6498a774ee55d0d1ddbffba4750f`.
No native source, table dump or asset bytes are distributed.

| Class | STR | INT | AWR | TAL | SPD | VIT | Total | Focus |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Paladin 1.2.0 | 70 | 40 | 60 | 50 | 60 | 80 | 360 | 3 |
| Blacksmith | 76 | 40 | 52 | 72 | 56 | 80 | 376 | 3 |
| Woodcutter | 78 | 44 | 70 | 52 | 58 | 74 | 376 | 3 |
| Monk | 74 | 74 | 60 | 48 | 60 | 62 | 378 | 3 |
| Herbalist | 44 | 76 | 70 | 58 | 64 | 52 | 364 | 3 |

Blacksmith retains stronger Strength and Talent and native Steadfast; Paladin
explicitly disables inherited Steadfast and pays its own action for Guard.
Woodcutter retains stronger Strength and Awareness. Monk retains substantial
Intelligence and different native support abilities. Herbalist is a faster
Intelligence specialist. These are different roles, not equal-stat targets;
a lower total does not by itself justify a stronger combat kit.

| Native weapon key | Base damage | Checks | Item tiers | Paladin comparison |
| --- | ---: | ---: | --- | --- |
| `bluntGreatHammer` | 32 | 5 | 3 | Highward: 32, four checks, tier 3 |
| `bluntIceHammer` | 34 | 5 | 4-6 | Mercy/Censure: 34, four checks, tiers 4-6 |
| `bluntRoyalHammer` | 38 | 5 | 5-6 | Verdict: 37, four checks, tiers 4-6 |

These native weapons have damage gain 1. Paladin inherits War Hammer's Crush,
Shockwave and Stun, and Censure appends its action. Native counterparts have
different action packages and modifiers, so the table establishes budget
anchors rather than complete item equivalence. Existing five-check Royal
Hammer and its higher-cost special actions remain distinct choices.

`CharacterStats.GetWeaponMaxDamage` starts from base weapon damage plus damage
gain times hero level, then applies other bonuses and multipliers. The comparison
below isolates only that base term. `TallyCharacterHealth` uses Vitality in both
starting health and per-level growth with integer truncation. Four fewer
Vitality points do not mean four fewer HP: integer base health can leave level-0
HP unchanged, while the growth difference accumulates with levels and rounding.

Starting comparison: native Smith Hammer is 10 damage with four checks;
Novice Hammer remains 10 with three. At their respective unmodified primary
stats, the simplified expected raw damage is 7.6 for Blacksmith and 8.0 for
Paladin. Paladin also starts with its custom apparel, while Blacksmith has
its native hammer and shield. That full starting-loadout difference remains
a matched-fight gate. Oathkeeper Great Hammer retains 24 damage across tiers
1-2, between native War Hammer (21 at tier 1) and Battle Axe (26 at tier 2),
both five checks. This broad band and four-check reliability remain a specific
early-game comparison rather than grounds for an unsupported blanket nerf.

## Focus, progression and best-case checks

Following the recent Thief pass, compare both unassisted rolls and guaranteed
checks. The Thief's final ordinary Sneak Attack bonus was reduced to 20% to cap
its best-case premium. Its matched-class campaign balance was still an open
gate in that work; its short live fights are not transferable Paladin evidence.

For independent checks at probability `p`, perfect chance with `f` guaranteed
checks is `p ** max(checks - f, 0)`. These illustrative calculations exclude
native accuracy penalties, ailments, difficulty, criticals, dodges and rounding.

| Base Vitality | Three checks, no Focus | Four checks, no Focus | Five checks, no Focus | Four checks, one guaranteed | Four checks, three guaranteed |
| --- | ---: | ---: | ---: | ---: | ---: |
| Old 84 | 59.27% | 49.79% | 41.82% | 59.27% | 84.00% |
| New 80 | 51.20% | 40.96% | 32.77% | 51.20% | 80.00% |

Base Focus capacity is three. Fully securing four checks requires additional
capacity or outside effects; spending more Focus never multiplies ally healing.
A perfect chance is not expected damage: ordinary partial attacks can deal
damage. Under a simplified linear partial-damage model, expected raw damage is
`D * (f + (n - f) * p) / n` for `f <= n`. For example, an unassisted level-8
Verdict normal attack changes from `47 * .84 = 39.48` to `45 * .80 = 36.00`,
a modeled 8.8% reduction. This excludes enemy mitigation and is not native DPS.

| Great hammer | Base damage, old to new | Level-8 base term, old to new |
| --- | --- | --- |
| Highward | 33 to 32 | 41 to 40 |
| Mercy / Censure | 36 to 34 | 44 to 42 |
| Verdict | 39 to 37 | 47 to 45 |

Mercy apparel now totals 86 Vitality, or 91 with Apprentice's five-point bonus,
leaving room below the native 95 cap. Matching Censure accessories can buy Speed
instead of Resistance. Full Verdict remains 16 Armor and 21 Resistance with
its matching accessories; that is a specific magical-enemy playtest priority,
not a measured reason to flatten the armor families.

Kingsfall remains 42 damage with five checks. A fully successful ordinary
Guard then charged attack deals a base-term `42 * 1.5 = 63` across two actions,
compared with `37 * 2 = 74` for two Verdict attacks. At level 8 these become
75 versus 90. This calculation excludes special actions, criticals and defenses.
It shows why Kingsfall's protection and timed burst can justify its higher
number without automatically improving sustained damage. One-handed hammers
still trade damage for shield benefits; ordinary shields supply no personal
Armor or Resistance and cost two Speed.

## Verification and remaining acceptance

`validate_paladin.py` checks the role tradeoffs against the actual package:
Vitality ceiling, Focus budget, 1H/2H damage and check tradeoffs, progression,
support-versus-Verdict damage, native great-hammer damage ceilings, Mercy cap
headroom and Kingsfall's two-action normal-attack budget at levels 0, 6, 8 and 10.
It also retains asset hashes, acquisition, appearance and identity checks.

Game-free verification for this candidate:

- Source package and balance guardrails passed.
- Exact archive validation, isolated helper lifecycle and PlayerMods results
  are recorded in the accompanying [verification receipt](balance-1.2.0-verification.json).
- Documentation links and `git diff --check` are checked before handoff.

The [exact candidate live receipt](balance-1.2.0-live.json) records native
registration, Journeyman starting stats, a natural solo fight, loot and
same-version fresh-process save/resume on the published framework DLL. One
restart crashed in native Mono compilation on the reporting worker; an unchanged
retry resumed successfully. No cause or Paladin regression is established.

Release scope is this conservative five-number tuning with native-data,
calculation and bounded gameplay evidence. It is not certification of matched
class parity. The broader campaign matrix below remains explicit follow-up;
it is not represented as completed by the starter smoke. The existing desktop
installation allowlist is preserved per the merged installation policy;
Windows/Linux gameplay and the player-reported Windows fingerprint remain
unverified locally. Earlier [1.1.0 Cleansing March evidence](cleansing-march-live-2026-09-24.json)
remains historical evidence for those exact bytes.

For broader balance acceptance, compare Paladin, Blacksmith and Monk at equal hero level,
difficulty, gear budget and Focus budget. Use starter, item tier 3 and endgame
gear against physical and magical enemies, a single dangerous foe and multiple
foes. Record damage per action, own HP lost, ally HP prevented/restored, Focus
spent, rescue use and turns to victory. Include both weapon routes, full Mercy,
Censure and Verdict, mixed/native gear, a solo surviving Paladin and native
classes using the transferable hammers. Repeat enough encounters to distinguish
target-selection luck from a consistent advantage. Verify starting stats,
equip/remove changes, ordinary acquisition and new-version save/resume.

Online co-op and Windows/Linux gameplay remain separate unverified gates.
Matched trials can justify further tuning; this tuning release does not
establish final balance. Publication is separately authorized by the maintainer.
