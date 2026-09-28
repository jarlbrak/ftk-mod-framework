# Blacksmith equipment combat

Implementation reference for the approved [gear redesign](REDESIGN-PROPOSAL.md).
The rules below describe framework code and game-free checks. Recorded native
UI, animation and encounter observations are scoped in
[redesign validation](REDESIGN-VALIDATION.md); they do not establish co-op,
exhaustive lifecycle coverage or statistical campaign balance.

## Actions

Only the native `blacksmith` class receives these actions. An exact registered
item grants one action through `Content.SetBlacksmithEquipment(modGuid, item,
new BlacksmithEquipmentBonuses(...))`. JSON content uses a `blacksmithGear`
object containing exactly one of `setHammerArmor`, `overhandArmorPenalty`, or
`temperArmor`. Allowed nonzero integer ranges are 2-5, 2-6, and 3-5 respectively.
Set Hammer requires a one-handed weapon, Overhand a two-handed weapon, and
Temper a trinket. Class affinity supplies separate passive stats.

| Action | Native action path | Result |
|---|---|---|
| Set Hammer | Weapon checks and Focus, single physical target, 75% coefficient | Positive HP damage grants the item's 2/3/4/5 Armor until the next scheduled own turn. A shield and the granting hammer must remain equipped. |
| Overhand | Weapon checks and Focus, single physical target, 115% coefficient | A committed outcome applies the item's 2/3/4/6 Armor penalty until the next scheduled own turn, including misses. |
| Temper | Native slot bypass and living-friendly target picker, no roll or Focus | Spend a full action and one use per character per encounter to grant 3/4/5 Armor through the target's next two completed scheduled turns. Self is allowed. |

Registration clones the themed weapon's prefab and replaces its inherited
proficiency dictionary with an empty private dictionary. Its ordinary native
basic attack remains enabled. Native shields retain their separate Taunt.
Set Hammer or Overhand is added to the native proficiency-button UI only for an
eligible Blacksmith; Temper comes from an equipped granting trinket. Other
classes receive the same basic weapon attack. Splash, Shockwave and Stun from
source templates are deliberately removed from these private weapons.
Vanilla database rows and prefabs are unchanged.

## Duration, stacking and equipment

The framework contributes `max(Set Hammer Armor, Temper Armor) - Overhand
penalty` through the native combat Armor modifier. Native Armor, Taunt and
other mods' independent Armor contributions remain additive. The native final
Armor clamp and disease adjustment still run.

- Equal or stronger Temper replaces the existing Temper and restarts its two
  future-turn duration. Weaker Temper spends its caster's use but neither lowers
  nor refreshes the stronger effect. Multiple Blacksmiths cannot sum Temper.
- Temper cast during the recipient's current turn excludes that turn's
  completion. The next two completed recipient turns count.
- Removing Set Hammer's granting weapon or any required shield ends that
  personal protection. Re-equipping cannot restore it.
- Committed Temper persists when its caster removes or exchanges the kit,
  including self-target Temper. Overhand exposure and the caster's Temper use
  also survive equipment changes.
- Death and resurrection reset positive protection. They preserve the
  encounter's used Temper and a committed penalty until its scheduled expiry.
  Incapacity alone does not invent an expiration; the schedule controls duration.
- Armor-specific dispel and all-proficiency removal clear this feature's
  positive Armor. They do not erase its committed Overhand cost or replenish
  Temper. Other providers' effects remain under their own native removal rules.
- Combat exit clears the actor's temporary modifiers. A new native encounter
  resets all uses and replay receipts. Active encounters prevent content hot
  activation. Mid-combat save/resume or reconnect restoration is not implemented
  or claimed; it requires an explicit supported transport/lifecycle contract.

A repeated heavy action during one own-turn interval cannot reduce existing
exposure by switching to a weaker weapon. Repeated playback cannot refresh any
feature effect. Extra actions supplied by another mod require compatibility
validation; this kit does not grant them.

## Native integration and authority

Installed-assembly inspection establishes these integration points:

- The host creates `EncounterSessionMC.FightOrderEntry.m_EntryID`. Initial and
  updated timelines transport those entries to peers. The first replicated
  visual entry identifies `CharacterDummy.EngageBattle`'s scheduled turn.
- Native `EncounterSession.RemoveAttacker` completes the current entry.
  `ApplyProficiencyEffect` removes interrupted entries; each removed identity
  counts once as a skipped start and completion. Native stun delays an entry
  rather than removing one, so it does not manufacture additional turns.
- Temper follows the native direct action's slot bypass and target picker, but
  starts a non-consumable harmless attempt. The native consumable path requires
  a `ProficiencyBase`; these private actions deliberately have none.
