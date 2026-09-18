# Mirewarden FTK model test

Original enemy art based on `concept.png`, generated with the built-in image
generation tool. The full prompt is recorded in `concept-prompt.txt`.

## Files

- `mirewarden_rigged.glb`: FTK runtime contract, 9,900 flat-shaded vertices,
  3,300 triangles, 37 named skin joints. Not a general-purpose glTF asset.
- `mirewarden_basecolor.png`: original 256px palette texture.
- `mirewarden-bind.blend`: editable original geometry in the actual troll T-pose.
- `mirewarden-preview.blend`: studio presentation with an approximate arm pose.
- `hero.png`, `front.png`, `back.png`: renders of the authored model.
- `source_mesh.json`: flattened authored geometry, palette UVs, explicit bone tags.
- `build_model.py`: reproducible Blender model builder.
- `validation.json`: independent binary and skin-contract check.

The extracted game reference lives only in gitignored `scratch/model-reference`.
It must not be included in an asset package or commit.

## Current route profile

[runtime-profile.json](runtime-profile.json) declares `ftkmf_modeltest_mirewarden`
for exact `trollCaveA` renderer `enTroll01`. It passes the current static
direct-route preflight and provides a repeatable staging input. Its static
result does not transfer the historical live archive to a new catalog revision.

## Rebuild

