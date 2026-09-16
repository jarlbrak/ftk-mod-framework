# Authoring and validating an FTK enemy model

This workflow turns original art into a replacement mesh that uses an existing
FTK enemy's animations. Start with a single chassis and a single finished
creature. Reuse the tooling across rigs, but earn compatibility evidence for
each rig separately. See [integration paths](CUSTOM-MODELS.md),
[export commands](../tools/ai-model-pipeline/README.md), and the
[skeleton register](MODEL-SKELETONS.md).

## Choose and identify the chassis

Record the game build, asset filename and its SHA-256, renderer path ID,
renderer hierarchy, mesh name, ordered joint names, parent relationships,
bind matrices, and animation controller. Store extracted reference data in
ignored scratch. IDs are local to an asset file/build, not public content IDs.
Verify the enemy DB row and its prefab association from local game data before
registering a new enemy. The runtime's renderer selection currently prefers
`enTroll01`, then falls back to a skinned renderer; a multi-renderer enemy needs
explicit inspection before treating that selection as correct. Inspect both
`SkinnedMeshRenderer` and `MeshRenderer` components. A rigid child with one
`MeshFilter` needs its own unskinned GLB in the filter's local coordinates and
an explicit `EnemyRendererMesh.ForStaticRenderer` assignment. Record the exact
path, target kind, native mesh, and material slot for every listed renderer.

If a rigid child needs placement discovery, start with original low-poly markers
in that `MeshFilter` local space. Use the exact path, permitted transform
metadata, your own authored landmarks, and live visibility results. Never derive
its geometry or placement from native static vertices, bounds, normals, UVs,
texture pixels, weights, or animation samples. Keep the probe as diagnostic
evidence. A result showing that an offset is occluded or overwhelming is useful
for the next original asset but is not visual acceptance.

For a direct-enemy profile with a rigid child, generate a fresh
`scratch/static-renderer-inventory.json` with
`inventory_static_renderers.py` from the same `resources.assets` file pinned by
the enemy mapping. Pass that inventory to
`validate_custom_model_profile_route.py --static-inventory ...` before staging.
The checker accepts only one `MeshFilter`, one usable native material slot, and
no co-located `SkinnedMeshRenderer`; it requires a linked exact skinned
assignment in the same profile for the controller/motion contract. The
[Abyssal Kraken V4 route preflight](../art-experiments/abyssal-kraken/route-preflight-v4-head-static.json)
shows the resulting skinned-head plus rigid-eye record.

Two meshes sharing bone names are not automatically interchangeable. Compare
hierarchy, bind/rest transforms, coordinate space, and animation controller.
Texture variants may share a profile; different proportions or bind transforms
may need separate profiles even when the bone names match.

## Design, model, and bind

Create a concept that resolves silhouette, proportions, surface materials, and
recognizable features at FTK's combat-camera distance. Inspect the actual rig's
range of motion before committing to long ornaments, oversized hands, or a
rigid torso. A reference image is art direction, not generated geometry.

Model in Blender, manually or with reproducible Python. Blender MCP can provide
interactive control when available; it is not a dependency of this pipeline.
No external paid generation service is required by the tools. If another
service is selected, check its terms and costs before using it.

Fit original geometry to the native mesh-local bind pose. For Mirewarden,
separate stone components use explicit single-bone tags, allowing articulation
without rubbery stone deformation. Organic creatures usually need blended
weights. Nearest-surface weight transfer is a starting point only when anatomy
and alignment agree. Do not weight a whole creature to one bone and count that
as full animation support. Keep presentation poses separate from export source.

## Export and validate

Use `extract_reference.py` with an explicit renderer ID and separate output
folder for each profile. `export_ftk_glb.py` writes the custom runtime contract;
`validate_glb.py` independently rereads the output. The tool guide contains the
commands and input schema. An ordinary Blender glTF export is not interchangeable.

