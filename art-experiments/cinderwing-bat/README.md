# Cinderwing bat

Original charcoal bat with copper membranes, ivory wing ribs, a bone mask, and
ember eyes. The geometry is authored in the actual `batA` flight bind pose.
It is a rigging exercise and original creature model, not a native-mesh reskin.

- Reference: `batA`, `enBat01`, renderer 121104, 53 joints.
- Runtime: 6,600 split vertices, 2,200 triangles; GLB contract validation passes.
- Native forward is -Z, unlike the wolf and cave troll. The common coordinate
  conversion remains the same; the source anatomy faces Blender +Y.
- Membranes have thickness and outward faces on both sides. Subdivided panels
  receive barycentric surface weights; face and claws have explicit rigid groups.
- Canonical V3 live evidence now covers this exact source binding, sampled art,
  idle, two native attacks, ordinary hit and recovery, animated death, one
  Collect, and strict Ready. Broader controller, sibling-source, lifetime, and
  final art coverage remain pending.

## Rebuild

Extract the local reference using the [pipeline guide](../../tools/ai-model-pipeline/README.md)
into ignored `scratch/model-reference/bat-a`. Then, from the repository root:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python art-experiments/cinderwing-bat/build_model.py
scratch/model-venv/bin/python art-experiments/cinderwing-bat/skin_model.py
scratch/model-venv/bin/python tools/ai-model-pipeline/validate_glb.py \
  art-experiments/cinderwing-bat/cinderwing_rigged.glb \
  --reference scratch/model-reference/bat-a/reference.npz
