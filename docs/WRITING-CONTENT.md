# Write a content mod

FTK Mod Framework adds content to the original *For The King* without editing the game's files. The [Paladin package](../marketplace/packages/paladin/content.json) is a complete source example. Published [Paladin 2.0.1](../marketplace/packages/paladin/README.md) requires framework 1.8.0. The [manifest](../marketplace/packages/paladin/manifest.json) defines identity and compatibility; its content file defines the class, equipment, abilities, models, and icons. Copy its structure, then use your own stable mod GUID, IDs, names, and original assets. The [marketplace guide](MARKETPLACE.md) covers review and publication.

## Start with a manifest

Place `manifest.json` and one or more JSON files containing `entries` arrays in a folder under `<game>/BepInEx/plugins/`. The Paladin 1.4.0 manifest begins:

```json
{
  "modGuid": "com.ftkmf.paladin",
  "name": "Paladin",
  "version": "1.4.0",
  "frameworkVersion": "1.2.1",
  "author": "JarlBrak"
}
```

Replace Paladin's identity and author with your own values. `version` identifies your mod release. `frameworkVersion` is the earliest framework version in the same major series on which you confirmed it works. An absent, invalid, older, or different-major declaration blocks content loading while leaving the mod visible. See [mod versioning](MOD-VERSIONING.md) before releasing an update. Optional manifest fields include `description`, `developmentOnly`, and `behaviorDll`; marketplace packages have a narrower [submission contract](MARKETPLACE.md#submit-a-mod).

## Add entries

