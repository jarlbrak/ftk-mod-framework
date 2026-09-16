---
name: ftk-custom-models
description: Author, export, inject, and validate custom For The King character models on existing game skeletons. Use for enemy meshes, player avatars, reskins, rig compatibility checks, or extending the tested skeleton catalog.
---

# FTK custom models

Deliver an original model inside FTK, with evidence of its appearance and motion.
A Blender render alone is not an integrated asset. Preserve the user's chosen
creature and scope; adapting every rig is a separate series of exercises.

## Find the project and current evidence

Use the active FTK checkout. If invoked elsewhere, resolve this skill's real
filesystem path and locate the repository containing `FTKModFramework/` and
`tools/ai-model-pipeline/`. Do not guess a Steam installation or deploy to a
second checkout's game. Read its `AGENTS.md` and `CLAUDE.md` where present.

Read [the model guide](../../docs/CUSTOM-MODELS.md) for integration and
[the authoring workflow](../../docs/MODEL-AUTHORING.md) for the actual exercise.
For a different rig, also read [the skeleton register](../../docs/MODEL-SKELETONS.md).
Enemy multipart assignments use [the renderer API](../../docs/MODEL-RENDERER-API.md).
Player avatars use [the class/skinset API](../../docs/MODEL-PLAYER-API.md);
equipment assembly and cloned-avatar lifetime need separate validation.
The exporter contract and executable commands live in
[the tools guide](../../tools/ai-model-pipeline/README.md).
Use [the topology coverage plan](../../docs/MODEL-TOPOLOGY-COVERAGE.md) and its
linked ownership finding to select the real route: direct enemy row,
resource-prefab override, or player skinset avatar. An unsupported empty
renderer is not a strict-swap candidate. Ownership resolution classifies the
inventory; it never transfers evidence between routes or approves a model.
The plan's `Original / any / known pairs` column identifies whether an exact
topology has an indexed authored example, broader evidence, and known source
pairs. It never approves an unrecorded pair or transfers a live result.
When selecting among already-authored packages, regenerate and read the
[package readiness ledger](../../docs/MODEL-PACKAGE-READINESS.md). It runs each
profile document through the current route preflight and distinguishes an exact
indexed original record from diagnostic-only evidence and a merely local
validation file. A named, pinned historical follow-up in the runtime index is
an explicitly indexed supplement, not evidence for a newer profile revision
without comparing its pinned profile/catalog hashes.
Then read the [candidate validation coverage report](../../docs/MODEL-CANDIDATE-VALIDATION-COVERAGE.md).
It joins the topology ownership plan to the structured gate and archive-
integrity ledgers and gives a strict route-specific follow-up queue. A canonical
representative is one exact-source-identity archive with all required evidence
shapes, exactly the complete renderer set for that identity within one topology
group, and an independently verified immutable artifact. Single-renderer and
multipart sources follow the same rule. It never combines fields from separate
archives, approves an unrecorded sibling pair, or makes an art-quality verdict.
Bronzewake V4 through V7 are the multipart reference: body, hair, armor and
boots each needed a separate exact-source canonical archive even though every
stage verified all four assignments on the same owner.
When one source profile binds renderer paths from different topology groups,
scope each canonical archive to the selected source renderer and topology.
Preserve companion assignments and assets as same-owner context, but grant no
route credit from their presence alone. Repeat live motion capture and root image
review with each companion renderer selected before closing its topology route.
A whole-owner archive can remain useful historical evidence while still being
noncanonical across topology boundaries. Rimecrown V4 through V8 are the
head, scarf, base, hat and middle-body references for this source-scoped
multipart rule.
Honeyback V3 is the single-renderer counterpart: one exact
`bearB / enBear01 / 121467` archive carries the complete route evidence, while
its earlier portrait-camera records remain separate presentation checks and do
not replace the canonical full-body run.
Duneshade V2 is the single-renderer ragdoll counterpart. Its exact
`snakeDesertA / enDesertSnakeA / 121552` route shows how to distinguish the
short native `Snake_DeathBig` handoff from the physical phase: prove the
animator-disable frame, per-body kinematic transition, measured movement and
settling boundary from telemetry, then use reviewed originals to judge whether
the authored body remains visually coherent. `m_DoRagdoll=true` alone is not
enough to claim observed ragdoll behavior.
Ashfang V2 is the same ragdoll method on the exact `wolfA / wolf01 / 121142`
route. Its animator remains enabled with all 14 bodies kinematic through death
frame 26, then disables as all 14 bodies become dynamic at frame 27. Measured
motion spans frames 28 through 48 and is zero through frame 119. Keep the
ordinary 58 to 50 HP hit separate from the explicit 50 to 0 KillSingle fixture,
and record that its authored main texture still inherits native `wolfA_e`
emission rather than claiming a base-color-only material.
Sargassum primary V2 is the single-renderer animated teardown counterpart. Its
exact `krakenTentacle / krakenTentacle / 121595` archive records complete idle,
enemy attack and ordinary-hit captures before an accepted 91-frame fixture
death prefix. Because `m_DoRagdoll=false`, use the observed `Tentacle_Death`
animator state and the exact renderer boundary. Frame 90 is inactive and not
visible with the animator disabled, and the next sample reports renderer
destruction. This proves sampled animated withdrawal only; it does not prove
full death duration, cleanup causality, corpse lifetime or final disposal.
Abyssal Crown V5 is the mixed skinned-and-rigid Kraken Head counterpart. Its
exact motion topology is `krakenHead / kraken2 / 121035`; the same-owner rigid
`Root_M/base/body/neck/eye/kraken2_eye` MeshRenderer is required structural
profile context and does not receive a second skinned topology or motion claim.
Archive all four authored assets and pin the stage result that proves both
assignments. Review the rigid insert across idle, attack, hit recovery and
disappearance even though motion telemetry targets only `kraken2`. The accepted
death prefix keeps `krakenDisappear` on frames 29 through 89 and retains the
same motion renderer inactive and not visible at frame 90 before the next sample
reports destruction. Because `m_DoRagdoll=false` and the source has zero
rigidbodies, describe this as animated disappearance. Do not infer cleanup
causality, corpse lifetime or final disposal from the teardown boundary.
Moonreed V2 is the single-renderer animated persistent-death counterpart. Its
exact `fairyA / enFairy01 / 121395` archive preserves four complete captures
because the first ordinary hammer attempt is a native dodge and the second is
the required 58 to 55 HP damage sample. Keep both attempts with distinct archive
keys. `m_DoRagdoll=false`, and the animator remains enabled while `fairy_die`
persists through frame 119. Two nonkinematic rigid bodies belong to inactive
break props, so they are not body ragdoll evidence. Review the small model and
strong native attack effects with explicit visibility limits, and do not turn
bounded death persistence into a general corpse-lifetime or cleanup claim.
Mirewarden V3 and Gloamcap V3 are the route-boundary pair for a topology group
shared by two integration kinds. The exact `trollCaveA / enTroll01 / 121153`
archive credits the direct-enemy route only. The separate exact
`impA / enbaseyimp / enBaseyImp / 121117` archive credits the resource-prefab
route only. Neither route transfers evidence to the other or to a sibling source.
Generate and read the [execution queue](../../docs/MODEL-VALIDATION-EXECUTION-QUEUE.md)
after candidate coverage. It starts with the candidate ledger's one priority
target, matches only an exact static-passing profile document, and lists an
unmatched target as a profile-authoring task. An explicit resource ownership
finding can instead require an adapter or retarget design. Enemy matching
requires route identity plus renderer ID/path; player matching also requires
the named profile and skinset. Choose one listed profile revision explicitly.
The queue is an execution list, never proof that a sibling profile or source
pair is covered.
Treat local source and game assets as authority over prior summaries.

## Resolve an exact authoring kit

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

## Working method

