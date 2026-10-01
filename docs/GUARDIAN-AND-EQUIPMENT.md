# Guardian classes and original equipment

These public APIs and data declarations ship in framework 1.0.0 and are used by
the published Paladin package. The [Paladin design](paladin/DESIGN.md) describes
their intended behavior; the [launch record](paladin/LAUNCH-1.0.0.md) separates
observed macOS gameplay from open live and co-op coverage.

## Guardian behavior

`Content.AddGuardian(classRow)` attaches the Guardian kit to an exact registered
custom class row. Call it after registration indexes are published, outside an
open content batch. The data loader performs capability registration after
`ContentRegistry.EndBatch` for this reason.

Guard is a guaranteed action targeting another living ally. It halves direct
incoming attack damage, rounding retained damage upward, until the guardian's
next turn or incapacitation. Damage over time is excluded. The designation
persists after protection expires. A focused attack that lands, including a fully
armor- or resistance-absorbed hit, heals the designated living ally for 8% of maximum HP, rounded down, once per
attack. Healing is capped by missing HP. An active Guard can prevent one lethal
direct hit per guardian per combat, leaving the ally at one HP. Native death
prevention takes precedence when the reduced hit remains lethal.

Multiple guards do not multiply reduction. Rescue selection uses stable ordinal
guardian identity and consumes one available charge. Combat state is transient.
Online peer agreement remains an explicit validation gate.

## Overworld ailment immunity

Framework 1.0.3 adds `Content.AddOverworldAilmentImmunity(classRow, displayName)` for an exact
registered custom class, or `overworldAilmentImmunity: { "displayName": "Cleansing March" }` on a JSON class
entry. It makes native `CharacterStats.HasImmunity` report Poison and Curse
immunity outside combat. This prevents new Poison and Curse from poison, curse
and chaos tiles. It also applies to other exploration sources of those two
conditions because a tile's curse can complete through a later RPC. Existing
conditions are not removed. Fire tile damage, chaos gold, Focus and item loss,
and combat ailments retain native behavior. The capability is independent of
equipment and of the Guardian kit, and opt-in identity follows the registered
class across a save. Native gameplay and co-op parity remain separate gates.

Registered Guardian classes show the three kit rules in native class-selection
ability text. Registered perk equipment appends its own bonuses to native item
stat text, or to weapon-front stat text even when no modifier row exists. Values
come from the same immutable bonus registration used by combat; the qualifier
lists registered Guardian class display names. Vanilla cards are unchanged.
Native weapon-back proficiency grids have no general perk text field and are
unchanged. Guardian class rules use six short lines. The class panel temporarily
top-aligns its ability label and moves the native equipment heading and list
together below the rendered text. Every native class refresh restores the
original positions and alignment first, including vanilla reuse. Unknown native
hierarchies are rejected without moving other labels. Text layout and fit still
require live validation.

`Content.SetGuardianEquipment(item, new GuardianEquipmentBonuses(...))` registers
Guardian-only bonuses on an exact custom equipment row. Constructor parameters
are `guardHealPercent`, `focusHealBonusPercent`, `retaliationDamage`,
`wardDebuffs`, `guardFocusRestore`, `guardReckoning` and `guardCleanse`.
The first three numeric values range from 0 to 20. Equipped bonuses choose the
strongest value in each field, rather than summing. Ward covers poison, stun,
daze and curse outcomes on guarded direct attacks. These bonuses never increase
Guard's 50% reduction. Other classes can use the equipment's ordinary stats.

### Legendary Guardian equipment

The following optional capabilities support the Paladin legendary equipment.
They remain encounter-local and require the Guardian class capability.

| Constructor/data field | Equipment | Behavior |
| --- | --- | --- |
| `guardFocusRestore: 1` | weapon | The first direct enemy hit actually reduced by each Guard restores one Focus to the guarded ally, capped at their normal maximum. Zero disables it; other values are rejected. |
| `guardReckoning: true` | weapon | The first reduced hit per Guard readies one charge for +50% damage on the next single-target blunt attack with that exact registered weapon. It is spent on the attempt, even on a miss, and expires at the end of the wielder's next turn. |
| `guardCleanse: true` | item or weapon | Guard removes one existing eligible condition from its ally, in priority order Stun, Daze, active Curse, Poison. |

Focus and Reckoning require retaining the weapon from the Guard action until the
trigger. Unequipping and re-equipping cannot restore the pending perk or charge.
Repeated Guard actions and overlapping guardians do not stack a charge; all
first-hit Focus opportunities triggered by the same protected outcome are spent,
but that outcome grants at most one Focus. A full-Focus ally still spends the
opportunity. Damage over time, dodges, and fully absorbed or unreduced damage do
not trigger these effects.

