## Fragile invariants

Part of the [ftk-custom-models skill](../SKILL.md). Read that entry point first: it
owns route selection, the working method, and the non-negotiables that apply here.

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
  [Mossglass V2](../../../art-experiments/mossglass-reliquary/jade-v2/live-validation-v2.json)
  preserves the live missing-top failure; the [V3 cap audit](../../../art-experiments/mossglass-reliquary/caps-v3/corrected-cap-audit.json)
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
  establish compatibility. See the [cube source evidence](../../../docs/evidence/cube-material-compatibility-source-v1/README.md).
  Preserve separate authored surfaces and verify native scrolling and owned
  material cleanup before claiming support; extra material slots on one
  submesh can redraw geometry rather than create another surface.
  For a single enabled native `_MainTex` scroller, replay the independent
  [timed material verifier](../../../tools/ai-model-pipeline/runtime-test/MULTI-SLOT-MATERIALS.md#repeat-the-timed-scroll-check)
  against the pinned raw capture. Stable material IDs and correct phase increments
  establish current-owner behavior only. The separate
  [owned two-slot lineage fixture](../../../docs/evidence/material-lifecycle-native-v1/README.md)
  now verifies clone/grandclone private materials, disabled-renderer phase
  accumulation and final resource disposal. Its explicit owner setup does not
  establish public GLB binding or native avatar lifetime.
  Use [Mossglass V5](../../../art-experiments/mossglass-reliquary/live-validation-v5/README.md)
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
  [explicit portrait marker API](../../../docs/MODEL-RENDERER-API.md#explicit-portrait-marker)
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
  Use the [arrival runner](../../../tools/ai-model-pipeline/runtime-test/ENEMY-ARRIVAL.md#reusable-once-only-runner)
  from an eligible native Ready Enemy slot. It stages and arms once, then polls
  only its predetermined capture. A timeout is not permission to submit Ready
  again. The reusable CLI has offline validation; the [Tamarind V2 record](../../../art-experiments/tamarind-trickster/live-validation-v2/validation.json)
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
  [passive enemy lifetime observer](../../../tools/ai-model-pipeline/runtime-test/ENEMY-LIFETIME.md)
  before native HUD creation. Its explicit plural and legacy singular checks
  are distinct. Ready, zero HP, hidden UI, and an empty renderer inventory do
  not establish final disposal; require native owner and resource destruction
  under unchanged pins. The [legacy singular live trial](../../../docs/evidence/legacy-singular-native-hud-lifetime-v1/README.md)
  verifies one native portrait clone and final destruction of its source and
  owned mesh, material and texture. Its `matLoot` body skipped tinting, so it
  does not prove combined tint-and-mesh ownership. The separate
  [PlantD singular plus tint trial](../../../docs/evidence/plantd-legacy-tint-native-hud-lifetime-v1/README.md)
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
  It can prevent combat creation entirely: the [Reefstrider setup failure](../../../art-experiments/reefstrider-fish/setup-failure-v1/README.md)
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
  The [guarded startup trial](../../../docs/evidence/story-setup-quest-id-v1/README.md)
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