- `DamageCalculator._finishEngageAttack` is after friendly target selection.
  Eligibility is checked there, before calculating a custom outcome. Selecting
  or hovering over an action spends no framework use. Temper adds a native
  Cancel button to its friendly picker. Temper clears the previous weapon slot
  preview on activation, including activation without hover. This local UI reset
  sends no bypass RPC and consumes no action. Cancel stops and disposes only that
  request's target-wait coroutine, closes the picker and restores the native
  stance menu. It supplies no fabricated target, commits no action, and spends
  neither Focus nor the encounter use. Slot bypass is sent only after an actual
  target passes commit validation. A solo caster with no alternative living
  target uses native immediate self-targeting, without opening a picker.
- The native acting owner's `DamageCalculator._playAttackSequence` sends
  `CharacterDummy.PlayAttackSequence` to all peers. This is native owner
  authority for damage, combined with host-authored schedule identity. It is
  an explicit refinement of the proposal's broader host-authority wording;
  the framework does not introduce a second attack RPC protocol.
- The private actions have no `ProficiencyBase` and no native amount display.
  Each granting item has a private proficiency row, making the native action ID
  identify the exact registered source and tier. Its serialized
  `DummyDamageInfo.m_ProficiencyAmount` carries the host timeline entry ID, with
  `m_ProfHasAmount` false. Replay rejects outcomes from an inactive entry and
  deduplicates by actor, host entry and action. The package still contains 32
  items; framework action rows are registered as part of each capability. It never uses animation callbacks as action identity.
- Native `CharacterDummy.ArmorMod` feeds `CharacterStats.TotalArmor` before
  final clamp and disease handling. No vanilla status record or shared
  proficiency object is overwritten.

Native Armor icons reflect the modifier. Their tooltip text includes the
active feature, magnitude and remaining duration; opposing positive protection
and exposure can both remain visible. Item cards, action descriptions, combat
log entries and native floating text describe the feature. Current native
layout and input observations are recorded in the validation guide; changes to
these paths require a fresh check.

## Verification

Focused game-free commands:

```sh
dotnet run --project FTKModFramework/Tests/BlacksmithCombat/BlacksmithCombat.csproj -c Release
dotnet run --project FTKModFramework/Tests/HotReloadResources/HotReloadResources.csproj -c Release
dotnet build FTKModFramework/FTKModFramework.csproj -c Release
git diff --check
```

The combat suite covers range validation, positive-hit qualification,
nonstacking, weaker/equal/stronger reapplication, self-turn exclusion, repeated
receipt/turn events, interrupted entries, swap rules, dispel, death/revive budget
retention, encounter reset, and independence from another Armor provider. The
resource suite checks hot-activation gating and registration rollback. The
target-wait tests cover cancel, repeated cancel/dispose, reopening, accepted
target completion, preserved turn/use, and isolation from another iterator.
These tests isolate rules and resource coordination; they do not run Unity or
establish native attack execution.

Native observations now cover both weapon paths at every tier, partial and
fully blocked Set Hammer outcomes, ordinary Focus spending, full-action costs,
ally and self Temper, cancellation, disabled used-action UI, compact selected
descriptions and native icons. Mixed equipment checks include a native shield
and a Hunter wearer without Blacksmith-only actions or affinity. Current
scheduled-turn expiry, consecutive dungeon encounters and native equipment
swaps also have recorded cases. Earlier wrong-stat or overlevelled smoke
observations remain separately labeled in the validation guide.

These are scoped cases, not exhaustive native coverage. Interrupt/stun/speed
interactions, every skipped-turn/death/revive path, other mods' Armor/dispel
behavior and host/client agreement retain their explicit limits. Game-free
tests cover the corresponding rule logic where listed above. Mid-combat
resume/reconnect remains unsupported. See the validation guide for final
delivery checks and release boundaries rather than treating this list as a
claim that all native lifecycle combinations have been exercised.

## Authoring example

A registered one-handed weapon can declare:

```json
"blacksmithGear": { "setHammerArmor": 3 },
"classAffinity": { "classId": "blacksmith", "modifiers": { "vitality": 0.01 } }
```

A two-handed weapon uses `overhandArmorPenalty`; a granting trinket uses
`temperArmor`. Exactly one property is allowed in `blacksmithGear`. Fields must
be integers, and unknown or null fields are rejected. The class affinity is
independent of the action declaration. The package builder assigns each slot's
approved passive benefit separately.

Combat buttons borrow existing native all-white outlined sprites: Set Hammer uses
`taunt`, Overhand uses `heavyattack`, and Temper uses `protect`. These sprites
remain game-owned. Custom combat icons are prohibited by the repository icon
rule; no Blacksmith PNG is embedded or loaded for an action button.

All eight weapons also retain their template item icons, because native basic
attack buttons use that same item icon. Their original 3D inventory previews
remain separate from this shared combat UI field.

Combat selection descriptions use one compact line for weapon actions so they
leave the native damage and accuracy row clear. Temper uses two short lines
with its damage and accuracy rows already hidden by the existing native-action
presentation. Detailed rules remain in item tooltips and this guide; active
status details remain on Armor tooltips and are not appended to the action
selection panel. Native layout verification remains required after text changes.
