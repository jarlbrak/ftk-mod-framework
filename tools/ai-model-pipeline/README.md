# FTK enemy mesh tools

This recreates the editor-free export path tracked in issue #98. The tools read a local game installation and write the exact format consumed by `Core/RuntimeGltfMeshLoader.cs`. They do not modify or launch the game.

## Setup and extraction

Run from the repository root with Python 3.11 or newer:

```sh
python3 -m venv scratch/model-venv
scratch/model-venv/bin/pip install -r tools/ai-model-pipeline/requirements.txt
scratch/model-venv/bin/python tools/ai-model-pipeline/extract_reference.py \
  --assets "$HOME/Library/Application Support/Steam/steamapps/common/For The King/FTK.app/Contents/Resources/Data/resources.assets" \
  --output scratch/model-reference
```

The default renderer path ID (`121152`) is the local game's `enTroll01` cave troll body. Path IDs are build-specific. An explicit `--renderer-id` selects another skinned renderer. The script writes `skeleton.json` (bone names, parent indices, rest matrices and positions) and `reference.npz` (geometry, weights and bind matrices). Store these only in gitignored local scratch. Extracted game content must never be committed or redistributed.

Resolve a current validation route into its exact extraction and Blender
scaffold commands with:

```sh
python3 tools/ai-model-pipeline/plan_model_authoring_kit.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/my-isolated-game \
  --topology-group EXACT_TOPOLOGY_GROUP \
  --route-kind directEnemy \
  --output scratch/my-authoring-kit.json
```

Pass `--all-routes` instead of the topology and route arguments for one complete
catalog. The planner verifies the current native asset hash, route ledgers,
renderer inventory, and Blender bridge audit before emitting commands. It keeps
the named topology as the primary target and records every other skinned
assignment required by the complete profile as a companion rig target with its
own exact bind variant. Rigid `MeshRenderer` assignments remain structural
profile data. Replace
`NEW_LABEL` in its workspace template and keep all extracted references under
ignored scratch storage. A local reference counts only when its renderer ID,
mesh name, ordered bone names, and joint count match the current inventory. The
catalog is authoring preparation, not live or art acceptance.

Verify the catalog's actual reference bytes with:

```sh
scratch/model-venv/bin/python \
  tools/ai-model-pipeline/verify_model_authoring_references.py \
  --catalog scratch/model-authoring-kit-catalog.json \
  --output scratch/model-authoring-reference-verification.json
```

Conditional skinned equipment is recorded separately in
`integration.apparelRigTargets` by resolving its exact native mesh name. When
those equipment rigs are outside the main character bridge audit, provide one or
more verified reports with `--supplemental-blender-audit`. Reports from another
native source build and duplicate rig profiles are rejected.

The verifier loads the pinned native asset once, decodes every unique primary,
companion, or apparel rig renderer, and compares positions, normals, UVs, triangles, joints,
weights, bind matrices, bone names, and the complete skeleton JSON. It writes
only a new report directly under `scratch/` and refuses overwrite. Passing
establishes reference equality for the current build only.

Every stageable authoring kit also contains `integration.profileTemplate`,
`preflightCommand`, and `stageCommand`. The starter retains exact native route
identity and structural fields while replacing the package key, display name,
GLB names, and texture names. It records optional copied settings that need
explicit review. `integration.companionRigTargets` includes exact authoring
inputs for skinned renderer paths outside the selected topology but required by
the complete profile. Current tests validate every starter against its JSON
schema and route preflight with asset existence deferred. Add original assets
before running the emitted command. Adapter-bound routes have no generic profile
starter.

Create a new route-specific package scaffold after selecting a stageable kit:

```sh
python3 tools/ai-model-pipeline/scaffold_model_package.py \
  --catalog scratch/model-authoring-kit-catalog.json \
  --topology-group EXACT_TOPOLOGY_GROUP \
  --route-kind directEnemy \
  --slug original-package-slug \
  --key ftkmf_modeltest_original_unique_key \
  --display-name "Original Model Name" \
  --asset-prefix original-asset-prefix
```

The scaffolder validates the catalog and all of its pinned route profiles,
isolated catalogs, and local references. It requires a new safe package slug,
model key, display name, and asset prefix. It also checks current isolated
catalogs for key, display, prefix, and generated filename collisions. Success
creates exactly three files under a new direct `art-experiments/<slug>/` child:
the customized runtime profile, a pinned authoring plan, and a README. The plan
contains primary, companion, and apparel rig inputs, the original assets still required,
and exact preflight and isolated-stage commands. It never invents GLB or PNG
files, stages the game, or assigns acceptance. Adapter-bound routes are rejected.

The extractor repairs a verified UnityPy 1.25.3 compressed skin decoder defect: a fourth influence residual is calculated from an integer total instead of the normalized total. It reconstructs that residual and rejects any remaining negative or unnormalized weights.

## Run the test suite

Run every pipeline test from the repository root with the standard library
runner (pytest also works when installed):

```sh
python3 -m unittest discover -s tools/ai-model-pipeline -p 'test_*.py'
```

The suite must finish with `OK` on a clean clone. Tests are in two groups:

- **Standalone tests** need only the committed repository plus, for the Kraken
  adapter and skin-probe families, the packages in `requirements.txt`. CI
  installs those packages and runs this command on every push and pull request.
- **Local-only tests** read gitignored inputs: extracted game data under
  `scratch/`, bulk capture media under `art-experiments/` and `docs/evidence/`,
  or a built framework DLL. They skip with a reason that names every missing
  path, so a skip is expected on a clean clone and a failure is a real
  regression. The helpers live in `local_inputs.py`; a new test that reads a
  gitignored input must use them rather than fail with `FileNotFoundError`.
- **Local evidence tests** are the subset of local-only tests that pin exact
  hashes of machine-local artifacts: the built framework DLL, the runtime
  helper DLL, and capture media. Presence is not enough for these, because CI
  builds a fresh DLL whose hash differs from the pinned evidence. They run only
  when `FTKMF_PIPELINE_LOCAL_EVIDENCE=1` is set and every pinned path exists,
  and skip with a reason that says so otherwise:

  ```sh
  FTKMF_PIPELINE_LOCAL_EVIDENCE=1 python3 -m unittest discover \
    -s tools/ai-model-pipeline -p 'test_*.py'
  ```

Local-only tests and the inputs each one needs. Rows marked "evidence" also
require `FTKMF_PIPELINE_LOCAL_EVIDENCE=1`:

| Test module | Required local inputs |
|---|---|
| `test_audit_model_validation_gates_current.py`, `test_audit_model_package_readiness_current.py`, `test_audit_topology_coverage_current.py`, `test_plan_model_authoring_kit_current.py` (integration-starter test) | `scratch/enemy-rig-mapping-reproducible.json`, `scratch/resource-enemy-base-preflight.json`, `scratch/rig-candidate-classification.json`, `scratch/static-renderer-inventory.json`, `scratch/unresolved-resource-paths.json`, `scratch/mirewarden-game/model-test-profiles.json` (see "Reconcile enemies", "Classify", and "Discover" sections for the generators) |
| `test_plan_model_validation_campaign_current.py`, `test_plan_execution_queue_player_route_current.py`, `test_run_execution_queue_route_current.py`, `test_plan_model_authoring_kit_current.py` | `scratch/model-validation-stage-readiness.json` and the isolated game copy at `scratch/mirewarden-game`; the authoring kit also needs `scratch/skeleton-inventory-reproducible.json`, `scratch/blender-all-rigs/bridge-audit.json`, and `scratch/blender-apparel-rigs-v1/bridge-audit.json` |
| `test_scaffold_model_package_current.py`, `test_verify_model_authoring_references_current.py` | The pinned authoring catalogs `scratch/model-authoring-kit-catalog-v54.json` and `-v53.json` (`plan_model_authoring_kit.py --all-routes`) plus the `scratch/model-venv` interpreter from the setup section |
| `test_kraken_production_adapter_contract_current.py` (evidence hash test) | `scratch/CharacterEventListener.analysis.cs`, the local decompile pinned by the adapter contract |
| `test_verify_kraken_skin_probe.py`, `test_verify_kraken_companion_skin.py` | `scratch/kraken-owned-skin-probe-v1/` (GLB and manifest) and `scratch/gloamfin-four-scenario-plan-v1/` from the Kraken skin-probe campaign |
| `test_verify_hearthveil_canonical_archive.py`, `test_verify_kraken_canonical_archive.py`, `test_verify_wildbloom_canonical_archive.py` (current-archive test), `test_audit_model_validation_archive_integrity_current.py` (Hearthveil test) (evidence) | Every file pinned by the archive's `integrity.json` under `art-experiments/*/live-validation-*-canonical/`, including the gitignored `.png`, `.gz`, and `.jsonl` captures |
| `test_verify_model_validation_archive_current.py`, `test_verify_model_validation_archive_player_current.py` (evidence) | The lossless `metadata/*.gz` files named by each indexed archive's `validation.json` under `art-experiments/` |
| `test_verify_kraken_production_archive.py` (evidence) | The compressed reports and screenshot pinned by `docs/evidence/kraken-production-adapter-v1/integrity.json` |
| `test_verify_kraken_portrait_followup.py` (evidence) | The built `FTKModFramework/bin/Release/net35/FTKModFramework.dll`, the runtime helper DLL, and the `scratch/kraken-portrait-followup-v2` captures pinned by `docs/evidence/kraken-portrait-followup-v1/validation.json` |
| `test_verify_kraken_visual_review.py` (raw-capture tests) (evidence) | The `scratch/mirewarden-game/model-test-output/` captures pinned by `docs/evidence/kraken-production-visual-v1/review.json` |
| `test_multi_primitive_glb.py` (byte-regression test) | `scratch/skeleton-audit/121328/reference.npz` from a pre-change extraction |

Extracted game data and live captures are never committed. A local-only test
that skips does not evidence in-game behavior; it only means the inputs were
not present on this machine.

## Authoring input

Use Blender for source modeling. Export JSON or NPZ arrays:

- `positions`: N by 3, already in Unity mesh-local space.
- `normals`: N by 3 in the same space.
- `uvs`: N by 2, top-origin V as expected by the FTK GLB contract. The runtime loader flips V with `1 - v` when assigning Unity mesh UVs. Convert native Blender/Unity bottom-origin UVs before passing them to the writer.
- `triangles`: triangle vertex indices, preserving the orientation of the extracted reference. Compare the sign of triangle cross products dotted with vertex normals, rather than relying on a clockwise label.
- Optional `vertex_bone_names`: one vanilla bone name per vertex, for rigid articulated parts.
- Alternatively `joints` and `weights`: N by 4, with optional `bone_names` defining the joint index mapping. Without `bone_names`, indices reference `skeleton.json` order.

If skin arrays are omitted, weights transfer from the closest point on the reference triangle surface using barycentric interpolation, keeping the strongest four influences. This only works well when the source mesh fits the reference anatomy. Use explicit groups for separate armor, stones and face details. `--rigid-bone Head_M` is a useful isolated diagnostic, not a full creature animation solution.

### Rigid MeshFilter child export

A selected rigid child uses a separate unskinned contract. Its source positions
are already in the exact `MeshFilter` transform's local space; no skeleton
reference is supplied:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/export_ftk_glb.py \
  --static --source scratch/my-rigid-part.json --output scratch/my-rigid-part.glb
```

The static writer requires one nonempty triangle primitive with `POSITION` and
indices, plus optional `NORMAL` and `TEXCOORD_0`. It deliberately writes no
skin, bone nodes, inverse bind matrices, `JOINTS_0`, or `WEIGHTS_0`. Do not use
`--rigid-bone` with `--static`: that option is a skinned diagnostic. A static
asset must be registered against an exact `MeshRenderer` with one `MeshFilter`;
it is not a substitute for a skinned body export. Use an original marker probe
when local-space placement needs live visibility evidence, without reading
native static surface data to shape the probe.

### Articulated wing and tail anatomy

For a winged creature, model the complete motion chain rather than attaching a
single root-rigid wing as decoration. Build the leading form and each flight
panel across the actual shoulder, elbow, wrist, and fingertip groups; blend
only at deliberate joints. Follow independent tail chains and leg/talon chains
as well. Use closed, consistently wound panel volumes when the live material is
one-sided, so a pose cannot reveal an invisible back face. The original
[Sunspire Roc](../../art-experiments/sunspire-roc/README.md) is a reproducible
36-bone `rocA` example: its source generator consumes binding metadata only,
creates independently articulated wings and tail plumes, and has separate
fresh ordinary and focused combat archives plus a constructed native row-
portrait supplement. The portrait record exercises one exact
`uiEnemyEncounterPortrait.Initialize` preview and does not stand in for every
HUD layout. It is evidence for `rocA/enRoc01`, not a compatibility claim for
other birds.

### Large humanoid bosses with external props

Treat the body palette and native attachments as separate ownership domains.
[Tidecrown Sovereign](../../art-experiments/tidecrown-sea-king/README.md) is a
reproducible 60-bone `seaKing/enSeaKing` example whose original generator reads
only palette bone names and inverse bind matrices. It builds closed mantle and
armor volumes over the actual spine, scapula, arm, wrist, and finger chains,
while retaining every palette entry in the GLB. The `Knee_L` and `Knee_R`
entries exist but have no native surface weight, so the design deliberately
does not invent a below-floor leg surface solely to fill them. This is a useful
pattern for a rig whose palette has helper or attachment bones outside its
native skinned surface.

Leave a weapon, shield, breakable children, tentacles, ragdoll bodies, native
controller, and portrait cache out of the replacement unless the exact target
renderer owns them. Tidecrown replaces only `enSeaKing`; its native trident
stays attached by the game's `WEAPON_HOLDER`. The exact V1 live record preserves
the 1.0 native scale, disabled inherited emission, a focus-only 720→719
same-target hit, a fixture ragdoll prefix, and strict Ready. Its separately
preserved ordinary no-focus run remains eight `no_hp_loss_unclassified` results.
That combination supports this exact renderer's reproducible integration
pattern, but it does not establish weapon intersection, portrait pixels,
tentacle compatibility, another 60-bone humanoid, or finished art.

For a Blender source authored X-right, Z-up and facing -Y, map positions and normals to Unity `(x,z,-y)`. This mapping has determinant +1, so preserve Blender triangle winding when transforming positions and normals this way. The extracted cave troll triangle cross products agree positively with its vertex normals; reversing this source winding makes the surface render inside-out. For other transforms, compare signed normal agreement against the extracted reference. Apply object transforms before export, including the inverse-transpose transform for normals. Preserve split normals and UV seams by splitting vertices where needed. All source geometry must match the selected reference's bind pose. The cave troll has horizontal arms; other rigs use their own extracted rest layout.

The reference NPZ deliberately preserves native Unity bottom-origin `uvs`. Before using that NPZ itself as a roundtrip source, copy its arrays to a separate source NPZ and set `uvs[:, 1] = 1 - uvs[:, 1]`; the exporter copies contract UVs unchanged. Do not flip UVs again if your authoring script already produces top-origin values.

The tools do not automatically align, decimate, smooth or pose a mismatched source model. Make these artistic decisions in Blender and export evaluated geometry. An ordinary Blender GLB is not accepted by the runtime contract.

## Export and validate

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/export_ftk_glb.py \
  --source scratch/my-model.json \
  --reference scratch/model-reference/reference.npz \
  --output scratch/my-model.glb
scratch/model-venv/bin/python tools/ai-model-pipeline/validate_glb.py \
  scratch/my-model.glb --reference scratch/model-reference/reference.npz
```