Reckoning multiplies the native damage multiplier before slot scaling, criticals,
armor, and resistance. It excludes item attacks, friendly or harmless actions,
authored area/splash actions, and Justice's secondary attack. FTK has no native
hammer subtype; author Reckoning on a registered blunt hammer weapon. Calculation
runs on the acting owner, and the existing native attack outcome carries the
result to the other peers. Their private charge state consumes the same attempt
receipt during playback. Charge feedback uses native HUD text. Eligible attack
previews show Reckoning and its increased damage. A crown effect and dedicated
sound are not implemented.

Cleanse removes one removable active curse in native enum order, preserving
permanent campaign curses. Poison is one stacked condition and all its stacks
are removed if selected. Native condition removal refreshes stats and effects;
it neither rewinds the combat timeline nor restores a lost action. Guard replay
cannot cleanse a second condition.

Focus uses the recipient owner's native synchronized update at impact. Guard
cleanse executes once per peer at the existing Guard application point, without
introducing another RPC. Fresh combat, resurrection reset, combat exit, and a
successful flee discard charges. These authority paths are source-backed;
multiplayer agreement and native end-to-end behavior still require live evidence.
Game-free coverage includes duplicate delivery, overlap, first-hit consumption,
miss consumption, charge expiry, equipment swaps, all cleanse-priority combinations,
capability validation, and installed-assembly hook signatures.

## Equipment stats and models

`Content.SetItemModifiers(modGuid, item, configure)` registers a private modifier
row under the item's allocated identity. Native equipment lookup requires this
identity match. Native item detail cards also resolve the item string through
`FTK_characterModifier.GetEnum`; the framework maps only registered modifier
names there to the same integer, preserving vanilla enum lookup for other names.
It begins with empty skills and modifiers, rather than inheriting
the template's defense bonuses. Repeated registration returns the first row.

`Content.SetItemMeshesFromGlb(item, ItemRendererMesh[])` replaces exact rigid
renderer paths on newly instantiated equipped weapons, shields and helmets.
`.` names the component root. Include break fragments when the native weapon has
them. Native prefabs, hit targets and animation structure remain intact.

`Content.SetItemDisplayMeshesFromGlb(item, ItemRendererMesh[])` separately replaces
rigid renderers relative to the native loot-display prefab root. Shops and large
inventory cards render these objects through the native offscreen camera; `icon`
only replaces small UI sprites. Loot wrappers can have different paths and local
transforms from equipped roots. Neither mapping falls back to the other. Apparel
items need rigid `displayModels` in addition to their skinned `apparelModels`.
Reuse authored geometry only after verifying its renderer-local coordinates.

`Content.SetItemApparelMeshesFromGlb(item, femaleBinding, maleBinding,
PlayerApparelMesh[])` supports body and boot items on any wearer. Binding skinsets
supply the native skeleton and garment assembly; custom meshes supply the visual
geometry. Each declaration names an exact renderer path and expected native mesh.
Absent alternate-sex or hidden garment paths are allowed. Equipped apparel
overrides class default apparel, but cannot replace required body renderers.
See [player model authoring](MODEL-PLAYER-API.md) for rig constraints.

### Helmet hair visibility (unreleased)

`Content.SetHelmetHairVisibility(item, top, bottom)` optionally controls native
hair sections on fresh instances of a registered custom helmet. A JSON `item`
entry can declare:

```json
{
  "helmetHairVisibility": {
    "top": false,
    "bottom": true
  }
}
```

Both values must be JSON booleans when the object is present. Omission or `null`
preserves the template's behavior. Hide a section only when the authored helmet
covers it; open crowns may require visible hair. This affects hair visibility,
not the head mesh, face, helmet proportions or attachment transform.

The supported template route creates a helmet through native `FTKHub.CreateHelmet`.
Templates that instantiate `m_WearablePrefab` directly bypass that route and are
rejected; choose a supported helmet template rather than assuming the setting
applies to every wearable prefab. The C# API returns `false` for unsupported,
vanilla or detached rows; an invalid JSON registration is reported as an error.
Only fresh helmet instances receive the policy; existing instances and native
prefabs remain unchanged. Verify front, side and rear views on each supported
appearance before accepting the fit. No released framework minimum is assigned
to this API yet.

### Authored metallic and smoothness masks (unreleased)

