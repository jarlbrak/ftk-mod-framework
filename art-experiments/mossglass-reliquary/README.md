# Mossglass Reliquary

Original rounded jade gel shell with an open ivory seed cage and faceted amber core, authored for native `cubeA` / `enJellyCube` renderer121012. Front is +Z, up +Y. The native CEL scale1.1 is preserved by profile factor1. Side views deliberately emphasize the opaque shell rather than a readable face.

Two genuine GLB primitives share the exact three-joint native palette and inverse binds. Primitive0 maps explicitly to native material slot0: fixed ivory/amber interior UVs. Primitive1 maps to native slot1: opaque jade shell with vertically repeating vein bands. The original native `ScrollingUVs` component retains its rate(0,0.2), `_MainTex`, phase and slot1 mapping through the reviewed owned-material compatibility seam. Both native shaders are opaque; the authored opening exposes the interior without transparency. Both slots initially retain native emission. Actual original-model color and scrolling readability at combat scale remain live gates.

The shell and ivory arch use graded Root_M / jellyCubeMid / jellyCubeTop weights. The amber seed uses the middle joint; the ribs have local middle/root blends. Full three-joint palette and inverse binds remain unchanged. This is entirely original analytic ring/tube/ellipsoid geometry and original PNG pixels; no native surface is copied. `verify_original_geometry.py` reruns source generation with native access limited to bone names and bindposes, reproducing geometry and textures exactly. Native bounds checking occurs separately after generation.

`mossglass.blend` is the editable rigged source; `mossglass-studio.blend` adds only non-exported studio presentation. Direct and saved/reopened GLBs independently validate. Bind geometry fits the native reference bounds; this is not proof of animation-envelope culling.

The first rib version sat behind the ivory jamb and separated visibly in the offline death study. `offline-history/rib-seat-failure-death.png` preserves that rejected view, reconstructed from the exact previous authoring constants after the active outputs had regenerated. Its audit pins the previous source hash. The corrected ribs begin on the interpolated jamb center, overlap its volume and share the matching local weights. This changes six rib seats only; the approved silhouette, shell, arch, seed and palette stay the same.

`native-pass`, `native-hit` and `native-death-junction` studies apply full original weights/IBMs to all120 recorded native frames per capture; sheets select six frames. Anatomy sheets remove Root_M travel, while `native-death-travel-pose-study.png` retains it in renderer-local coordinates. Reports separately record travel bounds. These are depth-tested original-geometry views with fixed all-frame framing and static palette colors, not Unity material/culling renders or evidence of visible scrolling. Root travel and native ground effects are not altered. The calibration's independent world-space audit supports downward death displacement; floor height and final original death visibility remain unverified.

Reproduce from the repository root with the locally extracted121012 reference and pinned calibration captures available:

```sh
scratch/model-venv/bin/python art-experiments/mossglass-reliquary/build_geometry.py
scratch/model-venv/bin/python art-experiments/mossglass-reliquary/verify_original_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/mossglass-reliquary/build_blender.py
scratch/model-venv/bin/python art-experiments/mossglass-reliquary/audit_native_poses.py
scratch/model-venv/bin/python art-experiments/mossglass-reliquary/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/6933d3ba40d84c31871be2ce96e5d36a.json --label hit --steps 0 26 30 40 60 119
scratch/model-venv/bin/python art-experiments/mossglass-reliquary/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/cd585a4165c8428fac8bfba859d509f6.json --label death-junction --steps 24 28 29 30 40 60
scratch/model-venv/bin/python art-experiments/mossglass-reliquary/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/cd585a4165c8428fac8bfba859d509f6.json --label death-travel --steps 0 24 29 30 60 119 --keep-root-motion
scratch/model-venv/bin/python art-experiments/mossglass-reliquary/finalize_manifest.py
```

Root reviewed hero/side and corrected selected native death-junction/hit/travel views and approved offline direction for live testing. No live original acceptance, finished-art completeness, material cleanup, clone independence or whole-controller coverage is claimed. Minimum health64 is a test fixture setting. Use the multi-slot Core/content versions together; the legacy single-primitive API cannot express this model.

## Canonical exact-source validation V5

[The canonical V5 archive](live-validation-v5/README.md) pins fresh session
`82de4aab601e4970b0a2c854f2b2d71e` against the exact `cubeA / enJellyCube /
121012` assignment and expected three-bone signature. One owner completes three
120-frame captures: settled idle plus native `AttackCrit` and `AOE_jelly`, an
ordinary no-focus HP 58 to 53 hit with native `Damaged` and `wobble_jelly`, and
an explicit `KillSingle` fixture with native `Death` and `deathHeavy_jelly`.
Two guarded Collect actions reach strict Ready at level 0, room 2.

Eighteen reviewed original PNGs accept the rounded jade shell, outward-facing
cap, open ivory cage, seated amber core, attack compression, hit recoil and
recovery, and the native animated disappearance into the sampled loot handoff.
The active renderer and enabled Animator retain `m_DoRagdoll=false`; the custom
surface leaves view by the early death frames, so no visible corpse or physics
ragdoll is claimed. Bright effects and the foreground hero obscure both causal
impact frames.

Per-frame material readback records slot 0 remaining fixed while the native
`_MainTex` scroller targets authored slot 1 at rate `(0, 0.2)` with advancing
phase. Both authored material regions remain visibly distinct. Selected stills
do not by themselves prove perceptually continuous scrolling, clone
independence, or long-session lifetime. The explicit fixture is not ordinary
lethal evidence, and no Cube E, resource prefab, sibling chassis, or topology
peer receives credit.

Rebuild this immutable supplement through the generic reviewed-case workflow:

```sh
python3 tools/ai-model-pipeline/archive_model_validation_case.py \
  art-experiments/mossglass-reliquary/live-validation-v5-plan.json
python3 tools/ai-model-pipeline/verify_model_validation_archive.py \
  art-experiments/mossglass-reliquary/live-validation-v5 \
  --check-video-metadata
```