1. Choose an exact supported route and a concrete visual brief. For an enemy,
   require the direct native enemy row; for a ResourceManager override, preserve
   its separate prefab identity; for a player, require the selected native
   skinset avatar. Inspect its real renderer, bind pose, skeleton, and animation
   controller before fitting art. Do not borrow a sibling's result merely because
   the topology matches. Reuse a verified rig profile only after checking the
   local game build.
2. Develop a strong silhouette and palette with a concept reference when useful.
   Blender Python or an available Blender MCP can author the actual mesh.
   MCP is optional transport, not a quality guarantee or a required paid service.
3. Start from `create_blender_template.py` when a new rig needs an editable
   armature and original calibration mesh. Export tagged original objects with
   `export_blender_model.py`; exact commands and rejection rules are in the tools
   guide. Calibration meshes establish fit, not finished creature quality.
   Author original geometry in the reference bind pose. Use rigid bone groups
   for articulated stones/armor, or appropriate blended weights for organic
   anatomy. For wings, tails, or other long articulated anatomy, follow each
   actual native chain with closed, consistently wound panels or volumes and
   explicit transition weights; a root-rigid decorative plane is not motion
   coverage. Do not pose the exported mesh to make a studio render look better.
4. Export through the repository's FTK-specific writer and independently
   validate the result. Ordinary Blender GLB is not this loader's contract.
5. Integrate through the existing public content API. Follow the project's
   specialized-agent rules for content or framework changes. Inspect renderer
   selection on each new chassis; the troll body name is not universal.
