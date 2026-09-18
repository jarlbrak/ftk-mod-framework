---
name: ftk-custom-models
description: Author, export, inject, and validate custom For The King character models on existing game skeletons. Use for enemy meshes, player avatars, reskins, rig compatibility checks, or extending the tested skeleton catalog.
---

# FTK custom models

Deliver an original model inside FTK, with evidence of its appearance and motion.
A Blender render alone is not an integrated asset. Preserve the user's chosen
creature and scope; adapting every rig is a separate series of exercises.

## Orient

Use the active FTK checkout. If invoked elsewhere, resolve this skill's real
filesystem path and locate the repository containing `FTKModFramework/` and
`tools/ai-model-pipeline/`. Do not guess a Steam installation or deploy to a
second checkout's game. Read its `AGENTS.md` where present, nearest file first.

Read [the model guide](../../docs/CUSTOM-MODELS.md) for integration and
[the authoring workflow](../../docs/MODEL-AUTHORING.md) for the actual exercise.
For a different rig, also read [the skeleton register](../../docs/MODEL-SKELETONS.md).
Enemy multipart assignments use [the renderer API](../../docs/MODEL-RENDERER-API.md).
Player avatars use [the class/skinset API](../../docs/MODEL-PLAYER-API.md).
The exporter contract and executable commands live in
[the tools guide](../../tools/ai-model-pipeline/README.md).

Treat local source and game assets as authority over prior summaries.

## Choose the route, then load its reference

Three integration routes exist and they never share evidence: a direct native
enemy row, a ResourceManager prefab override, and a player skinset avatar. An
unsupported empty renderer is not a strict-swap candidate.

Load only what the current step needs. Each reference is self-contained.

Paths in this table are relative to this file's own directory,
`skills/ftk-custom-models/`. A harness adapter that pointed you here holds no
references of its own, so never resolve them against the adapter's directory.

| Read this | When |
|---|---|
| [route-selection.md](references/route-selection.md) | Choosing or scoping a route: topology coverage, package readiness, candidate coverage, the execution queue, and the exact-source precedents that define a canonical archive. |
| [authoring-kit.md](references/authoring-kit.md) | Before opening Blender: resolving a topology into exact renderer, bind, and rig inputs, verifying references, and scaffolding a package workspace. |
| [rigid-renderer-route.md](references/rigid-renderer-route.md) | The target is a rigid `MeshRenderer` child rather than part of the skinned palette. |
| [player-apparel-route.md](references/player-apparel-route.md) | A player class or skinset: body and hair renderers, conditional apparel branches, native equipment, preview, and lease lifetime. |
| [isolated-staging.md](references/isolated-staging.md) | Running preflight, staging, deploying, or migrating a profile in an isolated game, including the `enkrakenhead` adapter exception. |
| [live-trial-archive.md](references/live-trial-archive.md) | Building the immutable evidence archive after a trial, regenerating the ledgers, and reviewing frames for a specific creature shape. |
| [invariants.md](references/invariants.md) | Required before authoring geometry and again before stating any result. The evidence-backed failure modes: winding, texture V, scale, materials, portraits, death and cleanup, story setup, process hygiene. |

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

   Fixed runtime captures write 120 full-size PNGs and can take several minutes
   of wall time while visibly slowing the isolated game. Keep the one issued
   capture running under the 360-second default (or a bounded explicit budget);
   a slow capture never authorizes another action, capture, or retry.
7. Deliver editable source, original runtime assets, reproducible commands,
   checksums, live evidence, and explicit remaining limitations. Update the
   matching skeleton entry with evidence instead of declaring all rigs supported.

## Non-negotiables

The detail and the evidence behind each of these is in
[invariants.md](references/invariants.md). Read it; do not work from this summary
alone.

- Extracted game meshes, textures, controllers, reference NPZ files, and DLLs
  stay in ignored local scratch. Package only original assets plus the minimal
  skeleton binding metadata the runtime contract requires.
- Never inspect native vertices, bounds, normals, UVs, texture pixels, weights,
  or animation data to place or shape original art.
- Never accept a fallback vanilla or procedural body as a successful mesh load.
- Registration, offline export, a calibration probe, a passing preflight, and a
  visually accepted original asset are five distinct results. None implies another.
- Evidence belongs to one exact source pair: route identity, renderer ID and
  path, and for a player also the named profile and skinset. A matching skeleton,
  bone count, or topology group transfers nothing.
- Endpoint HP zero and `alive:false` do not prove lethal damage; native flee
  writes the same values. Keep an explicit `KillSingle` fixture separate from
  ordinary lethal damage, and label every fixture-assisted result
  balance-unrepresentative.
- A bounded capture proves what it sampled. It does not establish every animation
  interval, culling, corpse lifetime, cleanup causality, or final disposal.
- Use a fresh isolated process per run. Never deploy to Steam.

Read each named model's manifest and the skeleton register for current evidence.
Reuse evidence only for a verified model, rig, and controller combination; keep
untested variants explicit.
