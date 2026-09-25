# Class actions and conditional proficiency damage

These authoring capabilities are introduced in framework 1.2.0. Content packages
using them must declare a minimum framework version of 1.2.0.

`Content.AttachClassProficiencies(classRow, proficiencyIds)` grants ordinary combat
proficiencies to an exact registered custom class. JSON class entries use the
existing `proficiencies` array. The native rolling action path supplies slots,
Focus, targeting, damage type, and attack effects. The equipped weapon still
supplies the skill check and base damage. Granting a spell does not imply an
Intelligence check. A proficiency's native slot and damage type overrides apply.

Explicit descriptions on registered custom actions also survive the native
"Standard Attack" fallback when `m_FullSlots` is false, there is no status prefab,
and the action does not ignore armor. This restores only the effect body; native
target labels, perfect-roll formatting, and armor-piercing descriptions remain.

The API rejects missing actions or an unregistered/copied class row without
partially granting the list. Calls are additive and idempotent, up to sixteen
distinct actions. Existing weapon actions and Guard remain available; a matching
weapon action is not duplicated. Register outside an open content batch, after
indexes have been published. JSON registration defers this validation until then.

## Equipped item actions

`Content.AttachItemProficiencies(item, proficiencyIds)` grants actions while an
exact registered custom nonweapon equipment row is equipped. JSON `item` entries
use the same `proficiencies` array. Supported types are armor, boots, helmets,
shields, necklaces, and trinkets. Registration is additive and idempotent, with a
maximum of sixteen distinct actions per item, and rejects incomplete grants.

Only positive counts in equipped slots grant actions. Backpack and belt copies
do not qualify. Actions disappear on the next native button rebuild after the
item is unequipped. Multiple equipped sources, class grants, and weapon actions
share one button per proficiency. Native equipment details list the granted
action names under "Actions while equipped". The rolling and damage rules remain
those of the proficiency and currently equipped weapon; grants are not class
restricted unless another explicit capability imposes that restriction.

The Paladin ownership arrangement can therefore keep `guardian: true` and
`overworldAilmentImmunity: {"displayName": "Cleansing March"}` on the class,
Censure in weapon `proficiencies`, and Smite in trinket `proficiencies`.
Cleansing March is the existing exploration passive, not a rolling combat action.
See [Guardian classes](GUARDIAN-AND-EQUIPMENT.md) for its scope.

## Random defense debuff outcomes

`Content.SetRandomDebuffOutcomes(row, outcomes)` accepts exactly two distinct
registered custom proficiency rows, including the declaring row itself. JSON uses
`randomDebuffOutcomes: ["armor_action", "resistance_action"]`.

One outcome must use the native armor behavior and the other native resistance
behavior. Both must be negative, single-target enemy debuffs. Their damage,
targeting, slot, chance, duration, and other combat fields must agree; the API
rejects incompatible outcomes without replacing an existing registration.

The acting owner selects one equally weighted outcome from a fixed hash of the
synchronized encounter seed, encounter index, active timeline entry, and actor
identity. Re-evaluating an action or changing its target does not reroll it. This
does not advance the game's random number generator. The selected proficiency ID
travels through the native attack outcome and its ordinary immunity/application
rules. If the active timeline identity is unavailable or inconsistent, the
debuff is suppressed while ordinary strike damage remains intact.

## Bonus against a specific resistance debuff

`Content.SetResistanceDebuffDamageBonus(row, sources, multiplier)` supports a
nonharmless, single-target magic action. The sources must be one to sixteen
distinct registered custom native resistance debuffs. The multiplier must be
finite, greater than one, and at most sixteen. JSON uses:

```json
"resistanceDamageBonus": {
  "sources": ["resistance_action"],
  "multiplier": 6.0
}
```

The bonus requires the victim's currently active native resistance status to have
a value of -1 or less and an exact matching source proficiency. Native defense
values truncate to integers, so fractional values that produce no reduction do not qualify. Unrelated resistance
reductions and the armor outcome do not qualify. Native expiry, replacement, or
removal ends eligibility. The status is not consumed. Its native duration is a
timeline timer and does not guarantee availability on the caster's next turn.

The multiplier applies separately to each victim before normal damage resolution,
including secondary victims added by native Justice. A base multiplier of
0.25 with a conditional multiplier of 6 produces 1.5 before the normal slot,
critical, and resistance rules. Existing eligible Reckoning multiplies this by
another 1.5. Damage previews include the active bonus. A single row cannot declare
both random outcomes and conditional damage.

## Verification boundary

The game-free CombatProficiencies suite exercises actual API and runtime source
against a small native boundary, including provenance, owner gating, stable
selection, rejected registrations, no consumption, and registration rollback.
Native combat, button layout and refresh, misses and immunity, animation, and
multiplayer agreement require separate live checks. Registration snapshots support
rollback. Live hot activation of the new capabilities has not been validated;
use a full game restart when changing content that declares them.
