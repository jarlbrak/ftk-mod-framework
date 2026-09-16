# Mournglass Wraith

Original model for exact chaosBeast renderer121008/enChaosBeast, CEL135754 and actual controller5960 ghostController. A narrow ivory mask sits inside a blue-green hood. Tapered sleeves connect to ivory hands, and one continuous mantle surface runs from chest through waist to a narrow hem. Its lower rings blend the native hip/knee influences. The canonical live trial now verifies the exact original binding, two material slots, settled idle, native attack, ordinary hit response, animated disappearance boundary and strict Ready progression.

The editable Blender scene and original generator retain all31 palette entries and exact bind matrices. The28 native-positive bones are weighted; MiddleFinger3_R/L and Hair_M remain unused but retained. Original generation reads only binding landmarks, never native surface vertices or triangles. Direct and saved/reopened exports, normalized weights, native bind bounds, positive closed-piece volume and independent regeneration pass. Separate pieces overlap; this is not a watertight union or collision-fit proof.

## Two native material slots

The current GLB contains two disjoint primitive groups:588 scrolling-cloth triangles in native slot0 and436 fixed mask/hand triangles in slot1. No triangle is duplicated or omitted. Slot0 uses mournglass.slot0.png, a dedicated repeat-safe blue-green tile without palette atlas cells. Slot1 uses mournglass.slot1.png for fixed ivory and dark details. material-partition-audit.json records this mapping.

Pinned source findings identify native material71 matChaosBeast on slot0 and70 matCHaosBeastFace on slot1. ScrollingUVs135814 updates slot0/_MainTex at rate(0,-.5). The current [runtime profile](runtime-profile.json) preserves this two-slot route through the reviewed multi-slot API and passes static direct-route preflight. Authoring does not bake scrolling into animation. Source materials differ in metallic/gloss properties and both have emission; the profile opts out of both inherited emissions. The canonical trial reads both Standard material instances using the authored slot textures with emission disabled, zero emission color and no emission map.

Native scale1.5 must remain through visual factor1. Preserve the eight native particle systems and FlickerLight/Light. Source has no rigidbodies or colliders, and the live renderer reports `m_DoRagdoll=false` with zero rigidbodies. Native death clip4959 calls conditional DeathFade around.776761s. The canonical trial observes `death_ghost` begin at retained frame18, with the renderer active and visible through frame19 and inactive, disabled and not visible from frame20. A visible corpse is not expected from those observations. Native particles and their materials remain unchanged.

## Canonical live validation

The immutable [V2 canonical archive](live-validation-v2-canonical/README.md)
pins the exact `chaosBeast / enChaosBeast / 121008` source, current profile,
three original assets, 334 retained source frames, 30 inspected original PNGs
and three presentation videos. Integrity verification passes with the root
review and all source, capture and asset mappings intact.

The pass capture preserves `cidle_ghost`, native `attackProf_ghost` and return
to idle. Its recorded native action deals 18 hero damage. The ordinary
zero-focus player action records `Damaged`, damage5 and new enemy HP53 from58,
then `damageSmall_ghost` and idle recovery. A later Ready-state poll finds HP45
after additional combat turns. No retained causal event explains that extra
eight-point loss, so the archive preserves it as a later net state without
attributing it to the ordinary hit.

The separate `KillSingle` fixture reduces the later HP45 state to0 for recorded
damage1000 and `Death`. This is fixture death evidence, not ordinary lethal
gameplay. The accepted 94-frame prefix ends at renderer destruction. It proves
the visible opening of animator-driven `death_ghost` and the inactive boundary,
but not a fully visible death clip, cleanup causality, later corpse lifetime or
final disposal. Strict Ready succeeds at dungeon level0 room2 without a Collect
submission.

## Historical recorded native motion

The [exact diagnostic](../../docs/evidence/chaosbeast-diagnostic-v1/validation.json)
provides 120 pass frames, 120 successful-hit frames and a 91-frame partial
explicit-death recording. The single-material calibration probe does not verify
the original's two material slots. The native renderer was disabled at frame29;
destruction interrupted the recording after frame90. That raw failure remains
preserved rather than being labeled a complete death capture.

`audit_native_poses.py` applies all331 retained bone poses to the unchanged
original surface. Four reviewed sheets cover attack, hit, the retained death
prefix and the worst shoulder stretch. The mantle, sleeves, shoulder drapes,
hands and mask remain connected in those projections. The maximum recorded
edge ratio is1.80146 at pass45 on the right shoulder drape (.047624→.085793).
This is an observed measurement, not a universal stretch threshold.
The death sheet labels poses whose native renderer was already hidden.

These sheets use per-triangle colors from the two original textures, with no
shader, UV scrolling or native FX simulation. Pass/hit sheets remove root
travel for anatomy review; the death sheet retains it. The
`root-native-pose-review.json` records exactly which images were viewed.
The final manifest freezes this generation for a live trial, with no live
original-model acceptance or all-controller claim.

## Reproduction

Run these exact commands from the repository root with the existing local toolchain and ignored reference at scratch/skeleton-audit/121008. Native references are local inputs and are not distributed with the original model.

```sh
scratch/model-venv/bin/python art-experiments/mournglass-wraith/build_geometry.py
scratch/model-venv/bin/python art-experiments/mournglass-wraith/audit_surfaces.py
scratch/model-venv/bin/python art-experiments/mournglass-wraith/verify_original_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/mournglass-wraith/build_blender.py
scratch/model-venv/bin/python tools/ai-model-pipeline/validate_glb.py art-experiments/mournglass-wraith/mournglass.glb --reference scratch/skeleton-audit/121008/reference.npz
scratch/model-venv/bin/python tools/ai-model-pipeline/validate_glb.py art-experiments/mournglass-wraith/mournglass-reopened.glb --reference scratch/skeleton-audit/121008/reference.npz
scratch/model-venv/bin/python art-experiments/mournglass-wraith/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/38a4c40f81b64e4e913ce926e31891e9.json --label pass --steps 0 42 48 54 60 104
scratch/model-venv/bin/python art-experiments/mournglass-wraith/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/1c0fb46ab57849218c81a487c074cc5e.json --label hit --steps 0 28 30 34 36 80
scratch/model-venv/bin/python art-experiments/mournglass-wraith/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/89ded9e532624d04b2ee62dcc93af2e9.json --label death-prefix --steps 0 25 27 28 29 60 --keep-root-motion
scratch/model-venv/bin/python art-experiments/mournglass-wraith/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/38a4c40f81b64e4e913ce926e31891e9.json --label shoulder-worst --steps 42 44 45 46 48 50
```

## Preserved review history

The rejected first generation used two separate lower lobes that read as detached boots, an overly round belly and a provisional single material. Its exact geometry, renders and review are preserved in offline-history/rejected-block-mantle-v1. It is not the current export.

The accepted revised bind generation, exact hero/side, candidate manifest and root review are preserved in offline-history/root-reviewed-connected-v2. That snapshot precedes this documentation correction. Current geometry and textures remain identical to the reviewed generation; updating documentation pins does not extend its acceptance to native motion or live rendering.

`offline-history/before-native-pose-review` preserves the later bind candidate
and all27 files it pinned before the recorded-pose documentation was added.
The old candidate manifests remain historical; use `manifest.json` for the
generation selected for the live trial.