The writer supplies one packed primitive, unsigned 16-bit joints and indices, float32 weights, bone-name skin nodes, and column-major vanilla inverse bind matrices. It rejects invalid geometry, influences, and the Unity 2017 vertex limit (65,535 or more).

Validation rereads the GLB bytes independently, verifies accessor bounds/types, skin normalization and matrix layout, then evaluates every vertex against the vanilla rest matrices. It also compares triangle cross-product/normal signs with the native reference and rejects predominantly reversed orientation. These checks catch transposed bind matrices, malformed skin data, and an accidentally inverted surface. It does not substitute for combat animation checks. Package only original model output and original textures in `FTKModFramework_content/models/`, then register with `Content.SetEnemyBodyMeshFromGlb` and inspect a live combat.

## Stage a pinned isolated model profile

Use the generic transaction to move an original direct-enemy or resource-prefab
profile into an isolated game copy only after offline validation and the matching
route preflight described below. Pin that preflight with the source proof. The
game root and stage output must each be direct children of this repository's
`scratch/` directory. The first command does not modify the game copy:

When beginning from [the validation execution queue](../../docs/MODEL-VALIDATION-EXECUTION-QUEUE.md),
first compare its selected profile revisions with the stopped isolated catalog:

```sh
python3 tools/ai-model-pipeline/audit_model_validation_stage_readiness.py \
  --game-root scratch/my-isolated-game \
  --output-json scratch/model-validation-stage-readiness.json \
  --output-markdown scratch/MODEL-VALIDATION-STAGE-READINESS.md \
  --overwrite
```

The report separates a profile already present byte-for-byte, an ordinary
append or asset stage, and an explicit isolated migration. It refuses a running
isolated game and never changes files. Pick one listed historical revision
before staging; it does not merge profile variants or prove live compatibility.
For routes with several stage-ready documents,
[`docs/model-validation-profile-selections.json`](../../docs/model-validation-profile-selections.json)
pins the current intended choice by exact path and SHA-256. Every alternative
remains visible. Changing that ledger requires a fresh readiness report.

### Run one exact queue route

After stage readiness reports `stage_ready_revision_available` for a direct
enemy or resource-prefab route, make a dry plan from the same isolated root:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_execution_queue_route.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/my-isolated-game \
  --topology-group EXACT_TOPOLOGY_GROUP \
  --route-kind directEnemy
```

The route runner checks the pinned queue and readiness inputs against the
current exact profile, catalog, declared assets, and native source assignments.
Use `--profile-document` to select a historical revision explicitly when the
stage-readiness report has no pinned current selection, and
`--motion-renderer-path` for a multipart source with multiple skinned motion
targets. It rejects adapter-or-retarget requirements. Player-avatar routes use
`runtime-test/plan_execution_queue_player_route.py` to pin their distinct
native-preview workflow. Pass `--output scratch/NAME.json` to preserve a new,
non-overwriting plan artifact. The campaign validates that artifact exactly and
removes its creation command once current; all native UI and behavior evidence
still remains separate.

For player routes, run the campaign's exact `planCommandText`. The campaign
names each proposed plan
`model-route-<topology>-playerskinset-plan-<12-hex-plan-id>.json`, where the plan
ID is derived from the complete expected plan. Changed queue or readiness inputs
therefore create a new immutable plan path and leave older plans as historical
records.

On Party Select, use `native-party-class inspect` for the intended player before
each class step. The helper accepts exactly one visible `classArrowNext` and one
visible `classArrowPrevious`; the central `toggleClass` control is reported but
cannot satisfy a directional request. Submit the fresh token and direction for
one native callback, then inspect again. The token pins the owner, current class,
arrow instance, callback, visibility, and direction, and fails closed if an
explicit arrow is absent or ambiguous. Keyboard focus movement across player
cards is not evidence that the class changed.

To validate the full queue and emit one exact command per remaining route, use:

```sh
python3 tools/ai-model-pipeline/plan_model_validation_campaign.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/my-isolated-game \
  --output scratch/model-validation-campaign.json
```

The campaign planner checks every selected profile, catalog row, asset, source
assignment, and motion renderer with the one-route runner. It classifies native
self-removal routes into their once-only passive-arrival workflow instead of
issuing an ordinary hero-action command. It does not launch FTK, navigate player
UI, review images, or create archives. Run and finish one route before assigning
its canonical credit.

Regenerate the campaign after each ordinary enemy run. When its proposed runner
record exists, the planner checks its topology, route, profile, catalog, assets,
assignments, renderer, case identity, and optional visual review. A matching
exercise advances through review-template, manual-review, and archive-planning
states. Invalid, stale, partial, and errored records stop for inspection. The
planner neither overwrites them nor treats them as permission to rerun.

The five-bone `enkrakenhead` resource route is the current explicit exception.
Its [production adapter contract](../../docs/evidence/kraken-production-adapter-design-v1/README.md)
defines the endpoint mapping and live gates. Do not make it pass this generic
runner by adding a profile. Its dedicated internal adapter has passed offline
review and its production observation campaign verifies exact-route binding,
native behavior, ordinary lethal gameplay, and two-owner teardown. The route
remains in `adapter_visual_archive_review_required` until root appearance,
continuous motion, camera, culling, portrait, progression, endpoint composition,
and canonical route archive review pass.

Run that exception through the
[five-report passive observer campaign](runtime-test/README.md#production-gloamfin-kraken-observer-campaign).
Verify its reports against one binary deployment receipt with
`verify_kraken_production_adapter.py`. Keep the terminal roles on fresh owners
and retain visual, endpoint-composition, progression, and archive review as
separate acceptance records.

To make the bounded live run, append `--run --output scratch/my-route-run.json`
and an optional exact `--launch-env NAME=VALUE` content opt-in. The output path
must be new. The runner refuses any concurrent FTK session or occupied bridge
port, owns one isolated process, and records binding matches plus the case
result. It leaves visual review and immutable archiving pending.

```sh
python3 tools/ai-model-pipeline/stage_custom_model_profile.py \
  --game-root scratch/my-isolated-game \
  --profile art-experiments/my-model/runtime-profile.json \
  --asset-dir art-experiments/my-model \
  --pin art-experiments/my-model/manifest.json \
  --pin art-experiments/my-model/route-preflight.json \
  --pin art-experiments/my-model/original-geometry-proof.json \
  --output scratch/my-model-stage
