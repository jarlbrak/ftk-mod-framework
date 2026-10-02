# Paladin artifacts

Status: released artifact identities with unreleased v4 role interaction. The Last Vigil,
Kingsfall and The Last Bastion have original runtime
models, icons and equipment rows. Gameplay validation is partial; see
[Legendary validation](LEGENDARY-VALIDATION.md) for artifact identities, observed
results and remaining gates. Earlier generated concept previews are not game
screenshots. Flavor quotations below remain proposals.

## Direction

The candidate retains one exceptional hammer for each Paladin loadout. Both use Vitality and
reward spending an action to protect another party member. Neither increases
Guard's 50% reduction, adds another rescue charge, or replaces the existing
Mercy, Censure and Verdict equipment families.

Original FTK calls its highest ordinary weapon rarity `artifact`. The installed
assembly's `FTK_itemRarityLevel.ID` lists common, uncommon, rare, artifact, quest
and lore; it has no legendary rarity. All three pieces use the native Artifact
rarity.

The native [Toy Hammer](https://fortheking.wiki.gg/wiki/Toy_Hammer) uses Luck and
its distinctive Bonk action. [The Walloper](https://fortheking.wiki.gg/wiki/The_Walloper)
combines a heavy striking identity with a large Speed penalty. The lesson for
this pair is a recognizable silhouette and one memorable rule each.

Installed-data comparisons, rather than FTK2's similarly named weapons:

| Native weapon | Base row and actions | Design lesson |
|---|---|---|
| Toy Hammer | Artifact; 7 magic damage, 1 Luck check; unique Bonk action. | An unusual governing stat and signature action can define the whole weapon. |
| The Walloper | Artifact; 50 physical damage, 6 Strength checks; its own heavy-hammer action. | A spectacular hit has an accuracy and speed cost. |
| Cursed Longsword | Artifact; 20 physical damage, 3 Strength checks; single-target and splash drain-life proficiencies. | Sustain changes the player's choices beyond the damage number. |
| Royal Hammer | Common; 38 physical damage, 5 Strength checks; splash, heavy attack and group reset proficiencies. | High damage and a broad action set already exist outside Artifact rarity. |

These are base database values, not promises about every level-adjusted combat
tooltip. The native single-target Cursed Longsword drain requires full slot
success with a 5-point accuracy penalty. Royal Hammer's heavy attack uses a
1.5 damage multiplier with a 35-point accuracy penalty. These weapons
need comparison against native actions as well as nominal damage.

## v4 armor role interaction

A matching Head, Body and Foot family keeps its core role when an Artifact replaces ordinary hands. Artifacts have no fixed family affinity in the candidate. Mercy's Guard-armed Smite bond requires the matching role core, not a matching artifact armament. Censure's Guard-armed physical charge likewise belongs to the core; if Kingsfall is also charged, the strongest +50% bonus applies once rather than multiplying. Changing weapon clears a pending role charge. Last Vigil, Kingsfall and Last Bastion cannot supply a matching 1H-plus-shield or 2H armament completion benefit. They retain their own conditional Guardian effects and item stats. Balance must compare each combined artifact loadout against the corresponding completed ordinary family, including the role's costs.

## The Last Vigil

One-handed guardian hammer. A compact reliquary head with silver or ivory
striking faces, aged gold framing and a protected blue light. A short dark grip
leaves room for a shield. The visual reads as a light kept burning through a siege.

Flavor proposal: "The watch ended. The light did not."

**Unbroken Watch:** The first time an active Guard from this wielder reduces a
damaging direct enemy attack, restore 1 Focus to the guarded ally.

- Once per Guard action; capped by the ally's normal Focus maximum.
- Does not restore the Paladin's own Focus, trigger from damage over time, or
  trigger from an attack that Guard did not reduce.
- If the ally is already at maximum Focus, the trigger is spent without an effect.
- Multiple hits or guardians cannot multiply the reward for one guarded outcome.
- The wielder must retain this weapon until the trigger; swapping it away ends
  this weapon's pending perk.

Unpublished v4 values: 19 physical damage, 4 Vitality checks, one hand.
The candidate Censure Hammer is 24/4. Last Vigil trades raw one-handed damage
for party Focus when the player correctly anticipates an attack. It supplies no
Mercy, Verdict or Censure armament completion benefit.

The player chooses whom to protect based on both incoming danger and who needs
Focus. The weapon grants no resource merely for pressing Guard on a safe target.

## Kingsfall

Two-handed retribution hammer. A broad dark iron or stone head framed by the
remains of a gold crown, a restrained amber fracture, and a long crimson grip.
Two clearly usable striking faces distinguish it from an axe or ornamental staff.

Flavor proposal: "A crown is no defence against a broken oath."

**Reckoning:** The first time this wielder's active Guard reduces a damaging
direct enemy attack, ready a charge that adds 50% damage to their next
single-target hammer attack.

- One charge maximum. Extra hits and repeated Guard actions do not stack it.
- A charge expires at the end of the wielder's next turn, when combat ends, or
  when the weapon is unequipped. It cannot carry into another fight.
- Starting the eligible attack consumes the charge, even if the attack misses.
- Increase the attack's damage before target mitigation. Exclude splash,
  reflection, retaliation and other secondary damage.
- Charge generation requires actual Guard mitigation, not damage over time,
  a dodge, or simply designating an ally.

Unpublished v4 values: 28 physical damage, 5 Vitality checks, two hands.
Candidate Censure Great Hammer is 30/5. Kingsfall trades uncharged damage
relative to that physical branch for Reckoning and gives up a shield. Its burst
requires first spending an action on Guard and having that protection matter.
Earlier native testing demonstrated one charged attack with partial slot success
at the previous damage values. Revised previews and impacts, criticals, mitigation
and the remaining lifecycle cases still need coverage.

The player alternates protecting and striking instead of receiving a free
retaliation attack. A Censure core can ready its own +50% physical vengeance after a real Guard-reduced hit, but it does not multiply with Kingsfall's Reckoning; one eligible attempt consumes the strongest charge, including on a miss. The candidate uses a "Reckoning ready" HUD message and a
charged action title and damage preview. The earlier crown-light and sound
concept is not implemented.

## The Last Bastion

Legendary shield and visual companion to The Last Vigil. A tall faceted heater
or kite silhouette with broad shoulders, a pointed base and three restrained
battlements. Ivory and silver planes, a heavy aged-gold perimeter and a recessed
sapphire ward suggest a small fortress. Keep its profile thin enough for native
shield animations and avoid a solid oversized block.

Flavor proposal: "Behind this shield, there is still tomorrow."

**Stand Firm:** Using Guard immediately removes one existing Poison, Curse,
Stun or Daze effect from the chosen ally. Guard then provides its normal 50%
direct-attack damage reduction.

- Remove one eligible condition per Guard action in deterministic priority:
  Stun, Daze, Curse, Poison. Curse removes one active curse, ordered by the native
  enum; permanent curses remain. Poison clears the stacked Poison condition.
- No ongoing immunity, self-cleanse, healing, extra rescue or stronger reduction.
- An ally without an eligible condition receives ordinary Guard only.
- The shield must be equipped when Guard resolves. Duplicate event delivery must
  not remove additional conditions.
- Do not retroactively restore a turn or action already lost to a condition.
  The implementation uses native proficiency removal and stat recalculation;
  live Stun, Daze and Curse timing coverage remains outstanding.

Candidate modifiers: no personal Armor or Resistance bonus and a 4-point Speed
penalty. This keeps the equipment valuable specifically for Guardians who spend
actions protecting allies. Other shield users gain no compensating passive stats.

Mercy armor core supplies a 4% qualifying Guard heal; Verdict's debuff
prevention requires matching armament completion. Last Bastion instead recovers
an ally who is already impaired. It does not grant either completion effect. Combining it with Last Vigil
helps a troubled ally recover, then supplies Focus if an attack actually hits
their Guard. The pairing does not complete a family armament.

## Acquisition and implementation boundaries

All three rows retain drop-enabled Artifact declarations at native item tiers 4
through 6, with night-market and dungeon-merchant eligibility. They are not
in the shared Guild Token exchange catalog. Ordinary town-market
stock is disabled. No class, Lore unlock or DLC gate is authored. This declares
eligibility, not a guaranteed drop; ordinary acquisition of these new pieces has
not yet been observed. The hammers retain ordinary Vitality weapon use for other
classes, while the signature effects require the Guardian capability. A custom
quest remains optional future work.

The public `GuardianEquipmentBonuses` API now provides `guardFocusRestore`,
`guardReckoning` and `guardCleanse`, with matching data declarations and helper
validation. Focus and Reckoning require a registered custom weapon. Cleanse can
be attached to registered custom equipment. Existing healing, retaliation and
ward bonuses are preserved. See [Guardian and equipment](../GUARDIAN-AND-EQUIPMENT.md)
for the authoring contract.

Acceptance should cover duplicate outcome delivery, overlapping Guardians,
multi-hit attacks, full Focus, missed charged attacks, weapon switching,
incapacitation, battle transitions and multiplayer agreement. Confirm equipped
and loot-display model fit, attack animations, item-card readability and save
round-trips with the authored 3D assets. Native balance comparisons and the
partial live trial do not establish complete gameplay or multiplayer coverage.
