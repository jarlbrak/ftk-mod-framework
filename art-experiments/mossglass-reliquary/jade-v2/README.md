# Mossglass jade texture V2

V1 live material color failed: the shell showed gold/orange columns despite correctly bound original geometry. [V1 evidence](../live-validation-v1.json) preserves three120-frame captures, normal8damage, explicit fixture death, two Collect votes and strict Ready0/2. Its initial wrong scene-path attempt stopped with zero game actions; the read-only journal and retired local claim are retained. Native scroll recurrence passed independently; that numerical result does not repair the color failure. Death40/60 shows native effects with no discernible original body; death visibility and resource lifecycle remain unaccepted.

[The diagnosis](v1-tiling-diagnosis.json) uses actual live slot1 scale(2,2), Repeat/Bilinear and original float32 UVs. Doubling atlas U centers moves shell samples into ivory/amber/brown palette boundaries. Native lighting can further affect color but does not explain away this concrete sampling defect.

V2 replaces **only the shell PNG** with a seamless, wholly jade vein tile. Runtime reuses the exact original `mossglass.glb` and `mossglass.slot0.png`; geometry, UVs, palette/IBMs, native material scale, native emission, scroll rate/phase and slot assignment remain unchanged. The separate profile key is `ftkmf_modeltest_mossglass_jade_v2`. No V1 file is overwritten.

The editable V2 scene changes only the shell image. Hero/side studio images explicitly include native2x shell texture scaling; they omit Unity lighting/material details. The reopened scene independently exports and validates; the original pinned GLB remains the runtime payload. Pattern visibility and perceived jade color at combat scale require the next live trial. The side remains shell dominated.

Reproduce from repository root:

```sh
scratch/model-venv/bin/python art-experiments/mossglass-reliquary/jade-v2/diagnose_v1_tiling.py
scratch/model-venv/bin/python art-experiments/mossglass-reliquary/jade-v2/build_texture.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/mossglass-reliquary/jade-v2/build_studio.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/mossglass-reliquary/jade-v2/check_reopened.py
scratch/model-venv/bin/python tools/ai-model-pipeline/validate_glb.py art-experiments/mossglass-reliquary/jade-v2/mossglass-jade-v2-reopened.glb --reference scratch/cube-topology-analysis/reference-121012/reference.npz
```