Use the extraction instructions in `tools/ai-model-pipeline/README.md` first,
with local reference output at `scratch/model-reference`. Then, from repo root:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/mirewarden-ftk/build_model.py
scratch/model-venv/bin/python tools/ai-model-pipeline/export_ftk_glb.py --source art-experiments/mirewarden-ftk/source_mesh.json --reference scratch/model-reference/reference.npz --output art-experiments/mirewarden-ftk/mirewarden_rigged.glb
scratch/model-venv/bin/python tools/ai-model-pipeline/validate_glb.py art-experiments/mirewarden-ftk/mirewarden_rigged.glb --reference scratch/model-reference/reference.npz
```

## Local integration

The Content change in `RealmBossAdventure` opts into these filenames only when
`FTK_MIREWARDEN_BODY=1`. `FTK_BASELINE_STOCK_BODY` must be unset. Normal launches
keep the existing boss configuration.

Place the GLB and PNG in the game's
`BepInEx/plugins/FTKModFramework_content/models/` directory. Build and install
the matching framework DLL, then launch with `FTK_AGENT_BRIDGE=1` and
`FTK_MIREWARDEN_BODY=1` for a single-player test. Back up existing saves and
the installed framework before testing. Do not run two installation workflows
against the same game directory concurrently.

## Live test results

The corrected model loaded inside FTK's Flooded Crypt on 2026-09-05/06.
The runtime reported 9,900 vertices, 3,300 triangles, 37 matched bones, and
zero dropped joint slots. `ingame-idle.png` is the clean in-game capture with
the normal loader material. The combat UI generated its portrait from the
custom model.

Verified: Release build (zero warnings/errors), live realm-boss SELF-TEST PASS,
GLB binary layout, joint names, normalized weights, bind-pose identity,
triangle winding, loader UV convention, palette colors, and mesh loading.
Live idle diagnostics show 19 bones changing rotation between samples; the
renderer remained visible and articulated. A normal `combat_turn` with
`cheat: None` completed, the boss's response reduced the hero from 38 to 18 HP,
and control returned to `Wait For Stance`. This demonstrates functioning
combat with the replacement mesh, not a full adventure playthrough.

`ingame-neutral.png` compares the live body with emission disabled through
scratch-only test support. The normal loader emission was restored afterward.
The package retains that normal material behavior.

The subsequent explicit-API test used a separate `trollCaveA` clone and completed
native hits, retaliation, lethal damage, ragdoll collapse, loot collection, and
return to the next-room Ready screen. See [live-validation.json](live-validation.json),
[combat footage](ingame-combat.mp4), and [death footage](ingame-death.mp4).
The hero had 999 maximum HP for this disposable fixture; the killing action
used native `CheatKillSingle`. Footage uses fixed simulation steps, not a
real-time performance benchmark or ordinary balance test.

The accepted captures have zero paused frames and 10.86/10.91 measured simulation
seconds. Death switches to native ragdoll physics, so the Animator stops early
while bones continue moving. The stone pieces fall and settle; their individual
triangles retain edge lengths within 0.0001% of the initial live pose. Separately
articulated stones can visibly separate in the ragdoll. Other attack variants,
extreme-pose intersections, final artistic refinement, and campaign progression
remain outside these checks. The studio pose is not animation evidence.

Live testing uses a separate APFS-cloned game under gitignored
`scratch/mirewarden-game`, with bridge port 8788 and a separate test save path.
The normal Steam installation is not the test target. Scratch helpers and
extracted reference data are excluded from this package.

The initial captures (`ingame-boss-first.png`, `ingame-boss-clear.png`) record
a rejected inside-out mesh. They are diagnostic failures, not acceptance
images. The current ZIP contains the corrected mesh and matching SHA-256
manifest. Install its `FTKModFramework_content` folder under `BepInEx/plugins`;
the matching opt-in framework build is required separately.

## Native-scale regression and initial apparel motion

[Native-scale evidence](live-validation-native-scale.json) records unchanged
Mirewarden geometry under frameworkb4554004 in session5754790cd92e4557b28b6f728024815c.
Neutral factor 1 preserves native0.95; inventory renderer world axes measure0.95.
The rock body remains coherent in reviewed attack/hit views. Ragdoll shows
rock-segment gaps and separated limb chunks, an existing art limitation rather
than a new scale explosion. This scoped size regression passes, not flawless
art, complete animation or culling/variant acceptance.

- [Attack](live-native-scale/attack.mp4), reviewed frames0/50.
- [First player apparel attack](live-native-scale/player-apparel-attack.mp4),
  Gambeson target, ordinary 8 damage 58 to50, reviewed 30/78.
- [Enemy hit](live-native-scale/nonlethal-hit.mp4), ordinary 8 damage 50 to42, frame30.
- [Kill-fixture death](live-native-scale/kill-fixture-death.mp4),42 to0, frames40/60.

All four captures completed 120 unpaused frames over about 10.908 game seconds.
Death disables Animator at frame28 with14 native rigidbodies. Two guarded
Collect actions reach strict Ready0/2. Videos replay12fps and establish no
real-time performance claim. The apparel capture preserves custom foot/body
markers but native backpack, helmet and weapon occlude them; equipment-cycle
and full playercatalog validation remain separate pending tests.

The [repeatable V2 archive](live-validation-v2/README.md) pins this native-scale
run as one catalog process with complete pass, ordinary HP 50→42 hit and
`KillSingle` HP 42→0 captures, native ragdoll telemetry, two guarded Collects and
the strict Ready boundary. Rock-segment gaps remain an art limitation; the
fixture death is not ordinary lethal acceptance.

The canonical [V3 exact-source archive](live-validation-v3/README.md) repeats
the current direct-enemy route for exact `trollCaveA / enTroll01 / renderer
121153`. Three complete 120-frame captures retain the exact renderer through
`cidle_troll`, `attackProf_troll`, ordinary HP 58→48 plus `damage_troll`
recovery, `attack_troll`, and the separate explicit-fixture
`deathHeavy_troll`. All 360 retained frames keep the renderer active, enabled,
visible, and identity-stable. The fixture records `m_DoRagdoll=true`, 11
surviving bodies, and all 11 nonkinematic from frame28 as the model physically
falls to the floor. One guarded Collect reaches strict Ready0/2.

Twenty-eight original PNGs were reviewed at full resolution. Intentional gaps
between the prototype rock masses remain visible, and native `trollCave_e`
emission warms the pale authored texture. The fixture is not ordinary lethal
evidence, no corpse lifetime after retained frame119 is claimed, and this
direct-enemy result does not credit sibling troll sources or the separate
Gloamcap resource-prefab route.