Each supported renderer declaration can optionally set `metallicGlossTexture` to an
`assets/*.png` package path. This replaces the inherited metallic/gloss map only
on the private material for that assignment. Omission or `null` preserves the
existing material behavior. Equipped, offhand, and display declarations are
independent; set the mask on each route that requires it. JSON supports the field
in `itemModels`, `offHandModels`, `displayModels`, `apparelModels.renderers`,
`playerModels` body/apparel/backpack arrays, and `raceBindings` body/apparel arrays.

```json
{
  "path": ".",
  "model": "assets/helmet.glb",
  "texture": "assets/helmet.png",
  "metallicGlossTexture": "assets/helmet-metallic-gloss.png"
}
```

Export an 8-bit RGBA PNG with metallic in red and smoothness in alpha, both in
`0..255`; use zero for green and blue, which the metallic/smoothness contract
does not read. Alpha is smoothness, not transparency or roughness: zero is rough
and 255 is fully smooth. The framework loads this data as linear,
without mipmaps, with a maximum dimension of 4096 and a 16 MiB file limit.
An RGB PNG is rejected because it loses authored smoothness. Launcher admission
checks the referenced mask signature and RGBA8 IHDR with the same size bounds;
the runtime additionally validates the complete PNG structure and chunk checksums.
Use the same UV layout as the model. The mask enables `_METALLICGLOSSMAP`, sets `_GlossMapScale`
to `1` and `_SmoothnessTextureChannel` to `0`, and disables
`_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A`. The inherited shader must support
`_MetallicGlossMap`, `_GlossMapScale`, and `_SmoothnessTextureChannel`; otherwise
the renderer transaction is rejected before committing any replacements.
The shader and unrelated material properties remain inherited.

