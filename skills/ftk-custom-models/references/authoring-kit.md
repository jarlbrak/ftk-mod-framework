## Resolve an exact authoring kit

Part of the [ftk-custom-models skill](../SKILL.md). Read that entry point first: it
owns route selection, the working method, and the non-negotiables that apply here.

Before opening Blender for an existing queue route, resolve its topology into
the exact renderer, bind, and rig inputs for the current game build:

```sh
python3 tools/ai-model-pipeline/plan_model_authoring_kit.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/my-isolated-game \
  --topology-group EXACT_TOPOLOGY_GROUP \
  --route-kind directEnemy \
  --output scratch/my-authoring-kit.json
```

Use `--all-routes` instead of the topology and route arguments to generate the
complete current catalog. The planner pins the native asset, queue, readiness
ledger, renderer inventory, and 230-profile Blender bridge audit. For each
renderer belonging to the selected topology, it records the exact topology,
bind, and rig fingerprints, existing ignored reference hashes when present, and
fresh extraction and Blender-scaffold commands under a `NEW_LABEL` workspace.
When the complete profile uses skinned renderers from other topology groups,
`integration.companionRigTargets` records their exact bind variants and the same
authoring inputs. Rigid `MeshRenderer` assignments remain structural profile
data because they do not use a skinned rig scaffold.
Conditional skinned equipment uses `integration.apparelRigTargets`. Apparel
targets resolve `expectedNativeMeshName` to one exact inventory renderer and
carry a separate bind palette because body or hair rigs cannot substitute for
equipment binds.
An existing reference counts only when its renderer ID, mesh name, ordered bone
names, and joint count match the current inventory.
Replace that label and use a new ignored directory before running either
command. A passing bridge representative proves the scaffold/export mechanics
for the exact rig profile only; it does not approve authored art or live use.
The legacy Kraken target remains adapter-bound even though its old rig can be
scaffolded. Its canonical archive is a validated exception path, not permission
to generate a generic resource profile.

After generating or refreshing the complete catalog, verify every referenced
local extract against a fresh decode from its pinned native asset:

```sh
scratch/model-venv/bin/python \
  tools/ai-model-pipeline/verify_model_authoring_references.py \
  --catalog scratch/model-authoring-kit-catalog.json \
  --output scratch/model-authoring-reference-verification.json
```

The verifier compares every NPZ array and the complete skeleton JSON for every
unique primary, companion, or apparel rig renderer. A `PASS` proves current-native
reference equality only. It is not original-art, export, runtime, motion,
gameplay, or visual acceptance.

Each stageable kit also includes a schema-valid integration profile starter.
It preserves the exact base enemy or class, resource or skinset identity,
combat fingerprint, renderer paths, renderer kinds, material-slot structure,
and apparel mesh guards from the selected working route. It replaces the key,
display name, GLB names, and texture names with safe original-asset starters.
Review every field listed under `copiedOptionalSettingsRequiringReview`, replace
`PACKAGE_DIR` and `NEW_LABEL`, use the primary and companion targets for all
skinned paths listed in `completeProfileRendererPaths`, and use every apparel
target for its conditional equipment branch before running the exact preflight
and stage commands. The Kraken adapter route intentionally has no generic
profile starter.

When a required equipment rig is outside the main 230-profile character bridge
audit, run the same bridge audit against its exact roundtrip references and pass
the new report to the planner. The current Blacksmith apparel set is reproduced
with:

```sh
blender --background --factory-startup --python-exit-code 1 \
  --python tools/ai-model-pipeline/audit_blender_bridge.py -- \
  --audit-dir scratch/all-raw-rig-audit \
  --output-dir scratch/blender-apparel-rigs-v1 \
  --python-executable scratch/model-venv/bin/python \
  --renderer-id 121113 --renderer-id 121211 --renderer-id 121248

python3 tools/ai-model-pipeline/plan_model_authoring_kit.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/my-isolated-game \
  --supplemental-blender-audit scratch/blender-apparel-rigs-v1/bridge-audit.json \
  --all-routes \
  --output scratch/model-authoring-kit-catalog.json
```

The planner rejects a supplemental audit from another native source build or a
duplicate rig profile.

Turn one stageable catalog route into a new original-asset package workspace:

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

The command validates every pinned catalog input, current source profile,
isolated catalog, and local reference before creating one new direct child of
`art-experiments/`. It checks the key, display name, and generated asset names
against current isolated catalogs. The new directory contains only
`runtime-profile.json`, `authoring-plan.json`, and `README.md`; it never creates
placeholder GLBs or textures. Follow the primary, companion, and apparel
authoring targets in the plan, create every listed asset from original work, and
then run the customized preflight and isolated staging commands. Existing
destinations and adapter-bound routes are rejected.