6. Test in an isolated single-player game. Verify deployment hashes and the
   opt-in configuration before launching a run. Capture the custom mesh in
   normal combat, then inspect idle, attack, hit, and death. Distinguish direct
   Animator playback, ordinary combat, and a complete campaign playthrough.
   For an already staged enemy, the [one-case runtime runner](../../tools/ai-model-pipeline/runtime-test/README.md#one-enemy-validation-sequence)
   automates capture and loot progression with pinned identity and once-only
   actions. Its `needs_visual_review` result still requires inspecting actual
   clip poses; default selected frames can miss attack or hit moments. If a
   native controller produces a complete same-target no-loss outcome such as a
   block/protection turn, pass bounded `--attack-attempts N` (1..8); each retry
   is recorded and the gate still requires measured nonlethal HP loss. Never
   retry uncertain actions or infer the native combat cause. Preserve partial
   failures and record visual verdicts separately from raw results. After a
   completed no-focus boundary, a fresh optional `--focus` trial may record the
   bridge's strongest legitimate native hit. Keep it as a separately labelled
   supplement and preserve the original no-focus result; focused damage never
   becomes ordinary no-focus evidence.

   A complete no-loss capture may retain an exact `Block`, `Dodge`, or no
   target-response observation for diagnosis. It is not hit-motion evidence;
   only the later positive-HP-loss attempt must prove the exact
   `Damaged`/`DamagedHeavy` target event and CEL trigger.

   After a retained run proves repeated exact native `Block` responses, one
   separate fresh execution-queue run may add
   `--cap-equipped-attack-skill`. The disposable fixture requires exactly one
   equipped right-hand weapon and zero spent focus, resolves that weapon's real
   native skill, and raises only the matching hero augmented stat to FTK's
   current native cap. Preserve its exact before/after receipt and every Block
   attempt. This does not set damage, attack results, enemy stats, focus, RNG,
   or animation authority, and the native result remains probabilistic. Label
   the balance unrepresentative and never transfer this evidence to ordinary
   lethal behavior. [Bronzehollow V4](../../art-experiments/bronzehollow-sentinel/live-validation-v4/README.md)
   is the canonical example; V3 is historical because it misattributed retained
   native equipment as authored geometry.

   If two separately retained bounded no-focus routes still prove exact native
   no-loss responses, stop blind retries. Inspect the native damage calculation
   and source row first. When that evidence supports a conservative target, one
   further fresh route may combine the native skill cap with
   `--minimum-native-weapon-max-damage N`. The runner applies the physical-damage
   fixture outside combat after entry preflight and immediately before dungeon
   entry. It pins the exact hero, stats object, equipped native weapon and row,
   character event listener, Animator, controller, focus and before-values. It
   changes only physical augmentation through the native trainer method; the
   weapon, focus, RNG, combat action, response and animation authority remain
   native. The request must be 1 through 100, exceed the observed native maximum,
   and add no more than 50 damage.

   Choose the minimum from the exact decompiled source row and native damage
   calculation, not from trial-and-error retries. The threshold must exceed the
   effective physical armor boundary; capping attack skill alone can improve the
   roll while every successful result still resolves to Block. Runtime scaling
   may change that boundary, so preserve the inspected source value and explain
   why the requested minimum is conservative. Bramblecoil V2 is the route-specific
   example: Jungle Snake C's inspected armor is 32, so the fixture requests a
   native maximum of at least 33.

   Preserve the inspect, apply and receipt records. The exercise may adopt only
   that same-session staged receipt, and may restore it once at native
   between-room `Ready` after combat and loot resolution. Restoration requires
   the same pinned objects and invariant values and must reproduce the exact
   prior physical augmentation and the native maximum expected at the hero's
   current legitimate level. Native combat can award XP and levels before Ready.
   Restoration must preserve that XP and level progression, remove only the
   fixture augmentation, and report the original maximum, current-level expected
   maximum, actual restored maximum, and any level progression used to derive it.
   A wrapper that compares against the stale pre-combat maximum must reject its
   own result; retain that rejection separately and rerun only after the verifier
   models the native level change. If the ordinary-action
   gate fails while combat remains active, stop the route; process disposal does
   not turn a rejected restoration into proof, and no dependent `KillSingle`
   receives credit. Label every accepted combat result balance-unrepresentative
   and keep fixture death separate from ordinary lethal behavior. The canonical
   [Amberwake V3 archive](../../art-experiments/amberwake-dragon/live-validation-v3/README.md)
   demonstrates a 10 to 30 maximum-damage fixture, zero-focus native 11-damage
   hit, exact Ready-time restoration, and separately labelled explicit death.
   [Bramblecoil V2](../../art-experiments/bramblecoil-jungle-snake/live-validation-v2-canonical/README.md)
   demonstrates a source-armor-derived 33 minimum, an applied maximum of 35, an
   ordinary native 1-damage hit, XP-backed level 0→2 progression, and restoration
   to the correct level-adjusted maximum of 12.

   Fixed runtime captures write120 full-size PNGs and can take several minutes
   of wall time while visibly slowing the isolated game. Keep the one issued
   capture running under the 360-second default (or a bounded explicit budget);
   a slow capture never authorizes another action, capture, or retry.
7. Deliver editable source, original runtime assets, reproducible commands,
   checksums, live evidence, and explicit remaining limitations. Update the
   matching skeleton entry with evidence instead of declaring all rigs supported.

## Rigid `MeshRenderer` child workflow

A selected target can be a rigid child rather than part of the skinned palette.
Before authoring, inventory the exact path and require exactly one
`MeshRenderer`, one `MeshFilter`, one native mesh, and one usable native material
slot, with no co-located `SkinnedMeshRenderer`. Multi-slot materials and native
`ScrollingUVs` are unsupported for a rigid assignment and must reject the
transaction rather than silently changing draw behavior.

Generate the read-only static inventory from the exact game asset pinned by the
enemy mapping, then pass it to the direct-profile preflight. The inventory must
name the same direct enemy/CEL root and child path; it records metadata only and
never decodes a native surface:

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

A static assignment needs `"rendererKind": "MeshRenderer"` and a linked
exact skinned assignment in the same direct profile. The skinned assignment
supplies the motion/controller contract; a static-only profile must reject.
[Abyssal Kraken V4](../../art-experiments/abyssal-kraken/README.md) and its
[pinned preflight](../../art-experiments/abyssal-kraken/route-preflight-v4-head-static.json)
are the reference combined head-plus-rigid-eye package.

Author that part entirely in the selected `MeshFilter` transform's local space.
Export it with the strict unskinned writer, without `--reference` or
`--rigid-bone`:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/export_ftk_glb.py \
  --static --source my-original-rigid-part.json --output my-rigid-part.glb
```

A static GLB has one triangle primitive, `POSITION` and indices, optional
normals/UVs, and no skin, `JOINTS_0`, `WEIGHTS_0`, bone palette, or inverse bind
matrices. Register it with `EnemyRendererMesh.ForStaticRenderer` or catalog
`"rendererKind": "MeshRenderer"`; never pass it through a skinned assignment.
The selected parent may animate the complete rigid part, but that does not give
the GLB a skin.

Do not inspect native static vertices, bounds, normals, UVs, texture pixels,
weights, or animation data to place or shape original art. Use only permitted
identity and transform metadata, material behavior, bone names/inverse binds
where applicable, your own authored landmarks, and observed live outcomes. If
placement is uncertain, first deploy an original low-poly marker probe that
maps visibility and occlusion. Preserve that diagnostic separately and do not
promote its result to art approval.

A rigid child cannot be the runner's selected skinned motion renderer. Capture
motion on a linked exact `SkinnedMeshRenderer`, then record the rigid child's
separate inventory: exact `MeshRenderer` kind, one live `MeshFilter`,
`ftkmf_static_glb_<file>`, private material/texture/emission state, owner, and
lifetime observations. Archive both claims together only after a fresh isolated
trial. Binding and sampled parent motion do not establish static-part art,
culling, all animation intervals, or final resource disposal.

## Player apparel lifecycle workflow

Treat each player class and skinset as an assembled outfit, not a single body
mesh. Inspect a real native avatar first and separate paths into two groups:
required body and hair renderers that must always be present, and conditional
apparel paths that appear only for a particular native equipment branch. Every
conditional assignment needs the exact expected native `sharedMesh.name`, as
well as its own original GLB, palette, bind matrices, and positive weights for
its complete palette. Do not substitute a similarly named renderer or infer a
layout from another class, gender, armor, or weapon.

Use a player profile with the required `renderers` array and optional `apparel`
array. Stage it through the same pinned transaction with `--catalog-kind
player`; the catalog, all body and apparel assets, and the player registration
report must stay in one receipt. If the profile declares `startingArmor`, launch
a fresh isolated process and fresh run after deployment. Native starting-item
processing owns the item transfer, so never grant the item through the test
bridge or assume an existing run acquired a revised profile.

The minimum live sequence for one player outfit is:

1. Inspect `equipment-inventory` and record the exact real hero instance ID,
   native item enum, and Body and Backpack counts. Do not continue when no
   native armor ownership exists.
2. Reach strict native Ready outside combat. Arm `lease-watch` on the real
   overworld avatar before each native transfer. The watcher is read-only and
   pins the old owned assets for later disposal observation.
3. Submit exactly one guarded `unequip-body` with the newly observed counts.
   Reinspect both overworld and combat renderer inventories. The expected
   default apparel branch must be custom, the boots must remain custom, and
   missing alternatives must be explicitly absent rather than silently mapped.
4. Read `lease-watch-state`, then arm a watch for the new avatar lease. Submit
   the inverse guarded `equip-body` only with fresh counts. Reinspect both
   scopes again and verify the equipped apparel branch and boots.
5. Read every watch until its old lease is absent and all pinned Mesh, Material,
   and Texture objects are Unity-null, or preserve a bounded
   `not-observed-disposed` result. Clear only diagnostic watch references after
   recording the result. Never invoke cleanup manually.
6. Separately inspect an actual native character-creation preview, overworld
   avatar, and combat dummy. To reach the preview without constructing a test
   avatar, first navigate normally to the visible offline Create Game screen,
   then run `command.py --root /absolute/scratch/game-copy
   native-create-character-preflight`. Require its read-only `eligible:true`
   result before issuing `native-create-character-screen`. The action accepts
   no payload and only invokes the exact enabled native Create Game callback;
   it waits for the game's own room, map, character-create root, and reciprocal
   preview-owner relationship. It does not select a class.

   After the native screen appears, call
   `native-create-character-input-state` before using a keyboard control and
   again after every navigation step that matters. It records the real FTK
   focus owner and selected native control without changing either. Navigation
   state varies with the previously focused control, so never assume a fixed
   number of arrow presses. Require the report to name the intended owner and
   intended owner before activating a class control. `toggleClass` is the
   central class button and is not a directional arrow. Use the guarded
   `native-party-class inspect` operation to resolve exactly one visible
   `classArrowNext` and one visible `classArrowPrevious` owned by that player;
   zero or multiple distinct arrows fail closed. Submit the returned token and
   direction for one native `OnClassClick` or `OnClassClickLeft` callback, then
   inspect again before another step. Tokens pin the owner, class identity,
   control identity, callback, visibility, and direction. Horizontal keyboard
   navigation can cross player cards and must never be treated as class cycling. It
   must never call private `SetClass`, change readiness, or construct an avatar.
   If the resulting native default is not the target profile, choose it through
   these native controls, then call `player-preview-state` with the exact profile
   key, skinset, catalog hash, and current owner ID. Record at least a guarded
   native pass, ordinary attack, and incoming damage capture on the selected
   custom renderer. Review frames for fit, branch selection, accessories,
   clipping, culling, and material response. Combat motion does not substitute
   for preview or equipment evidence.

The Blacksmith Female fixture uses body and hair paths plus two armor branches
and boots. [Hearthveil Blacksmith](../../art-experiments/hearthveil-blacksmith/README.md)
is the reusable original-art example. Its [V3 canonical route](../../art-experiments/hearthveil-blacksmith/live-validation-v3-canonical/README.md)
combines the historical V1 native attack, hit, equipment rebuild, lease disposal
and Ready evidence with current same-session Party Select body and six-bone hair
idle captures and explicit `death_overworld` motion fixtures. Verify it with
`verify_hearthveil_canonical_archive.py`. The death captures are native-state
playback on a real preview owner; they do not prove ordinary player death,
corpse lifetime, or cleanup. Use this archive as a sequence and verifier
template, not proof for another
skinset, apparel branch, or player layout. Repeat the full discovery,
transaction, native-equipment, preview, motion, and lifetime sequence for every
new layout. A successful previous skinset never establishes the renderer paths,
native mesh identities, equipment behavior, or cleanup of another one.

[Wildbloom Herbalist](../../art-experiments/wildbloom-herbalist/README.md)
provides the separate `herbalist_Female` 24/6/7-bone authoring package, including
the player-only seven-bone `hairBottom` layout. Its
[V2 canonical route](../../art-experiments/wildbloom-herbalist/live-validation-v2-canonical/README.md)
uses the exact directional native Party Class controls and preserves all three
meshes through Party Select, overworld, ordinary combat, native progression,
and one combat-avatar rebuild. It records ordinary enemy HP `72 to 62`, hero HP
`999 to 962`, strict Ready, and level 1 progression. Its death captures are
native-state playback fixtures, and its kill fixture is used only to reach
progression. No custom apparel is declared, so native clothing remains
game-owned. Run `verify_wildbloom_canonical_archive.py` before using it as a
sequence template. Do not reuse Hearthveil's six-bone hair or transfer this
evidence to FishPerson, another skinset, final owner teardown, or final art.

[Tideglass Fishsmith](../../art-experiments/tideglass-fishsmith/README.md)
provides the distinct `blacksmith_Fish` route. Its V1 native-preview archive
shows the exact casing `playerFIsh`: the body and seven-bone `hairBottom` are
visible in the default Fish preview, while `hairTop` is bound but inactive.
Record that state rather than claiming every configured renderer is visible.
When a real preview exposes an art problem, first freeze its source assets and
raw evidence in a non-overwriteable archive, then stage a hash-pinned asset
revision with `--replace-existing-assets` and restart the isolated game before
a fresh preview. A revised studio render never upgrades the earlier live result.

## Repeatable live-trial record

For each new exact model/chassis assignment, preserve a small immutable evidence
supplement beside the authoring package. Use a fresh isolated process and the
catalog/profile hash recorded by the runner. Keep the complete pass, attack and
explicit `KillSingle` captures, their journals/results, the case result, profile
and manifest references, and every source PNG hash. A local `archive.py` may
gzip metadata losslessly, pin all source PNGs, copy a deliberately small set of
root-reviewed originals, and derive presentation videos; it must refuse to
overwrite a completed destination. Verify artifact hashes and every gzip
round-trip independently after the archive is built:

For a completed queue-route runner record, first turn the reviewed exact run
into a fresh archive plan rather than copying a historical plan:

```sh
python3 tools/ai-model-pipeline/prepare_model_visual_review.py \
  scratch/my-isolated-game/model-test-output/case-<id>/case-result.json \
  --output scratch/my-route-root-review.json
```

Inspect the pinned source images, replace every pending observation, and set a
reviewed status before using that review below. The plan generator rejects a
pending template.
When the case retains repeated accepted actions, use the unique archive action
keys in each reviewed frame row, for example `attack-attempt-1` and
`attack-attempt-2`. A shared `attack` label plus the same frame index would map
two sources to one selected filename, so the archiver rejects the collision.

```sh
python3 tools/ai-model-pipeline/make_execution_queue_archive_plan.py \
  --record scratch/my-route-run.json \
  --visual-review scratch/my-route-root-review.json \
  --plan-output art-experiments/my-model/live-validation-vN-plan.json \
  --archive-output art-experiments/my-model/live-validation-vN \
  --revision VN \
  --limit 'State the observations outside this archive.'
```

The generator checks the runner, case, catalog, profile document, source-asset,
and review-session pins before it writes a new plan. It does not select review
frames, build the archive, or make a visual conclusion.

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py \
  art-experiments/<package>/live-validation-vN --check-video-metadata
```

The verifier rejects changed metadata, selected-frame copies that differ from
their pinned source images, unsafe game-payload mappings, and changed
presentation-video hashes or metadata. Its `PASS` is artifact integrity only;
use the validation ledger and root review for behavior and art conclusions.

After a batch of new archives, regenerate
[`MODEL-VALIDATION-ARCHIVE-INTEGRITY.md`](../../docs/MODEL-VALIDATION-ARCHIVE-INTEGRITY.md)
with:

```sh
python3 tools/ai-model-pipeline/audit_model_validation_archive_integrity.py \
  --overwrite --check-video-metadata
```

Keep any historical integrity gap visible until a
fresh immutable replacement exists; do not silently loosen the verifier or
relabel the model result.

After the original-model index, validation-gate ledger, topology coverage, and
package-readiness report are current, regenerate candidate coverage with that
current archive-integrity ledger:

```sh
python3 tools/ai-model-pipeline/audit_model_candidate_coverage.py \
  --output-json docs/model-candidate-validation-coverage.json \
  --output-markdown docs/MODEL-CANDIDATE-VALIDATION-COVERAGE.md \
  --overwrite --fail-on-unmapped
```

The report compares the exact indexed validation hash against the integrity
ledger. An absent, changed, or integrity-unverified artifact stays in the
follow-up queue even when its structured fields are complete.

Turn the remaining routes into concrete profile-and-renderer work items with:

```sh
python3 tools/ai-model-pipeline/audit_model_validation_execution_queue.py \
  --output-json docs/model-validation-execution-queue.json \
  --output-markdown docs/MODEL-VALIDATION-EXECUTION-QUEUE.md \
  --overwrite
```

The queue chooses the priority archive's exact source assignment where one
exists, otherwise one remaining exact topology representative. It does not
choose among multiple matching historical profile documents, prove a deployment,
or promote static preflight into a live result. A target without a profile needs
new authoring before a trial can be staged, unless the queue records an explicit
adapter-or-retarget requirement.

For a new enemy route, use the plan-based
[`archive_model_validation_case.py`](../../tools/ai-model-pipeline/archive_model_validation_case.py)
builder described in
[MODEL-VALIDATION-ARCHIVES.md](../../docs/MODEL-VALIDATION-ARCHIVES.md).
It requires an exact root review and immutable source paths, verifies a declared
frame identity across every retained image, and emits the common archive shape.
Do not retrofit it onto a historical archive or use it to upgrade a review that
was never performed.

For a game-owned player preview, use
[`archive_player_model_validation.py`](../../tools/ai-model-pipeline/archive_player_model_validation.py)
with the player plan in the same guide. It pins the native preview session,
requires a concrete observed-avatar count, and checks the selected renderer and
native idle clips across every retained frame. It does not transfer a preview
observation to combat, apparel branches, teardown, portraits, or another
skinset.

The root review names exact frame paths and hashes and records what is visible
under native effects and hero occlusion. It must separate ordinary damage from
the explicit kill fixture, native Collect/Ready progression from art acceptance,
and selected-frame observations from claims about every animation interval,
culling, collision/sleeping, material properties, portraits, or final resource
disposal. Update the skeleton register and runtime evidence index with a new
supplemental entry while leaving historical failed or superseded trials intact.
Pin each supplement's `path` and `sha256` directly in that index entry; the
reconciliation and package-readiness audits inventory nested pinned references
without promoting them to a new acceptance result.
For a constructed fixture that intentionally has no runtime source assignment,
pin it in its own named runtime-index collection; the original-model audit will
report it separately rather than treating it as an unresolved enemy archive.
Immediately run [the original-model index audit](../../tools/ai-model-pipeline/audit_original_model_index.py)
with `--summary --fail-on-novel --fail-on-unresolved`. A novel exact native
assignment must be added to the runtime index before treating the archive as
registered, and a runtime-style archive must declare its exact native identity.
An unindexed historical supplement for an already indexed exact assignment
remains a separate record, not a reason to rewrite it. This reconciliation
checks registration and native identity only; it never converts evidence
presence into an art or gameplay PASS.

Regenerate the [validation evidence ledger](../../docs/MODEL-VALIDATION-GATES.md)
at the same time:

```sh
python3 tools/ai-model-pipeline/audit_model_validation_gates.py \
  --output-json docs/model-validation-gates.json \
  --output-markdown docs/MODEL-VALIDATION-GATES.md \
  --overwrite --fail-on-unresolved --fail-on-integrity
```

It records only explicit structured evidence in each indexed archive. A
`recorded` cell is neither a gate PASS nor an art verdict; a blank cell means the
current adapter did not find the named record. Preserve explicit capture labels
or state clips for idle, attack, hit and death, numeric ordinary no-focus HP
transitions, and a structured `finalReady`/`ready` result so a future author can
audit the same package without interpreting prose.

Use each archive's `unrecordedSourceSpecificCoreEvidence` list to plan a
source-specific follow-up without treating it as a failure verdict or applying
it to a sibling source route.

For new archives, use a direct top-level `binding`, `visualReview`, and
`captures` array. Label captures exactly `idle`, `attack`, `nonlethal-hit`, and
`kill-fixture`/`death` when those actions are actually captured. Store ordinary
no-focus damage as numeric `beforeHp`/`afterHp` under `ordinaryAttack` or
`ordinaryHit`, keep `explicitKillFixture` separate, and store native
progression as `finalReady`/`ready` with `ok:true`. Player records also need
`avatarOwners.preview.observedAvatars`; zero records a pending native preview,
not a pass. Leave an unobserved field absent rather than deriving it from
prose, a generic pass capture, a focused trial, or a sibling source.

Measure an ordinary enemy hit inside the action's own `before` and post-capture
`after` states. Waiting for the next native player turn can advance more enemy
actions or status ticks. Preserve that later net result as `readyHpOutcome` or
`laterReadyObservedHp`, but never attribute the difference to the submitted
attack without its own causal native damage record. The generic archive builder
applies this action-window rule to older retained cases as well.

When one complete capture contains several causal motions, keep its real action
and terminal outcome, store the summarized native clip ranges under `states`,
and include `causalMotion` from the recorder when available. A mixed capture
such as idle, native attack, recovery, and full-health flee must not be reduced
to a generic `pass` label or split into invented actions. The gate adapter can
read explicit `cidle`, `attack`, `damage`, and `death` clip stems while the
archive keeps the actual chronology and limits.

If a completed runner becomes an `enemy_trial_record_identity_mismatch` after
the campaign is regenerated, inspect every plan field before deciding to run
again. A changed profile document, catalog, asset, topology, route kind, source
assignment, or motion renderer requires a new trial. If those semantic fields
all match and only the generated queue or stage-readiness hashes changed,
preserve the immutable runner, pin a reconciliation record that lists both old
and current snapshot hashes, conduct the root image review, and build a new
archive from the retained captures. Do not replay complete live actions solely
because unrelated route completions changed campaign bookkeeping. Verdigrin V4,
Sunspire Roc V4 and Belladusk V3 are exact-source examples for this workflow.
Belladusk also demonstrates why the new root review must derive each selected
frame's label from the raw animator timeline: two older selected frames described
as attacks were actually inside `idle` and receive no attack credit in V3.

Current examples are the [Rimecrown V3 historical whole-owner archive](../../art-experiments/rimecrown-sentinel/live-validation-v3/README.md), [Rimecrown V4 canonical head archive](../../art-experiments/rimecrown-sentinel/live-validation-v4-head/README.md), [Rimecrown V5 canonical scarf archive](../../art-experiments/rimecrown-sentinel/live-validation-v5-scarf/README.md), [Rimecrown V6 canonical Root_M-weighted base archive](../../art-experiments/rimecrown-sentinel/live-validation-v6-base/README.md), [Rimecrown V7 canonical hat archive](../../art-experiments/rimecrown-sentinel/live-validation-v7-hat/README.md), and [Rimecrown V8 canonical middle-body archive](../../art-experiments/rimecrown-sentinel/live-validation-v8-middle-body/README.md),
[Honeyback V3 canonical bearB archive](../../art-experiments/honeyback-portrait-v2/live-validation-v3-canonical/README.md),
[Duneshade V2 canonical snakeDesertA archive](../../art-experiments/duneshade-desert-asp/live-validation-v2-canonical/README.md),
[Ashfang V2 canonical wolfA archive](../../art-experiments/ashfang-wolf/live-validation-v2-canonical/README.md),
[Moonreed V2 canonical fairyA archive](../../art-experiments/moonreed-sylph/live-validation-v2-canonical/README.md),
[Sargassum primary V2 canonical krakenTentacle archive](../../art-experiments/abyssal-kraken/live-validation-sargassum-primary-v2-canonical/README.md),
[Abyssal Crown V5 canonical krakenHead archive](../../art-experiments/abyssal-kraken/live-validation-head-v5-canonical/README.md),
[Mirewarden V2 archive](../../art-experiments/mirewarden-ftk/live-validation-v2/README.md) and [Mirewarden V3 exact-source archive](../../art-experiments/mirewarden-ftk/live-validation-v3/README.md),
[Mireglass Croaker V1 archive](../../art-experiments/mireglass-croaker/live-validation-v1/README.md),
[Sablevine Serpent V2 archive](../../art-experiments/sablevine-basey-snake/live-validation-v2-scale055/README.md),
[Rustpetal V1 historical archive](../../art-experiments/rustpetal-snapper/live-validation-v1/README.md) and [Rustpetal V2 canonical exact-source archive](../../art-experiments/rustpetal-snapper/live-validation-v2/README.md),
[Belladusk V2 archive](../../art-experiments/belladusk-pitcher/live-validation-v2/README.md) and [Belladusk V3 canonical exact-source archive](../../art-experiments/belladusk-pitcher/live-validation-v3/README.md),
[Cinderbloom V3 archive](../../art-experiments/cinderbloom-plant/live-validation-v3/README.md),
[Emberjaw V2 archive](../../art-experiments/emberjaw-skull/live-validation-v2/README.md),
[Cinderwing V2 archive](../../art-experiments/cinderwing-bat/live-validation-v2/README.md),
[Cinderwing V3 exact-source archive](../../art-experiments/cinderwing-bat/live-validation-v3/README.md),
[Bronzewake V2 archive](../../art-experiments/bronzewake-champion/live-validation-v2/README.md),
[Bronzewake V3 archive](../../art-experiments/bronzewake-champion/live-validation-v3/README.md),
[Bronzewake V4 exact-source archive](../../art-experiments/bronzewake-champion/live-validation-v4/README.md),
[Bronzewake V5 exact-body-source archive](../../art-experiments/bronzewake-champion/live-validation-v5/README.md),
[Bronzewake V6 exact-boots-source archive](../../art-experiments/bronzewake-champion/live-validation-v6/README.md),
[Bronzewake V7 exact-hair-source archive](../../art-experiments/bronzewake-champion/live-validation-v7/README.md),
[Bronzehollow V1 archive](../../art-experiments/bronzehollow-sentinel/live-validation-v1/README.md),
[Bronzehollow V2 archive](../../art-experiments/bronzehollow-sentinel/live-validation-v2/README.md) and [Bronzehollow V4 canonical native-cap archive](../../art-experiments/bronzehollow-sentinel/live-validation-v4/README.md),
the [Tamarind V2 passive-arrival archive](../../art-experiments/tamarind-trickster/live-validation-v2/README.md),
the [Verdigrin V2 archive](../../art-experiments/verdigrin-mimic/live-validation-v2/README.md), [Verdigrin V3 archive](../../art-experiments/verdigrin-mimic/live-validation-v3/README.md), and [Verdigrin V4 exact-source archive](../../art-experiments/verdigrin-mimic/live-validation-v4/README.md),
the [Sunspire Roc V2 combat archive](../../art-experiments/sunspire-roc/live-validation-v2/README.md), [Sunspire Roc V3 portrait archive](../../art-experiments/sunspire-roc/live-validation-v3/README.md), and [Sunspire Roc V4 canonical exact-source archive](../../art-experiments/sunspire-roc/live-validation-v4/README.md),
the [Copperveil V2 archive](../../art-experiments/copperveil-spider/live-validation-v2/README.md) and [Copperveil V3 exact-source archive](../../art-experiments/copperveil-spider/live-validation-v3/README.md),
the [Tideglass V2 archive](../../art-experiments/tideglass-crab/live-validation-v2/README.md) and [Tideglass V3 exact-source archive](../../art-experiments/tideglass-crab/live-validation-v3/README.md),
the [Mossglass V4 archive](../../art-experiments/mossglass-reliquary/caps-v3/live-validation-v4/README.md) and [Mossglass V5 exact-source archive](../../art-experiments/mossglass-reliquary/live-validation-v5/README.md),
the [Vesper Eye V3 archive](../../art-experiments/vesper-eye/live-validation-v3/README.md) and [Vesper Eye V4 exact-source archive](../../art-experiments/vesper-eye/live-validation-v4/README.md),
the [Amberwake Dragon V2 archive](../../art-experiments/amberwake-dragon/live-validation-v2/README.md) and [Amberwake Dragon V3 canonical fixture-assisted archive](../../art-experiments/amberwake-dragon/live-validation-v3/README.md),
the [Emberglass Bee V2 archive](../../art-experiments/emberglass-bee/live-validation-v2/README.md)
and [Emberglass Bee V3 archive](../../art-experiments/emberglass-bee/live-validation-v3/README.md),
plus the [Emberglass Bee V4 exact-source archive](../../art-experiments/emberglass-bee/live-validation-v4/README.md),
the [Resinmaw V2 archive](../../art-experiments/resinmaw-bogling/live-validation-v2/README.md),
the [Duskquill V2 archive](../../art-experiments/duskquill-raven/live-validation-v2/README.md) and [Duskquill V3 exact-source archive](../../art-experiments/duskquill-raven/live-validation-v3/README.md),
the [Basilight V2 archive](../../art-experiments/basilight-cockatrice/live-validation-v2/README.md) and [Basilight V3 exact-source archive](../../art-experiments/basilight-cockatrice/live-validation-v3/README.md),
the [Lunacrest V3 archive](../../art-experiments/lunacrest-clam/live-validation-v3/README.md),
the [Reefstrider V2 archive](../../art-experiments/reefstrider-fish/live-validation-v2/README.md) and [Reefstrider V3 exact-source archive](../../art-experiments/reefstrider-fish/live-validation-v3/README.md),
the [Gloamcap V2 archive](../../art-experiments/gloamcap-imp/live-validation-v2/README.md) and [Gloamcap V3 exact-resource archive](../../art-experiments/gloamcap-imp/live-validation-v3/README.md),
the [Thistlewick V2 archive](../../art-experiments/thistlewick-hexer/live-validation-v2/README.md) and [Thistlewick V3 exact-source archive](../../art-experiments/thistlewick-hexer/live-validation-v3/README.md),
the [Saffronspine pufferA V2 archive](../../art-experiments/saffronspine-puffer/live-validation-v2/README.md),
the [Saffronspine pufferA V3 exact-source archive](../../art-experiments/saffronspine-puffer/live-validation-v3/README.md),
and the [Saffronspine pufferB V2 archive](../../art-experiments/saffronspine-puffer-b/live-validation-v2/README.md).
For a many-limbed controller that leaves a visible corpse, use Copperveil V3 as
the `spiderB / enSpiderB / renderer 121386` example. Review at least one settled
frame, the causal native attack frame, post-attack recovery, the causal
`Damaged` frame, post-hit recovery, the causal `Death` frame, a readable
airborne or transitional death pose, a settled corpse, and the terminal loot
frame. A corpse visible behind a native loot overlay supports the sampled
handoff only. Keep ordinary lethal damage and later corpse lifetime false or
out of scope unless separate evidence directly establishes them. Inspect each
leg chain for detached segments and stretching rather than treating an intact
central body as sufficient.

For a long-limbed biped that enters native physics death, use Reefstrider V3 as
the `fishA01 / enFishA / renderer 121695` example. Review the causal native
`Death` frame even when impact effects obscure the torso, then add readable
launch, descending, settled, and terminal-loot frames. Distinguish native
impact particles from authored loose geometry by checking the frozen piece
manifest and earlier frames. A connected corpse behind the loot panel supports
only the sampled handoff; retain ordinary-lethal and later-lifetime limits
unless separate evidence proves them.

For an articulated creature whose native death remains animator-driven, use
Basilight V3 as the `cockatriceC / enChicken / renderer 121484` example. Record
both the death clip and per-frame Animator and `m_DoRagdoll` state. An enabled
Animator with `m_DoRagdoll=false` supports a native animated fall and prone pose;
do not call it a physics ragdoll. Review the recoil, fall, settled animation
pose, and terminal loot handoff separately, and retain ordinary-lethal and later
corpse-lifetime limits unless direct evidence establishes them.

For a small flying creature whose native death uses physics, use Emberglass V4
as the `beeA / Monster Bee / renderer 121062` example. Preserve every accepted
ordinary attempt when the bounded retry policy first observes no HP loss. Keep
the zero-loss attempt separate from the later successful nonlethal hit, and use
the native trigger, clip readback, and visible game label only for what each
source directly records. Review wing, feeler, limb, abdomen, and tail continuity
through idle, attack, dodge, hit recoil, recovery, and the physics transition.
When `m_DoRagdoll=true`, verify the rigid bodies actually change from kinematic
to dynamic before calling the death a physics ragdoll. Record a readable falling
pose, first grounded pose, last visible active-renderer pose, and teardown
boundary. A grounded body through one sampled interval does not establish later
corpse lifetime or native cleanup causality. The shared 47-bone topology still
requires exact beeB and dragonflyB representatives; do not transfer beeA credit
to those sources.

For an articulated bird, [Sunspire Roc V4](../../art-experiments/sunspire-roc/live-validation-v4/README.md)
combines a retained ordinary no-focus combat session with a separate constructed
native row-portrait session. It preserves `cidle_roc`, `attackProf_roc`, ordinary
HP 81 to 71 with `damageLight_roc`, later `attackCrit_roc`, animated
`deathHeavy_roc`, two native Collects, strict Ready, and the 328 by 280 portrait.
Its binding-only source generator and closed wing panels are a reusable `rocA`
example, not evidence for a different bird rig. V4 is also the reference for
repairing an older archive layout without replay: prove that the historical and
current selected profile objects, assets, source renderer, and motion renderer
are identical; preserve the old archives; review original pixels again; and
write a new immutable archive with every structured gate and integrity pin.
Catalog-wide hash drift alone is insufficient reason to replay a complete
trial. Any selected-profile or semantic route change requires a fresh run.

For an animator-driven plant, use
[Belladusk V3](../../art-experiments/belladusk-pitcher/live-validation-v3/README.md)
as the exact `plantE / enJungleNibbler_A / renderer 121530` example. Preserve
the raw interval boundaries for `idle`, `attack1`, `hit1` and `death`, and select
review frames from inside those intervals. Record the ordinary HP transition
separately from the explicit `KillSingle` fixture. When every retained frame
reports `m_DoRagdoll=false` and zero rigidbodies, call the result animated death.
Do not infer collision, physics sleeping or corpse lifetime from a low death
pose. The route credits only plantE even when another plant looks similar.

For an animator-driven plant whose source renderer is removed during native
cleanup, use [Rustpetal V2](../../art-experiments/rustpetal-snapper/live-validation-v2/README.md)
as the exact `plantD / enJungleNibbler_C / renderer 121537` example. Require
complete idle/native-attack and ordinary-hit captures first. After an exact
native `Death` trigger, retain a renderer-destroyed death prefix only when every
sample before the boundary keeps the selected mesh, renderer path, bone
signature, owner and CEL identity. Review the fall, a readable fallen pose, the
Victory handoff, and the final telemetry sample separately. If the last sample
marks the renderer inactive or not visible, a blurred body shape in that PNG
does not establish an active corpse. Record `m_DoRagdoll=false` and zero
rigidbodies as animated death, preserve the raw `ok:false` result, and leave the
full requested duration, cleanup causality and later corpse lifetime unproved.

For a small bird whose native behavior can remove it during a pass capture, use
[Duskquill V3](../../art-experiments/duskquill-raven/live-validation-v3/README.md)
as the exact `crowC / enCrow / renderer 120964` example. Preserve the stopped
run, its partial raw capture, and the last active frame as rejected boundary
evidence. Do not accept a renderer-destroyed pass prefix as idle or attack
coverage and do not call native self-removal a binding failure without evidence
that the binding changed first. Start one fresh isolated process for the retry
and require complete captures there. In the successful process, review the full
wing span during flight and attack, the compact recovery pose, the damage
response, and the animated floor pose. `m_DoRagdoll=false` with zero rigid
bodies establishes an animator-driven `BirdDeath`, not a ragdoll. Keep sibling
bird rows, ordinary lethal damage, and lifetime after the sampled loot window
outside the exact-source result.

For an enemy whose native proficiency attacks, robs, and then removes the
full-health enemy after the hero passes, use [Thistlewick V3](../../art-experiments/thistlewick-hexer/live-validation-v3/README.md)
as the exact `scourgeG / enScourgeLeprechaun / renderer 121222` example. The
complete terminal capture records `cidle_impUnarmed`,
`attackProf_leprechaun`, upright recovery, uniform translucency, and renderer
disable while HP remains 58. Preserve that sequence as native robbery/flee,
not death, and do not automatically replay it. Capture an ordinary nonlethal
hit before any pass that can remove the enemy, or use a separately retained
process with exact identity and hashes. Keep the ordinary HP transition,
explicit `KillSingle` death capture, and native loot/Ready progression in
separate fresh-process claims. `m_DoRagdoll=false` makes Thistlewick's sampled
death animated rather than a body ragdoll.

For a tall articulated humanoid with game-owned attachments, use
[Tidecrown Sovereign V2](../../art-experiments/tidecrown-sea-king/live-validation-v2-canonical/README.md)
as the `seaKing/enSeaKing` profile. Its 60-bone generator uses only bone names
and inverse bind matrices, retains the whole palette, and builds the robe,
mantle, arms, fingers, armor, and crown along actual chains. `Knee_L` and
`Knee_R` remain palette entries but have no native surface weight, so it does
not fabricate a below-floor leg shell merely to occupy them. Keep native trident,
shield, breakable children, tentacles, ragdoll bodies, controller, and portrait
cache outside the GLB unless the selected replacement renderer owns them. The
V2 record pins the exact 1.0-scale binding and disabled material emission. It
preserves two Blocks followed by a fixture-assisted ordinary zero-focus
720-to-719 `Damaged` response, a 91-frame fixture-ragdoll prefix and strict
Ready. The fixture changes only the equipped native weapon skill augmentation
to FTK's native cap and leaves focus, RNG, weapon, damage, enemy stats, response
and animation authority native. Keep its balance limitation, giant-camera crop
and fixture-only lethal result explicit. Reuse the authoring and archive shape
for this exact renderer, then repeat all live checks for another humanoid,
accessory layout, or controller.

## Pinned isolated profile transaction

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

[Lichenfang Prowler](../../art-experiments/lichenfang-basey-wolf/README.md) is a
reusable original-art example for this exact route type. Its `enbaseywolf`
package contains deterministic source geometry, a proof rerun, the route
preflight, pinned isolated deployment, and a scoped live record through native
idle, attack, nonlethal hit, fixture death, and Ready. Its result remains
limited to the exact `enbaseywolf/Wolfie` pair; repeat the complete sequence for
every new resource prefab.

[Sablevine Serpent](../../art-experiments/sablevine-basey-snake/README.md) is
the paired long-chain example for `enbaseysnake`: it explicitly covers all 44
body/tongue joints with closed volumes. Its immutable V1 archive records a
camera-fit rejection. The fresh V2 profile reuses the exact GLB and palette at
`visualScale: 0.55`, measures the spawned CEL scale in registration, binding,
and every capture frame, and accepts scoped normal combat-camera fit. Preserve
both revisions and create equivalent evidence for every new snake-controller
package.

[Rivenquill Cockatrice](../../art-experiments/rivenquill-basey-cockatrice/README.md)
is the paired resource-prefab example. Its independently archived
[boss route](../../art-experiments/rivenquill-basey-cockatrice/live-validation-v1-boss/README.md)
and [small route](../../art-experiments/rivenquill-basey-cockatrice/live-validation-v1-small/README.md)
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
[`model-validation-profile-selections.json`](../../docs/model-validation-profile-selections.json)
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
[production adapter contract](../../docs/evidence/kraken-production-adapter-design-v1/README.md)
and its machine-readable gates. It preserves native CEL and weapon-controller
authority while a separate event-disabled sampler maps modern endpoints to the
old palette. The generic profile scaffolder remains invalid for this route even
though it can scaffold the five-bone source. Do not create a generic profile,
reuse `kraken2` direct-enemy evidence, or transfer any modern Kraken conclusion
to renderer 121260.

For `enkrakenhead`, follow the runtime helper's
[production observer campaign](../../tools/ai-model-pipeline/runtime-test/README.md#production-gloamfin-kraken-observer-campaign).
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
[`kraken-production-adapter-v1`](../../docs/evidence/kraken-production-adapter-v1/README.md).
It proves exact production binding, native behavior, all four proficiencies,
ordinary lethal DEATH, ordinary party-loss VICTORY, and natural teardown in two
fresh owners. The later
[canonical route archive](../../art-experiments/gloamfin-kraken/live-validation-v2-canonical/README.md)
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

## Fragile invariants

- Extracted game meshes, textures, controllers, reference NPZ files, and DLLs
  stay in ignored local scratch. Package only original assets plus the minimal
  skeleton binding metadata required by the runtime contract.
- Match mesh-local space and inverse bind matrices. For the documented Blender
  `(x,z,-y)` mapping, preserve winding. Check triangle-normal agreement against
  the selected native reference; do not generalize the troll to arbitrary rigs.
  Triangle-normal agreement alone does not prove outward-facing surfaces: an
  authored cap and its generated normals can both point inward and still pass.
  Verify cap cross products against the intended exterior (for example +Y top,
  -Y bottom), and check closed-volume orientation where applicable, separately
  from normal agreement. Do not blindly flip an entire open or mixed surface.
  Review backface-culling behavior as well as double-sided studio renders.
  [Mossglass V2](../../art-experiments/mossglass-reliquary/jade-v2/live-validation-v2.json)
  preserves the live missing-top failure; the [V3 cap audit](../../art-experiments/mossglass-reliquary/caps-v3/corrected-cap-audit.json)
  verifies the corrected exported triangles in bind and recorded native poses.
- The loader flips texture V. Export top-origin V exactly once. The reference
  NPZ itself contains native bottom-origin V. Make studio previews apply the
  same orientation, then inspect live frames: Cairnfire Troll B V1 bound and
  animated correctly but its palette was vertically reversed until a fresh
  pinned texture revision corrected it.
- Public visual scale is a factor of the captured native CEL scale:1 preserves
  it, including non-uniform axes, and repeated application must not compound it.
  Record requested/native/spawned scale separately and verify the deployed Core
  contract; source arithmetic does not establish live fit. The native baseline
  is per exact resource-prefab/source pair: Rivenquill's matching boss and small
  Cockatrice rigs measure `0.9` and `0.35`, respectively. See the renderer guide.
- Inspect inherited native materials when a correct texture looks washed out.
  Check native scripts that index material slots as well as shader properties.
  CubeA's UV scroller targets slot 1, so the pinned single-slot replacement
  cannot preserve it. A shared skeleton with single-material CubeE does not
  establish compatibility. See the [cube source evidence](../../docs/evidence/cube-material-compatibility-source-v1/README.md).
  Preserve separate authored surfaces and verify native scrolling and owned
  material cleanup before claiming support; extra material slots on one
  submesh can redraw geometry rather than create another surface.
  For a single enabled native `_MainTex` scroller, replay the independent
  [timed material verifier](../../tools/ai-model-pipeline/runtime-test/MULTI-SLOT-MATERIALS.md#repeat-the-timed-scroll-check)
  against the pinned raw capture. Stable material IDs and correct phase increments
  establish current-owner behavior only. The separate
  [owned two-slot lineage fixture](../../docs/evidence/material-lifecycle-native-v1/README.md)
  now verifies clone/grandclone private materials, disabled-renderer phase
  accumulation and final resource disposal. Its explicit owner setup does not
  establish public GLB binding or native avatar lifetime.
  Use [Mossglass V5](../../art-experiments/mossglass-reliquary/live-validation-v5/README.md)
  as the exact `cubeA / enJellyCube / renderer 121012` example. Preserve two
  actual primitives, bind each primitive to its native material slot, and record
  every frame's slot IDs, texture names, offsets, scroller target index, property,
  rate, and phase. A fixed slot0 plus advancing slot1 readback establishes the
  sampled owner contract; selected stills alone do not prove perceptually
  continuous scrolling or long-session material lifetime.
  Vesper Eye retained native emission that turned its black pupil yellow. The
  enemy renderer API offers optional `disableNativeEmission: true` per assignment;
  leave native emission when the authored look depends on it. Verify the deployed
  replacement material and live appearance; offline texture correctness alone
  does not establish color fidelity. The current route stage validator checks
  configured texture and emission values on every single-material skinned or
  rigid assignment before gameplay and records each private material ID. Preserve
  its stage journal in the archive to retain full multipart material snapshots.
  See the renderer API and Vesper's v1/v2/v3/v4 evidence.
- Verify portrait framing separately from the combat body. Briarback's fixed
  native chest portrait marker missed its face; selecting the verified head
  marker corrected both turn-strip and enemy-health-panel portraits. Use the
  [explicit portrait marker API](../../docs/MODEL-RENDERER-API.md#explicit-portrait-marker)
  only for the affected custom enemy and inspect both newly captured UI views;
  do not apply a blanket camera override.
- Preserve the native animation bounds unless measured motion requires a
  deliberate expansion. Mesh bind-pose bounds are not an animation envelope.
- Never accept a fallback vanilla/procedural body as a successful mesh load.
  Check loader logs, joint mapping, and the visible silhouette. Existing
  procedural-body settings can hide a successfully swapped renderer.
- Measure actual simulation time and pose progress, not requested capture
  duration. Tutorials can pause a successful capture. Native ragdoll deaths
  disable the Animator deliberately: inspect physics-driven bone motion, corpse
  settling, and victory/loot progression rather than demanding a full death clip.
  Native cleanup can also destroy the captured renderer after combat. Preserve
  the partial capture and its error, then verify progression separately; do not
  relabel an interrupted recording as a complete capture.
  A raw `Animator.Play` death clip proves playback and its events only. When a
  death branch depends on gameplay state, issue the native CEL/owner trigger and
  record its trigger state separately. For a `FallOffLimb` body, opt in only
  after an explicit lease owns that exact renderer; compare one normal-death
  fixture with a same-binary policy-omitted control, then record native
  Collect/Ready separately. Do not turn this snake-specific mechanism into a
  requirement for rigs without fall-off behavior.
  Endpoint HP zero and `alive:false` do not prove lethal damage: native enemy
  flee also writes those values. Check intermediate damage, death animation,
  and explicit removal/flee evidence before assigning a death verdict.
  Keep explicit `KillSingle` fixture evidence separate from ordinary lethal
  damage. For the latter, preserve each guarded normal attack and any dodge,
  then inspect native death and loot progression; do not finish with a health
  setter or kill fixture. The runtime helper guide documents this sequence.
  A self-removing proficiency after an accepted hero pass is a different
  boundary from removal before hero readiness. Preserve the completed terminal
  pass once, classify its native action and HP state, and use a planned
  multi-process workflow for still-missing hit, death, or progression gates.
  Do not send the same pass again merely because a generic exercise wrapper
  reports that the target is gone before its next action.
  An enemy that removes itself before hero readiness needs passive arrival
  observation; changing its AI or initiative would test a different behavior.
  Use the [arrival runner](../../tools/ai-model-pipeline/runtime-test/ENEMY-ARRIVAL.md#reusable-once-only-runner)
  from an eligible native Ready Enemy slot. It stages and arms once, then polls
  only its predetermined capture. A timeout is not permission to submit Ready
  again. The reusable CLI has offline validation; the [Tamarind V2 record](../../art-experiments/tamarind-trickster/live-validation-v2/validation.json)
  demonstrates the once-only arm/Ready/capture/archive path for a native
  self-removing enemy. A terminal result, including an errored partial capture,
  is not visual acceptance. Tamarind's record distinguishes the bounded native
  suicide transition and body hiding from stale attack-info fields, ordinary
  hero hits, loot progression and a visible full-death animation.
  If exact native behavior makes a generic gate impossible, record the exception
  only in a machine-readable `ftkmf.source-specific-evidence-applicability.v1`
  object. Pin the exact native chassis and workflow, use a gate-specific allowed
  reason code, and point to existing evidence inside the same validation record.
  The gate auditor must reject missing pointers, unknown reason codes and source
  mismatches. Never transfer the exception to a sibling source. Tamarind V2 is
  the bounded example: `hitMotion` is inapplicable because native self-removal
  has no received-hit phase, and `ordinaryDamage` is inapplicable because the
  self-removal precedes a hero attack. Its idle, native attack, removal and Ready
  evidence remain required and recorded independently.
- For actual native portrait-clone and final model resource disposal, use the
  [passive enemy lifetime observer](../../tools/ai-model-pipeline/runtime-test/ENEMY-LIFETIME.md)
  before native HUD creation. Its explicit plural and legacy singular checks
  are distinct. Ready, zero HP, hidden UI, and an empty renderer inventory do
  not establish final disposal; require native owner and resource destruction
  under unchanged pins. The [legacy singular live trial](../../docs/evidence/legacy-singular-native-hud-lifetime-v1/README.md)
  verifies one native portrait clone and final destruction of its source and
  owned mesh, material and texture. Its `matLoot` body skipped tinting, so it
  does not prove combined tint-and-mesh ownership. The separate
  [PlantD singular plus tint trial](../../docs/evidence/plantd-legacy-tint-native-hud-lifetime-v1/README.md)
  measures the requested body tint and verifies final disposal of all six
  owned resources, including the intermediate tint and FX material copies.
  This establishes the observed clone-first lineage with calibration geometry;
  tint pixel fidelity, other destruction orders and never-active owners remain
  separate checks.
- Initialize dungeon rooms before selecting one; wait for native spawn/camera
  readiness. Repeated forced acknowledgments cannot repair aborted combat.
  Finish the native story coordinator and its queued pages before entering the
  dungeon. A portrait-button submission, a temporarily absent modal, or message
  type `None` alone does not prove completion: page callbacks can still be
  waiting on an overworld camera transition. A story arriving after staging is
  a stopped setup case, not permission to force its callbacks during combat.
  It can prevent combat creation entirely: the [Reefstrider setup failure](../../art-experiments/reefstrider-fish/setup-failure-v1/README.md)
  occurred after the initial story had finished. `pending_story_message` with
  no renderer matches establishes no binding or model defect. Use the current
  authoring guide's startup limitations; a longer quiet delay is not a verified
  substitute for completing native location-triggered quest work before entry.
  For native story buttons, verify the active click callback and its exact
  owning panel. `uiPortraitMessageHud.EnableButton` activates the callback
  without updating its initial `m_Clickable` field; that field alone can make
  an automation wait forever. MultiQuest presentation can also reset the page
  index between phases, so page identity must include the native phase.
  Native definition-backed quests receive negative IDs in
  `GameLogic._assignQuestID`. Validate the present quest through its exact
  native table/reference join; rejecting every negative ID incorrectly blocks
  the first custom story quest. Do not confuse an absent-quest sentinel with
  an observed, registered quest whose actual ID is `-1`.
  The [guarded startup trial](../../docs/evidence/story-setup-quest-id-v1/README.md)
  preserves the original timeout and zero-submission evidence.
  After an invalid room or failed initialization, diagnose/reset the test rather
  than advancing further. Scratch recovery helpers are not production APIs.
- Use a fresh isolated process for each new run, and strict native Ready staging
  for subsequent candidates in that run. Returning to the title screen has exposed
  a second-run map-generation failure and is not a verified reset procedure.
  A bridge `menu` label alone does not establish native startup readiness.
- Do not restart or deploy repeatedly without first checking the exact launch
  environment, target DLL hash, renderer readiness, and the failure log. If the
  user or another task changes the session, establish ownership before resuming.

Read each named model's manifest and the skeleton register for current evidence.
Registration, offline export, a calibration probe, and a visually accepted
original asset are distinct results. Reuse evidence only for a verified model/rig
and controller combination; keep untested variants explicit.
