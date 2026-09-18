# Lunacrest Clam

Original lapis and pearl clam for `clamA`, exact renderer `enClam` (121306), seven native palette joints. The upright ivory fan closes on `ClamShell_Joint`; the lower bowl and rear hinge support follow `Root_M`. Separate overlapping hinge knuckles meet at the native pivot. The lilac mantle and pearl follow Tongue_1–3. No surface vertices span the two shell halves.

Native opening faces Unity +Z. Terminal joint4 and joint6 remain in the exact palette and inverse binds but receive no weights in this authored surface. They have minor native influences (maximum 0.193548 and 0.032258 respectively), so they are not described as unused native joints. All shipped surface geometry and palette pixels are original. Extracted native references remain in ignored scratch.

`hero.png`, `side.png`, and `lunacrest-studio.blend` show the bind-pose design. `lunacrest.blend` is the editable armature scene. `build_geometry.py` regenerates original geometry; `build_blender.py` saves and reopens the scene before independent export validation. `runtime-profile.json` specifies the exact assignment and minimum health 64. Material170 matMimic has white tint and zero emission RGB despite its keyword; the profile preserves emission behavior.

The initial detached fan was corrected with independently weighted hinge surfaces. Parent review approved the corrected studio side and native-pose study for live testing. Direct export and saved Blender reopen pass; all original bind vertices fit inside native surface bounds. These checks do not establish animated culling, materials, or live acceptance.

`audit_native_poses.py` applies captured native bone matrices to original vertices for all120 observed attack frames, normalizes root motion, and shows open/closed samples in `native-pose-study.png`. Hinge center separation stays 0.04227–0.04247 mesh units. This is an offline geometry diagnostic, not a replacement for live observation.

Native calibration observations are separate: normal hit58→48; death uses no ragdoll and disables the enClam object during its death state. The source analyst verified DeathFallOff hides enClam, without a replacement corpse MeshRenderer or rigidbody. Source evidence: `scratch/clam-death-source-findings.md` and `scratch/clam-death-source.json`. DeathLight and DeathRevive skip DeathFallOff and remain untested routes. The original asset must still be tested through that native hide/FX lifecycle; no persistent custom corpse is promised. Authored idle, attack, hit, death, material, visibility and cleanup acceptance remain pending.

Reproduce from repository root with local extracted121306 reference present:

```sh
.venv-3dgen/bin/python art-experiments/lunacrest-clam/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/lunacrest-clam/build_blender.py
.venv-3dgen/bin/python art-experiments/lunacrest-clam/audit_native_poses.py
.venv-3dgen/bin/python art-experiments/lunacrest-clam/finalize_manifest.py
```

The pose audit additionally needs its recorded capture. Rebuilding resets manual review status; a fresh studio and live review is required for changed assets.

Live V1: [hashed archive](live-validation-v1.json), [attack](live-v1/attack.mp4), [normal hit](live-v1/nonlethal-hit.mp4), [death](live-v1/kill-fixture-death.mp4). Three 120-frame unpaused captures show readable idle/return body, hinge and portraits, but the top shell clips out of frame during attack40: **camera fit failed at factor 1**. Normal damage 58→48 is partly hero-occluded. Direct death48→0 hides the renderer from frame25, consistent with native DeathFallOff; no persistent native corpse is configured. DeathLight/Revive remain untested. One Collect reached Ready0/2. The same geometry at factor 0.80 is staged separately and awaits live testing; no corrected-fit claim yet.

V2 retest at factor 0.80 now has [hashed evidence](live-validation-v2.json) and three 120-frame [attack](live-v2/attack.mp4), [normal-hit](live-v2/nonlethal-hit.mp4), [death](live-v2/kill-fixture-death.mp4) captures with unchanged geometry. Attack40 shell tip stays inside screen, correcting V1 clipping; the healthbar still overlaps it. Native attack animation executes despite the blocked attack. Normal damage 58→47 is partly hero-occluded; death47→0 becomes inactive at raw frame25 as expected from native DeathFallOff. One Collect reached Ready0/3. The first pass request failed its initial HTTP500 read before any action; the separate pass2 followed a responsive read-only recheck. This is scoped camera-fit acceptance, not all variants or full campaign coverage; V1 failure remains preserved.

## Fresh live V3

[The V3 archive](live-validation-v3/README.md) records a fresh catalog-411 run in session `67212ee1c3ee4c80918e8dfffc0bae43`. The exact clamA `enClam` renderer (121306) bound to owner `369188` with the expected seven-joint signature at public visual scale `1.0` (captured native CEL scale `0.8`). Selected idle and attack views retain the opened shell, lower bowl, hinge body and pale rim. The runtime material uses the authored basecolor and reports black emission with no emission map.

Pass, ordinary attack and explicit `KillSingle` captures are complete 120-frame recordings. The ordinary attack changes HP `58→50` with `cheat=None` and no focus. The fixture reaches the native Victory loot surface; one guarded native Collect is accepted and strict Ready is observed at level 0 room 2. The native hide path and victory overlay limit full death deformation, corpse settling, culling, portrait/resource lifetime and finished-art acceptance.

## Canonical live V4

[The independently verified V4 archive](live-validation-v4/README.md) records the current queue-selected exact route in session `2ba2b98dd846400ebc5b6b8ca6429b4e`. The authored `lunacrest.glb` binds to native `clamA / enClam / renderer 121306` with seven-bone signature `53cb593a6091824fe4eb312ccacf4f08c45c4ca97b50a4957d862bc13777f0bf`. Its three complete 120-frame captures retain that exact mesh identity through combat idle, `Clam_Attack1`, `Clam_Attack2`, ordinary `Clam_Damaged`, recovery, and the native death cleanup.

The ordinary no-focus attack reduced health from 58 to 50. The separate explicit `KillSingle` fixture triggered `Death`; `DeathFallOff` deactivated `enClam` at reviewed frame 24 before `Clam_DeathDirect` could be seen on the hidden renderer. One guarded Collect then reached strict Ready at level 0 room 2. Review of 26 exact original PNGs found no sampled hinge separation, torn surface, viewport-edge clipping, or unexpected renderer loss. Close attacks pass behind the top HUD, while the hero and native effects obscure fine surface detail. This evidence accepts the clean native hide-and-debris handoff and does not claim an articulated corpse, ordinary lethal damage, DeathLight, DeathRevive, portrait behavior, collision, extended culling, long-session lifetime, full campaign completion, or final art approval.

After regenerating candidate coverage, the execution queue, stage readiness, and campaign, reproduce the exact route with the campaign's current command. For the V4 inputs it was:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_execution_queue_route.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/mirewarden-game \
  --topology-group 0db0dbdf202b8e11 \
  --route-kind directEnemy \
  --profile-document art-experiments/lunacrest-clam/runtime-profile.json \
  --motion-renderer-path enClam \
  --attack-attempts 8 \
  --run \
  --output scratch/model-route-0db0dbdf202b8e11-directenemy-run.json
```

The output path is immutable. Choose a new output path for a later run, and always prefer the newly generated campaign command when any indexed archive, queue input, staged catalog, profile, or asset has changed.
