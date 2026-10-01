# Paladin combat

Status: unreleased v4 source candidate, 2026-09-30. Its role percentages are provisional until matched native encounters. Earlier combat captures describe older package values. The [package source](../../marketplace/packages/paladin/content.json) is the authority for this candidate's exact fields.

## Baseline Guardian

Guard spends a turn to protect another ally against qualifying direct enemy attacks. The neutral Guardian profile reduces the guarded hit by 50%. The conditional rescue remains once per combat and does not refresh when equipment changes. Cleansing March prevents new Poison and Curse during exploration; it does not remove existing ailments or prevent combat conditions. A party can include multiple Guardians, but each guarded outcome and rescue must resolve once through the native authoritative path.

Censure and Smite are baseline Paladin class actions, available only with an eligible equipped hammer. They are not item grants: another class using a Paladin hammer receives only Strike, the native 1.0 single-target regular hit before ordinary modifiers. The explicit empty weapon action list removes inherited splash, Shockwave and Stun on all 14 Paladin hammers, including Last Vigil and Kingsfall. Guard remains equipment-independent. Both button creation and attack commit must check the current class and equipped hammer, so swapping to an ineligible weapon cannot leave a usable stale class action. One-handed Censure uses three checks, two-handed Censure four; the action deals 0.75 weapon damage and on application randomly lowers Armor or Resistance by four or six respectively. Its two outcomes can coexist and follow native expiry. Trinkets no longer grant actions. The v4 Smite source uses a 0.50 weapon-damage coefficient as magic damage, with a 1.5 multiplier when the target has the qualifying Censure Resistance outcome. Mercy's three-armor core strengthens baseline Smite instead of unlocking it. Smite still scales with the equipped hammer's damage and native weapon skill. Eligible high-damage native hammers require separate balance assessment; non-hammer weapons cannot supply Smite scaling.

## Equipment roles

Head, Body and Foot choose the role. Two same-family pieces grant only the minor theme. All three grant the core role and its costs even when the hands are foreign. A matching 2H hammer with empty offhand, or matching 1H hammer plus shield, then grants one completion effect. Accessories and Artifacts do not count toward completion; an Artifact can retain a completed armor core while forgoing its completion benefit.

| Core | Main strength | Attached cost | Completion |
| --- | --- | --- | --- |
| Mercy | Best effective Guardian healing and reliable magic Smite | Lower physical output and Guard reduction | Qualifying focused-hit healing rises from 12% to 15% of ally max HP |
| Verdict | Best durability and ally protection | Lower damage and healing, slower initiative; Guard still costs a turn | Guard prevents reviewed debuffs on a qualifying direct hit |
| Censure | Strongest ordinary physical role | Lower healing, Smite, Guard and personal defenses | Conditional physical gain against Censure's Armor outcome |

The source core profiles use 65/60/125 physical percent for Mercy/Verdict/Censure and 150/75/50 healing percent respectively, before other item and action factors. These are authored inputs, not measured encounter results. The core role cannot be split across families. A foreign weapon may still contribute its own ordinary action or stats, but it cannot grant another role's signature effect. When a role breaks or changes, superseded protection and dependent pending effects must end without restoring Focus or rescue.

Mercy's Guard heal has a 4% base, scaled by its 150% healing profile to 6% of the guarded ally's maximum HP. Its qualifying focused-hit heal is 12%, or 15% with matching armament. These amounts round down and cannot exceed missing HP; Guard healing has a minimum of 1 HP while injured. Focused healing requires Focus spent, a qualifying landed attack and a living designated ally; it is not limited to Smite. Verdict's small retaliation theme is limited to one guarded outcome; its debuff-prevention ward requires matching armament. Censure's 1.2 Armor-outcome payoff is authored for qualifying physical damage, not every status tick or splash target. The full effect and cost should be visible in tooltips, including the armor count and missing armament pieces.

## Artifact actions and remaining gates

The Last Vigil can restore one Focus to a guarded ally after a qualifying mitigated hit. Kingsfall can ready a one-use Reckoning strike after a qualifying Guard. The Last Bastion removes one eligible existing condition when Guard resolves. Each keeps its ordinary slot identity and has no set affinity. See [Artifacts](ARTIFACTS.md) for trigger limits.

Game-free tests cover the isolated rule evaluator and package structure. They do not establish animation, co-op synchronization, save/reload, native damage ordering or balance. Native trials must compare neutral, Mercy, Verdict, Censure, mixed and artifact loadouts across no-injury fights, magic pressure, multiple enemies, durable targets and solo survivors. Record damage, effective healing, prevented damage, own HP loss, Focus spent, turns and burst separately. Preserve the native white outlined action glyphs, including basic attacks.

Website impact: combat descriptions and item cards change at release, after the candidate's native gates are met. The published site remains unchanged for this unreleased work.

Smite deals holy-themed magic damage at 0.50 weapon damage before ordinary modifiers. A perfect roll can stun with an authored native 25% effect chance, subject to damage, immunity and existing-control rules; partial hits can deal magic damage but cannot stun. Mercy's three matching armor pieces raise the pre-mitigation coefficient to 0.80 before the Censure Resistance payoff. Bounded corrected-source R26 combat observed a perfect hit fully blocked by Resistance, perfect damaging hits with and without a new stun, and one Mercy hit after the Paladin's Censure Resistance mark. R25 separately observed damage without stun against an immune Wisp. Positive partial damage without stun was observed on prior source bytes in R21 and remains relevant only by the specific source-equivalence argument in [Validation](VALIDATION.md#unreleased-smite-damage-and-stun). None of these controlled encounters measures proc frequency, campaign pacing or representative balance.