Check positions/normals in Unity mesh-local space, signed triangle-normal
agreement, split vertices for flat normals/UV seams, normalized nonnegative
weights, joint-name mapping, column-major inverse bind matrices, and the
runtime's vertex/index limits. The loader flips V, so its input uses top-origin
V; native extracted UVs need conversion if used as roundtrip test input.

The documented Blender X-right/Z-up/-Y-front conversion `(x,z,-y)` preserves
orientation. Reversing the resulting triangles made the first Mirewarden
render inside-out. Compare against the selected native reference rather than
relying on a clockwise/counterclockwise label. The extractor also repairs a
verified UnityPy 1.25.3 negative fourth-weight residual; this is decoder repair,
not permission to silently fix arbitrary malformed input.

## Select the exact integration route

The exact source owner determines the profile route. A serialized enemy row uses
`validate_custom_model_profile_route.py`; a ResourceManager prefab override uses
`validate_resource_model_profile_route.py` and must declare its own
`resourcePrefab`. A player avatar uses the skinset-specific player preflight and
has separate preview and equipment checks. Do not route a resource prefab through
the direct enemy checker or treat matching bone names as evidence that one route
covers another.

For a resource-prefab artwork package, preserve the resource load path, base
chassis, CEL-relative renderer path, source renderer ID, rig/controller
fingerprint, original declared assets, route-preflight record, geometry proof,
and staged deployment receipt together. [Lichenfang Prowler](../art-experiments/lichenfang-basey-wolf/README.md)
shows that package shape for `enbaseywolf/Wolfie`: it has reproducible original
33-bone geometry, a pinned isolated deployment, and a scoped native live archive.
Copy the process for a fresh exact resource route; never transfer its binding or
trial result to direct `wolfA` or another resource prefab.

[Sablevine Serpent](../art-experiments/sablevine-basey-snake/README.md) is the
companion long-chain example for the separate `enbaseysnake` resource rig. Its
body, closed dorsal vanes and eight-joint tongue each follow the exact declared
palette instead of placing a decorative root-rigid plane over a snake. Its V1
archive retains a camera-fit rejection. Its fresh V2 profile keeps the same
original bytes, uses `visualScale: 0.55`, measures the spawned CEL scale in
registration and every capture frame, and accepts limited normal combat-camera
fit. Keep both records when correcting a profile.

[Rivenquill Cockatrice](../art-experiments/rivenquill-basey-cockatrice/README.md)
shows why equal bind data does not make two resource routes interchangeable.
Its `enbaseycockatriceboss` / `bossCockatrice` archive records a native CEL-root
scale of `0.9`; its independently trialled
`enbaseycockatricesmall` / `cockatriceC` archive records `0.35`. Both use the
same authored assets and 50-bone bind signature. Preserve separate profiles,
bindings, captures, visual reviews, and acceptance scopes for each source pair.

## Integrate and test

Register a single skinned body through `Content.SetEnemyBodyMeshFromGlb`.
For a multipart plan, use `Content.SetEnemyBodyMeshesFromGlb` with every exact
skinned or rigid assignment documented in the [renderer API](MODEL-RENDERER-API.md).
Confirm the body's visual settings allow the selected renderer to remain visible.
In the current loader, neutral tint still receives texture-driven emission, so
compare materials under the actual diorama lighting. A missing file or fallback
body is a failed asset test even if the encounter continues normally.

Use an isolated game copy and disposable single-player run, with a distinct
bridge port if another task is testing FTK. Verify the deployed DLL/model hashes,
launch variables, and startup visual configuration before starting a run.
Separate save files do not necessarily isolate PlayerPrefs or platform state.
Do not copy a whole old save backup over newer active saves.

When the framework, test helper, or content plugin changed, use
`tools/ai-model-pipeline/deploy_isolated_test_binaries.py` for a dry review and
then an explicit `--execute` deployment while that isolated game is stopped. It
pins the source and replacement bytes under the isolated game's backup directory
and never needs a manual plugin copy. Supply any of `--framework`, `--helper`,
and `--content`, with at least one required. Re-run registration after the
binary deployment.

