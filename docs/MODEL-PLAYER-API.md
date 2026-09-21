# Custom class renderer assignments

Register player meshes on a class created through `Content.AddClass`, then select one of that class's valid skinsets:

```csharp
Content.SetClassBodyMeshesFromGlb(customClass, customClass.m_Skinsets[0],
    new PlayerRendererMesh("Body/BodyMesh", "my_class.glb", "my_class.png"));
```

The path and skinset slot above are illustrative. Inspect the actual assembled avatar for each skinset. Paths are exact, case-sensitive and relative to the returned `CharacterEventListener` root; `"."` targets the root. Registration requires both the custom class ID recorded by `ContentRegistry` and the exact row object present in the class database. Passing a vanilla row, an unregistered clone, or a different row with the same ID is rejected. The selected skinset must exist and appear in that custom class's `m_Skinsets` array.

Registrations are keyed by class ID and resolved skinset ID. A custom class may reuse a vanilla skinset without changing other classes that use it. No vanilla class row, skinset row or avatar prefab is mutated. Re-registering the same class/skinset replaces its assignment snapshot for future avatar creation.

The API uses the strict, all-or-nothing [renderer transaction](MODEL-RENDERER-API.md). Separate exact overload hooks apply it after native `CharacterEventListener.CreateAvatar(CharacterOverworld)` and `CreateAvatar(uiQuickPlayerCreate)` finish assembling clothing, equipment and cosmetic tints. Missing paths, incompatible bones/bind matrices, or missing requested textures leave the newly assembled native avatar intact. Equipment that changes renderer paths may therefore require another supported layout; this API does not hide, replace or normalize native equipment automatically.

For equipment variants, a separate overload accepts a nonempty required body array plus conditional apparel:

```csharp
Content.SetClassBodyMeshesFromGlb(customClass, skinset, requiredBodyMeshes,
    new[] {
        new PlayerApparelMesh("armorBlacksmithF(Clone)", "armorBlacksmith",
            "my_blacksmith_armor.glb", "my_blacksmith_armor.png"),
        new PlayerApparelMesh("armorGambesonF(Clone)", "armorGambesonF",
            "my_gambeson.glb", "my_gambeson.png")
    });
```

The example paths and native mesh names were observed on stitched female apparel; they are not portable to every class/skinset. `ExpectedNativeMeshName` matches `sharedMesh.name` exactly, not the renderer name. Each descriptor is immutable and registration copies both input arrays. Paths must be unique across required and conditional assignments. The existing `params PlayerRendererMesh[]` overload remains entirely required.

All required body/hair paths must still exist. A conditional path is skipped only when its transform is absent from the assembled CEL. A present path with an ambiguous transform, zero or multiple skinned renderers, a null mesh, or an unexpected native mesh name rejects the whole outfit before mutation. Present apparel and the required body are combined into one strict transaction, so bad GLB bones/bind matrices or a requested texture failure rejects all selected changes together. There is one resource lease for the combined set, not a second apparel transaction after body replacement. Reapplying to an already dressed avatar retains its successful lease before checking native names that have already been replaced. Re-registration affects future avatar creation, not existing dressed avatars.

Absent variants produce an explicit skipped-assignment diagnostic. An absent alternative alone does not indicate an unstyled outfit: equipping one variant normally removes another. Only inspecting actual active renderers that were not selected for custom replacement establishes native fallback. Such native equipment stays in place and may be unstyled; a conditional registration does not establish a fully custom outfit. Runtime support still requires default outfit, equipped and unequipped rebuilds, deliberate native-identity mismatch rejection, and lease cleanup checks on the new framework version.

