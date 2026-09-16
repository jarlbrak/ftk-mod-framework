# Duskquill Raven

Original charcoal/indigo crow with ivory beak, amber eyes, blue-violet feather bands, broad jointed wings and a five-feather tail. Native bird callbacks and effects remain game-owned. No native surface or texture pixels are included.

![Hero](hero.png)
![Side](side.png)

The proposed profile `ftkmf_modeltest_duskquill` uses crowC, `enCrow` renderer120964, combat profile `b896515d0e2b5954af56c20be69f400685d49f9a16d5309b7e4265cadb64d25c`, `duskquill.glb` + `duskquill_basecolor.png`, minimum health64. Visual factor1 preserves the native CEL scale0.9. Full39 palette and inverse binds remain unchanged. The pipeline's representative120960 and freshly extracted target120964 have exactly equal names and all inverse matrices; `target-binding-proof.json` checks the actual target bounds separately.

Mesh up is+Y and the head/beak faces+Z. Native PortraitCam transforms to approximately(-.8705,.5593,2.7621) in bind mesh space, ahead of the head; its low oblique view may emphasize the beak. Actual native portrait pixels remain a live gate. EncounterCam exists but no camera override is selected. First/only material matCrow86 has zero emission despite its keyword; `_EmissionColorUI` is white. Live portrait/material appearance must be observed; emission opt-out stays false.

Two broad inner wings follow native arm/hand joints; each outer fan follows its three distinct four-joint feather chains. Shoulder collars overlap attachment regions. Original tail feathers follow Tail1/Tail2, retaining Tail_end in the palette. The skeleton has no leg joints, so the small authored talons remain Root_M-rigid; no independent foot motion is claimed. All unused palette names remain available without silently weighting them.

## Rebuild

From repository root with ignored local binding references available:

```sh
scratch/model-venv/bin/python art-experiments/duskquill-raven/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/duskquill-raven/build_blender.py
scratch/model-venv/bin/python art-experiments/duskquill-raven/verify_original_geometry.py
scratch/model-venv/bin/python art-experiments/duskquill-raven/verify_target_binding.py
scratch/model-venv/bin/python art-experiments/duskquill-raven/audit_native_poses.py --label pass
scratch/model-venv/bin/python art-experiments/duskquill-raven/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/231a3a6879ac4f739e143487696cf7e8.json --label hit
scratch/model-venv/bin/python art-experiments/duskquill-raven/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/69794462552a4bd79980d5fbc107049b.json --label death
scratch/model-venv/bin/python art-experiments/duskquill-raven/finalize_manifest.py
```

Editable `duskquill.blend` is saved/reopened before bridge export and independent validation to ignored `scratch/duskquill-roundtrip`. Studio scene is separate. Original provenance proof reruns authoring with native reference reads restricted to names/IBMs and requires byte-identical source/palette/pieces. Native surface reads occur only in separate comparison/bounds validation.

All120 frames of each diagnostic capture are skinned offline, using exact captured bone matrices and original full weights. Six-frame depth grids remove Root_M motion/rotation for articulation inspection; renderer-local travel bounds remain separately reported. This does not show actual world floor/camera placement. The largest pass edge stretch occurs at8 in the right inner wing:0.01825→0.09832 mesh units (5.388×). This is an explicit elbow/wing-fold live review point, not automatically accepted by binary validation. Native AttackSlide events also move the dummy in world space; do not resize or offset the model to cancel them.

The first source encounter recorded a visible nonlethal8 hit58→50, then later endpointHP0 and removal. Its wrapper stopped; removal is flee-consistent but the runtime flee flag was not captured. The separate death capture is explicit KillSingle and animated BirdDeath, not proof of ordinary lethal damage or the first encounter's cause. Neither diagnostic transfers automatic original art acceptance. Original idle/attack/hit/death, portrait, material, culling and progression remain live gates; no bird-family/controller-wide acceptance.

