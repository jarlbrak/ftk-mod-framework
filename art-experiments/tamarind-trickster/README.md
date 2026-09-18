# Tamarind Trickster

Original brown-and-cream monkey candidate for exact monkeyC renderer 121301/enMonkeyBasey, CEL138586/enBaseyMonkey and controller5979. The editable Blender scene, original generator and palette contain no native surface geometry. Full42 palette and exact bind matrices are retained;35 native-positive bones are used,7 unused entries remain. Direct and saved/reopened exports, positive closed-piece volume, bind bounds and binding-only regeneration pass. Overlapping pieces are not a watertight union.

The root-reviewed hero and side, matching source/export bytes and initial review are preserved under offline-history/root-reviewed-bind-v1. Root accepted the initial palette, seated eyes and chest direction. The pronounced lower-jaw shelf and straight tapered tail need focused native mouth, shoulder and tail motion studies. The canonical native monkeyC V2 arrival records exact binding, settled appearance, native attack and self-removal. Its acceptance remains bounded by the limits below. root-bind-review.json preserves the later independent check; its pending-source wording is historical.

## Verified source constraints

source-findings.json pins the architect's exact native analysis. Native scale 1 and +Z front agree with authored landmarks; tail extends -Z. The sole body material is1056 matLoot with emission1.4. [runtime-profile.json](runtime-profile.json) declares the exact `monkeyC` `enMonkeyBasey` route with native emission disabled and passes current static direct-route preflight. It is a repeatable staging input; live material readback and any later catalog revision still require their own observation.

The prefab has11 rigidbodies,10 CharacterJoints and11 colliders; these remain native and do not establish collision fit for the original visual surface. PM_DeathDirect5032 invokes DeathFade around0.0796s and DeathFallOff around0.1719s. Indirect5195 invokes DeathFallOff around0.8708s and DeathFade around0.8806/0.9022s. Fade depends on runtime m_FadeMaterials. These source events do not guarantee actual fade, ragdoll activation, a visible corpse or120 enabled frames. Future captures must record those facts and distinguish analytical surfaces after hide from game-visible geometry.

The body prefab has no rigid MeshRenderer, but the row separately assigns monkeyBarrelCurse through native weapon holders. Retain that weapon, its attachment transforms, native effects and AI. Body-only findings do not describe every runtime weapon renderer or callback. No sibling monkey/controller inheritance is claimed.

Reproduce with scratch/model-venv/bin/python build_geometry.py, audit_surfaces.py and verify_original_geometry.py from this directory's scripts; run Blender --background --python build_blender.py for the editable/reopened model and studio views. The exact diagnostic arrival supplies offline pose fitting. Canonical V2 covers the custom body binding, selected settled views, native attack and native self-removal. Original in-game portraits, continuous material behavior and detailed weapon overlap remain pending.

## Recorded arrival pose fit

The [native arrival evidence](../../docs/evidence/monkeyc-arrival-native-v1/README.md) preserves the diagnostic body, actual enSuicideCurse attack, synchronous58-to-0 secondary damage and two native Collects. The120samples cover64.234680wall seconds and6.283203game seconds. Body inactivity starts at sample108; the later indirect-death poses are analytical and were not visible surfaces.

`audit_native_poses.py` applies every captured bone matrix and native inverse bind matrix to the original source. It checks full bone order and stable renderer/CEL/owner identity, records all-frame edge stretch and bounds, and renders selected views with an explicit hidden-body label. It does not render native meshes, weapons, shader effects or lighting. Root-motion normalization is for anatomy inspection; `--keep-root-motion` retains renderer-local travel.

The first study exposed a pointed shoulder flap and3.452137maximum edge stretch. Earlier bytes and views remain in `offline-history/before-arrival-shoulder-fit` and `offline-history/arrival-shoulder-weights-only`. Raising/straightening the original shoulder segment and blending chest/scapula/shoulder weights removes the low flap in reviewed views. The revised maximum remains2.999987, which is evidence to inspect rather than a blanket pass threshold. The full 42 bone palette,35weighted bones,1968triangles and original palette remain. `root-native-pose-review.json` pins actual reviewed views and their limitations.

From the repository root, repeat the study using the pinned raw capture path (or an identical decompressed archive):

```sh
scratch/model-venv/bin/python art-experiments/tamarind-trickster/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/3dcae051935d428bb2a62178954f8a67.json --label arrival --steps 0 15 30 95 105 119
scratch/model-venv/bin/python art-experiments/tamarind-trickster/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/3dcae051935d428bb2a62178954f8a67.json --label arrival-side --steps 0 15 30 95 105 119 --view 1 .15 -.1
scratch/model-venv/bin/python art-experiments/tamarind-trickster/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/3dcae051935d428bb2a62178954f8a67.json --label shoulder-worst --steps 90 91 94 95 96 99 --view -.7 .25 1
```

Direct binary validation, saved/reopened Blender export, positive closed-piece volume, bind-bound containment and binding-only original geometry regeneration pass after the change. Studio or diagnostic acceptance is not original live acceptance. Use passive arrival capture for the original trial without changing native AI or initiative.

## Canonical live arrival V2

Session `d09d285440894f2f8eeca877568975b3` used the exact catalog-411 profile `ftkmf_modeltest_tamarind` on `monkeyC`, renderer `enMonkeyBasey` (121301), owner `369188`, bone signature `3fa26f0f6ae1e210b9b1e1126d3365aa41e40e9a479f03d52b712ad08dd5e3ed`, and visual scale `1.0`. Strict native Ready at level 0, room 2 was recorded immediately before the passive arrival runner armed once and clicked native Ready once. The complete capture retains 120 samples. Samples 40 and 60 show the authored monkey and held barrel clearly. Native `PlayAttackSequence` enters `enSuicideCurse` at game frame 29945, secondary damage occurs at frame 29972, sample 90 includes the purple suicide effect, HP falls from 58 to 0, and the renderer is inactive from sample 108.

Tamarind's exact native suicide proficiency ends the encounter before an ordinary hero action can be submitted or a received-hit phase can exist. The archive records `hitMotion` and `ordinaryDamage` as `not_applicable_native_behavior` under `ftkmf.source-specific-evidence-applicability.v1`, using the exact workflow, narrow reason codes and evidence pointers to the native suicide fixture. This applicability does not transfer to sibling monkey sources. The same capture supplies structured idle, native-attack and native-self-removal records; it does not claim ordinary lethal damage, post-combat loot, a visible full death animation or a corpse. The first 25 samples document native entry-camera settling rather than a stable art view. Native effects and hero occlusion limit fine material and weapon inspection, and the runner's initial `currentAttackInfo` remains classified as potentially stale.

The [V2 validation record](live-validation-v2/validation.json), [lossless archive README](live-validation-v2/README.md), [root frame review](../../scratch/tamarind-root-visual-review.json) and [archive byte review](../../scratch/tamarind-root-archive-review.json) preserve the raw capture, journals, helper responses, 120 source-image pins, seven selected originals and a 120-frame presentation video. Rebuild the archive from the repository root with:

```sh
python3 art-experiments/tamarind-trickster/live-validation-v2/archive.py
```

The script is offline-only and refuses a completed destination. It intentionally excludes game DLLs and native payloads; the authoring manifest and asset hashes remain pinned in the archive.