`CharacterDummy.CreateAvatar(bool)` clones the completed overworld avatar and selects the weapon's native combat controller. Custom meshes and materials are shared across that native clone operation. The resource owner serializes a lease ID and renderer references so Unity remaps renderer references into the clone. A process-local reference count retains the custom assets until the last acquired avatar owner is destroyed. `Awake` retains active cloned leases. A narrow transpiler also retains the complete clone hierarchy immediately after the native `Instantiate` result is assigned to `m_EventListener`, before `SetVisible` and later initialization. This covers inactive body and equipment owners whose `Awake` has not run, including when later native initialization throws. Retention is idempotent and never replaces the native exception. The insertion requires the exact unique native clone/assignment pattern; it does not wrap combat creation in a Harmony finalizer. Unity 2017 may omit `OnDestroy` on a clone that was never active. The active framework plugin therefore prunes destroyed owner components each frame; normal release removes its owner record, so pruning cannot release it twice. This covers an explicitly retained inactive clone destroyed without activation. Missing leases are logged; the owner never adopts original game assets as a recovery shortcut.

This path preserves the native animation controller and the native animated renderer bounds. It does not retarget rigs, prove bounds for oversized models, or claim compatibility with all equipment and weapon controllers.

Before marking a class/skinset supported, verify:

- Character-creation preview and overworld assembly use the intended custom renderer set.
- Combat clones keep valid meshes, materials and their own renderer/bone references.
- Destroying the overworld avatar first leaves the combat clone valid, and destroying the combat clone first leaves the overworld avatar valid. Destroying the last owner releases custom resources exactly once.
- Inactive clone retention and a native creation exception preserve resource ownership and the original exception.
- Equipment/avatar rebuilds, the actual weapon controller, idle/attack/hit/death motion and culling behave correctly.

A registration log or successful build is not live player-model validation. Calibration probes test binding and motion; they are not finished player art.


## Observed Blacksmith calibration encounter

The first successful fresh-process custom-class run used
`ftkmf_modeltest_player_blacksmith_female` with `blacksmith_Female`.
[The runtime evidence index](model-runtime-validation.json) records the journal,
session, binary hashes, capture intervals and four avatar inventory snapshots.
This is one calibration case, not support for all 96 skinsets.

| Exact CEL-relative path | Observed custom GLB |
|---|---|
| `playerBlacksmith` | `probe_121067.glb` |
| `hairBottom` | `probe_121083.glb` |
| `hairTop` | `probe_121083.glb` |

All three assignments appeared in both overworld and native combat avatars.
Both avatars reported acquired lease ID 2 with two references. `hairTop` remained
inactive under native equipment, while overworld renderers were disabled during
combat. Their custom mesh identities were still present. At the later Ready
snapshot, the native combat avatar still existed and both owners still reported
two lease references. **This is retention evidence, not cleanup or final-release
proof.** The separate 106-assertion synthetic lifecycle test does not turn this
native observation into an equipment/rebuild/destruction test.

Two successful 120-frame fixed-step captures recorded the player's
`damageLight_blunt1H` response and `attack_blunt1H`, followed by victory and idle.
The ordinary player attack killed the wolf with 8 HP remaining. The earliest hit
and attack intervals were not captured. Two guarded native Collect votes were
submitted, with a fresh fixture snapshot between them; each returned `clicked`.
The next strict Ready snapshot succeeded at level 0, room 2 with an empty fight
order and encounter combat false. No loot item identity is inferred from the
returned `None` item field.

The fixture used a disposable single-player save, 999 hero max HP and quiet
tutorials. An earlier second-run attempt after returning to title failed map
generation before custom-class application; it contributes no model evidence.
**Visual status remains unaccepted:** the thin probe is calibration geometry,
not finished player art. One Blacksmith Female fixture now also verifies native
`armorCloth1` (item59) unequip/re-equip: Body1 to Backpack1 to Body1, both avatar
CELs rebuilt in each direction, all three custom meshes stayed bound, and native
armor returned. Lease IDs changed 3 to 5 to 6 and current references settled at2;
old lease disposal was not directly measured in that earlier run. A subsequent
watched fixture in session `432473c3a0f24b169472a2272e01e70c` directly observed
both old leases3 and5 absent from the lease dictionary and all nine resources
on each Unity-null after native unequip/re-equip. The new lease6 held two
references and both avatars retained all three custom meshes plus restored
native armor. The watch did not destroy, retain the production lease, or prune
resources. This verifies disposal of those watched resources only, not global
leak freedom or final-owner teardown. See
[the equipment rebuild evidence](model-runtime-validation.json).
Character-creation preview, player death, native owner destruction/release,
culling, other equipment/weapon states, art acceptance, and coverage of all96
skinsets remain pending.

