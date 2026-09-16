# Isolated production-spawn model test content

This separate BepInEx plugin registers original model test enemies once through
`Content.AddEnemy` and `Content.SetEnemyBodyMeshesFromGlb`. Native enemy stats,
weapons, animation controllers, and combat behavior are inherited unchanged.
It never writes original database rows. The runtime-test plugin stages these
registered IDs through the production spawn path; it owns game control and
isolated saves. This plugin does not reload meshes, stage enemies, or launch FTK.

## Build and input

Build the framework first, then this plugin against an isolated game copy:

```sh
dotnet build FTKModFramework/FTKModFramework.csproj -c Release
dotnet build tools/ai-model-pipeline/runtime-test-content/RuntimeModelTestContent.csproj \
  -c Release -p:TestGameRoot=/absolute/path/to/scratch/game-copy
```

`TestManagedDir` optionally overrides the managed DLL directory. Without it the
project detects `FTK.app/Contents/Resources/Data/Managed` or `FTK_Data/Managed`
under `TestGameRoot`. No installed-game directory is a default. Build output is
`bin/Release/net35/FtkRuntimeModelTestContent.dll`; game references are not copied.
Deploy only that DLL alongside the matching framework and runtime-test plugin
inside the isolated copy. Build does not deploy anything.

Place the profile document at `<game-copy>/model-test-profiles.json`. The schema
is `profiles.schema.json`; `profiles.example.json` contains Ashfang on `wolfA`
and Mirewarden on `trollCaveA`. Original GLBs and PNGs must already exist under
`<game-copy>/BepInEx/plugins/FTKModFramework_content/models/`.

Enable with both `FTK_MODEL_TEST=1` and `FTK_MODEL_TEST_ROOT` equal to the exact
absolute game-copy root. Its immediate parent must be named `scratch`; symlinks
in the root, profile file, or asset paths are refused. No other environment
variable changes which profiles load. The file is read before table startup;
changing it requires a new isolated process.

## Profile identity and validation

Each profile has a unique `key` starting `ftkmf_modeltest_`, exact native
`baseEnemy` enum name, `displayName`, 64-character lowercase hex `combatProfile`,
and explicit `renderers`. Each renderer gives a CEL-relative `rendererPath`,
GLB `glbFile` basename, and optional PNG `textureFile` basename or null.
`rendererKind` is optional and defaults to `SkinnedMeshRenderer`; use the exact
value `MeshRenderer` only for a rigid native target with one `MeshFilter`. Use `.`
only for a renderer on the CEL root itself. No descendant-name guessing occurs.

`combatProfile` is the inventory fingerprint for a single renderer/controller
combination. For multipart enemies derive and document a stable SHA-256 of the
complete ordered renderer/controller fingerprints. The plugin rejects duplicate
profile keys and duplicate renderer paths within each profile. Different original
assets or calibration probes may share a combat fingerprint in the same run;
each needs its own stable profile key. The plugin treats fingerprints
as caller-provided identity metadata; runtime pose evidence must establish that
the profile actually matches its claim.

Limits: 1 MiB JSON, 1 to 512 profiles, 1 to 16 renderer assignments per profile,
64 MiB per asset. Unknown fields, unknown native enum names, missing files,
non-basename assets, path traversal, and existing custom row IDs fail closed.
The entire input is validated before registration; table-dependent checks run
after framework initialization. All profiles need complete assets, including
the examples, before the batch can register.

A `MeshRenderer` assignment maps to `EnemyRendererMesh.ForStaticRenderer`. The
native target must resolve to exactly one `MeshRenderer`, one `MeshFilter`, one
native mesh and one usable native material slot. Its GLB is unskinned and uses
the selected MeshFilter local coordinates. `materialSlots` is not valid for a
rigid assignment. The Core transaction validates every profile assignment before
it changes any renderer, so a bad rigid path or asset leaves paired skinned
assignments unchanged.

The verified native CEL-relative paths are `wolf01` for `wolfA` and `enTroll01`
for `trollCaveA`, from the local reproducible inventory and decompile analyst.
These metadata facts do not establish custom asset quality or successful live
injection. Example Ashfang model production/spawn validation remains pending.
Mirewarden evidence applies only to its documented build and observed motions.

## Hook and results

