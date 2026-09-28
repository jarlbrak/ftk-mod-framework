# Native class equipment affinity

`classAffinity` adds a small typed stat bonus to one equipment item for one
named class. It does not restrict who can equip the item. The item's ordinary
actions and `modifiers` still work for every class.

## Data declaration

```json
{
  "kind": "item",
  "id": "example_forge_armor",
  "template": "armorHeavy1",
  "displayName": "Forgework Coat",
  "classAffinity": {
    "classId": "blacksmith",
    "modifiers": {
      "vitality": 0.01
    }
  }
}
```

`classId` names an existing class row. The contract currently accepts
`armor`, `resistance`, `reflect`, `vitality`, and `speed`; a value is applied
only to the matching class while the item is equipped. Armor, Resistance, and
Reflect accept 0 or 1. Vitality and Speed accept 0 or `0.01`, which is one
native stat point. A declaration must contain at least one nonzero value.
Unknown fields and class names reject the affinity declaration. Affinity does
not create set-count thresholds or combat procs.

Ordinary item stats live alongside it in `modifiers`:

```json
"modifiers": { "armor": 2, "resistance": 1 },
"classAffinity": {
  "classId": "blacksmith",
  "modifiers": { "vitality": 0.01 }
}
```

## Runtime contract

The loader allocates a private `FTK_characterModifier` row with the same
deterministic ID as the item. Native `CharacterStats.TallyCharacterMods` and
`TallyCharacterDefense` rebuild from equipped modifier IDs; narrow postfixes add
the matching affinity after those resets. Items in inventory, the Belt, and
unrelated classes contribute no affinity. Equipping or removing an item causes
the native tally to recompute its contribution. Every contributing equipped
item row is visited once, matching the game's modifier-list behavior.

Item and weapon detail cards show the class label and the exact conditional
bonus, for example `Blacksmith bonus: +1 Vitality`. The localized class name is
resolved when the card is shown, so registration timing does not freeze a fallback
label. Multiple bonus values retain separate lines. Missing class or item
registrations contribute nothing; the patch leaves
the native tally result intact if it cannot read the equipped list. No vanilla
rows or prefabs are modified. Because all peers must load the same package
identities and registration order, co-op determinism still needs live testing.

`Content.SetItemClassAffinity(modGuid, item, classRow, bonuses)` exposes the
same contract to compiled content. `ItemClassAffinityBonuses` validates the
per-item bounds before registration.

Offline tests cover the matching class, equipped-row selection, additive items,
duplicate IDs, conflicting registration, description formatting, and registry
rollback. These tests do not establish saved-game persistence, live UI layout,
or host/client agreement.