## First guarded player-recorder case

`record_player_case` completed case `de74a2c43e09408da660987caa8497a5` in prior
session `4fac21c62c7c4624a648ec782d88c34d`, before the new scale/framework/helper
versions. The recorder joined actual hero-235442 to the equipment-owner combat
CEL and verified the registered Blacksmith Female profile with starting armor59.
It captured stable `probe_121067` identity for120 unpaused frames over 10.908325
seconds. `attack_blunt1H` sampled frames16..39 (normalized0.113..0.983), while
the enemy dodged and stayed at58HP. Player `damageLight_blunt1H` sampled76..86
and111..119; this is actual player motion evidence, not successful enemy damage.

Parent-reviewed0/30/60 showed armor/backpack concealing most custom body geometry.
The result verifies guarded identity and native motion recording, not exposed
original player art or new skinset coverage. An unequipped capture is still
needed. [The runtime index](model-runtime-validation.json) retains source hashes,
owner-join provenance and version boundaries; later deployments inherit no live
acceptance from this old-version case.

A second guarded player recording on corrected Core (`afffa948...`, session
`61d9e44ebed54190ae8b9e4e8b503d6f`) captured actual player damage and a successful
ordinary attack reducing Cinderbloom58 to50HP. Native item59 moved Body1 to0 and
Backpack0 to1 before capture, but default armor/boots still concealed the custom
body. Original player-art acceptance remains pending. The source apparel audit
identifies two additional stitched rig profiles outside the CEL-only230 baseline;
these are metadata findings, not validated custom apparel. Exact owner joins,
capture/equipment hashes and that coverage gap are in the runtime index.

The conditional Blacksmith Female fixture has now completed one native owned
item59 unequip/re-equip cycle in session5754790cd92e4557b28b6f728024815c,
frameworkb4554004/helperdd0da596. Both combat and overworld avatars retain five
custom SMRs: body, two hair parts, boots, and the applicable default armor or
Gambeson. Item59 moves Body to Backpack to Body. Leases4 to6 to8 rebuild both
avatars; final lease8 settles at two references. Watches observe all15 owned
resources from each old lease4 and6 Unity-null and absent from the lease table.
This verifies this fixture's rebuild and cleanup, not global leak freedom.
Rigid backpack, helmet, shield and hammer remain native and can obscure the
custom body; this does not establish finished original player art.

The97-profile player catalog also passes base-skinset runtime decode preflight.
All three conditional apparel paths are absent on that base prefab and are
explicitly not decoded by this preflight; assembled outfit evidence above is
separate. Hashed inputs and observations are recorded in
[the runtime evidence index](model-runtime-validation.json).

## Repeatable original-player workflow

An original player package must preserve one exact class and skinset contract.
Document each always-present body or hair renderer and each conditional apparel
renderer separately. Conditional records use the CEL-relative transform path and
exact native `sharedMesh.name`; similar names or a compatible bone count do not
establish an interchangeable layout. Every authored GLB retains the selected
palette, inverse bind matrices, and its own original geometry proof.

Stage player profiles with `--catalog-kind player`. The one transaction pins the
player catalog row, required body assets, conditional apparel assets, and player
registration report. For a profile with `startingArmor`, launch a fresh isolated
process and run so native initialization owns the item. Never use a helper to
grant or force the starting item.

