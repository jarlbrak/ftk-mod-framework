# Paladin combat contract

Status: current implemented rules, with evidence limits, 2026-09-24. The 1.2.0 balance candidate changes class Vitality and four weapon damage values; see [balance review](BALANCE-1.2.0.md). New accessories in [Equipment](EQUIPMENT.md) do not change these rules. Read
[Design](DESIGN.md) for the class promise and [Artifacts](ARTIFACTS.md) for the
three optional legendary effects.

## Actions and decisions

| Action | Equipment | Cost and checks | Result |
| --- | --- | --- | --- |
| Guard | Any supported equipment | One action; no checks or Focus | Designate another living party member and halve qualifying direct attack damage until the Paladin's next turn |
| Ordinary hammer attack | Equipped 1H or 2H hammer | One action; weapon's Vitality checks; ordinary optional Focus | Native weapon damage and equipped action behavior |
| Censure | Censure hammer | One action; 3 checks for 1H, 4 for 2H | 75% damage coefficient and native temporary Armor reduction of 4 or 6 respectively |
| Focused attack | Any otherwise eligible equipped attack | Ordinary attack action plus at least one Focus | A qualifying landed hit heals the designated ally once |

Guard is available independently of weapon choice. Paladin hammers are not
required to protect allies or trigger baseline focused healing. Non-Paladins
can use the equipment's ordinary stats and actions; Guardian-only bonuses do
not give them the class kit.

## Guard and ally designation

Guard always succeeds against an eligible ally. It cannot target the user,
enemies, dead allies or allies who have fled. Only one ally is designated per
Paladin; choosing another moves both designation and protection.

Protection expires at the next own turn start or immediately when the guardian
cannot act: death, zero HP, Stun, Petrification, fleeing or combat exit. Daze is
not a separate hard-incapacity test in the current implementation; native turn
timing still controls expiry. An incapacitated but living ally can receive Guard.

The designation survives ordinary protection expiry. This is why an attack on
the following turn can still heal the ally without maintaining active Guard.
Combat/revival resets must follow the existing state contract; they must not
restore a spent rescue inside the same encounter.

Reduction applies to the target's qualifying direct enemy attack outcome after
native personal defenses. It retains `ceil(damage / 2)`: 9 becomes 5, and 1 stays
1. Area attacks can protect the designated ally's portion. Damage over time is
excluded. Multiple Guardians never apply successive 50% reductions.

Guard has no taunt, self-defense, immunity or baseline healing effect. Shield
bonuses can add the specific effects listed in [Equipment](EQUIPMENT.md).

## Focused healing

The baseline heal is **8% of the recipient's maximum HP**, once per committed
attack, regardless of how many Focus points were spent or targets were hit.
Spending Focus without landing an eligible hit does not heal. The Paladin and
designated ally must still be eligible at impact; healing never revives a dead
ally, targets the Paladin itself, or occurs outside combat.

The current native hit rule includes a successful focused attack absorbed by
Armor or Resistance (`Block` or `MagicBlock`). It excludes zero-slot failures,
dodges and harmless actions. This is an on-hit rule, not a positive-HP-damage
requirement. Do not silently change it to match an accessory or another class.

Healing is floored to an integer and capped at missing HP. Baseline focused
healing has no guaranteed one-HP minimum. A full-health recipient spends that
attack's healing opportunity, so a later splash callback cannot heal again.
Mercy's 1H and 2H hammers raise the percentage to 10% and 12% respectively.

Example: an ally at 32/50 HP receives 4 HP from a baseline qualifying focused
hit, 5 from Mercy Hammer, or 6 from Mercy Great Hammer. Additional Focus secures
more checks but does not multiply healing.

## Divine Intervention

Once per Paladin per combat, a qualifying lethal direct hit against its actively
guarded ally leaves that ally at **1 HP**. Resolve protection first. No input or
additional resource is required. Expired Guard, self damage and damage over time
do not qualify.

Native Resist Death and sanctum rescue outcomes take precedence when the hit
remains lethal after Guard. Preserve that native rescue without spending the
Guardian charge; Guard can also reduce the hit enough to make it nonlethal.

Overlapping Guardians consume only one eligible rescue, selected
deterministically. Another Guardian's unused rescue remains available for a
later lethal hit. Recasting Guard, swapping gear and reviving within the same
combat must not reset a spent rescue. A genuinely new combat resets it.

## Equipment effects

Ordinary shields trade away personal Armor and Resistance and cost 2 Speed.
Novice, Oathkeeper, Highward and Mercy shields heal the ally on Guard for 2%,
3%, 4% and 8% maximum HP respectively. This separate on-Guard heal guarantees
at least 1 HP when a living ally is injured, then caps at missing HP.

Censure Aegis prevents Poison, Stun, Daze and Curse proficiency outcomes on
qualifying guarded direct attacks. It is prevention, not removal of existing
conditions. Verdict Aegis adds 4 retaliation through the native enemy attack
resolution; area targets do not multiply that reward. Guard still reduces
damage by 50% in every case.

For identical Guardian bonus fields, use the strongest equipped value rather
than summing. Different fields can coexist. The new accessories therefore
have **no Guardian bonus fields**: a stat item must not let every build combine
all shield specialties or duplicate artifact triggers.

Censure's armor reduction follows its native proficiency clock. Source and
serialized metadata establish approximately 3.33 combat timeline units, not
three seconds or a fixed number of turns. See the
[timing audit](censure-native-lifetime.json) and its separate live expiry gate.
Its authored `m_FullSlots` is false; do not describe it as perfect-only.

## Counterplay and balance checks

At base 80 Vitality, independent unfocused perfect chances are about 51.2% for
three checks, 41.0% for four and 32.8% for five. These are illustrative
probabilities, not a live damage model: native proficiency accuracy, modifiers,
rounding and enemy defenses still apply. Kingsfall's fifth check is a real
reliability cost despite its larger damage value.

A shield does not add personal defense in this package. Passing up a 2H attack
to Guard can prevent a loss, but it also lengthens a fight. Slow initiative,
split enemy targets, incapacitation and damage over time should preserve that
tradeoff. Artifacts are optional and cannot be required for baseline viability.

Compare complete Mercy, Censure and Verdict outfits, mixed outfits, native
accessories, one- and two-handed routes, a solo surviving Paladin, and parties
with multiple Paladins. Record Focus, damage, healing, turns, rescue usage and
incoming losses across several encounters rather than judging a single burst.

## Authority and verification

Use deterministic actor/attack identities and the native synchronized outcome
paths. Duplicate callbacks, multiple animation impacts and equipment rebuilds
must not create additional healing, rescue or artifact opportunities. Accessory
stats use ordinary equipment persistence; no new combat resource is proposed.

The API contract is [Guardian and equipment](../GUARDIAN-AND-EQUIPMENT.md).
[Validation](VALIDATION.md) and [legendary validation](LEGENDARY-VALIDATION.md)
record bounded tests. Single-player fixture evidence does not establish
multiplayer agreement, every native action or all lifecycle cases.
