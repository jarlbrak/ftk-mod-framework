# Ashfang wolf model exercise

Original low-poly wolf geometry for the verified `wolfA` combat chassis. The
charcoal coat, silver face, autumn-colored ruff, amber eyes, and exposed fangs
are authored in Blender Python. No native mesh geometry is included.

Source: `build_model.py` and editable `ashfang-bind.blend`. Native reference is
local-only at ignored `scratch/model-reference/wolf-a`, renderer 121142 in this
installation's resources.assets. The earlier discovery example renderer 120975
is not used for this export. Verify IDs against a fresh enemy mapping.

From the repository root:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/extract_reference.py --assets "$HOME/Library/Application Support/Steam/steamapps/common/For The King/FTK.app/Contents/Resources/Data/resources.assets" --renderer-id 121142 --output scratch/model-reference/wolf-a
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/ashfang-wolf/build_model.py
scratch/model-venv/bin/python art-experiments/ashfang-wolf/skin_model.py
scratch/model-venv/bin/python tools/ai-model-pipeline/validate_glb.py art-experiments/ashfang-wolf/ashfang_rigged.glb --reference scratch/model-reference/wolf-a/reference.npz
```

`skin_model.py` transfers blended weights from the nearest native surface, then
applies explicit rigid overrides for face and paw details. This is an authoring
choice for this example, not an automatic skin solution for every creature.
The saved `.blend` is editable bind-pose geometry; the exported GLB contains the
skin. `source_mesh.json` records the final weights for inspection/reproduction.

Current result: 9,564 split vertices, 3,188 triangles, 33 joints. Binary/skin
validation passes; median nearest-surface transfer distance 0.0342 and maximum
0.4190 mesh units. Those distances are diagnostic, not an artistic pass/fail rule.
The ears extend slightly above the native reference's rest bounds, so live
culling and animation bounds need checking.

`hero.png` and `side.png` are studio bind-pose renders. Runtime file names are
`ashfang_rigged.glb` and `ashfang_basecolor.png`; install under
`BepInEx/plugins/FTKModFramework_content/models` with matching content wiring.
[runtime-profile.json](runtime-profile.json) now declares the exact `wolfA`
`wolf01` route and passes current static direct-route preflight. It is a
repeatable staging input, not a transfer of historical evidence to a later
catalog revision.

## Canonical exact-source evidence

The immutable [V2 canonical archive](live-validation-v2-canonical/README.md)
pins the current profile, original GLB and texture, exact `wolfA / wolf01 /
121142` identity, all 360 capture PNGs, 24 reviewed original frames and three
presentation videos. Its integrity verifier passes with video metadata checks.

The fresh isolated run preserves settled `cidle_wolf`, native
`attackProf_wolf`, ordinary `damaged_wolf` and recovery, two later native
`attack_wolf` sequences, and explicit-fixture `deathHeavy_wolf` followed by the
14-body ragdoll. The ordinary zero-focus unmodified hammer attack reduced
Ashfang from 58 to 50 HP for native `Damaged` and 8 damage. The separate
`KillSingle` fixture reduced 50 to 0 for native `Death` and 1000 damage. One
guarded Collect reached strict Ready at dungeon level 0, room 2.

All 14 bodies remain kinematic through death frame 26. At frame 27 the animator
disables and every body becomes dynamic. Measured motion is nonzero from frames
28 through 48, then zero through frame 119. The exact custom renderer remains
active, enabled, reported visible and identity-stable throughout all three
complete captures. This bounded result does not establish general corpse
lifetime, cleanup causality or final disposal.

The reviewed originals show one coherent custom wolf across idle, upright
proficiency, hit recoil, forward attacks, airborne ragdoll and the settled side
pose. Native effects, the foreground hero, combat UI, loot UI and late depth
blur limit stated views. The Standard `matWolfA` material uses the authored
`ashfang_basecolor.png` main texture while retaining native `wolfA_e` emission.
Portraits, collision, all-angle culling, every animation, sibling wolf sources,
long-session resource lifetime and finished-art approval remain open.

## Historical prototype evidence

Ashfang bound to the native `wolfA` chassis at CEL-relative renderer `wolf01`
in a disposable dungeon encounter. [live-validation.json](live-validation.json)
records source hashes, runtime identity, sampled animation intervals, ragdoll
telemetry, and remaining checks.

- [Attack replay](live/attack.mp4): `attack_wolf`, frames 43–58; earliest attack
  interval is missing. [Mid-attack still](live/attack-0050.png).
- [Dodge and attacks replay](live/dodge-attacks.mp4): `dodge_wolf`, frames 19–37;
  another normal attack and a partial `attackProf_wolf` interval at 105–119.
- [Lethal hit and ragdoll replay](live/lethal-ragdoll.mp4): native ordinary attack,
  displayed 10 damage, enemy HP 8→0. [Hit](live/lethal-ragdoll-0032.png),
  [fallen pose](live/lethal-ragdoll-0055.png),
  [final pose](live/lethal-ragdoll-0119.png).

The lethal capture records Animator disabling at frame 26 and 14 nonkinematic
rigidbodies moving, then a 6.60 game-second low-velocity tail. The frozen
`deathHeavy_wolf` normalized time after the handoff is not an animation-progress
failure: motion is then recorded by the rigidbody telemetry. This establishes
the observed native handoff, not a general physics or visual-quality guarantee.
Native coin and Godsbeard loot were confirmed by the operator; the saved
post-loot state records combat inactive, gold 11→15, XP 0→4, and dungeon room 1→2.
The next native Ready slot was verified at level 0, room 2, and the native
`VoteButton.OnLeftClick(Ready)` action returned `clicked` for the following
Cinderwing encounter. This verifies inter-room progress, not campaign completion.

These are offline fixed-step gameplay captures, replayed at 12 fps: 120 frames,
10 seconds of video, approximately 10.908 game seconds per capture. The fixture
used 999 hero max HP and quiet tutorials. Source PNGs are 1280×831; videos pad one
black bottom row to 1280×832 for H.264 compatibility. No real-time performance
claim follows from this timing. Full raw capture JSON remains local in ignored
`scratch/mirewarden-game/model-test-output`; selected PNGs are unchanged copies.

**Historical art status: prototype.** The architect's 17-frame sampled review reported no
obvious catastrophic deformation, but occlusion limits visibility. Thin
pole-like legs, a spike-like tail, and armor-like orange fins still need artistic
revision. Review of the complete unoccluded motion and silhouette is pending.
No nonlethal received-hit capture exists; the proficiency attack is partial.
Full culling-envelope checks and other controller variants remain unverified.

## Nonlethal-hit followup (same original assets)

[Followup evidence](live-validation-hit-followup.json) records framework900f/
helper434, sessionb3322a5d049e4638acecf30928de8d06. The ordinary attack now
establishes nonlethal8 damage (58 to50) with damaged_wolf and attack/idle
telemetry. Reviewed frame30 is mostly hero-occluded, so this does not prove
full-body visible hit deformation. The kill fixture50 to0 completes120 frames;
reviewed40/60 show rolling/collapse with the existing thin-leg/fin art limits.

- [Nonlethal hit](live-hit-followup/nonlethal-hit.mp4)
- [Kill-fixture death](live-hit-followup/kill-fixture-death.mp4)

Both captures contain120 unpaused frames, replayed at12fps; no real-time
performance claim. One guarded Collect reaches strict Ready0/3. Original
geometry and earlier evidence remain unchanged; culling and final polish
are not established by the successful damage result.
