# Guardian classes and original equipment

These APIs and data declarations are introduced for framework 0.1.4. They are
implemented on the development branch and have not yet passed the Paladin live
acceptance gates. See [the Paladin design](paladin/DESIGN.md) and
[implementation plan](paladin/IMPLEMENTATION.md).

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
are `guardHealPercent`, `focusHealBonusPercent`, `retaliationDamage` and
`wardDebuffs`. Numeric values range from 0 to 20. Equipped bonuses choose the
strongest value in each field, rather than summing. Ward covers poison, stun,
daze and curse outcomes on guarded direct attacks. These bonuses never increase
Guard's 50% reduction. Other classes can use the equipment's ordinary stats.

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

## Data package declarations

The content entry supports the following typed capabilities. The complete
[Paladin source package](../marketplace/packages/paladin/content.json) is a working
authoring example, subject to its documented live gates.

| Property | Entry kind | Meaning |
| --- | --- | --- |
| `guardian: true` | class | Guardian kit |
| `guardianBonuses` | item, weapon | The four equipment bonus fields above |
| `modifiers` | item, weapon | `armor`, `resistance`, `reflect` integers 0-100; `vitality`, `speed` numbers -1 to 1 |
| `itemModels` | item, weapon | Equipped rigid renderers: `path`, `model`, `texture` |
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
