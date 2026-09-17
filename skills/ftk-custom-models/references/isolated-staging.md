## Pinned isolated profile transaction

Part of the [ftk-custom-models skill](../SKILL.md). Read that entry point first: it
owns route selection, the working method, and the non-negotiables that apply here.

For a direct serialized enemy row, first run the static route preflight. It
pins the exact `baseEnemy`, renderer path, combat profile, and native source
ID before any staging work:

```sh
python3 tools/ai-model-pipeline/validate_custom_model_profile_route.py \
  --profile art-experiments/my-model/runtime-profile.json \
  --asset-dir art-experiments/my-model \
  --output art-experiments/my-model/route-preflight.json
```

It is read-only and does not establish runtime acceptance. A resource-prefab
override must not pass through this direct-enemy preflight.

When that profile declares a rigid `MeshRenderer` child, first create its
fresh static inventory and add
`--static-inventory scratch/static-renderer-inventory.json` to this command.
The preflight rejects a non-strict child rather than rerouting it through the
skinned contract.

For a ResourceManager prefab override, use its matching static preflight. It
pins the exact resource load path, base chassis, renderer path, controller/rig
fingerprint, and source renderer ID before staging:

```sh
python3 tools/ai-model-pipeline/validate_resource_model_profile_route.py \
  --preflight scratch/resource-enemy-base-preflight.json \
  --profile art-experiments/my-model/runtime-profile.json \
  --asset-dir art-experiments/my-model \
  --output art-experiments/my-model/route-preflight.json
```

It accepts only one preflighted resource renderer per profile. The result is
still a source-route check, never a runtime, visual, motion, gameplay, or art
acceptance claim.

### Give low-health fixtures a nonlethal observation window

When the native base enemy would die from the required ordinary no-focus hit,
add an optional test-only integer such as `"minimumBaseHealth": 64` to the
new custom profile before its static preflight. Registration applies `Math.Max`
only to that isolated custom clone, so a higher native base value is preserved.
Native scaling can still change the spawned HP; record the actual before/after
values rather than assuming the floor makes every hit nonlethal. It is not a
balance change or a runtime health setter.

If a profile was already deployed, do not edit its active catalog row. Preserve
the old preflight and deployment receipt, create a newly pinned preflight for
the changed profile, then use a reviewed catalog migration or a fresh profile
key and restart the isolated game. The normal stage path accepts an existing row
only when it is byte-identical. A deliberate migration uses
`--replace-existing-profile` for exactly one row, pins both canonical row hashes
in its receipt, and still requires the isolated game to be stopped.

[Lichenfang Prowler](../../../art-experiments/lichenfang-basey-wolf/README.md) is a
reusable original-art example for this exact route type. Its `enbaseywolf`
package contains deterministic source geometry, a proof rerun, the route
preflight, pinned isolated deployment, and a scoped live record through native
idle, attack, nonlethal hit, fixture death, and Ready. Its result remains
limited to the exact `enbaseywolf/Wolfie` pair; repeat the complete sequence for
every new resource prefab.

[Sablevine Serpent](../../../art-experiments/sablevine-basey-snake/README.md) is
the paired long-chain example for `enbaseysnake`: it explicitly covers all 44
body/tongue joints with closed volumes. Its immutable V1 archive records a
camera-fit rejection. The fresh V2 profile reuses the exact GLB and palette at
`visualScale: 0.55`, measures the spawned CEL scale in registration, binding,
and every capture frame, and accepts scoped normal combat-camera fit. Preserve
both revisions and create equivalent evidence for every new snake-controller
package.

[Rivenquill Cockatrice](../../../art-experiments/rivenquill-basey-cockatrice/README.md)
is the paired resource-prefab example. Its independently archived
[boss route](../../../art-experiments/rivenquill-basey-cockatrice/live-validation-v1-boss/README.md)
and [small route](../../../art-experiments/rivenquill-basey-cockatrice/live-validation-v1-small/README.md)
share an authored GLB, palette, and 50-bone bind signature, but measure native
CEL-root scales of `0.9` and `0.35` respectively. A matching skeleton never
transfers a resource selection, native scale, controller behavior, capture, or
acceptance result to a sibling source pair.

For a player skinset avatar, use the player route preflight before staging. It
requires the complete exact body/hair renderer set for the chosen serialized
skinset avatar and pins the rig classification plus both native source assets:

```sh
python3 tools/ai-model-pipeline/validate_player_model_profile_route.py \
  --classification scratch/rig-candidate-classification.json \
  --profile art-experiments/my-model/runtime-profile.json \
  --asset-dir art-experiments/my-model \
  --output art-experiments/my-model/route-preflight.json
```