python3 tools/ai-model-pipeline/deploy_custom_model_stage.py \
  --game-root scratch/my-isolated-game --stage scratch/my-model-stage
```

Stop the isolated game before the dry deploy. The dry deploy verifies the
source catalog, candidate catalog, every current model hash, every staged model
hash, and the exact planned paths. Keep the game stopped, then execute the
transaction:

```sh
python3 tools/ai-model-pipeline/deploy_custom_model_stage.py \
  --game-root scratch/my-isolated-game --stage scratch/my-model-stage \
  --label my-model --execute
```

The deployer preserves a timestamped backup under that isolated game copy and
refuses a running game, symlinks, a stale catalog, or model-directory drift.
Never point it at the Steam installation.

For one changed existing profile row, preserve the old stage receipt and use a
separate migration stage with the explicit flag. The receipt pins the old and
new canonical row hashes and the deployer verifies that only the named row
changed before copying the catalog:

```sh
python3 tools/ai-model-pipeline/stage_custom_model_profile.py \
  --game-root scratch/my-isolated-game \
  --profile art-experiments/my-model/runtime-profile.json \
  --asset-dir art-experiments/my-model \
  --replace-existing-profile \
  --output scratch/my-model-profile-migration
```

### Update runtime test binaries reversibly

When the framework, runtime helper, or content plugin changes for a validation
contract, stage ordinary source binaries inside this repository and use the
binary deployer while the isolated game is stopped. The first command is a dry
review; the second creates a hash-pinned backup and replaces only the selected
plugin files:

```sh
dotnet build FTKModFramework -c Release
dotnet build tools/ai-model-pipeline/runtime-test/RuntimeModelTest.csproj \
  -c Release -p:TestGameRoot="$PWD/scratch/my-isolated-game"
dotnet build tools/ai-model-pipeline/runtime-test-content/RuntimeModelTestContent.csproj \
  -c Release -p:TestGameRoot="$PWD/scratch/my-isolated-game"

python3 tools/ai-model-pipeline/deploy_isolated_test_binaries.py \
  --game-root scratch/my-isolated-game \
  --framework FTKModFramework/bin/Release/net35/FTKModFramework.dll \
  --helper tools/ai-model-pipeline/runtime-test/bin/Release/net35/FtkRuntimeModelTest.dll \
  --content tools/ai-model-pipeline/runtime-test-content/bin/Release/net35/FtkRuntimeModelTestContent.dll
python3 tools/ai-model-pipeline/deploy_isolated_test_binaries.py \
  --game-root scratch/my-isolated-game \
  --framework FTKModFramework/bin/Release/net35/FTKModFramework.dll \
  --helper tools/ai-model-pipeline/runtime-test/bin/Release/net35/FtkRuntimeModelTest.dll \
  --content tools/ai-model-pipeline/runtime-test-content/bin/Release/net35/FtkRuntimeModelTestContent.dll \
  --label my-runtime-binaries --execute
```

Each of `--framework`, `--helper`, and `--content` is optional, but at least one
must be supplied. `deploy_isolated_test_binaries.py` refuses a running game, symlinks, missing
ordinary files, external source paths, and hash mismatches. Its receipt proves
only binary deployment; repeat registration and the relevant live evidence
afterward.

The same transaction handles a custom-class player profile. Pass
`--catalog-kind player` to both commands, run the player skinset preflight below,
and pin its output with the authoring proof. Supply a document that validates
against `player-profiles.schema.json`. Its required body renderers and optional
conditional apparel all contribute their declared GLB and PNG assets to the same
pinned models directory. The player deploy backup also retains the player
registration report. A successful stage proves only the on-disk transaction:
preview assembly, combat clones, equipment rebuilds, motion, lifetime, and art
still need the player-specific live checks.

For a deliberate revision of an asset already declared by an unchanged profile,
stage to a new directory with `--replace-existing-assets`. The supplied profile
must byte-match its catalog row; the receipt records both old and new hashes,
and the deployer replaces only the declared asset after rechecking the old hash.
Re-run offline validation and a fresh live trial after such a revision. This is
useful for a corrected texture mapping, but it is not a way to silently migrate
a profile or overwrite unrelated content.

## Discover serialized rig candidates

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/inventory_skeletons.py \
  --assets "$HOME/Library/Application Support/Steam/steamapps/common/For The King/FTK.app/Contents/Resources/Data/resources.assets" \
  --output scratch/skeleton-inventory-reproducible.json
```

This writes metadata only, with source/dependency hashes and per-renderer resolution errors. External pointers resolve through each owning asset file's actual external table. Profiles combine bone-name topology **and exact per-name bind matrices**, independent of joint array order. A topology match alone does not mean compatible bind space.

These are discovery candidates, including player body parts, accessories, portraits and NPCs. They are not a supported enemy catalog. Only renderers stored directly in the selected asset file are enumerated. Ancestor Animator controllers are serialized defaults; enemy weapon assignment can replace the live controller. Enemy database mapping and live renderer/controller checks remain separate gates.

## Audit native reference roundtrips

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/audit_skeletons.py \
  --assets "$HOME/Library/Application Support/Steam/steamapps/common/For The King/FTK.app/Contents/Resources/Data/resources.assets" \
  --inventory scratch/skeleton-inventory-reproducible.json \
  --output scratch/skeleton-audit
```

The audit chooses one renderer per exact bind/topology profile, extracts its native mesh, flips a copy of native UVs, exports and independently validates the GLB. Use repeated `--renderer-id` arguments for an explicit subset. Output must be gitignored; the script refuses another destination because reference geometry and roundtrip GLBs remain copyrighted local extracts. It records each extraction/export/validation failure in `audit.json` and per-renderer `audit.log`, continues across failures, and checks the inventory's source hash before starting.

Passing this audit establishes only that the selected native reference survives this offline tooling path. It does not establish that a new custom mesh fits, that runtime renderer selection succeeds, or that combat animations work.


## Reconcile enemies and generate an honest coverage baseline

With the matching game Managed assemblies and `ilspycmd` installed:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/map_enemy_rigs.py \
  --resources "$HOME/Library/Application Support/Steam/steamapps/common/For The King/FTK.app/Contents/Resources/Data/resources.assets" \
  --inventory scratch/skeleton-inventory-reproducible.json \
  --output scratch/enemy-rig-mapping-reproducible.json
python3 tools/ai-model-pipeline/coverage_report.py \
  --inventory scratch/skeleton-inventory-reproducible.json \
  --mapping scratch/enemy-rig-mapping-reproducible.json \
  --audit scratch/skeleton-audit/audit.json \
  --output scratch/model-coverage-baseline.json
```

The mapper checks the enemy enum, serialized array count, independent database
copies, typed prefab pointers, and weapon animation controllers. Its verified
field recognizer is not a universal Unity/FTK-version deserializer; unexpected
layouts fail instead of silently dropping rows. `ilspycmd` must be able to find
its installed .NET runtime (configure `DOTNET_ROOT` when needed).

The coverage generator checks source hashes and refuses to overwrite an existing
output. It creates a fresh baseline with custom-model/live checks pending;
never use it to overwrite annotated evidence. Native-reference passes are kept
separate from original-model binding, appearance, motion, and gameplay checks.

For isolated live checks, use the [runtime test plugin](runtime-test/README.md).

## Verify immutable model-validation archives