A separate-plugin Harmony postfix runs after `com.ftkmf.framework` on the
verified zero-argument `TableManager.Initialize`. BepInEx declares the framework
dependency. The handler attempts registration once, even if initialization is
called repeatedly. The explicit standalone test-plugin scope is the reason for
this additional callback; no framework registration hook is modified.

Each clone is namespaced under `com.ftkmf.model-test-content` for deterministic
integer allocation. The exact string database ID remains its local `key`, for
example `ftkmf_modeltest_ashfang`. Pass that exact key to runtime-test
`stage-enemy` in its `enemy` argument, never the vanilla `baseEnemy`.

`model-test-registration.json` records requested count, successfully registered
keys, resolved integer IDs, base enemies, combat fingerprints, and any error.
A `SELF-TEST PASS [model-test-content]` verifies row identity roundtrip and visual
registration only. Status is explicitly `registered_spawn_validation_pending`.
If registration throws after some rows were cloned, the report records partial
progress and the plugin never retries in that process. Rebuild the clean test
session after diagnosing the error; there is no unsafe database rollback.

For each staged clone, separately verify production mesh-swap logs, renderer
selection, private material ownership, texture visibility, vanilla-fallback
absence, native idle/attack/hit/death playback, and combat screenshots. A
registration pass does not count toward successful runtime rig coverage.

## Generate the complete native-enemy calibration batch

The standard-library Python generator joins the verified mapping and probe
manifest by exact rig fingerprint. It emits one stable profile per native enemy
row, including every renderer on multipart enemies, plus explicitly supplied
original artwork profiles. Calibration geometry remains labeled separately from
finished creature artwork in the provenance manifest.

```sh
python3 tools/ai-model-pipeline/prepare_runtime_profiles.py \
  --mapping scratch/enemy-rig-mapping-reproducible.json \
  --probes scratch/rig-probes/probes.json \
  --probe-root scratch/rig-probes \
  --models-dir scratch/runtime-profile-batch/assets \
  --output scratch/runtime-profile-batch/model-test-profiles.json \
  --manifest scratch/runtime-profile-batch/provenance.json \
  --examples tools/ai-model-pipeline/runtime-test-content/profiles.example.json \
  --example-assets art-experiments/ashfang-wolf \
  --example-assets art-experiments/mirewarden-ftk
```

All output paths must be under `scratch`. Existing output documents are refused;
existing assets are accepted only if their hashes match. The tool verifies the
source game asset hash and each probe GLB against its manifest. It copies only
original probe/artwork GLBs and PNGs, never extracted native meshes or DLLs.
Output is a staging batch, with no game launch or plugin deployment.

CEL paths reverse the verified renderer ancestry and trim the terminal prefab
root. The complete root identity is checked using the CEL component path ID;
ambiguous duplicate target paths fail. Repeated object names are preserved,
including the gladiator's identically named root and direct child.

For multipart profile identity, hash the UTF-8 prefix
`ftk-multipart-combat-v1\n` followed by compact JSON of the sorted
`[rendererPath, combatProfile]` pairs. Single-renderer cases retain the inventory
combat fingerprint. Multiple test keys may share either fingerprint.

The current verified batch contains 363 calibration cases plus two artwork
examples, covering 162 exact native rig profiles and 222 renderer/controller
combinations. These are prepared cases, not completed live tests. The provenance
manifest records all input hashes, native row IDs, CEL/prefab/weapon path IDs,
controllers, renderer mappings, original asset hashes, and pending status.

## Optional player calibration profiles

`model-test-player-profiles.json` is an additional optional file under the same
isolated game root. Its absence preserves enemy-only behavior. The ordinary
enemy profile document is still required. Player parse failures are reported
separately and do not prevent valid enemy profiles from registering.
Each startup refreshes the player registration report before examining the
optional input, writing `absent` if missing or `refused` if rejected. An older
successful report therefore cannot represent an absent profile in the current run.

Each entry has `key`, `baseClass`, `displayName`, `skinset`, `defaultSkinType`, and
the same renderer assignment shape as enemies. See `player-profiles.schema.json`
and `player-profiles.example.json`. Player keys must start
`ftkmf_modeltest_player_`; 1 to 128 profiles are accepted. All native enum names
are exact and case-sensitive. The verified initial class is `blacksmith` and
skinset is `blacksmith_Female`, with `defaultSkinType` set to `Female`.