If an exact native chassis would die from the ordinary no-focus test hit, a new
isolated profile may set `minimumBaseHealth` to an integer from 1 through 1000.
Registration applies `Math.Max` only to the custom clone before spawn, preserving
any higher native base value. Native scaling can still change combat HP, so the
actual same-target before/after record remains the evidence. This is a test
fixture, not a balance change or a live health setter. A changed profile needs a
new route preflight and a reviewed catalog migration or fresh key. The normal
stage path keeps an active row byte-identical; a deliberate one-row migration
uses `--replace-existing-profile` and pins both canonical row hashes before the
isolated deployment.

For the Mirewarden experiment, `FTK_MIREWARDEN_BODY=1` selects the authored body;
`FTK_BASELINE_STOCK_BODY` must be unset. The default sample configuration remains
procedural. These are sample-specific test switches, not generic rig selectors.

Prefer normal encounter flow. The measured isolated-harness setup is:

1. Start the disposable run and wait for an actual nonempty, living party.
   `inSession` alone can become true before party creation has finished.
2. Apply `quiet-tutorials`, then `fortify-party` with `targetMaxHp:999`.
   Use the runner's guarded native story setup to finish the exact queued pages
   and their continuations outside the dungeon. An absent modal alone does not
   establish completion; do not substitute generic dialog dismissal.
3. Call the bridge's `enter_dungeon`. Immediately after its successful result,
   call helper `stage-enemy` with
   `{"enemy":"EXACT_CUSTOM_ROW_KEY","level":0,"room":1,"regenerate":true}`.
   Do not wait for dungeon dialogs or camera flow between these calls.
4. Stop on any rejected operation and inspect the state/log before continuing.
   After staging succeeds, let native flow spawn the selected single enemy;
   the measured sequence requires no `dungeon_encounter` call.
5. Verify the exact custom row, production mesh assignment, and native combat
   readiness before capturing motion or issuing combat actions.

This historical sequence is not yet reliable for every fresh HollowMire run.
The [Reefstrider setup failure](../art-experiments/reefstrider-fish/setup-failure-v1/README.md)
shows that initial story completion can be followed by a newly triggered quest
message during dungeon entry, before any combatant exists. Both the ordinary
runner and interrupted-start continuation now stop at `pending_story_message`
without inventory or binding acceptance. Preserve that result and diagnose the
native quest transition outside the dungeon; longer quiet waits and repeated
staging are not verified repairs. The pre-entry native quest-completion gate
completed two fresh startup trials with one discovery submission, its callback,
and verified quest/destination readiness. In both trials, both story chains had
already finished before positioning/discovery; these observations do not prove
the gate repairs the late-story failure. Preserve that causal limit in new
reports. See the [second trial](evidence/entry-preparation-native-v1/second-trial/validation.json).

Waiting for dialogs after entry but before staging caused a race with native
encounter startup. Sending dungeon acknowledgments early also caused missing
dummies and aborted initiative construction; additional acknowledgments did
not recover it. An empty initiative list alone does not identify the wait
condition: inspect dialogs, camera state, and logs. See the
[runtime helper guide](../tools/ai-model-pipeline/runtime-test/README.md) for
command transport and fixture semantics, and the
[test-content guide](../tools/ai-model-pipeline/runtime-test-content/README.md)
for registered custom row IDs. The HP and tutorial changes are disposable
fixture settings, not production content or representative game balance.

The test used one hero to avoid a separately observed custom-realm party
association issue. This does not establish multiplayer or full-adventure
compatibility. Scratch helpers for selecting the boss room and replaying
animations were test aids; they are not shipped APIs.

## Evidence and completion

For each profile, record these independently:

| Check | Required evidence |
|---|---|
| Export | Source/tool versions, hashes, binary/skin validation report |
| Runtime binding | Correct renderer and mesh, expected joints, zero unintended dropped joints, no fallback |
| Appearance | Live combat screenshot at intended scale, palette/material review |
| Idle | Visible articulation and stable bounds over time |
| Attack | Native attack motion, shoulders/wrists/extremities remain attached and in bounds |
| Hit | Light/heavy response as applicable, no spikes or objectionable clipping |
| Death | Applicable death pose, no detached parts or incorrect culling |
| Gameplay | Normal attack resolves, damage occurs, turn order resumes |

Direct Animator playback can inspect geometry through a native clip, but label
it as playback. It does not prove attack events or gameplay logic work. Exact
state paths differ from trigger and clip names: the tested troll controller uses
`Base Layer.ATTACK`, while the trigger is `Attack` and the clip is `attack_troll`.
Discover the controller for other rigs instead of copying those names. For a
death branch with state-dependent callbacks, capture the native CEL/owner
trigger and its reported trigger state separately from `Animator.Play`. If the
native death uses `FallOffLimb`, enable a custom-body policy only after the
explicit mesh lease owns that exact renderer, then compare the normal-death
fixture with a same-binary policy-omitted control and record native cleanup
separately.

A passing export/build does not imply passing motion checks. Keep missing checks
pending and repeat visual tests when geometry, skinning, materials, or the
chassis changes. Co-op parity and complete adventure progression are separate
integration gates when those are in scope.

Deliver editable source, original GLB/PNG, rebuild commands, a SHA-256 manifest,
and selected live evidence. Regenerate the package after the final edits and
verify the enclosed bytes against that manifest. Never ship extracted reference
meshes, game DLLs, or test helpers as part of the original asset package.

After registering a new original archive, regenerate the machine-readable
[validation evidence ledger](MODEL-VALIDATION-GATES.md). It reads only explicit
structured records, so a missing cell means "not recorded by this adapter," not
that the behavior failed. Do not substitute it for human review or a final gate
verdict:

```sh
python3 tools/ai-model-pipeline/audit_model_validation_gates.py \
  --output-json docs/model-validation-gates.json \
  --output-markdown docs/MODEL-VALIDATION-GATES.md \
  --overwrite --fail-on-unresolved --fail-on-integrity
```

When adding a new live archive, retain explicit capture labels or clip names for
idle, attack, hit, and death, explicit numeric ordinary no-focus HP transitions,
and a structured `finalReady`/`ready` result. This lets the ledger expose what
was actually recorded without reading a prose status string.

Regenerate the repository-wide preservation ledger before candidate coverage.
It checks immutable archive artifacts only and keeps historical gaps visible
without relabeling them as model failures:

```sh
python3 tools/ai-model-pipeline/audit_model_validation_archive_integrity.py \
  --overwrite --check-video-metadata
```

Regenerate the candidate-coverage report after the gate ledger and current
preservation ledger. It keeps direct enemy rows, resource-prefab overrides and
player skinsets separate, and names the closest source-specific archive for
every remaining route. A canonical route representative is one archive for a
single exact source identity with every required structured field, exactly the
complete renderer set for that identity within one topology group, and a
verified immutable artifact. This accepts complete multipart sources while
rejecting partial, mixed-identity, cross-topology, duplicate, or extra
assignments. It does not combine fields from older archives, transfer a result
to an unrecorded source pair, or approve the art:

```sh
python3 tools/ai-model-pipeline/audit_model_candidate_coverage.py \
  --output-json docs/model-candidate-validation-coverage.json \
  --output-markdown docs/MODEL-CANDIDATE-VALIDATION-COVERAGE.md \
  --overwrite --fail-on-unmapped
```

Use the report's priority route only as a work queue. Inspect the linked
archive's acceptance scope and retain all of its stated limits before selecting
the fresh isolated trial.