After a local `archive.py` freezes a fresh isolated trial, verify the archive
before adding its `validation.json` to the runtime index:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py \
  art-experiments/<package>/live-validation-vN --check-video-metadata
```

The checker verifies gzip-lossless metadata mappings, source and selected-image
hashes, presentation-video hashes and, when requested, their frame counts and
dimensions. It rejects unsafe game payload mappings and paths outside the
archive. It is an artifact-integrity check only, so it never upgrades an
archive into proof of binding, animation, gameplay, or art quality.

Regenerate the repository-wide [archive-integrity ledger](../../docs/MODEL-VALIDATION-ARCHIVE-INTEGRITY.md)
after adding or changing archives:

```sh
python3 tools/ai-model-pipeline/audit_model_validation_archive_integrity.py \
  --overwrite --check-video-metadata
```

The ledger reports historical preservation gaps separately from model behavior
or visual acceptance.

For a new exact enemy route, use `archive_model_validation_case.py` with a
[versioned archive plan](../../docs/MODEL-VALIDATION-ARCHIVES.md). For a
game-owned player preview, use `archive_player_model_validation.py` and the
player plan in that guide. Both builders require matching review sessions,
exact per-frame runtime identity, and a new output directory. They preserve
reviewed metadata and images without copying native game payloads.

If an enemy exercise needed bounded ordinary-attack retries, keep every
accepted capture in the archive plan under `attack-attempt-N`. The archive tool
uses the unique `nonlethal_hp_loss` attempt for the derived ordinary-damage
record and preserves earlier no-loss attempts as their own evidence.

For a completed `run_execution_queue_route.py` record and a root-reviewed
frame report, use `make_execution_queue_archive_plan.py` to create the new
archive plan. It rechecks the exact runner, case, profile document, catalog,
source assets, and review session before writing a non-overwriting plan for
`archive_model_validation_case.py`.

## Reconcile original-model evidence with the index

After archiving a new original-model live trial, add its evidence path and hash
to `docs/model-runtime-validation.json`, then run:

```sh
python3 tools/ai-model-pipeline/audit_original_model_index.py \
  --summary --fail-on-novel --fail-on-unresolved
```

The audit scans `art-experiments/**/live-validation*.json` and supplemental
`live-validation*/validation.json` records, resolves declared base enemy and
renderer identifiers against the exact native mapping, and recognizes every
explicit pinned path in each runtime-index record, including a named historical
follow-up. It distinguishes directly indexed evidence, an unindexed supplement
for an already indexed exact assignment, an unindexed exact assignment with no
indexed original evidence, and a separately indexed constructed fixture that
does not claim a runtime source pair. `--fail-on-novel` catches a new exact
assignment that is absent from the index, while `--fail-on-unresolved` catches
a runtime-style archive with no declared native identity. A constructed fixture
pinned in its own index collection remains separate from both conditions. It is
an evidence-registration check only: it does not accept gameplay, art, motion,
culling, portraits or resource lifetime. Use `--output scratch/my-reconciliation.json`
for a full immutable review report; the command refuses to overwrite one.

## Reconcile explicitly recorded validation gates

The runtime index and the topology plan deliberately avoid deciding whether an
archive contains all live evidence required for a finished model. Regenerate the
separate conservative ledger after an indexed archive changes:

```sh
python3 tools/ai-model-pipeline/audit_model_validation_gates.py \
  --output-json docs/model-validation-gates.json \
  --output-markdown docs/MODEL-VALIDATION-GATES.md \
  --overwrite --fail-on-unresolved --fail-on-integrity
python3 tools/ai-model-pipeline/test_audit_model_validation_gates.py
```

The audit recognizes only direct structured evidence: runtime binding objects,
named visual-review fields, capture labels or sampled clip names for idle/attack/
hit/death, numeric ordinary no-focus HP changes, and `ok:true` Ready objects.
It does not parse free-text statuses, treat a generic pass capture as idle,
convert a focused hit into ordinary damage, or call any recorded field a PASS.
The generated [ledger](../../docs/MODEL-VALIDATION-GATES.md) makes historical
schema gaps visible while retaining the linked evidence artifact as the source
for the actual visual and gameplay verdict. Each JSON record also lists its
unrecorded source-specific core evidence shapes, so a follow-up can add the
exact missing structured record without calling that absence a failed behavior.

## Reconcile candidate validation coverage

After topology coverage, package readiness, the validation-gate ledger, and the
archive-integrity ledger are current, regenerate the route-specific follow-up
queue:

```sh
python3 tools/ai-model-pipeline/audit_model_candidate_coverage.py \
  --output-json docs/model-candidate-validation-coverage.json \
  --output-markdown docs/MODEL-CANDIDATE-VALIDATION-COVERAGE.md \
  --overwrite --fail-on-unmapped
```

The report accepts a canonical representative only when one archive covers a
single exact source identity, contains exactly the complete renderer set for
that identity within one topology group, has every required structured evidence
shape, and the matching immutable `validation.json` hash is independently
verified by the current
[archive-integrity ledger](../../docs/MODEL-VALIDATION-ARCHIVE-INTEGRITY.md).
Complete multipart sources are eligible; partial, mixed-identity,
cross-topology, duplicate, and extra-assignment archives are not. This remains
a route-coverage and preservation rule, not an art or behavior verdict.

## Generate the validation execution queue

Candidate coverage deliberately identifies the closest incomplete archive. Turn
each remaining route into an actionable static-profile match with:

```sh
python3 tools/ai-model-pipeline/audit_model_validation_execution_queue.py \
  --output-json docs/model-validation-execution-queue.json \
  --output-markdown docs/MODEL-VALIDATION-EXECUTION-QUEUE.md \
  --overwrite
```

For a route with indexed evidence, the queue uses its one priority archive's
exact source assignment. For a route without one, it uses a remaining exact
topology representative. It lists only profile documents whose package passed
the current static preflight. Enemy matches require the exact direct-enemy or
resource-prefab identity and renderer ID/path; player matches also require the
exact named profile and skinset. More than one matching document remains a
choice for the author to make explicitly.

The generated [execution queue](../../docs/MODEL-VALIDATION-EXECUTION-QUEUE.md)
is planning data. It does not establish a live bind, motion, gameplay, visual
review, acceptance, or equivalence to an older profile revision. An unmatched
target identifies missing profile authoring unless an explicit resource
ownership finding classifies it as an adapter-or-retarget design requirement.
Use `--fail-on-unmatched` only when that authoring backlog is expected to be
empty.

## Generate original rig calibration probes

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/make_rig_probe.py \
  --reference scratch/model-reference/reference.npz \
  --skeleton scratch/model-reference/skeleton.json \
  --output-dir scratch/my-rig-probe
# Or generate and validate every audited representative:
scratch/model-venv/bin/python tools/ai-model-pipeline/make_rig_probe.py \
  --audit-dir scratch/skeleton-audit --output-dir scratch/rig-probes
```

The generator creates original octahedral joint indicators and tapered connecting segments from inverse-bind joint centers and the extracted parent list. It does not copy native vertices, faces, normals or surface weights. By default every joint gets explicitly weighted geometry, including isolated or coincident joints. Connecting segments use their endpoint bones. A fixed original four-color palette distinguishes joint markers, roots and segments.