It validates conditional apparel only as declared assets and schema. Native
equipment branches, class registration, preview, combat, lifecycle, and visual
review remain separate live checks.

After offline validation, stage each original direct-enemy or resource-prefab
profile with the generic transaction. Pin the route preflight with the geometry
proof so the isolated receipt carries both its source identity and authored-asset
evidence. The isolated game and the new stage output must be direct children of
the active repository's `scratch/` directory:

For routes selected from the execution queue, first run this read-only comparison
while the isolated game is stopped:

```sh
python3 tools/ai-model-pipeline/audit_model_validation_stage_readiness.py \
  --game-root scratch/my-isolated-game \
  --output-json scratch/model-validation-stage-readiness.json \
  --output-markdown scratch/MODEL-VALIDATION-STAGE-READINESS.md \
  --overwrite
```

It tells you whether the exact profile and declared assets are already present,
can be appended, or require an explicit isolated catalog migration. It does not
select among historical variants, change files, or prove a live result.
When several historical revisions are stage-ready, the audit uses
[`model-validation-profile-selections.json`](../../../docs/model-validation-profile-selections.json)
to pin the current intended document and keeps every alternative visible. A
changed selection invalidates the stage-readiness report. If no pinned selection
exists, pass `--profile-document` to the route runner explicitly.

For an enemy or resource-prefab route whose next action is
`stage_ready_revision_available`, create a dry pinned route plan before launch:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_execution_queue_route.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/my-isolated-game \
  --topology-group EXACT_TOPOLOGY_GROUP \
  --route-kind directEnemy
```

It verifies the current execution queue and stage ledger, profile document,
isolated catalog, assets, and every selected source assignment. Supply
`--profile-document` if several stage-ready historical documents remain and the
selection ledger has no exact current choice. Supply
`--motion-renderer-path` for a route with several selected skinned parts. A
player skinset stays on the native-preview workflow; an adapter-or-retarget
route needs its explicit design before it can enter this runner.

To produce exact commands for the whole unfinished queue after every route has
a selected revision and motion renderer, generate a read-only campaign plan:

```sh
python3 tools/ai-model-pipeline/plan_model_validation_campaign.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/my-isolated-game \
  --output scratch/model-validation-campaign.json
```

The campaign validates every route through the one-route planner and separates
ordinary enemy launch commands, once-only passive-arrival workflows, native
player workflows, and adapter implementation tasks. Execute only one enemy
route at a time. Its command is still a bounded trial, so manual image review
and a fresh immutable archive remain required.

Regenerate the campaign after each ordinary enemy run. If its proposed immutable
record exists, the planner validates that record against the current topology,
route, profile, catalog, assets, source assignments, and motion renderer. A
matching completed exercise advances to an exact review-template command, a
pending review remains pending, and a reviewed result advances to archive
planning. Invalid, stale, partial, and errored records require inspection. Never
overwrite them or infer permission to repeat uncertain game actions.

For the known five-bone `enkrakenhead` exception, use the exact
[production adapter contract](../../../docs/evidence/kraken-production-adapter-design-v1/README.md)
and its machine-readable gates. It preserves native CEL and weapon-controller
authority while a separate event-disabled sampler maps modern endpoints to the
old palette. The generic profile scaffolder remains invalid for this route even
though it can scaffold the five-bone source. Do not create a generic profile,
reuse `kraken2` direct-enemy evidence, or transfer any modern Kraken conclusion
to renderer 121260.

For `enkrakenhead`, follow the runtime helper's
[production observer campaign](../../../tools/ai-model-pipeline/runtime-test/README.md#production-gloamfin-kraken-observer-campaign).
Stage each role with `run_case.py new-run`, then use
`run_kraken_production_campaign.py run`. Capture `native-combat-death` with the
single 324-health Kraken and fortified hero. Relaunch for a fresh helper session,
then capture `enemy-victory-terminal` with native `ogreA` as the companion and
`--skip-fortify`. Only the selected Gloamfin renderer owner may bind custom
assets. Pin both reports to one verified framework/helper/content deployment
receipt and use `run_kraken_production_campaign.py assemble` for the offline
campaign.

Failed native proficiency rolls remain valid failed attempts, but each of the
four proficiencies needs at least one successful window. Treat
`RespondToDodge` as a damage-phase callback; only an actual Dodge response earns
DODGE. Preserve unused incoming attack calculations while requiring every
applied hit to match one earlier calculation in order. Archive a passing campaign
with `archive_kraken_production_campaign.py` and verify it independently with
`verify_kraken_production_archive.py`.

The current observer archive is
[`kraken-production-adapter-v1`](../../../docs/evidence/kraken-production-adapter-v1/README.md).
It proves exact production binding, native behavior, all four proficiencies,
ordinary lethal DEATH, ordinary party-loss VICTORY, and natural teardown in two
fresh owners. The later
[canonical route archive](../../../art-experiments/gloamfin-kraken/live-validation-v2-canonical/README.md)
combines that authority with reviewed appearance, idle, attack, ordinary hit,
fixture death, sampled camera/culling, Ready0/2 and a fresh native portrait.
The route is covered; keep the generic-profile exclusion and exact-source scope.

For a legacy Kraken portrait follow-up, arm passive `portrait-watch` before
encounter creation and export the exact finalized 204x172 native texture with
`portrait-texture-capture`. The framework fallback is eligible only for the
exact `enkrakenhead` resource, a synthetic custom row without an explicit
portrait marker, and an actually leased explicit custom mesh at unit renderer
world scale. It derives aspect from the render target and may uniformly reduce
only the disposable portrait clone within 10%-100%. It must not change the
source/live CEL, bones, camera, clip planes, shared prefab or combat model.
Preserve the first-snapshot trace, active UI texture users, framework/helper
hashes, deployment receipts and reviewed PNG. A clean offline framing test is
not pixel acceptance.

For a `playerSkinset` route, write the equivalent current native-preview plan:

```sh
python3 tools/ai-model-pipeline/runtime-test/plan_execution_queue_player_route.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/my-isolated-game \
  --topology-group EXACT_TOPOLOGY_GROUP \
  --output scratch/model-route-EXACT_TOPOLOGY_GROUP-playerskinset-plan-EXACT_PLAN_ID.json