Optional `startingArmor` supplies one exact native body-armor key for an
equipment-rebuild fixture. The example uses `armorCloth1`, verified as native
item 59: equippable armor, equip slot, uncursed, no DLC requirement, with female
`armorGambesonF` and male `armorGambesonM` wearables. Before any player class in
the batch is registered, the plugin checks exact enum/key resolution, armor
object type, the computed `m_Equippable` property, non-cursed status, and usable
Armor components on the native wearable prefab and any supplied male variant.
An invalid item rejects the entire player batch. Enemy registration remains
independent, as it does for other invalid optional player input.

The selected ID is appended exactly once to a newly allocated custom-class
`m_StartItems` array, preserving inherited entries and their order. Native class
arrays and item rows are untouched. With the field absent, starting items are
unchanged. The ordinary new-run inventory initialization receives the item;
there is no debug grant or direct backpack mutation. Reports record the key and
`startingArmorAppendedCount`. This adds an owned item, not an automatically
equipped one, and native equip/unequip still requires its live Ready/state gates.

The plugin uses `Content.AddClass`, supplying a new seven-element `m_Skinsets`
array whose every slot points at the chosen skinset. Verified native slot order
is Female=0, Male=1, Undead=2, Cat=3, Demon=4, Fish=5, Goblin=6, with None=-1.
Both native avatar callers can preserve an explicit selected slot, so a
one-element fixture would be unsafe. `m_DefaultSkinType` uses the requested
explicit value. Stats, starting weapons, and original rows are unchanged.
Selecting Male/Female can still change native clothing gender; this fixture
does not normalize equipment or prove compatibility with every clothing choice.

`Content.SetClassBodyMeshesFromGlb` registers the assignments for that custom
class and skinset. Its production hooks target assembled preview and overworld
avatars; the native combat avatar clones that result. The separate
`model-test-player-registration.json` has the same run ID as the enemy report,
reports the seven-slot fixture choice, and labels successful registration
`registered_avatar_validation_pending`. Use its exact custom class key with
`start_run`. Registration does not certify successful custom rendering.

Prepare the initial original Blacksmith calibration probes:

```sh
python3 tools/ai-model-pipeline/runtime-test-content/prepare_player_profiles.py \
  --classification scratch/rig-candidate-classification.json \
  --probes scratch/rig-probes/probes.json --probe-root scratch/rig-probes \
  --output-dir scratch/player-profile-batch \
  --base-class blacksmith --default-skin-type Female --skinset blacksmith_Female
```

For the explicit armor fixture, use a new output directory and append
`--starting-armor armorCloth1`:

```sh
python3 tools/ai-model-pipeline/runtime-test-content/prepare_player_profiles.py \
  --classification scratch/rig-candidate-classification.json \
  --probes scratch/rig-probes/probes.json --probe-root scratch/rig-probes \
  --output-dir scratch/player-armor-profile-batch \
  --base-class blacksmith --default-skin-type Female --skinset blacksmith_Female \
  --starting-armor armorCloth1
```

The offline preparer validates the field's syntax and records the fixture in
provenance. Actual native item/type checks occur in plugin preflight, before
player registration. Start a fresh owned process/run to receive changed starting
items; an existing save or Ready transition does not re-seed inventory. Then
verify ownership, native equip, unequip, original equipment restoration, avatar
mesh/material rebuilds, and cleanup separately. A successful build or appended
item self-test does not establish successful equipment rebuilds.

Output contains the optional profile document, `assets/`, and provenance with
source/asset hashes and exact avatar/armor/renderer path IDs. Original probes
join by rig fingerprint. The body target is `playerBlacksmith` with 24 bones;
`hairTop` and `hairBottom` use the same six-bone original probe. These paths are
verified on the native avatar prefab; clothing assembly and active renderer
visibility still need live inspection. Hair or body probes may be obscured by
native apparel, which must not be mistaken for a missing registration.

Repeat `--skinset` for several explicit choices or use `--all-skinsets` to
prepare the full metadata catalog. This keeps the supplied base class and
starting weapon for every test fixture, so it is not all-controller coverage.
Existing output directories are refused. The generator copies no game meshes,
starts no game, and makes no runtime compatibility claims.

