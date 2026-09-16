# Mournglass Wraith

Original bind candidate for exact chaosBeast renderer121008/enChaosBeast, CEL135754 and actual controller5960 ghostController. A narrow ivory mask sits inside a blue-green hood. Tapered sleeves connect to ivory hands, and one continuous mantle surface runs from chest through waist to a narrow hem. Its lower rings blend the native hip/knee influences. Root accepted this revised bind direction for motion fitting only; native animation and final appearance are not accepted.

The editable Blender scene and original generator retain all31 palette entries and exact bind matrices. The28 native-positive bones are weighted; MiddleFinger3_R/L and Hair_M remain unused but retained. Original generation reads only binding landmarks, never native surface vertices or triangles. Direct and saved/reopened exports, normalized weights, native bind bounds, positive closed-piece volume and independent regeneration pass. Separate pieces overlap; this is not a watertight union or collision-fit proof.

## Two native material slots

The current GLB contains two disjoint primitive groups:588 scrolling-cloth triangles in native slot0 and436 fixed mask/hand triangles in slot1. No triangle is duplicated or omitted. Slot0 uses mournglass.slot0.png, a dedicated repeat-safe blue-green tile without palette atlas cells. Slot1 uses mournglass.slot1.png for fixed ivory and dark details. material-partition-audit.json records this mapping.

Pinned source findings identify native material71 matChaosBeast on slot0 and70 matCHaosBeastFace on slot1. ScrollingUVs135814 updates slot0/_MainTex at rate(0,-.5). A future runtime profile must preserve this native component, rate, phase and explicit material ownership through the reviewed multi-slot API. Authoring does not bake scrolling into animation. Source materials differ in metallic/gloss properties and both have emission; runtime emission choices and actual material readback remain pending.

Native scale1.5 must remain through visual factor1. Preserve the eight native particle systems and FlickerLight/Light. Source has no rigidbodies or colliders, so no ragdoll is assumed. Native death clip4959 calls conditional DeathFade around.776761s; this does not prove runtime visibility or a visible corpse. Native motion, scrolling-phase behavior, effects, portraits and death are future validation gates. No catalog or runtime appearance is frozen.

## Reproduction

Run these exact commands from the repository root with the existing local toolchain and ignored reference at scratch/skeleton-audit/121008. Native references are local inputs and are not distributed with the original model.

```sh
scratch/model-venv/bin/python art-experiments/mournglass-wraith/build_geometry.py
scratch/model-venv/bin/python art-experiments/mournglass-wraith/audit_surfaces.py
scratch/model-venv/bin/python art-experiments/mournglass-wraith/verify_original_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/mournglass-wraith/build_blender.py
scratch/model-venv/bin/python tools/ai-model-pipeline/validate_glb.py art-experiments/mournglass-wraith/mournglass.glb --reference scratch/skeleton-audit/121008/reference.npz
scratch/model-venv/bin/python tools/ai-model-pipeline/validate_glb.py art-experiments/mournglass-wraith/mournglass-reopened.glb --reference scratch/skeleton-audit/121008/reference.npz
```

## Preserved review history

The rejected first generation used two separate lower lobes that read as detached boots, an overly round belly and a provisional single material. Its exact geometry, renders and review are preserved in offline-history/rejected-block-mantle-v1. It is not the current export.

The accepted revised bind generation, exact hero/side, candidate manifest and root review are preserved in offline-history/root-reviewed-connected-v2. That snapshot precedes this documentation correction. Current geometry and textures remain identical to the reviewed generation; updating documentation pins does not extend its acceptance to native motion or live rendering.