```

Use the exact output path and command printed by the current validation
campaign. Its 12-hex plan ID is derived from the complete expected player plan,
so an unrelated queue or readiness change produces a new immutable path while
the older plan remains historical. The output must be a new JSON file directly
under `scratch/`. Regenerating the campaign validates that plan byte-for-byte,
removes the creation command when it matches, and stops for inspection when an
existing file differs. A current plan pins inputs only; the native preview,
combat, equipment, lifetime, motion, review, and archive work remains pending.

It verifies the exact player catalog row, required renderers, conditional
apparel, and source assets without launching FTK or selecting a class. Follow
the native Party Select, combat, equipment, lifetime, and archive procedure
afterward; a preview plan does not establish those observations.

After the dry plan is current and no FTK session is running, add `--run` and a
new `--output scratch/my-route-run.json`. The runner owns one isolated process,
refuses stale inputs and a busy bridge port, records the binding stage plus one
bounded exercise, then stops only that process. The record leaves manual visual
review and an immutable archive pending and is never canonical-route credit.

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

Stop the isolated game before the dry deploy and keep it stopped through the
explicit `--execute` command. The receipt pins the source and candidate
catalogs, every current and staged model hash, and the authoring files. The
deployer creates a local backup and refuses a running game, symlinks, stale
catalogs, or model-directory drift. Do not deploy this transaction to Steam.

When stage readiness reports a changed existing catalog row, make a separate,
one-profile migration stage rather than overwriting an ordinary stage:

```sh
python3 tools/ai-model-pipeline/stage_custom_model_profile.py \
  --game-root scratch/my-isolated-game \
  --profile art-experiments/my-model/runtime-profile.json \
  --asset-dir art-experiments/my-model \
  --replace-existing-profile \
  --output scratch/my-model-profile-migration
```

Review and deploy that stage with the same stopped-game commands. Its receipt
pins the old and new canonical row hashes and rejects a catalog whose other
pre-existing rows have drifted.

When the framework, runtime helper, or content plugin changes, deploy those binaries with
the same stopped-game discipline instead of copying them by hand:

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

The first command is a dry review. Each binary flag is optional, but at least
one must be supplied. The receipt pins only the selected framework/helper/content
bytes and their backup; rerun profile registration and live capture after a
binary change.

For a custom-class player profile, pass `--catalog-kind player` to both
commands and pin the player route preflight alongside the source proof. Its
document validates against `player-profiles.schema.json`, and both required body
and optional conditional apparel assets are pinned in the same models directory.
A staged player profile still needs its own preview, combat-clone,
equipment-rebuild, motion, lifetime, and art checks.

For an already declared profile, use a new stage and
`--replace-existing-assets` only when deliberately replacing a declared GLB or
PNG. The profile document must exactly match the current catalog row; the stage
records the old and new asset hashes and the deployer rechecks the old hash
before replacement. Revalidate the asset and run a fresh live trial. Do not use
this option to change a profile row or replace another model's asset.
