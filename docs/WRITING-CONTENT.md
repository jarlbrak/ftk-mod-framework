# Write a content mod

FTK Mod Framework adds content to the original *For The King* without editing the game's files. The [Paladin package](../marketplace/packages/paladin/content.json) is a complete source example. Its 1.2.0 source candidate requires framework 1.0.3; the published catalog still serves Paladin 1.0.1. The [manifest](../marketplace/packages/paladin/manifest.json) defines identity and compatibility; its content file defines the class, equipment, abilities, models, and icons. Copy its structure, then use your own stable mod GUID, IDs, names, and original assets. The [marketplace guide](MARKETPLACE.md) covers review and publication.

## Start with a manifest

Place `manifest.json` and one or more JSON files containing `entries` arrays in a folder under `<game>/BepInEx/plugins/`. The Paladin 1.2.0 source manifest begins:

```json
{
  "modGuid": "com.ftkmf.paladin",
  "name": "Paladin",
  "version": "1.2.0",
  "frameworkVersion": "1.0.3",
  "author": "JarlBrak"
}
```

Replace Paladin's identity and author with your own values. `version` identifies your mod release. `frameworkVersion` is the earliest framework version in the same major series on which you confirmed it works. An absent, invalid, older, or different-major declaration blocks content loading while leaving the mod visible. See [mod versioning](MOD-VERSIONING.md) before releasing an update. Optional manifest fields include `description`, `developmentOnly`, and `behaviorDll`; marketplace packages have a narrower [submission contract](MARKETPLACE.md#content-submission).

## Add entries

Each entry has a `kind`, a stable local `id`, a vanilla `template` to clone, and a `displayName`. Supported kinds are `item`, `weapon`, `proficiency`, `class`, `enemy`, and `encounter`. The loader reads entries in deterministic `(modGuid, id)` order and resolves cross-file references after reading all files. Do not use hard-coded custom enum integers.

The Paladin's [Novice Hammer](../marketplace/packages/paladin/content.json) illustrates a weapon entry. Its `fields` set damage, Vitality skill, level range, rarity, and shop/drop eligibility. `itemModels` and `displayModels` provide different original meshes for equipped and inventory views; `icon` provides the 2D image. The Paladin class entry uses local IDs in `startweapon` and `startitems`, so those items can be declared elsewhere in the same file.

`fields` accepts the game's serialized field names or case-insensitive friendly aliases. Common aliases are `rarity`, `goldValue`, `minLevel`, `maxLevel`, `dropable`, and `townMarket`; weapons add `damage`, `damageType`, `skill`, `slots`, and `damageGain`. Classes add `strength`, `intelligence`, `awareness`, `talent`, `speed`, `vitality`, `startingGold`, `focus`, `primaryStat`, `startWeapon`, `startItems`, and `skills`. Unknown fields log a warning; values that cannot be converted reject the affected entry. A class's `skills` object is copied privately so it cannot change its vanilla template.

An `AddWeapon` or JSON weapon entry inherits the template's action list unless you attach replacement proficiencies. Pick a template whose inherited actions you intend to keep. Item levels are progression tiers, not hero levels; verify ordinary shop and loot availability in game. Clone and register content through the public API so vanilla rows and prefab assets remain unchanged.

## Models and behavior

Use original PNG and GLB assets for marketplace packages. The Paladin package shows the path-relative `icon`, `itemModels`, `displayModels`, `playerModels`, and apparel declarations. Mesh paths must match the actual renderer hierarchy and skeleton; a valid JSON file or successful build cannot prove the model fits in game. Follow [custom models](CUSTOM-MODELS.md), the [player renderer contract](MODEL-PLAYER-API.md), and the [renderer transaction contract](MODEL-RENDERER-API.md).

For a compiled mod, reference `FTKModFramework.dll` and the game's publicized `Assembly-CSharp` from a .NET 3.5 BepInEx 5 plugin. Register through `FTKModFramework.Core.Content` after `GridEditor.TableManager.Initialize` has populated the tables. `Content.AddItem`, `AddWeapon`, `AddProficiency`, `AddClass`, `AddEnemy`, and `AddEncounter` clone and register rows; `Content.AttachProficiencies` and `AttachEnemyProficiencies` connect actions to privately cloned weapons. `Content.Db<T>()` ensures a table index exists before direct reads. `Content.AddPassive` binds one of the framework's closed `PassiveTrigger` moments to a registered class; it does not create a database row or a chance roll. Keep registrations idempotent and pass your plugin GUID to each call.

A data mod can declare `behaviorDll` for a `ProficiencyBase` subclass or custom quest-logic verb, but the marketplace's current [content contract](MARKETPLACE.md#content-submission) does not distribute behavior DLLs. Campaign and adventure authoring have separate [campaign](CAMPAIGNS.md) and [adventure](ADVENTURES.md) guides; their availability as framework APIs does not mean that a campaign package has shipped.

In framework 1.0.3, `Content.AddOverworldAilmentImmunity(classRow, displayName)` or a class
entry's `overworldAilmentImmunity` object with a `displayName` opts an exact registered custom class
into native Poison and Curse immunity while outside combat. This includes tile
hazards and other exploration sources, but does not cure existing conditions,
prevent tile damage or losses, or change combat immunity. The Paladin 1.2.0
candidate uses this declaration for Cleansing March. Verify the effect in game
before advertising it as a released ability.

## Validate and play

Read `BepInEx/LogOutput.log` for registration summaries and entry-specific errors. Verify the class or item in its native screen, acquire it through an ordinary route, test its action or modifier, then save and resume with the same mod set. A package hash proves bytes, not visual fit or gameplay behavior. The [marketplace guide](MARKETPLACE.md) lists publication checks.

By default, prepared marketplace changes apply on the next launch. The opt-in [title-screen activation mode](HOT-RELOAD.md) can apply supported Paladin or empty selections in the same process on the audited macOS build, before entering an adventure. Other mods continue to use next-launch activation. Removing a mod used by a save can make that save unloadable until the mod is restored. Co-op clients need the same enabled content and assets; matching IDs alone do not prove multiplayer behavior.