```

Use your local Blender executable on other systems. `cinderwing-bind.blend` is
editable source without the studio stage. The hero/top renders show the bind
pose, not flight validation. [runtime-profile.json](runtime-profile.json)
declares `ftkmf_modeltest_cinderwing` for exact `batA` path `enBat01` and passes
the current static direct-route preflight. That profile is a staging input, not
proof that a future catalog revision inherits this archive's live evidence.

Native meshes, extracted reference data, and game DLLs stay in ignored scratch.
The manifest records original asset hashes and separate validation statuses.

## Recorded live prototype evidence

[live-validation.json](live-validation.json) preserves capture status, hashes,
sampled animation intervals, and remaining checks for the native `batA` chassis.

- [Attack replay](live/attack.mp4): native Pass followed by `attackCrit_bat`
  (frames 42–55) and `batAttackProf` (71–76).
  [Attack still](live/attack-0050.png).
- [Nonlethal hit replay](live/nonlethal-hit.mp4): ordinary native attack displayed
  5 damage, HP 10→5; `batTakeHit` at frames 25–32, then retaliation.
  [Hit still](live/nonlethal-hit-0029.png).
- [Lethal death replay](live/lethal-death.mp4): ordinary native attack displayed
  10 damage, HP 5→0; `batDeath` at frames 23–90.
  [Death motion](live/lethal-death-0032.png),
  [fallen pose](live/lethal-death-0060.png),
  [late pose](live/lethal-death-0090.png).

**The death capture failed early:** its result is `ok=false`, with
`Renderer destroyed during capture`. It contains exactly 92 frames and
8.3417 game seconds, not a completed 120-frame capture. Source review confirms native end-combat cleanup destroys or queues the
avatar for diorama removal. The disappearance is consistent with that lifecycle
and observed room advancement; no runtime callstack proves the exact branch. `m_DoRagdoll` remains false in every recorded frame,
and no rigidbodies were recorded. Animator stays enabled through frame 90 and
is reported disabled in the final sample. This is animated bat death evidence,
not evidence of ragdoll physics. `batDeath` starts at normalized time 0.1375,
so the earliest first-cycle interval is missing even though later frames pass
1.0. The hit and proficiency attack also omit their earliest intervals.

After death, the saved state records combat inactive, no live enemies, an empty
fight order, and dungeon level 0, room 3. XP rises 4→7; gold remains 15. No loot
collection action was recorded for this encounter. The next native Ready slot at level 0, room 3 was verified; native Ready
returned `clicked`, and the following encounter loaded both plant probe meshes.
This confirms inter-room progress, not campaign completion.

Attack and hit videos contain 120 frames (10 seconds at 12 fps). Death contains
92 frames (7.6667 seconds at 12 fps). These are offline fixed-step gameplay
replays, with a 999 max-HP hero and quiet tutorials; they do not demonstrate
real-time performance. Odd source-image heights receive one black padding row
for H.264 compatibility. The selected PNGs are unchanged source copies. Full
raw capture data remains in ignored scratch.

**Art status: prototype.** The parent review found no obvious exploded geometry
and coherent wing folds in the sampled frames, but the small body limits
readability and the hero occludes some death motion. Complete unoccluded visual
review, animation bounds/culling checks, and other controller variants remain
unverified. The studio renders are not substitutes for those checks.

## Native-scale regression (unchanged model)

[Native-scale evidence](live-validation-native-scale.json) records framework
`9e533f89`, session `b3478c0c658f47c6a3ba82b94b9a6f2a`, and unchanged GLB/texture
hashes. Neutral factor1 now preserves native scale0.78; inventory world axes
measure approximately0.78. The small bat stays readable and onscreen in reviewed
views, with coherent hit response and a settled death pose with intact wings.
This size regression passes within those views; fine detail, effects/hero
occlusion, full animation endpoints and culling/other variants remain limited.

- [Native attack](live-native-scale/attack.mp4), reviewed frames0/50.
- [Ordinary nonlethal hit](live-native-scale/nonlethal-hit.mp4), HP58 to50, frame30.
- [Kill-fixture death](live-native-scale/kill-fixture-death.mp4), HP50 to0, frames40/60.

Each capture completed120 unpaused frames over about10.908 game seconds; each
video replays10 seconds at12fps. One guarded Collect reached strict Ready0/2.
This new completed death capture preserves the earlier92-frame failed capture
as historical evidence, and does not turn the prototype into finished art.

The [repeatable V2 archive](live-validation-v2/README.md) pins this native-scale
run as one catalog process with complete pass, ordinary HP58→50 hit and
`KillSingle` HP50→0 captures, one guarded Collect and the strict Ready
continuation boundary. The earlier92-frame renderer-destroyed capture remains
historical; no ordinary lethal or finished-art claim is inferred from the
fixture death.

## Canonical exact-source V3

The [V3 archive](live-validation-v3/README.md) is the canonical record for the
exact `batA / enBat01 / renderer 121104` source assignment. Session
`ea0b19dbfc254df580c87e250e66e6e7` binds `cinderwing_rigged.glb` to one owner
through renderer instance `-245392` and the expected 53-bone signature
`b679ad6b7c78ab349179c3f7babca9077c01d1bb300ff9f047433f44d7236c16`.
The captured native CEL scale is 0.78 and the profile multiplier remains 1.0.

Three complete 120-frame captures preserve the exact renderer through
`batFlySlow`, `batAttackProf`, recovery, `attackCrit_bat`, ordinary
`batTakeHit`, a second recovery and native retaliation, and explicit-fixture
`batDeath`. The renderer remains active, enabled and visible in all 360 frames.
The material uses `cinderwing_basecolor.png`; the live readback retains the
native emission keyword with black emission color and no emission map.

The ordinary no-focus hit reduces the same target from 58 HP to 48 HP. Death
uses the separate explicit `KillSingle` fixture at 48 HP, so it is not ordinary
lethal evidence. `m_DoRagdoll=false` and zero rigid bodies make this an animated
death, not a ragdoll. The sampled floor pose remains visible through death frame
119, but the archive makes no later corpse-lifetime claim. One native Collect
reaches strict Ready at level 0 room 2.

Twenty-two exact original PNGs were inspected at original resolution. They
accept the compact dark body, pale muzzle, red-orange wing membranes, wing
folds, two native attacks, hit response, recovery, animated fall, and sampled
floor pose in normal combat framing. Native UI, bright effects, foreground hero
overlap, loot overlay, depth blur, and the bat's small scale limit fine surface
inspection. Sibling bat sources, other controller clips, portraits, collision,
extended culling, long-session resource lifetime, broader campaign progression,
and final art-direction approval remain outside V3.

Rebuild the immutable archive only from the pinned reviewed inputs with:

```sh
python3 tools/ai-model-pipeline/archive_model_validation_case.py \
  art-experiments/cinderwing-bat/live-validation-v3-plan.json
python3 tools/ai-model-pipeline/verify_model_validation_archive.py \
  art-experiments/cinderwing-bat/live-validation-v3 \
  --check-video-metadata
```