Use `--joint-scope weighted` for multipart renderers whose bone palette includes
joints the native mesh does not weight. This selects markers only for joints
appearing in a native vertex slot with a strictly positive weight. A segment is
created only when its direct parent and child are both selected; missing joints
are never skipped to create a longer bridge. The complete native bone palette,
order and inverse bind matrices remain unchanged. Native weight values only
supply a usage boolean; generated vertices retain original endpoint weights.
Empty selections and invalid positively weighted joint indices are rejected.
The default `--joint-scope all` preserves previous GLB/source/texture output.
Reports include selected and omitted joint counts and selected indices. Both
Blender scaffold and batch bridge CLIs forward this option.

For example, create a separate weighted Snowman hat diagnostic:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/make_rig_probe.py \
  --reference scratch/skeleton-audit/121404/reference.npz \
  --skeleton scratch/skeleton-audit/121404/skeleton.json \
  --output-dir scratch/weighted-snowman-probes/121404 --radius-scale 6 \
  --joint-scope weighted
```

The five Snowman subsets selected 1/8, 1/11, 1/16, 6/13 and 20/31 palette joints
for hat, head, base, scarf and middle body respectively. All five passed the
independent exporter validator and Blender scaffold/save/reopen/export audit.
This corrects diagnostic geometry that previously overlaid the complete skeleton
on every part; it does not change production loading or validate live death poses.

Use `--connections none` for markers-only diagnostics when native animation
separates bones. The default `hierarchy` keeps existing connector geometry
byte-for-byte. This option changes only generated diagnostic faces: selected
joint markers, full binding palette and inverse binds remain intact. It is
forwarded by both Blender CLIs and recorded in probe/scene/audit metadata.

Snowman demonstrates why weighted joint selection alone is insufficient. Its
native middle body gives `BotttomBall` only 1/31 influence on 57 vertices, with
remaining influence on chest/arm bones. A generated rigid endpoint at that bone
creates an artificial `BotttomBall`–`ChestBall` bridge as death separates the
centers from about 0.74 to 7.34 world units. Markers-only diagnostics omit this
invented connection; they do not alter or approximate the authored blend weights.
A usage threshold or surface-component filter is not a substitute: both bones
are valid native influences on the same vertices.

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/make_rig_probe.py \
  --reference scratch/skeleton-audit/121696/reference.npz \
  --skeleton scratch/skeleton-audit/121696/skeleton.json \
  --output-dir scratch/snowman-markers-only-v1/121696 \
  --radius-scale 6 --joint-scope weighted --connections none
```

Use `--radius-scale 6` when the default probe is too thin at the combat camera.
The optional finite multiplier accepts 0.25 through 16 and defaults to 1,
preserving previous geometry byte-for-byte. It changes only original marker and
segment thickness, preserving joint centers, bone names, hierarchy, weights,
and native binding matrices. The factor is recorded in `probe.json` and batch
metadata. `create_blender_template.py` and `audit_blender_bridge.py` accept and
forward the same option, recording it in scene/audit metadata. Broader markers
may overlap or extend beyond native animation bounds; visibility and culling
still need live review. Always use a new output directory for a new factor.

For rigs with a large native body but only a few closely spaced joints, use
`--marker-radius 0.20` to set the diagnostic octahedron radius explicitly in
mesh units. The value must be finite and positive. It overrides the entire
automatic radius calculation, including `--radius-scale`; that scale is not
applied again. Omitting it preserves automatic-sizing geometry byte-for-byte.
Probe metadata records the actual radius, override and radius source; both
Blender CLIs forward and record the override. No native surface is copied.

Beholder's two joint centers span about 0.20 units while the native body spans
about 2.5 units. Its automatic 6× markers had radius 0.0144, too small at the
combat camera. New diagnostic exports with radius 0.20 and markers only passed
the exporter and Blender bridge for both body and eye; visibility still needs
live review. Explicit sizing changes diagnostic geometry, not production bounds
or the authored character. Large markers can overlap or exceed the animation
bounds, so choose and review the size per use.

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/make_rig_probe.py \
  --reference scratch/skeleton-audit/121031/reference.npz \
  --skeleton scratch/skeleton-audit/121031/skeleton.json \
  --output-dir scratch/beholder-markers-v1/121031 \
  --joint-scope weighted --connections none --marker-radius 0.20
```

For the two exact `plantA` renderer references in this build:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/make_rig_probe.py \
  --reference scratch/skeleton-audit/120953/reference.npz \
  --skeleton scratch/skeleton-audit/120953/skeleton.json \
  --output-dir scratch/probe-visible-test/120953 --radius-scale 6
scratch/model-venv/bin/python tools/ai-model-pipeline/make_rig_probe.py \
  --reference scratch/skeleton-audit/121072/reference.npz \
  --skeleton scratch/skeleton-audit/121072/skeleton.json \
  --output-dir scratch/probe-visible-test/121072 --radius-scale 6
```

Both thicker exports passed independent validation and the Blender
scaffold/save/reopen/export subset audit. Their default-factor regeneration
matches the original GLBs byte-for-byte. The thicker assets have not yet been
deployed or visually accepted in the game.

Output includes editable `probe-source.json`, `rig-probe.glb`, `probe-palette.png`, and a `probe.json` report. Batch mode also writes `probes.json` with every success or failure. The GLB embeds the minimal vanilla binding metadata required by the runtime contract. Native reference files remain in ignored local storage; they are used by the independent validator to compare bind matrices and orientation.

**Calibration only:** probes expose renderer selection, bone mapping and animation behavior. They are not finished creatures, visual-design examples, or artistic acceptance. Coincident markers may overlap even though each joint has weighted geometry. Offline export validation does not demonstrate runtime or combat-animation success.


## Classify non-enemy character candidates

Use actual skinset avatar pointers and ancestor components to distinguish player
and diorama relationships from unresolved character prefabs:

```sh
DOTNET_ROOT=/opt/homebrew/opt/dotnet/libexec scratch/model-venv/bin/python \
  tools/ai-model-pipeline/classify_rig_candidates.py \
  --resources "$FTK_DATA/resources.assets" \
  --inventory scratch/skeleton-inventory-reproducible.json \
  --enemy-mapping scratch/enemy-rig-mapping-reproducible.json \
  --output scratch/rig-candidate-classification.json
```

`FTK_DATA` is the explicitly selected local game Data directory. Use the local
.NET runtime environment appropriate to your machine. This metadata classifier
requires UnityPy and ilspycmd. Player references establish ownership, not support
through the enemy API. Keep unresolved prefab roles unresolved until source or
live evidence identifies them. See [the register](../../docs/MODEL-SKELETONS.md)
for current counts and validation boundaries.

## Reconcile topology coverage routes

After the candidate inventory, enemy mapping, resource-prefab preflight, and
runtime evidence index are current, regenerate the coverage plan with its pinned
ownership classification:

```sh
python3 tools/ai-model-pipeline/audit_topology_coverage.py \
  --ownership docs/evidence/nonenemy-topology-ownership-v1/findings.json \
  --output-json docs/model-topology-coverage-plan.json \
  --output-markdown docs/MODEL-TOPOLOGY-COVERAGE.md \
  --overwrite
python3 tools/ai-model-pipeline/test_audit_topology_coverage_current.py
```