Before a player case passes, check preview/overworld assembly, combat clone
meshes and materials, actual equipped controller motions, clothing rebuilds,
and both avatar destruction orders. See `docs/MODEL-PLAYER-API.md` for the
resource-ownership validation gates.

## Measured live setup and current registration evidence

Use the [runtime helper guide](../runtime-test/README.md) for command transport
and operation details. The successful sequence on the isolated game copy was:

1. After `start_run`, wait for a nonempty party whose members are alive.
   A true `inSession` flag alone is too early.
2. Call `quiet-tutorials`, then `fortify-party` with `targetMaxHp:999`.
   Dismiss introductory dialogs and verify that modal state remains false.
3. Call `enter_dungeon`; immediately after success submit `stage-enemy` with
   `{"enemy":"EXACT_CUSTOM_ROW_KEY","level":0,"room":1,"regenerate":true}`.
   Select the registered custom key from the current registration report.
4. Stop if either request is rejected. Do not wait for dungeon dialogs before
   staging and do not try to recover a rejection with `dungeon_encounter`.
   After successful staging, native flow starts the correct single-enemy
   encounter automatically; this measured sequence needs no additional
   `dungeon_encounter` request.
5. Inspect production mesh logs and the live renderer/party state before
   recording combat evidence. Capture game time and animator progress as well
   as screenshots so a paused scene cannot count as a motion pass.

Waiting for dungeon dialogs between entry and staging raced native encounter
startup. This ordering is part of the measured fixture and should be preserved.
Release quiet tutorials before testing campaign completion. Fortification is
explicit test setup and makes no claim about ordinary game balance.

The content plugin with SHA-256
`d32d4035b8d8f211a6748444a3c1647a6d4109b665868c44922e14a6882d9c35`
includes the corrected player-report freshness behavior. Current live logs
reported registration PASS for one custom Blacksmith class and 366 enemy test
rows. The original generated batch contained 365 enemy profiles; the live count
includes the additional test case supplied for that run. These are registration
results, not successful model coverage. Native custom-player avatar spawning,
appearance, motion, and clone ownership checks remain pending.

## Optional native resource prefab fixtures

Enemy profiles may supply `resourcePrefab`, an exact native Resources basename
matching `^[A-Za-z0-9_-]{1,120}$`. Omit it to retain the existing native base enemy
prefab. Before registering any enemy in the batch, the plugin loads each explicit
GameObject asset and requires an unparented, non-scene prefab with exactly one CEL
on its root, a root Animator, and every assigned skinned renderer at its exact
CEL-relative path. Only the new `Content.AddEnemy` clone receives that CEL in
`m_EnemyAsset`; its base weapon/controller is preserved. Native rows and prefabs
are not modified. Resource profiles add their exact path, prefab/CEL instance IDs
and prefab name to the registration report; ordinary profiles retain the old
report shape.

Animator presence and the offline controller path comparison do not prove live
compatibility. Run the catalog decoder against the resolved resource CEL before
staging combat, then review production spawn, animation and visuals separately.
The current nine candidates exclude the Kraken head variant with additional
missing controller paths. The generator selects one deterministic resource path
per exact orphan rig group and records unselected alternatives, without claiming
those alternatives have equivalent gameplay.

```sh
python3 tools/ai-model-pipeline/prepare_resource_profiles.py \
  --preflight scratch/resource-enemy-base-preflight.json \
  --resources scratch/unresolved-resource-paths.json \
  --inventory scratch/rig-candidate-classification.json \
  --probes scratch/rig-probes/probes.json --probe-root scratch/rig-probes \
  --models-dir scratch/resource-profile-batch/assets \
  --output scratch/resource-profile-batch/model-test-profiles.json \
  --manifest scratch/resource-profile-batch/provenance.json
```

This stages nine candidate profiles and ten original probe/palette assets without
deploying or running a game. Use a new scratch output directory for another run.
Existing profile/manifest outputs are refused; existing assets must be byte
identical. The output is a standalone profile list. To combine it with an existing
catalog, append the nine profile objects explicitly to a reviewed copy, require
unique keys and the512-profile bound, and preserve each pre-existing profile
object unchanged. Reloading a modified list into an already initialized helper
session is refused by the existing loaded-profile equality checks.

## Optional enemy health fixture