Generate the execution queue next. It resolves that one priority archive target
to an exact static-passing profile document, or exposes the missing profile as
an authoring task. An explicit resource ownership finding can instead require an
adapter or retarget design. For an unindexed route it selects one remaining
exact topology representative. It never turns a static preflight match into
live evidence:

```sh
python3 tools/ai-model-pipeline/audit_model_validation_execution_queue.py \
  --output-json docs/model-validation-execution-queue.json \
  --output-markdown docs/MODEL-VALIDATION-EXECUTION-QUEUE.md \
  --overwrite
```

Read the selected target, profile document, and profile revision together.
Enemy matches require the exact route identity and renderer ID/path; player
matches also require the named profile and skinset. If the queue lists more than
one historical profile document, choose one explicitly and give that revision
its own isolated trial and immutable archive. A target without a profile is a
planning gap, not evidence of a failed game route.

Before staging the selected revision, compare it with the stopped isolated test
catalog and models directory:

```sh
python3 tools/ai-model-pipeline/audit_model_validation_stage_readiness.py \
  --game-root scratch/my-isolated-game \
  --output-json scratch/model-validation-stage-readiness.json \
  --output-markdown scratch/MODEL-VALIDATION-STAGE-READINESS.md \
  --overwrite
```

This read-only check distinguishes an already-present profile, an ordinary
append or asset stage, and a deliberately pinned one-row catalog migration. It
does not choose among historical variants or establish a live result.
The accompanying [profile selection ledger](model-validation-profile-selections.json)
pins the intended current document for routes with several stage-ready
historical alternatives. The readiness report verifies the exact document hash,
retains every alternative, and becomes stale if the selection ledger changes.

When the report marks the selected enemy or resource-prefab revision
`stage_ready_revision_available`, make a dry route plan before launching the
isolated game:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_execution_queue_route.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/my-isolated-game \
  --topology-group EXACT_TOPOLOGY_GROUP \
  --route-kind directEnemy
```

The plan pins the current queue, stage-readiness report, exact profile document,
isolated catalog, declared assets, and every selected native source assignment.
It chooses a stage-ready revision only when that revision is unique or selected
by the hash-pinned profile selection ledger. Pass `--profile-document` for a
route with several historical revisions and no current selection, and
`--motion-renderer-path` when a
multipart route has several selected skinned renderers. Player skinsets use the
separate native-preview workflow; a route marked adapter-or-retarget-required
remains a design task.

Once every current route has an exact selected revision and motion renderer,
generate a complete read-only campaign plan:

```sh
python3 tools/ai-model-pipeline/plan_model_validation_campaign.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/my-isolated-game \
  --output scratch/model-validation-campaign.json
```

It reuses the same strict route validation and emits exact dry and bounded live
commands for ordinary enemy routes, once-only passive-arrival templates for
self-removing enemies, read-only native-preview plans for player routes, and
explicit adapter work for incompatible routes. It does not launch FTK or combine
several routes into one evidence claim.

Regenerate this plan after an ordinary enemy run. An existing proposed runner
record is validated against the current route, profile, catalog, assets,
assignments, and motion renderer. A matching completed exercise advances to a
review-template command. A pending review stays pending, and a reviewed result
advances to archive planning. Invalid, mismatched, partial, or errored records
stop for inspection and never become an automatic retry.

The current `enkrakenhead` exception has a concrete
[production adapter contract](evidence/kraken-production-adapter-design-v1/README.md)
with a machine-readable endpoint map, native-controller boundary, numerical
guards, lifecycle rules, and acceptance gates. Its dedicated internal adapter
is implemented and approved offline, but that does not make the route generically
stageable or live-accepted. Keep the queue on adapter validation and omit a
generic profile until fresh isolated evidence and an immutable archive satisfy
that contract.

For this resource-prefab exception, use the
[five-run passive observer campaign](../tools/ai-model-pipeline/runtime-test/README.md#production-gloamfin-kraken-observer-campaign).
Every run uses a fresh real owner and the same verified framework, helper, and
content deployment receipt. The aggregate verifier covers controller, callback,
sampler, and lifetime evidence; visual, endpoint-composition, portrait/culling,
progression, and archive review remain distinct acceptance records.

To resolve any queued topology into reproducible local extraction and Blender
inputs, generate one exact authoring kit:

```sh
python3 tools/ai-model-pipeline/plan_model_authoring_kit.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/my-isolated-game \
  --topology-group EXACT_TOPOLOGY_GROUP \
  --route-kind directEnemy \
  --output scratch/my-authoring-kit.json