The audit keeps direct serialized enemy rows, ResourceManager prefab overrides,
skinset-avatar routes, and unsupported empty renderers distinct. Its input hashes
make a stale ownership finding fail the current-build regression test. A zero
`unresolvedOwnershipGroups` count means every zero-direct-row group has an
explicit route or exclusion; it does not approve a model, controller, source
pair, combat outcome, or visual result. Re-research a changed local game build
instead of editing the ownership finding to match new hashes.

The generated table reports `Original / any / known pairs` separately. An
original count identifies an indexed authored-model record for the exact source
pair; it does not promote that record's live, visual, motion, or gameplay status
to the whole topology or its siblings.

The Markdown output also lists one deterministic unrecorded source/controller
route for every incomplete topology, with the number of additional exact routes
still open. Use it to choose the next package, then inspect the JSON plan for
the complete route list. The first route is an ordering aid only; it never
transfers evidence to its siblings.

## Audit authored package readiness

The package ledger runs every checked profile document through the correct
current-build preflight, then separately shows exact indexed **original** source
evidence, diagnostic-only source evidence, package-local validation files, and
rigid-child prerequisites. Named, pinned historical follow-ups in the runtime
index count as explicitly indexed files while remaining separate from the
profile's exact-source acceptance. It keeps historical profile revisions as
separate rows and does not infer that an archive applies to a later revision
with the same native route. Its generated fresh-isolated-trial queue lists the
exact profile revisions that passed static preflight but still need their own
archived original-model evidence.

```sh
python3 tools/ai-model-pipeline/audit_model_package_readiness.py \
  --output-json docs/model-package-readiness.json \
  --output-markdown docs/MODEL-PACKAGE-READINESS.md \
  --overwrite --fail-on-preflight
python3 tools/ai-model-pipeline/test_audit_model_package_readiness.py
```

Before that audit, generate a fresh `scratch/static-renderer-inventory.json`
when direct profiles may include rigid `MeshRenderer` children, using the
command below. A preflight pass is an offline route-and-asset result only; an
indexed original record still needs its own gate ledger and reviewed live
evidence.

## Preflight a direct-enemy artwork profile

Before staging a newly authored direct-enemy package, check that its declared
enemy, renderer paths, and combat-profile fingerprint still match the exact
current mapping. This read-only step is stricter than JSON schema validation;
it rejects a compatible-looking sibling path or a stale combat fingerprint.

```sh
python3 tools/ai-model-pipeline/validate_custom_model_profile_route.py \
  --profile art-experiments/my-model/runtime-profile.json \
  --asset-dir art-experiments/my-model \
  --output art-experiments/my-model/route-preflight.json
python3 tools/ai-model-pipeline/test_validate_custom_model_profile_route.py
```

The generated record pins the mapping and local game asset hashes, native
prefab/controller identities, exact source renderer IDs, selected rig/combat
profiles, and declared asset hashes. It supports direct serialized enemy rows.
Resource-prefab overrides must use the distinct preflight below. A passing result
still needs the pinned stage, deployment, native inventory, and full live
validation sequence.

### Preflight a rigid `MeshRenderer` child in a direct profile

First inventory rigid children from the exact same local game asset that the
enemy mapping pins. The inventory records hierarchy, direct enemy root/CEL,
one `MeshFilter`, co-located renderer type, native mesh pointer, and native
material-slot count without decoding native surface data:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/inventory_static_renderers.py \
  --assets "$FTK_DATA/resources.assets" \
  --mapping scratch/enemy-rig-mapping-reproducible.json \
  --output scratch/static-renderer-inventory.json
python3 tools/ai-model-pipeline/validate_custom_model_profile_route.py \
  --static-inventory scratch/static-renderer-inventory.json \
  --profile art-experiments/my-model/runtime-profile.json \
  --asset-dir art-experiments/my-model \
  --output art-experiments/my-model/route-preflight.json
```

The static assignment must explicitly use `"rendererKind": "MeshRenderer"`
and have a linked exact `SkinnedMeshRenderer` assignment in the same direct
profile for motion/controller compatibility. The preflight accepts only a
strict one-filter, one-usable-native-material target with no co-located skinned
renderer. It rejects multi-slot and ambiguous children instead of altering their
draw behavior. The [Abyssal Kraken V4 preflight](../../art-experiments/abyssal-kraken/route-preflight-v4-head-static.json)
is the worked seven-bone-head plus rigid-eye example; its static route remains a
separate live appearance claim.

## Preflight a resource-prefab artwork profile

Before staging an authored `resourcePrefab` profile, check it against the
pinned resource-prefab source route. It requires the exact resource load path,
base chassis, renderer path, controller/rig fingerprint, and one renderer
assignment; a direct serialized enemy profile must not use this route.

```sh
python3 tools/ai-model-pipeline/validate_resource_model_profile_route.py \
  --preflight scratch/resource-enemy-base-preflight.json \
  --profile art-experiments/my-model/runtime-profile.json \
  --asset-dir art-experiments/my-model \
  --output art-experiments/my-model/route-preflight.json
python3 tools/ai-model-pipeline/test_validate_resource_model_profile_route.py
```

The immutable record pins the resource-preflight artifact, exact resource
identity, base chassis, CEL, renderer source ID, controller, rig/combat
fingerprints, and declared asset hashes. It proves no current source asset hash
beyond the preflight artifact, and it establishes no deployment or live result.

### Keep ordinary-hit captures nonlethal when needed

An enemy profile may declare `"minimumBaseHealth": 64` (integer 1 through
1000) before its route preflight when the exact native chassis would otherwise
die from the ordinary no-focus test hit. The test-content registration uses
`Math.Max` on the isolated custom clone only, so it never lowers a native base
value or changes shipped balance. Spawn scaling can still change combat HP;
preserve the observed before/after values in the capture.

Treat this as a profile revision. A prior static preflight and deployment remain
historical evidence. Create a new pinned preflight, then use one reviewed,
single-row `--replace-existing-profile` migration or a fresh key and restart the
isolated game. The normal staging path still rejects a changed existing catalog
row.

[Lichenfang Prowler](../../art-experiments/lichenfang-basey-wolf/README.md) is the
current full original resource-prefab example: its `enbaseywolf/Wolfie` package
contains deterministic authored geometry, an independent source proof, this
route preflight, a pinned isolated deployment, and a scoped native live archive.
Reuse its package shape for a new exact resource route, never its binding or
evidence for a sibling prefab.

[Sablevine Serpent](../../art-experiments/sablevine-basey-snake/README.md) adds
the scale-correction example for the distinct `enbaseysnake/enSnake_Basey`
resource route. Its immutable V1 camera-fit rejection and V2 scale-0.55
acceptance use identical original asset hashes but fresh profile keys and
separate live archives. Treat a camera-fit correction as a profile revision:
measure native baseline and spawned scale at runtime, then preserve a new visual
review rather than recasting an earlier archive.

## Preflight a player skinset artwork profile

Before staging a player package, verify its body and hair assignments against
the exact serialized skinset avatar. The preflight requires the complete native
body/hair renderer set for that skinset, preventing a profile from quietly
borrowing a same-named renderer from another avatar. It also pins both source
asset databases used by the skinset classification.

```sh
python3 tools/ai-model-pipeline/validate_player_model_profile_route.py \
  --classification scratch/rig-candidate-classification.json \
  --profile art-experiments/my-model/runtime-profile.json \
  --asset-dir art-experiments/my-model \
  --output art-experiments/my-model/route-preflight.json
