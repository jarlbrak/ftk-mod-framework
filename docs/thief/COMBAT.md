# Thief combat rules

Status: implemented rules accompanying [the class design](DESIGN.md). All percentages below are design decisions; [Validation](VALIDATION.md) distinguishes tested behavior from remaining live gates.

## 1. Eligible attacks

The Opportunist capability belongs to the Thief class. Weapons declare precision support through the typed public Content API. Every paired-dagger set and bow in this package carries that tag. The native `dualKnife` and `dualDagger` rows are also explicitly eligible, retaining their actual native actions, checks, stats, and ownership gates. Their ordinary physical basic attacks qualify; unverified special proficiencies do not. One-handed weapons are ineligible. Release one does not guess other native weapon eligibility from a name, animation, governing stat, or all weapons of a broad type. A different weapon still functions normally; the class card says Sneak Attack requires a precision weapon.

An eligible attack is a committed, direct, single-target, physical weapon action authored to allow Sneak Attack. Normal Strike and Shoot qualify. Feint, Draw Out, Pierce, Thread the Needle, consumables, magic damage, splash, AoE, repeat attacks, damage over time, retaliation, reflection, and off-turn attacks do not. A multi-hit animation representing one native damage outcome remains one attack; an action with several separately calculated damaging hits is excluded.

All normal attacks keep native partial-success damage, critical behavior, enemy dodge rules, and armor. Perfect means every required slot succeeded, including slots secured with Focus. Native perfect player attacks bypass enemy evasion. Perfect does not mean the target necessarily loses HP after mitigation.

## 2. Open targets

For a particular Thief, an enemy is Open when either condition holds:

1. The enemy has not yet begun its first scheduled turn in this combat.
2. A different party member dealt positive direct weapon or spell attack damage to that enemy since its most recent scheduled turn began.

The second condition records the contributing party member. Damage from the Thief itself never creates its own public opening. A teammate's single-target direct attack qualifies, including another Thief's attack. AoE, DoT, reflected damage, consumables, companion damage, and zero-damage or dodged attacks do not qualify. The contributor only needs to have been a living party member at the time of the hit; later death does not rewind the distraction.

An opening closes at the start of that enemy's next scheduled turn, including a turn it loses to incapacity. Turn skipping cannot preserve the first-turn opening indefinitely. Retain the set of all qualifying contributors until the enemy's next turn begins; new hits add to that set and never replace earlier contributors. For the acting Thief, the set must contain at least one different party member. Repeated hits by the same contributor change nothing and never stack damage bonuses. A summoned or revived enemy starts with the first-turn entitlement already spent; its later openings require an ally hit. Only enemies present at the genuine encounter start receive that initial entitlement.

The opening is not consumed by attacking it. Several Thieves may each exploit the same enemy if turn order permits, but each must satisfy the different-party-member rule and the personal once-per-turn limit. Target markers are therefore rendered for the currently acting Thief, not as an unconditional promise to every player.

## 3. Sneak Attack

When the player commits an eligible attack, capture the target's opening state, the actor's current turn identity, any Prepared token, and artifact eligibility. Canceled targeting spends nothing. A committed attack uses this captured state; damage caused by that attack cannot qualify itself retroactively.

If the enemy was Open, or the actor had Prepared, a perfect result adds **20% of the actor's current maximum weapon damage** to the normal attack before mitigation. A partial result receives ordinary partial damage only. This is one enhanced native hit, not a second damage event. Two visible blades never double the listed damage, Sneak Attack, or artifact reward. Native critical and Frozen calculations operate on the enhanced damage through their normal order. Armor is deducted once. No automatic armor bypass is granted.

Let `D` be the native current maximum weapon damage after level growth and ordinary equipment modifiers, `s` the successful-slot fraction, and `b` the bonus fraction. For eligible ordinary attacks, base damage before native critical/Frozen/mitigation is `RoundNative(D * (s + b))`, where `b = 0.20` only when `s = 1`, the captured opening qualifies, and this turn's entitlement is unused. Otherwise `b = 0`. No extra rounding is introduced before the native rounding step. Candle's End replaces `b` with `0.75`; it does not add or multiply both values.