```

Use `--all-routes` for a consolidated catalog. Each target pins its native
renderer ID, topology, bind, rig profile, controller names, existing local
reference hashes, and the passing Blender bridge representative. Its fresh
commands write only beneath a replaceable `scratch/model-authoring-NEW_LABEL/`
workspace. Existing references must match the inventory's renderer ID, mesh,
ordered bone names, and joint count. This resolves authoring inputs and
mechanics; route preflight,
deployment, live behavior, visual review, and archive acceptance remain
separate.
The selected topology stays in `targets`. Exact skinned bind variants used by
the rest of the complete profile appear under
`integration.companionRigTargets`, including multipart enemy and player body or
hair assignments. Rigid `MeshRenderer` entries stay in the profile as
structural assignments and do not receive rig scaffolds.
Conditional skinned equipment appears separately under
`integration.apparelRigTargets`. Each target resolves the profile's
`expectedNativeMeshName` to one exact inventory renderer and preserves its own
bind palette.

Verify all catalog references against a fresh native decode before authoring:

```sh
scratch/model-venv/bin/python \
  tools/ai-model-pipeline/verify_model_authoring_references.py \
  --catalog scratch/model-authoring-kit-catalog.json \
  --output scratch/model-authoring-reference-verification.json
```

This compares every stored NPZ array and complete skeleton JSON for each unique
primary, companion, or apparel rig renderer against the catalog-pinned native asset. It
is a local reference provenance check, not evidence for authored art or game
behavior.

For every stageable route, the kit's `integration.profileTemplate` is a valid
starter document for the correct enemy, resource, or player schema. Native
identity, combat fingerprint, renderer kinds and paths, multipart material-slot
maps, and apparel mesh guards remain exact. The starter replaces the model key,
display name, and authored asset filenames. Review its copied optional settings
and author every skinned path using its primary, companion, or apparel rig target
before running the emitted preflight and staging commands. The template passes route
semantics with asset existence deferred; it does not pass asset preflight until
the named original GLBs and textures exist.

Create a concrete package workspace from one stageable route with:

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

The scaffolder revalidates all top-level catalog pins, every route profile and
isolated-catalog pin, and every local primary, companion, or apparel reference. It refuses
an existing destination, unsafe identity, current key or display-name collision,
generated asset-name collision, or adapter-bound route. Success creates one new
`art-experiments/<slug>/` directory containing only the customized
`runtime-profile.json`, a pinned `authoring-plan.json`, and package instructions.
The plan lists the original GLBs and textures still to create, all exact rig
targets, including conditional apparel, copied optional settings requiring
review, and customized preflight and
isolated-stage commands. No placeholder asset or acceptance claim is generated.

After reviewing the plan and only when no other FTK session is running, append
`--run`, a new record path, and any package-specific launch opt-in:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_execution_queue_route.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/my-isolated-game \
  --topology-group EXACT_TOPOLOGY_GROUP \
  --route-kind directEnemy \
  --attack-attempts 8 \
  --run --output scratch/my-route-run.json
```

The runner refuses a stale pin, a running FTK process, or an occupied bridge
port. It owns and stops only the isolated process it launches, performs one
binding stage and one bounded exercise, and records the resulting binding
matches and case paths. Its record always leaves manual visual review and a new
immutable archive pending; it never grants canonical-route credit.