`minimumBaseHealth` is an optional integer1..1000, rejected before any enemy
clones if malformed or out of range. Registration applies
`clone.m_HealthTotal = Math.Max(clone.m_HealthTotal, minimumBaseHealth)` only to
the custom test row. Omission preserves existing behavior, and a higher native
base value is never reduced. This is an explicit recording fixture, not balance
advice or a live health setter. Native enemy scaling, dungeon modifiers,
difficulty and chaos can affect actual combat health; read current HP before an
attack rather than assuming that a requested floor guarantees a nonlethal hit.

For profiles using the fixture, the registration report separates
`minimumBaseHealth`, `nativeTemplateBaseHealth`, and `actualBaseHealth`.
`computedHealth` is null with status `requires_native_spawn_context` because
native `GetHealthTotal()` needs a current hero/dungeon/difficulty context.
The action recorder's actual combat state is the evidence for resulting HP.

The staged375-profile example `scratch/runtime-profile-375-health64-v1` adds
only `minimumBaseHealth:64` to each original375 profile and records the source
and output hashes in `provenance.json`. The active catalog and model assets
were not modified. Use this list only in a fresh initialized test session;
loaded-profile equality checks continue to reject mid-session catalog edits.

## Optional public visual scale factor

`visualScale` is an optional finite number from0.1 through4. Startup validates
all profiles before registering any clones; strings, booleans, null, NaN,
infinities and out-of-range numbers are refused. Omission preserves the existing
registration behavior. For a supplied value, registration calls
`Content.SetEnemyVisual(row, Color.white, factor)` before
`Content.SetEnemyBodyMeshesFromGlb`. The mesh registry merges only renderer
assignments into that visual, retaining the scale/tint fields. Reversing this
order would replace the visual entry and lose its mesh assignments.

The factor is applied by the public API to a spawned clone, not to native
prefabs or database rows. Registration reports `visualScale`, the resolved
`nativePrefabRootLocalScale`, and a status stating that actual native spawn
measurement is pending. A factor of1 means unchanged native scale. The separate
Core correction to enforce this factor contract must be deployed and measured
before claiming actual fitted dimensions; this content plugin cannot verify it
at registration.

## Optional native fall-off policy

`fallOffPolicy` may be exactly `"preserve-custom-body"` on an explicit plural
renderer profile. Omission preserves native `FallOffLimb` behavior. The parser
rejects every other value and rejects the field on `bindingKind:
"legacy-singular"` because that route has no exact renderer ownership contract.

After successful plural renderer registration, the content plugin calls
`Content.SetEnemyFallOffPolicy(row, EnemyFallOffPolicy.PreserveCustomBody)`.
Keep that order when authoring through the public API: register visual settings
first, explicit meshes second, and the fall-off policy last. A later full
`SetEnemyVisual` replaces the stored visual record and restores the default
native policy.
The framework applies it only to a spawned CEL whose completed explicit lease
owns the exact native fall-off renderer. Registration records the requested
string; it does not establish that a death animation reached the event, that the
body remained visible, or that combat cleanup completed. Capture a normal native
death and an unopted native control separately.

`scratch/runtime-profile-378-fitted-v1` preserves all377 prior profiles and adds
`ftkmf_modeltest_resource_enbaseycockatriceboss_fitted`, base `cockatriceC`,
resource `enbaseycockatriceboss`, exact renderer `enBaseyCockatrice`, unchanged
original probe121693, health floor64 and visual factor0.55. Its `combatProfile`
is taken from the exact current native `cockatriceC` profile, not copied from
the boss baseline. The provenance records that derivation and the resource
controller identity separately. The existing boss-key baseline remains intact.
The378 catalog is staged only: helper schema/metadata support for `visualScale`
and the corrected Core scale-factor behavior require coordinated deployment and
live checks. This feature does not change model geometry, native rigs or active
session content.

### Conditional player apparel fixture

Optional `apparel` contains0..16 assignments with `rendererPath`, exact
`expectedNativeMeshName`, `glbFile`, and optional `textureFile`. Paths must be
unique across mandatory renderers and apparel. Native names must be nonblank,
at most160 characters, and contain no control characters. The entire input and
all referenced files are checked before any player class is cloned. Registration
reports preserve the exact required and optional arrays for recorder provenance.

The mandatory body array remains required. When apparel is specified the plugin
calls the public four-argument `Content.SetClassBodyMeshesFromGlb` overload.
An absent apparel path is conditional and retains native/unstyled behavior;
a present path with the wrong native mesh rejects the whole avatar replacement.
Startup registration alone cannot validate assembled apparel or animation.