One committed eligible attempt per own turn can claim the entitlement. It is spent on the qualifying attempt even if the roll is partial or armor absorbs everything. Ordinary turns only have one action; this limit also protects against replay, native extra-action interactions, and later content. New animation callbacks or extra actions within the same turn do not refresh it. A genuinely new scheduled own turn does.

Artifact rewards requiring a **damaging Sneak Attack** additionally require a perfect enhanced attack that leaves the target with less HP. Blocks, immunity, and zero final damage grant no Focus or Evasion reward. A killing hit counts once.

## 4. Twin Feint, preparation, and weapon actions

Each custom weapon shows its basic attack and one authored weapon action. Slip Away is the Thief's class action. The package replaces the cloned template's inherited actions on a private weapon prefab, keeping the ordinary combat menu small. Preparation weapons and penetration weapons are separate choices across the progression; no single custom weapon offers both actions.

| Action | Required weapon | Checks | Damage coefficient | Extra rule |
| --- | --- | ---: | ---: | --- |
| Strike | Package paired daggers | Weapon's 2 or 3 Speed checks | 1.00 | Can Sneak Attack |
| Shoot | Package bow | 4 Awareness | 1.00 | Can Sneak Attack |
| Feint | Package paired daggers | Weapon's 2 or 3 Speed checks | 0.60 | Positive direct damage grants Prepared |
| Draw Out | Package bow | 4 Awareness | 0.60 | Same Prepared rule as Feint |
| Pierce | Listed paired daggers | Weapon's 2 or 3 Speed checks | 0.75 | Perfect result bypasses positive physical armor |
| Thread the Needle | Listed bow | 4 Awareness | 0.75 | Same armor rule as Pierce |
| Locksmith preparation | Locksmith endgame weapon | Normal checks | 0.80 | Replaces its Feint/Draw Out coefficient |
| Wayfarer penetration | Wayfarer endgame weapon | Normal checks | 0.85 | Replaces its Pierce/Thread the Needle coefficient |

**Twin Feint** is a Thief-only benefit of explicitly eligible paired daggers, including the two native sets. After an ordinary Strike with exactly one failed check causes positive direct HP damage, grant Prepared once per own turn. Resolve this after damage; it cannot enhance the same attack. A Strike that consumed Prepared may earn a replacement through this near miss. Two failed checks, a fully blocked hit, a dodged partial attack, a special action, and secondary impacts grant nothing. Both native two-check sets and later three-check sets use this exact one-failure rule. Bows and one-handed weapons do not receive Twin Feint.

Feint and Draw Out retain ordinary partial damage at their reduced coefficient. Any positive HP loss grants one Prepared token to the wielder, independent of the target's existing Open state. They cannot trigger Sneak Attack, artifact rewards, or a second preparation token. A fully absorbed or dodged hit grants nothing. For a different Thief, their positive direct hit can create an ordinary opening.

Prepared makes the next eligible attack count as having an opening, against any living enemy. Consume it when that attack commits, including on failure. It never stacks, never increases the Sneak Attack multiplier, and is consumed even when the target was already Open. It expires at the end of the actor's next scheduled turn, on weapon change, on incapacity/death, or on combat exit. A skipped next turn still reaches that expiry. Taking another non-eligible weapon attack clears an existing token before resolution; a new successful Feint can replace it. Passing or using an item does not extend its deadline.

Piercing actions never receive Sneak Attack. On perfect success, they bypass positive physical armor while preserving negative armor vulnerability and native critical rules. On partial success, they deal ordinary fractional damage at their stated coefficient against full armor. The native proficiency success gate appears suitable: verify the exact cloned proficiency and the combination of full-slot success and armor ignore before claiming the authored action works.

No action adds an independent accuracy penalty, hidden second proc chance, guaranteed critical, extra hit, or enemy turn reset. Focus costs are the normal cost of securing chosen slots. There is no extra Focus fee to press Feint or a piercing action. On a non-Thief, these weapons retain ordinary damage and penetration; a preparation action retains its reduced damage but grants no Prepared token. Its card must state that the preparation benefit requires Opportunist. No dynamic hiding or extra action-menu system is required.