For a `playerSkinset` route, create the matching read-only native-preview plan:

```sh
python3 tools/ai-model-pipeline/runtime-test/plan_execution_queue_player_route.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/my-isolated-game \
  --topology-group EXACT_TOPOLOGY_GROUP \
  --output scratch/model-route-EXACT_TOPOLOGY_GROUP-playerskinset-plan-EXACT_PLAN_ID.json
```

The output is optional when inspecting stdout. For campaign execution, use the
exact output path and command printed by the current campaign. Its 12-hex plan
ID is derived from the complete expected player plan, so changed queue or
readiness inputs produce a new immutable path without overwriting the earlier
plan. A regenerated campaign validates that file exactly and reports
`native_player_plan_current` only while every pinned route input still matches.
This status does not establish any live player behavior or canonical coverage.

It pins the exact player catalog, required avatar renderers, conditional apparel,
and asset files before the documented native Party Select, combat, equipment,
and archive sequence. It does not launch FTK, select a class, or transfer
preview evidence to combat or gameplay.

For new archives, use these stable top-level fields when the observation exists:

| Evidence | Canonical field shape |
|---|---|
| Runtime binding | `binding` with renderer path, mesh, bone signature, owner, and controller where available |
| Visual review | `visualReview` with separate observed and not-accepted boundaries |
| Motion | `captures` array with labels exactly `idle`, `attack`, `nonlethal-hit`, and `kill-fixture`/`death` as applicable |
| Ordinary gameplay | `ordinaryAttack` or `ordinaryHit` with numeric `beforeHp` and `afterHp`, and `focus:false` where known |
| Fixture death | `explicitKillFixture` separate from the death capture record |
| Progression | `finalReady` or `ready` with `ok:true`, level, room, and button count |
| Player preview | `avatarOwners.preview` with an explicit `observedAvatars` count; zero is a pending state, not a preview pass |

Leave a field absent when it was not observed. Do not fill it from a prose
summary, a sibling chassis, or an unrelated focused trial.

After a local archive script completes, independently check its immutable
artifacts before registering its `validation.json` in the runtime index:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py \
  art-experiments/<package>/live-validation-vN --check-video-metadata