Before each real equipment transfer, record `equipment-inventory`, reach strict
native Ready, and arm `lease-watch` on the actual overworld owner. Supply the
fresh hero ID, item enum, and exact Body and Backpack counts to one native
`unequip-body` or `equip-body` request. Reinspect both overworld and combat
avatars after the rebuild. Record the expected selected apparel path, retained
required body/hair paths, retained boots or other shared apparel, the absent
alternative path, new lease identity, and old-lease lifetime result. Submit the
inverse only after a fresh native inventory snapshot. Native cleanup remains an
observation: preserve a bounded `not-observed-disposed` result when it does not
settle instead of invoking cleanup manually.

Character-creation preview, overworld, and combat are separate owners. Inspect
all three and record a native pass, ordinary attack, and incoming damage capture
for the selected custom renderer. Review the frames for branch choice, fit,
accessories, clipping, culling, and material response. A correct combat mesh
identity does not establish preview assembly, equipment cycling, another
skinset, another weapon controller, player death, final owner teardown, or
artistic approval. The reusable
[FTK custom-model skill](../skills/ftk-custom-models/SKILL.md) contains the
full authoring, transaction, and archive sequence.

For a stage-ready `playerSkinset` route, first print the exact current input
plan from the queue and stopped isolated catalog:

```sh
python3 tools/ai-model-pipeline/runtime-test/plan_execution_queue_player_route.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/my-isolated-game \
  --topology-group EXACT_TOPOLOGY_GROUP
```

The plan pins the selected profile document, player catalog, required preview
renderers, conditional apparel, and all declared assets. It does not launch the
game, select a native class, or observe a preview. Use its exact profile and
skinset identity in the native Party Select sequence below.

When preserving the plan, use the exact content-addressed output path and
`planCommandText` from the current validation campaign. Its 12-hex plan ID is
derived from the complete expected plan, so input changes create a new immutable
file instead of overwriting or silently reusing an older plan.

For a character-creation preview, first reach the visible offline Create Game
screen through FTK's normal menu. Run `native-create-character-preflight` and
require `eligible: true`; then run `native-create-character-screen`. On the
actual Party Select screen, use `native-create-character-input-state` before
keyboard navigation and after every selection that matters. For repeatable
directional selection, use `native-party-class inspect` for the intended
player. It accepts only one visible `classArrowNext` and one visible
`classArrowPrevious`; the separate central `toggleClass` control is ineligible.
Submit the fresh token and direction for one native callback, then inspect
again. The token pins the owner, current class, arrow identity, visibility,
callback, and direction. Zero or multiple distinct explicit arrows fail closed.
Horizontal keyboard navigation can move focus across player cards and does not
establish a class change. Once the selected native default is the target profile, use
`player-preview-state` with the exact profile key, skinset, catalog hash, and
current preview-owner ID before capturing or reviewing the body.

## Wildbloom Herbalist canonical seven-bone route

[Wildbloom Herbalist](../art-experiments/wildbloom-herbalist/README.md) adds an
original `herbalist_Female` package for the player-only seven-bone `hairBottom`
topology. Its exact 24-bone body, six-bone crown, and seven-bone shoulder-braid
GLBs pass the binary and bind contract and reproduce from a binding-only source
proof. Its [V2 canonical evidence](../art-experiments/wildbloom-herbalist/live-validation-v2-canonical/README.md)
pins deployment, class ID 114, exact directional native class selection, all
three custom meshes, and six 24-frame preview captures across idle and explicit
`death_overworld` playback. The same meshes survive overworld, ordinary combat,
and a second native combat-avatar build. A 120-frame capture records
`attack_blunt1H`, enemy HP `72 to 62`, `damageLight_blunt1H`, and hero HP
`999 to 962`; native loot then reaches strict Ready and a second combat at
level 1, XP 38, gold 46.

No custom apparel is declared. Native Herbalist clothing remains game-owned.
Death is a native-state playback fixture, and the separate kill fixture only
opens progression. Final owner teardown, Unity-null resources, corpse lifetime,
portraits, multiplayer, every equipment combination, every camera, and final
art approval remain outside the claim. Verify the 38-file archive with
`python3 tools/ai-model-pipeline/verify_wildbloom_canonical_archive.py`.