Each entry has a `kind`, a stable local `id`, a vanilla `template` to clone, and a `displayName`. Supported kinds are `item`, `weapon`, `proficiency`, `class`, `enemy`, and `encounter`. [Custom races](CUSTOM-RACES.md) and the [Lore Store unlock](#unlock-the-lore-store) are exceptions without a template. The loader reads entries in deterministic `(modGuid, id)` order and resolves cross-file references after reading all files. Do not use hard-coded custom enum integers.

The Paladin's [Novice Hammer](../marketplace/packages/paladin/content.json) illustrates a weapon entry. Its `fields` set damage, Vitality skill, level range, rarity, and shop/drop eligibility. `itemModels` and `displayModels` provide different original meshes for equipped and inventory views. An optional `icon` replaces small UI sprites, including a weapon's basic-attack glyph; the candidate omits it on weapons to retain native combat symbols. The Paladin class entry uses local IDs in `startweapon` and `startitems`, so those items can be declared elsewhere in the same file.

An `item` entry may set `description` for its native item card. This is useful for custom resources: their synthetic ID otherwise appears as an untranslated description key. The authored text replaces only that missing-key fallback; native cannot-use messages still take precedence.

`fields` accepts the game's serialized field names or case-insensitive friendly aliases. Common aliases are `rarity`, `goldValue`, `minLevel`, `maxLevel`, `dropable`, and `townMarket`; weapons add `damage`, `damageType`, `skill`, `slots`, and `damageGain`. Classes add `strength`, `intelligence`, `awareness`, `talent`, `speed`, `vitality`, `startingGold`, `focus`, `primaryStat`, `startWeapon`, `startItems`, and `skills`. Unknown fields log a warning; values that cannot be converted reject the affected entry. A class's `skills` object is copied privately so it cannot change its vanilla template.

An `AddWeapon` or JSON weapon entry inherits the template's action list. `proficiencies` adds actions to that list. Set `replaceProficiencies: true` on a JSON weapon to replace the inherited list with the declared actions, or call `Content.ReplaceProficiencies` for a registered weapon. Replacement uses a private prefab copy and keeps the native template unchanged. Since framework 1.7.0, an explicit empty `proficiencies: []` clears special actions while preserving an enabled regular attack. A missing or null array is an error when replacement is requested. Class-only actions conditioned on exact equipped weapons use [`weaponProficiencies`](COMBAT-PROFICIENCIES.md#weapon-gated-class-actions-framework-170). Item levels are progression tiers, not hero levels; verify ordinary shop and loot availability in game.

Equipment can declare `modifiers` with `armor`, `resistance`, `vitality`, `speed`, `awareness`, `talent`, `focusCapacity`, and `reflect`. The four stat fields are fractional bonuses (for example, `0.05` for five stat points); `focusCapacity` adds to maximum Focus. The modifier row is private to the custom item's ID and starts empty, so bonuses from a cloned native template are not inherited. An omitted bonus has value zero. All modifier values are checked when the package loads.

See [class actions and conditional proficiency damage](COMBAT-PROFICIENCIES.md) for equipment-independent rolling class actions, random armor/resistance outcomes, and bonuses requiring an exact active resistance-debuff source.

## Models and behavior

Use original PNG and GLB assets for marketplace packages. The Paladin package shows path-relative `icon`, `itemModels`, `displayModels`, and apparel declarations. Mesh paths must match the actual renderer hierarchy and skeleton; a valid JSON file or successful build cannot prove the model fits in game. Follow [custom models](CUSTOM-MODELS.md), the [player renderer contract](MODEL-PLAYER-API.md), and the [renderer transaction contract](MODEL-RENDERER-API.md).

Two unreleased presentation options have focused contracts:

- [`metallicGlossTexture`](GUARDIAN-AND-EQUIPMENT.md#authored-metallic-and-smoothness-masks-unreleased) supplies an optional linear RGBA metallic/smoothness map per renderer. Equipped and item-display assignments are separate.
- [`helmetHairVisibility`](GUARDIAN-AND-EQUIPMENT.md#helmet-hair-visibility-unreleased) supplies explicit top/bottom hair booleans for supported custom helmet templates, without changing head geometry.

Omitting either option preserves its inherited behavior. Do not copy a historical
framework minimum for a package using these options; select a release that
actually includes the APIs before publishing.

For a compiled mod, reference `FTKModFramework.dll` and the game's publicized `Assembly-CSharp` from a .NET 3.5 BepInEx 5 plugin. Register through `FTKModFramework.Core.Content` after `GridEditor.TableManager.Initialize` has populated the tables. `Content.AddItem`, `AddWeapon`, `AddProficiency`, `AddClass`, `AddEnemy`, and `AddEncounter` clone and register rows; `Content.AttachProficiencies` and `AttachEnemyProficiencies` connect actions to privately cloned weapons. `Content.Db<T>()` ensures a table index exists before direct reads. `Content.AddPassive` binds one of the framework's closed `PassiveTrigger` moments to a registered class; it does not create a database row or a chance roll. Keep registrations idempotent and pass your plugin GUID to each call.

A data mod can declare `behaviorDll` for a `ProficiencyBase` subclass or custom quest-logic verb, but the marketplace's current [content contract](MARKETPLACE.md#submit-a-mod) does not distribute behavior DLLs. Campaign and adventure authoring have separate [campaign](CAMPAIGNS.md) and [adventure](ADVENTURES.md) guides; their availability as framework APIs does not mean that a campaign package has shipped.

In framework 1.0.3, `Content.AddOverworldAilmentImmunity(classRow, displayName)` or a class
entry's `overworldAilmentImmunity` object with a `displayName` opts an exact registered custom class
into native Poison and Curse immunity while outside combat. This includes tile
hazards and other exploration sources, but does not cure existing conditions,
prevent tile damage or losses, or change combat immunity. The Paladin 1.4.0
package uses this declaration for Cleansing March. Verify the effect in game
before advertising it as a released ability.

## Unlock the Lore Store

Framework 1.3.0 adds a `loreStoreUnlock` entry. Its only supported form is:

```json
{ "kind": "loreStoreUnlock", "id": "all" }
```

While the mod is loaded, every Lore Store entry the player could buy reads as purchased. Entries from free DLC packs also unlock. Entries for unowned paid DLC, entries the game hides from the store, and cloud promotions it is not currently offering keep their real state. A purchased class is also shown at character creation.

The framework writes no Lore Store purchase, so removing the mod restores the player's real purchases. Lore reveals and the party's shared reveal state remain genuine progress. In co-op, most unlocks apply only to the player who has the mod. The game shares some world unlocks with the party, except entries it requires every player to own. Co-op is unverified. The entry requires next-launch activation. [Lore Store Unlocked](../marketplace/packages/lore-store-unlocked/README.md) is the package that uses it.

## Validate and play

Read `BepInEx/LogOutput.log` for registration summaries and entry-specific errors. Verify the class or item in its native screen, acquire it through an ordinary route, test its action or modifier, then save and resume with the same mod set. A package hash proves bytes, not visual fit or gameplay behavior. The [marketplace guide](MARKETPLACE.md) lists publication checks.

By default, prepared marketplace changes apply on the next launch. The opt-in [title-screen activation mode](HOT-RELOAD.md) can apply managed data-package selections in the same process on the audited macOS build, before entering an adventure. Manual content and unsupported plugin forms continue to use next-launch activation. Removing a mod used by a save can make that save unloadable until the mod is restored; the framework then refuses the resume with a message rather than loading it (see [save compatibility](MARKETPLACE.md#save-compatibility)). Renaming or removing a content ID has the same effect on saves that use it. Co-op clients need the same enabled content and assets; matching IDs alone do not prove multiplayer behavior.

## Shared equipment progression (framework 1.7.0)

Framework 1.7.0 first shipped a universal physical currency through
`Content.SetEnemyDropRule(registeredItem, rule)` and
`Content.RegisterTownExchange(offers)`. Content packages using them must
declare a minimum framework version of 1.7.0. Separate class packages can
append distinct offers to the same framework Guild Token. Duplicate offers and
conflicting currencies are rejected.

An item may declare `enemyDropRule` with integer `minimumDisplayedLevel`,
`ordinaryChancePercent`, `bossChancePercent`, `guaranteedByOpportunity` and
`namedBossGroups` arrays of native enemy row IDs. Reward decisions are made by
the master for admitted native drops, with campaign-persisted miss counts and
stable ordering. Chance, campaign exposure and live persistence are separate
verification obligations.

A class can declare `townExchange: { "offers": [...] }`; a gear-only mod can
declare top-level `townExchangeCatalogs`. Each offer specifies `item`, `name`,
`family` and native inventory `slot`. The declaring class supplies the registered owner identity.
C# authors pass that registered class ID to `TownExchangeOffer`; unscoped offers
are rejected. Stock includes only offers for the current buyer's class and hides
items in their backpack or any equipped slot. Purchase confirmation checks
ownership and class again before spending a token. The framework supplies the
token and Back Alley vendor; Paladin 2.0.0 contributed the first catalog. No
separate currency package is needed. The currency is neither class-bound nor a
specialization selector. See
[Framework Equipment Exchange](GUARDIAN-AND-EQUIPMENT.md#framework-equipment-exchange-framework-170)
for catalog and legacy-token details.

Registered catalog items become exclusive to the exchange prospectively;
ordinary shop/drop candidates and saved merchant stock are filtered, while
already-owned items remain. Current purchases are solo-only pending verified
network transaction support. Token and exchange registration require a game
restart for content changes; hot activation is rejected. Do not describe these
limitations as multiplayer or release approval.