```

This verifies gzip-lossless metadata, pinned source and selected PNGs, and
presentation-video hashes, frame counts, and dimensions. It rejects game DLL
and resource-payload mappings. A passing integrity check does not accept a
binding, animation, gameplay transition, or visual review.

Read [MODEL-VALIDATION-ARCHIVE-INTEGRITY.md](MODEL-VALIDATION-ARCHIVE-INTEGRITY.md)
beside the validation-gate and candidate-coverage ledgers. Candidate coverage
checks the exact indexed validation hash against this ledger; a stale, absent,
or integrity-unverified artifact remains a route follow-up even if its
structured evidence fields are otherwise complete.

[Model-validation evidence archives](MODEL-VALIDATION-ARCHIVES.md) documents
the reusable enemy and player plan builders. Use
`archive_model_validation_case.py` for a fresh enemy exercise and
`archive_player_model_validation.py` for a native player-preview capture,
instead of duplicating a prior route's archive script. Their frame-identity
checks and immutable destinations give every new skeleton the same preservation
and review baseline.

## Reusable agent skill

The versioned [ftk-custom-models skill](../skills/ftk-custom-models/SKILL.md)
encodes this workflow. Install or link its directory under your Codex skills
folder. Keep it with this repository so its documentation links resolve; use
`$ftk-custom-models` in an FTK checkout. The skill does not install Blender MCP
or assume that connector is present.

### Verify anatomical facing before approving a studio render

Determine face-forward from the exact native head/jaw anatomy and joint reference,
then compare an actual combat pose before approving the authored face. Do not
assume that mesh-local plusZ or minusZ is universally forward. Cinderbloom v1
fit native bounds and passed rig checks but placed the mouth, teeth and throat
opposite the native head/jaw direction, hiding its face in combat. Correct the
local authored anatomy and its joint mapping; do not rotate the entire model or
inverse bind matrices to repair a local face error. Keep native surface analysis
in ignored scratch, and preserve each failed iteration's hashes and live captures
before retesting corrected geometry.

Cinderbloom v2 then corrected only the local authored face/jaw anatomy, preserved
native palettes and inverse binds, and passed the targeted facing check in a new
native combat capture. Both the failed v1 and successful v2 hashes/media are
retained in the [model directory](../art-experiments/cinderbloom-plant/README.md).
This establishes the value of checking actual anatomy and combat facing; it does
not turn one facing fix into full culling, animation or artistic acceptance.

### Measure scale at the native and spawned roots

The corrected public visual scale is a per-axis multiplier of the native CEL
baseline: factor1 leaves native scale unchanged, and repeated application does
not compound it. Record the requested factor, native prefab root local scale and
actual spawned CEL local scale as distinct values. Sablevine V2 establishes the
full record: factor `0.55`, native root `[1,1,1]`, and live spawned CEL root
`[0.550000011920929,0.550000011920929,0.550000011920929]` in registration,
binding, and all 360 capture frames. Arithmetic or a valid GLB alone never
accepts fitting; V2 also retained a fresh normal-combat visual review.
See [the renderer scale contract](MODEL-RENDERER-API.md#public-visual-scale-is-a-factor)
for API order and the distinction between local scale and visible world size.

The native baseline belongs to the exact source pair, including a resource
prefab. Rivenquill's paired Cockatrice archives make this concrete: the boss
resource spawned at `0.9` and the small resource spawned at `0.35` with the
same GLB, palette, and bind signature. A value observed on one prefab is not a
default for another prefab or controller source.

A narrow or absent body during death does not by itself demonstrate bad skinning. Resinmaw's visible death was limited, but raw active/scales and phase-aligned comparison against the native AcidBlobA diagnostic showed the same32-bone sink motion. Keep native-motion agreement, material visibility and artistic acceptance as separate findings; retain native FX boundaries.

When a portrait fails, inspect actual native pixels before changing geometry. Honeyback v2's reusable [tested camera profile](../art-experiments/honeyback-portrait-v2/runtime-profile-encounter-camera.json) kept the same mesh while an existing EncounterCam changed the underside closeup into a recognizable whole-bear portrait. Its earlier failed camera remains documented. Combat portrait success does not validate the distinct native encounter-row preview dimensions or initialization route.

### Preserve native theft and flee behavior during tests

A pass can let a theft/flee enemy leave before the planned hit or death action.
Preserve that removal and the stopped exercise: HP0, `alive:false` or disappearance
alone do not establish death. Check intermediate health/gold changes, observed
clips and native indicators; if flee flags were not recorded, say
"source-consistent theft/flee" rather than claiming direct flag proof. The
[Fergus pass-only diagnostic](evidence/fergus-diagnostic-pass-v1/validation.json)
retains a complete pass capture and the wrapper stopped before hit/death.

Keep native AI enabled. Use separate fresh encounters for an immediate hit-first
test and a death-first test, before an initial pass gives the enemy another turn.
Record the actual result of each independently; the hit test may still trigger
retaliation or removal. Explicit `KillSingle` is a fixture death, not ordinary
lethal damage, as preserved in the separate
[Fergus death-first diagnostic](evidence/fergus-diagnostic-death-v1/validation.json).
Neither record establishes the still-pending normal-hit test or global Fergus
haunt progression.

Between encounters, observe strict native Ready and verify that the queued slot
permits enemy substitution. A Ready Trap or other ineligible room is not permission
to force another enemy there. Preserve native loot/progression observations and
obtain a fresh eligible encounter when needed; do not disable AI or change removal
behavior to make a planned capture sequence finish.