C# authors can use the additional immutable constructor argument:
`new ItemRendererMesh(path, glb, albedo, metallicGlossTexture)` or
`new PlayerApparelMesh(path, nativeMesh, glb, albedo, metallicGlossTexture)`.
`PlayerRendererMesh` accepts the same fourth argument as `ItemRendererMesh`.
The explicit enemy renderer API also accepts a mask on its single-material
skinned and rigid assignments; see the [renderer contract](MODEL-RENDERER-API.md#authored-material-masks-unreleased).
Existing constructor signatures remain available. These are model-directory
paths for C# authors; JSON package paths are resolved and verified by the loader.

The transaction owns the new texture alongside the mesh and material, including
rollback and cloned-avatar lifetime. Item hot reload preflights the shader and
mask before publication, and package admission requires referenced mask files.
Offline checks establish parsing and resource safety only. Check equipped and
item-camera materials separately in the installed game before accepting an
asset; a mask does not certify its appearance or fix geometry or UV defects.
No released framework minimum is assigned to this option yet. A package using it
must select a compatible framework release before publication.

## Data package declarations

The content entry supports the following typed capabilities. The complete
[Paladin source package](../marketplace/packages/paladin/content.json) is a working
authoring example, subject to its documented live gates.

| Property | Entry kind | Meaning |
| --- | --- | --- |
| `guardian: true` | class | Guardian kit |
| `overworldAilmentImmunity` with `displayName` | class | Prevent new Poison and Curse outside combat; requires framework 1.0.3 |
| `guardianBonuses` | item, weapon | The equipment bonus fields above; Focus and Reckoning require a weapon |
| `modifiers` | item, weapon | `armor`, `resistance`, `reflect` integers 0-100; `vitality`, `speed` numbers -1 to 1 |
| `itemModels` | item, weapon | Equipped rigid renderers: `path`, `model`, `texture` |
| `offHandModels` | item, weapon | Separate rigid renderers for a native paired weapon's offhand root |
| `helmetHairVisibility` | helmet item | Unreleased: required boolean `top` and `bottom` when present; overrides native hair visibility on fresh equipped instances from `FTKHub.CreateHelmet`; direct `m_WearablePrefab` routes are rejected |
| `displayModels` | item, weapon | Loot/card rigid renderers, relative to the native display prefab root |
| `apparelModels` | item | `femaleBinding`, `maleBinding`, and `renderers` with `nativeMesh` in addition to model fields |
| `playerModels` | class | Array of `skinset`, required `body`, optional `apparel` and `backpack` renderer declarations |
| `icon` | item, weapon, proficiency, Guardian class | Original PNG; a Guardian class assigns its Guard action icon |

Stat fractions use native units: `speed: -0.02` means minus two Speed points.
Icons do not replace the class portrait. Class portraits use the native avatar
rendering path, which requires separate visual validation.

Models and textures must reside under the package's `assets/` directory and use
relative paths. The loader pins package identity and asset bytes, rejects path
escape and symlinks, and refuses changed bytes on later resolution. Models are
bounded GLB files with embedded buffers; external resources, animation payloads,
sparse accessors and morph targets are unsupported. Original PNG textures are
required. Marketplace validation also checks archive inventory and references.
Changes require a game restart. Offline validation does not establish visual fit,
animation quality, resource lifetime, shop acquisition or save compatibility.

### Returning to the title screen

The framework retains successfully registered row objects for the current process.
When native title recreation initializes fresh database components, it restores
those rows at their original array positions and rebuilds lookup indexes. This
preserves saved class indices and existing equipment/Guardian capability identity
without repeating content discovery, model loading, or author callbacks.

Restoration leaves newly created vanilla rows intact. A type, position collision,
or missing-prefix mismatch is rejected rather than replacing unrelated rows.
Checks are per database, not a rollback transaction across all databases. Offline
recreation checks cover these rules; live same-process resume must also confirm
that native UI and retained model/icon resources remain usable.
# Unreleased equipment-set extension

The current working candidate adds `Content.SetGuardianProfile`,
`Content.SetGuardianSmiteAction` and `Content.AddGuardianEquipmentSet`. These
are not claims about the latest published framework. The immutable profiles
bound physical, exact-action Smite and Guardian healing percentages and Guard
reduction. Other classes that only call `AddGuardian` retain the original defaults.

A set binds exact registered head, body, feet, one-handed, shield and two-handed
rows. Two matching armor slots select its minor profile; all three select its
core profile. Matching one-handed/shield or two-handed/empty-offhand adds its
completion bonuses. Accessories never count. Profiles include their limitations;
completion cannot restore another family's profile. Optional core proficiency
grants deduplicate existing actions. The optional Armor payoff identifies exact
registered negative-Armor proficiencies and applies only with complete armament.

Data classes use `guardianProfile`, `guardianSmiteAction` and
`guardianEquipmentSets`. Every profile declares `physicalPercent`, `smitePercent`,
`healingPercent`, `guardReductionPercent` and optional `bonuses`. Set entries name
`id`, `head`, `body`, `feet`, `oneHand`, `shield`, `twoHand`, `minor`, `core`,
`completion`, optional `coreProficiencies`, and optional `armorDamageBonus` with
`sources` and `multiplier`. References resolve after all base rows and ordinary
capabilities are registered. Invalid registration is reported; no native row is
used as a mutable set definition.

The candidate Paladin values and live gates are documented in
[Paladin equipment](paladin/EQUIPMENT.md). Successful registration and pure
evaluation tests do not establish damage, UI, lifecycle or multiplayer approval.

## Framework Equipment Exchange (1.7.0 candidate)

The framework supplies the universal Guild Token and native Back Alley vendor. Supporting enabled mods contribute offers; without a valid catalog, the vendor and token reward rule remain inactive. The token row remains registered so existing inventory references survive. Adding catalogs does not multiply the shared drop rule or campaign miss counter. Class filtering, owned-item hiding and confirmation-time duplicate checks apply to every catalog. Content changes require a restart when exchange progression is active.

For a class entry, omit `townExchange.token` and declare the existing `offers` list. A gear-only mod can instead declare top-level `townExchangeCatalogs` in its content file:

```json
{
  "entries": [
    { "kind": "item", "id": "guild_helm", "template": "helmetHeavy1", "displayName": "Guild Helm" }
  ],
  "townExchangeCatalogs": [
    {
      "ownerClass": "blacksmith",
      "offers": [
        { "item": "com.example.gear:guild_helm", "name": "Guild Helm", "family": "Guild", "slot": "Head" }
      ]
    }
  ]
}
```

The manifest in this example must declare `modGuid` as `com.example.gear`. Use the native class string ID for vanilla classes, or `modGuid:id` for a custom class. Qualified item references identify the contributing item without depending on globally ambiguous short names. Existing class-entry offer IDs can remain local to their owning mod. C# authors register their offers with `Content.RegisterTownExchange(offers)` after registering the class and items. There is no separate currency package or player enable switch for the framework service.

The reserved token identity remains `com.ftkmf.equipment-exchange:FTK_itemsDB/equipment_token` for legacy saves. A legacy Exchange declaration is recognized without registering another token or drop rule. This candidate's single-player behavior and migration require final native verification; the existing online purchase restriction remains in force.
