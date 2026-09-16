# Mossglass cap winding V3

V2 corrected the jade texture color, but native images exposed an open-looking top notch. The independent binary audit confirms both original shell caps faced inward: top cross products pointed down, bottom up. Positive triangle/normal agreement missed this because the generated normals were inward too. Blender's double-sided studio presentation did not expose the native backface-culling defect. This source fault is consistent with the live missing top surface; it is not an explanation for extreme attack framing or the native downward death motion.

V3 reverses exactly32 cap triangles and96 associated normals. All positions, UVs, weights, joints, full three-bone palette/IBMs, primitive assignments, other820triangles and other2460normals are unchanged. It reuses V2's jade-only shell PNG and the original ivory/amber slot0 PNG. Native tiling, scroll phase/rate, emission and scale remain untouched. No V1/V2 file is overwritten.

`audit_caps.py` independently decodes binary GLB accessors, tests geometric exterior directions in bind pose, then applies all360 recorded native pass/hit/death matrices. Corrected direct and saved-reopened caps face outward throughout those observed poses. This checks winding against the exterior, not just agreement with generated normals. It does not establish visible gameplay death, complete culling or attack framing.

`build_caps.py` derives the correction from the hash-pinned original source and asserts the exact unchanged arrays. `build_blender.py` imports the original corrected source into the exact native rig, saves/reopens and independently exports before studio rendering. Studio shell texture uses native2× scaling. Hero/side are studio views only.

Reproduce from repository root:

```sh
scratch/model-venv/bin/python art-experiments/mossglass-reliquary/caps-v3/audit_caps.py
scratch/model-venv/bin/python art-experiments/mossglass-reliquary/caps-v3/build_caps.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/mossglass-reliquary/caps-v3/build_blender.py
scratch/model-venv/bin/python art-experiments/mossglass-reliquary/caps-v3/audit_caps.py --corrected
scratch/model-venv/bin/python art-experiments/mossglass-reliquary/caps-v3/audit_caps.py --corrected --reopened
scratch/model-venv/bin/python art-experiments/mossglass-reliquary/caps-v3/finalize_manifest.py
```

[V1](../live-validation-v1.json) retains its mixed-atlas color failure. [V2](../jade-v2/live-validation-v2.json) preserves jade color correction separately from cap failure, near-screen-edge attack travel, occluded hit views and invisible late-death body. Native effects remain. V3 needs its own live test; no completed-art or lifecycle acceptance is claimed.

## Fresh catalog-411 live validation V4

The fresh repeatable run used session `022120d503914c8f8d0e584a21477f41`, Cube A,
renderer `enJellyCube` (121012), owner `369188`, and bone signature
`320e13b7f80ce3886d35d8718ec2f4b47f84df8210e0ca0ded4513d7c59e65eb`. Both
authored primitives bound through the three-joint palette with native material
slots 0 and 1. The jade shell, corrected cap, ivory rib cage and amber core
remain readable in selected idle and attack views.

The ordinary attack changed the same target from HP 58 to 50 with `cheat=None`
and no focus. The explicit `KillSingle` fixture completed 120 frames, one
native Collect was accepted, and strict native Ready was observed at level 0
room 2. This fixture death is not ordinary lethal damage.

The byte-pinned supplement is
[`live-validation-v4/validation.json`](live-validation-v4/validation.json),
with its offline-only archive script at
[`live-validation-v4/archive.py`](live-validation-v4/archive.py). The archive
validation SHA is `c8ba7c6bf68790897ec5133612cb6ac1a81028c24bcc0d79eedaa34a1f492056`;
it retains 360 source PNG hashes, six selected originals and three 120-frame
presentation videos while excluding native payloads and DLLs. Native effects,
hero/UI occlusion, inherited emission, scroll phase, full culling, floor contact
and final resource lifetime remain separate checks.