Prepare the verified Blacksmith Female fixture without changing96 existing
profiles (all paths below are explicit local inputs and outputs):

```sh
python3 tools/ai-model-pipeline/prepare_player_apparel_fixture.py \
  --catalog scratch/mirewarden-game/model-test-player-profiles.json \
  --existing-models scratch/mirewarden-game/BepInEx/plugins/FTKModFramework_content/models \
  --probes scratch/additional-rig-probes-v1/probes.json \
  --probe-root scratch/additional-rig-probes-v1 \
  --models-dir scratch/player-apparel-97-v1/models \
  --output scratch/player-apparel-97-v1/model-test-player-profiles.json \
  --manifest scratch/player-apparel-97-v1/manifest.json
```

This adds `ftkmf_modeltest_player_blacksmith_female_apparel`, preserving the three
mandatory Blacksmith renderer assignments. Original weighted marker probes
(radius6, no connections) cover default `armorBlacksmithF(Clone)` with native
mesh `armorBlacksmith`, `bootsBlacksmith(Clone)` with mesh `bootsBlacksmith`, and
conditional item59 `armorGambesonF(Clone)` with mesh `armorGambesonF`.
The fixture omits explicit `startingArmor`; inherited native starting items are
unchanged. The Gambeson branch requires naturally owned/equipped item59 before
its equipment-cycle test. No grant, launch or deployment is performed. Existing
outputs are refused; existing asset files are accepted only when identical.
These are calibration fixtures, with apparel runtime and art acceptance pending.

For a repeatable full equipment cycle, add `--starting-armor armorCloth1` and use
new `scratch/player-apparel-97-v2` output paths in that command. Native start
items auto-equip item59, allowing the first Gambeson check, an owned-item native
unequip to exercise default apparel, and native re-equip to restore Gambeson.
This explicit starter fixture avoids a runtime grant or reliance on random loot.
The default-omitted v1 catalog remains preserved; neither catalog implies that
its new conditional apparel has passed live validation.

The v2 Blacksmith fixture subsequently passed its one native item59 equipment
cycle: both avatars retain five custom SMRs through Gambeson/default/Gambeson,
and old leases4/6 each have15 owned resources observed Unity-null and absent.
New lease8 settles at refs2. The97-profile base-prefab decoder passes separately;
its three absent conditional paths are not decoded there. This supersedes the
initial fixture's pending status only for this measured outfit and equipment
cycle. Rigid accessories and final player-art acceptance remain outside scope.

## Optional native material-slot assignments

Enemy renderer objects may use `materialSlots` instead of legacy `textureFile`/`disableNativeEmission`:

```json
{
  "rendererPath": "enJellyCube",
  "glbFile": "cubea_material_layers_v1.glb",
  "materialSlots": [
    {"primitiveIndex": 0, "nativeMaterialSlot": 0, "textureFile": "cubea_material_layers_v1.slot0.png", "disableNativeEmission": false},
    {"primitiveIndex": 1, "nativeMaterialSlot": 1, "textureFile": "cubea_material_layers_v1.slot1.png", "disableNativeEmission": false}
  ]
}
```

`MaterialSlotFixture.Read` is shared with helper preflight. It requires2–4 explicit integer bijections, rejects unknown descriptor fields and mixed legacy options even if null, and bounds PNG basenames. Missing per-slot texture preserves the native texture; explicit null is rejected in this new mode. Missing emission means false. Content preflight checks each PNG through the existing local-file, symlink and size rules, then uses the public immutable `WithNativeMaterialSlots` factory. Runtime Core still checks actual native slot counts, skin and submesh structure atomically.

The new content candidate requires the matching multi-slot Core API even for a catalog of legacy rows, because registration references its new types. Keep the older content DLL with older live Core; do not replace only one side. Build candidates into a separate output directory with `-o /absolute/scratch/candidate/content` to preserve a frozen standard build. The [portable cube fixture](../multi-primitive-tests/README.md) is original calibration geometry, not a finished creature or a live PASS.

The opt-in [singular GLB and tint lifetime fixture](LEGACY-SINGULAR.md) uses a separate profile and actual legacy public API branch. Existing profiles retain their plural route.