## Hearthveil Blacksmith live record

[Hearthveil Blacksmith](../art-experiments/hearthveil-blacksmith/README.md) is
the first original six-mesh player package to complete the generic player
transaction and native apparel lifecycle on `blacksmith_Female`. Its V1 archive
pins class ID 113, the six original GLBs, the shared palette, the player catalog
hash, deployment receipt, and all raw output hashes. The profile selects body,
two hair parts, boots, and one of the default `armorBlacksmithF(Clone)` or
equipped `armorGambesonF(Clone)` branches.

In isolated session `eeadf75d450540558db47a6908ae9667`, native item59 moved
Body/Backpack `1/0 → 0/1 → 1/0`. Both overworld and combat owners rebuilt at
each transition, all expected custom meshes remained assigned, and the expected
armor branch swapped. The retired leases 4 and 6 were each absent and all 15
pinned assets Unity-null. A 120-frame native player attack observed enemy HP
`72 → 62`; a 120-frame native pass observed hero HP `970 → 913` and two sampled
`damageLight_blunt1H` responses. The post-pass fixture then reached normal Loot
and strict Ready at level 0, room 3 through two guarded native Collect votes.

Selected live frames show a coherent custom silhouette under native rigid
accessories, UI, and effects. The [V3 canonical route](../art-experiments/hearthveil-blacksmith/live-validation-v3-canonical/README.md)
retains V1's combat, equipment, lifecycle, and progression authority and adds a
fresh same-session native Party Select owner with exact body and six-bone lower
hair idle captures. It also preserves visible `death_overworld` deformation for
both renderers as explicit native-state playback fixtures. Those fixtures do
not establish ordinary player death, corpse lifetime, or cleanup. Run
`python3 tools/ai-model-pipeline/verify_hearthveil_canonical_archive.py` to
verify all 15 retained files, exact owner and renderer identities, animation
progress, selected-frame hashes, and the V1/V2 evidence pins. The result still
does not establish final art approval, all cameras/culling, ordinary player
death, final-owner teardown, other equipment, another controller, or another
player layout. The generated [validation
evidence ledger](MODEL-VALIDATION-GATES.md) retains the two supplements as
separate entries so the preview does not transfer coverage to a different
skinset or profile.

## Custom class backpacks

`Content.SetClassBackpackMeshesFromGlb(classRow, skinset, meshes)` registers
`PlayerRendererMesh` assignments for rigid renderers relative to the native
backpack root. Use `"."` for its root renderer. The exact registered custom class
row and one of its declared skinsets are required. Vanilla classes are unchanged.
In a data package, use an optional `backpack` array alongside `body` and `apparel`
in each `playerModels` entry, with the same `path`, `model`, and `texture` fields.

The native `CharacterEventListener.UpdateBackpack` creates and parents a fresh
backpack before the framework applies its transaction. Assignments preserve that
native transform, animation attachment, and detach behavior. A separate lease on
the backpack root owns its replacement mesh and material; avatar cloning retains
child leases, and replacing or destroying a backpack releases its own resources.
An absent native backpack is left absent. Invalid renderer assignments preserve
the native instance. This route does not change native prefab assets or inventory.

The Paladin reliquary backpack has offline registration, transaction, asset, and
package validation. Its native fit, motion, rebuild, clone, and death-detachment
coverage require explicit live observations; a successful build does not prove them.

### Authored equipment palette and native customization

For explicit player, apparel, backpack, and item/display model assignments, a
provided replacement texture preserves its authored main palette. A private
material inherited from a native `_main` slot gets a neutral color multiplier
and remains neutral when native avatar tinting runs again or clones the avatar.
Skin and hair slots still accept the native skin/hair customization colors.
Assignments without a replacement texture retain native main-color tinting.
Enemy renderer assignments keep their existing material behavior. No shared
native material is modified.