python3 tools/ai-model-pipeline/test_validate_player_model_profile_route.py
```

The record pins the selected skinset avatar/CEL, source renderer IDs, exact
renderer paths, rig fingerprints, joint counts, and declared assets. Conditional
apparel is checked for schema and assets only: its native equipment branch,
class registration, preview, combat, lifetime, and art still need their own
isolated validation.

## Scaffold and export from Blender

The minimal CLI bridge was exercised with Blender **5.2.1 LTS** on the local wolf (`121142`, 33 bones) and bat (`121104`, 53 bones) references. It is not a Blender add-on and does not generate finished creature art.

Create a local bind scene, using an already extracted renderer reference:

```sh
blender --background --factory-startup --python-exit-code 1 \
  --python tools/ai-model-pipeline/create_blender_template.py -- \
  --reference scratch/skeleton-audit/121142/reference.npz \
  --skeleton scratch/skeleton-audit/121142/skeleton.json \
  --output scratch/blender-authoring/wolf.blend \
  --python-executable scratch/model-venv/bin/python \
  --with-reference
```

`--with-reference` is optional. It creates a hidden wireframe guide explicitly tagged as copyrighted, local reference only. The entire bind scene must be saved under gitignored storage. Bone names, hierarchy and rest orientations are derived from inverse bind matrices; the exact native bind matrices and reference hash are retained in `FTK_BIND_METADATA.json`. Blender rest orientations are checked within `1e-4` numerical tolerance; one common positive uniform bind scale is normalized in Blender orientation frames only, with native joint positions and exact inverse binds preserved. The factor is recorded in metadata. Nonuniform, sheared or reflected rest frames are refused by this minimal scaffold.

The scene starts with original editable calibration geometry, vertex groups, an armature modifier and a packed original palette. Replace that calibration mesh with original art, or remove its `ftk_export` custom-property flag and tag the intended mesh objects with `ftk_export = True`. Each exported mesh needs a single active FTK armature modifier, normalized vertex-group weights with one to four positive influences, an active UV map, and one material. All exported objects must share one saved or packed original PNG, linked directly from an Image Texture node to the Principled BSDF Base Color. The bridge supports the albedo image, not procedural materials, shader baking or multiple texture sets. It preserves per-loop normals and UV seams.

Export the tagged original geometry:

```sh
blender --background scratch/blender-authoring/wolf.blend --python-exit-code 1 \
  --python tools/ai-model-pipeline/export_blender_model.py -- \
  --output scratch/blender-authoring/wolf-original.glb
```

Use `--selected` to export selected mesh objects instead of the tagged set. A reference guide is rejected even if selected or accidentally tagged. The bridge also rejects a posed/edited rig, unknown bones, more than four influences, non-normalized weights, enabled unbaked non-armature modifiers, nonzero shape keys, negative transforms, degenerate faces, or ambiguous materials. It uses ordinary vertex-group linear skinning and refuses envelope or preserve-volume modes. Apply intended geometry modifiers yourself before exporting; the tool never silently decimates or changes the design.

Output is the runtime `.glb`, its original `.png`, an editable `.source.json`, and `.validation.json`. The bridge maps Blender coordinates to Unity `(x,z,-y)`, preserves winding, flips UV V exactly once and delegates to the existing writer and independent validator. Native guide geometry is excluded. Keep `--python-exit-code 1` in automated commands so a Blender Python exception fails the shell command.

Wolf and bat calibration exports passed the binary, skin and orientation checks; posed-rig, unknown-bone, reference-guide, modifier, influence-count and material-ambiguity rejection cases were also exercised. These checks do not replace live renderer/controller validation or artistic review.

To audit the complete Blender scaffold/save/reopen/export path for all native reference representatives:

```sh
blender --background --factory-startup --python-exit-code 1 \
  --python tools/ai-model-pipeline/audit_blender_bridge.py -- \
  --audit-dir scratch/skeleton-audit \
  --output-dir scratch/blender-all-rigs \
  --python-executable scratch/model-venv/bin/python
```

Repeated `--renderer-id` arguments select a subset for a rerun. Each profile creates an original calibration scene without native guide geometry, saves and reopens the `.blend`, exports through the bridge, and launches the independent validator in the explicit Python environment. `bridge-audit.json` records source hashes, per-profile scene/source-mesh/GLB/texture hashes, validation results, and the phase and reason for every failure. A separate `bridge-audit.log` lives in each renderer directory. This is additional authoring-tool coverage beyond a native-mesh roundtrip; it remains offline calibration evidence, not artistic or live-game acceptance.

The local 230-profile audit completed with 230 passes after adding common uniform bind-scale support for the `hairTop` representative. The initial run's one scaffold rejection was preserved separately. Final exports had a maximum bind/rest reconstruction error of `3.56e-15`; every result records the original reference, scene, source geometry, GLB and texture hashes. This result applies to the audited local game build and Blender 5.2.1, not arbitrary rigs or future versions.

## Summarize capture timing and recorded ragdoll evidence

```sh
python3 tools/ai-model-pipeline/summarize_capture.py \
  --result scratch/my-capture.json --output scratch/my-capture-summary.json
python3 -m unittest discover -s tools/ai-model-pipeline -p test_summarize_capture.py
```

The summary preserves `visualReview: pending`. In captures that actually record the newer fields, `ragdollEvidence` reports Animator enabled transitions, native `m_DoRagdoll` observations, Rigidbody coverage/truncation, active nonkinematic body movement, and each body's trailing low-velocity interval. The latter requires both linear and angular velocity samples, consecutive frames, and advancing game time; its explicit thresholds are 0.01 game units/second and 0.01 radians/second. These measurements describe a possible settling interval, not a successful ragdoll or visual-quality verdict.

Missing fields remain `not_recorded`. In particular, renderer `enabled` is not Animator `enabled`, frozen normalized clip time does not establish a ragdoll handoff, and `m_DoRagdoll` only records native configuration. Missing/truncated bodies prevent whole-ragdoll conclusions. The supplied older capture `0a9cba3642c0435489e5f188214cbf7a` lacks Animator-enabled and physics fields; its timing and state data remain usable, but those ragdoll measurements cannot be reconstructed from it.

### Opt-in2–4 native material slots

The default writer/Blender bridge remains a single primitive. An original source JSON can instead include `primitives: [{"triangles": [...]}, ...]` with2–4 nonempty triangle groups. All groups share the top-level positions, normals, UVs, weights, joints and exact palette/IBMs. The writer emits one distinct uint16 index accessor and material ID per group. Runtime native-slot mapping is explicit in `EnemyRendererMaterial` descriptors; it is not inferred from material names.

For Blender, `export_blender_model.py --output scratch/cube.glb --native-material-slots 2` treats each polygon's Blender material index as its primitive index. Every requested index must contain original faces. Each slot must use one saved original PNG consistently across exported objects, with the same direct image-to-Principled requirements as the single-material bridge. Outputs are `cube.slot0.png`, `cube.slot1.png`, source JSON and independently validated GLB. Bind-pose, unknown-weight, modifier, UV and native-reference exclusion rules remain in force. The runtime descriptors name these separate PNGs and map primitive indices to native slots.

See [multi-primitive tests](multi-primitive-tests/README.md) for original synthetic tests, actual loader/transaction tests with Unity boundaries, and the local pre-change single-primitive byte regression. This is an offline implementation candidate until owned live cubeA and clone-lifetime checks pass.