## 5. Slip Away

An equipment-independent self action, available once per Thief per genuine combat. It consumes the full action, requires no check or Focus, deals no damage, grants Prepared, and readies **50% reduction of the next direct enemy attack** against that Thief before the beginning of their next scheduled turn.

- Apply reduction after native personal defenses. Retain `ceil(damage / 2)` from a positive qualifying outcome; a one-point hit still deals one.
- Spend protection only when a direct enemy attack would otherwise cause positive damage. Dodges, fully absorbed hits, ally damage, and DoT neither benefit nor consume it.
- The charge covers one committed enemy attack, including all its direct hits on this Thief. An AoE can consume it and reduce this Thief's portion only. It never protects teammates or subsequent enemy attacks.
- Clear protection at next own turn start, incapacity/death, or encounter exit. It does not revive, cleanse, force enemies to retarget, or prevent status effects attached to a hit.
- Guard and Slip Away use the strongest applicable percentage once. A hit while both apply still consumes Slip Away if it otherwise qualifies. Paladin rescue and other after-damage effects retain their own contracts.
- The use is spent when the self action resolves. Reviving, changing equipment, or replaying the action response cannot refresh it.

Slip Away grants Prepared before ending the action, so preparation survives into the next turn while protection expires at its beginning. This does not add a free attack. Against an ordinary unarmored target, two perfect regular attacks still outdamage Slip Away followed by one Sneak Attack.

## 6. Lifecycle and authority

In this design, incapacity means death, Stun, or removal from active combat. Daze retains its native timing behavior; if it causes a turn boundary or skip, the normal deadlines still apply. No extra debuff immunity is granted.

The encounter authority owns opening contributors, committed attack receipts, turn entitlements, Prepared, protection, and artifact spent state. Clients consume synchronized results for markers, previews, and combat text. No client-local random roll selects a shared result. Use native stable participant and encounter identities plus a committed attack identity; object addresses and animation frames are not sufficient.

At a genuine new encounter, reset class and artifact per-combat uses and initialize only the actual starting enemy roster. A dungeon's next battle is a new encounter. Native revive/rebuild hooks inside the current battle do not reset spent charges. Weapon swapping clears transient Prepared/Evasion as specified, but never restores a spent artifact use. Multiple copies of an artifact share its per-wielder per-combat budget.

Do not add a new mid-combat save feature. If a supported resume, reconnect, or host-change path reconstructs an ongoing encounter, it must preserve these receipts, used charges, roster entitlements, and deadlines. A release cannot silently treat that path as a fresh fight. Between fights, only normal package/class/item identity needs persistence. Exact supported network recovery paths require investigation and tests.

Fleeing ends transient protection and preparation. A later encounter can reinitialize the class combat kit under native encounter rules, but the package creates no gold, items, repeatable rewards, or external persistent loot state through these abilities.

## 7. Player-facing language

| Place | Proposed text |
| --- | --- |
| Class passive | **Sneak Attack:** Perfect single-target strikes with a precision weapon deal 20% extra damage against enemies that have not acted, or were damaged by a teammate since their last turn. Once per turn. |
| Target, first turn | **Opening: has not acted** |
| Target, ally hit | **Opening: distracted by an ally** |
| Personal state | **Prepared: your next precision attack can Sneak Attack. Expires after your next turn.** |
| Twin Feint | **With paired daggers, a basic strike that misses exactly one check and deals damage prepares your next precision attack. Once per turn.** |
| Feint | **Deal 60% damage. If the target loses HP, prepare your next precision attack.** |
| Pierce | **Deal 75% damage. Perfect success ignores armor. Cannot Sneak Attack.** |
| Slip Away | **Once per combat. Spend your turn to prepare a strike and halve the next direct attack against you before your next turn.** |
| Exhausted protection | **Slip Away used** |

Action previews show perfect damage separately from partial outcomes and distinguish an available Sneak Attack from a guaranteed damaging hit. Markers need text or shape as well as color. No other character should gain these markers, class actions, or bonuses merely from enabling the package.