The initial offline review approved this version for live testing: wing tips remain connected in selected poses, with angular inner-wing bends and the measured small-edge stretch retained as limitations.

## Scoped live trial

[Live validation](live-validation.json) now records three complete 120-frame
captures on exact native `crowC`: enemy action, a normal player attack with a
visible 13-point critical hit (58 to 45 HP), and an explicit KillSingle death.
One native Collect returned the game to strict Ready at level 0, room 2.
The original dark raven, ivory beak and attached flight silhouette are visible,
with its face in both native portrait surfaces. The hero occludes much of the
hit pose; feather effects, camera blur and loot UI limit fine death review.
All 120 death frames retained the active renderer and enabled Animator.

Seven selected PNGs, three verified 120-frame videos, raw captures and journals
are preserved in `live-v1/`. The raw runner remains `needs_visual_review`; the
separate root verdict states exactly what was inspected. This is scoped evidence
for this original on `crowC`, not every bird variant, camera or material state.

## Fresh live V2

[The V2 archive](live-validation-v2/README.md) records a fresh catalog-411 run in session `594b337748914937b26473ee389ea71e`. The exact crowC `enCrow` renderer (120964) bound to owner `369188` with the expected binding signature at public visual scale `1.0` (captured native CEL scale `0.9`). Selected idle and attack views retain the dark raven body, wings, head and pale beak through native flight motion. The runtime material uses the authored basecolor, retains the native half-gray tint, and reports black emission with no emission map.

Pass, ordinary attack and explicit `KillSingle` captures are complete 120-frame recordings. The ordinary attack changes HP `58→53` with `cheat=None` and no focus. The fixture reaches native Victory and strict Ready at level 0 room 2; the encounter exposes a Ready vote directly, so no Collect action is claimed. The small airborne silhouette, native UI/effects and victory overlay limit fine feather/eye detail, complete death deformation, settled-ragdoll, culling, portrait/resource lifetime and finished-art acceptance.

## Canonical exact-source V3

[The V3 archive](live-validation-v3/README.md) pins the exact `crowC / enCrow /
renderer 120964` assignment in session `8233b0fa916b47d78e7a6cf5a33ba625`.
Renderer instance `-245364` on owner `369188` uses the authored GLB with the
expected 39-joint signature at native CEL scale `0.9`. The inherited
`matCrow (Instance)` uses `duskquill_basecolor.png`; its emission keyword is
enabled while emission color is black and no emission map is present.

Three complete 120-frame captures preserve the exact renderer through
`BirdFlySlow`, native `birdAttack2`, ordinary `BirdDamage`, recovery, a second
native `birdAttack2`, and explicit-fixture `BirdDeath`. The ordinary no-focus
hit changes the same target from 58 to 50 HP. The separate `KillSingle` fixture
starts at 50 HP, records `m_DoRagdoll=false` and zero rigid bodies, and is
animated death evidence rather than ragdoll or ordinary lethal evidence. The
renderer remains active, enabled, and visible in all 360 retained frames. Two
native Collect actions reach strict Ready at level 0 room 2.

Twenty exact original PNGs accept the attached charcoal and indigo body, pale
beak, amber eyes, segmented wing spans, hit recoil, attack recovery, animated
fall, and sampled spread-wing floor pose. Native effects, the foreground hero,
loot overlay, depth blur, camera movement, and the small flying scale limit fine
surface review. No corpse lifetime after the sampled window is claimed.

An earlier fresh process is preserved in the same archive as rejected boundary
evidence. It retained the exact binding through pass frame 50, then the native
crow deactivated from frame 51 and was destroyed after 107 of 120 requested
frames without a death trigger or combat event. This stochastic escape or
self-removal is not a model-binding failure and is not canonical capture
evidence. Sibling bird rows, other controller clips, portraits, collision,
extended culling, long-session resource lifetime, and final art approval remain
outside V3.
