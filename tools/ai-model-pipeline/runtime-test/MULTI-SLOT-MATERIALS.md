# Explicit native material slots

Enemy renderer profiles may opt into `materialSlots` with2..4 descriptors. Each
specifies exact integer `primitiveIndex` and `nativeMaterialSlot`; both index sets
must bijectively cover0..N-1. Optional `textureFile` is a bounded PNG basename and
optional `disableNativeEmission` is boolean. Omit a texture to preserve that native
slot texture. Explicit nulls and unknown fields are rejected. Renderer-level
textureFile/disableNativeEmission cannot coexist with materialSlots, even as null.
The shared content/helper parser is `MaterialSlotFixture.cs`; player profiles and
legacy single-primitive enemy assignments retain their existing contract.

`catalog-preflight` calls the exact strict5-argument runtime decoder only for this
explicit mode, passing primitive→native-slot mapping. The native prefab must have
exactly N nonnull materials. Results include per-slot file hashes/options, native
material IDs/shaders/texture transforms and decoded submesh index/UV hashes. Only
the returned owned mesh is destroyed. There is no prefab binding/instantiation,
material modification, controller execution, or visual verdict in preflight.

`material-state` is an optional read-only command with existing fresh inventory
identity fields: scope=enemies, rendererId, ownerInstanceId, rendererPath,
expectedMesh and boneSignature, in addition to id/session/op. It resolves the
actual current EnemyDummy→CEL→renderer reference and records shared material IDs,
shader names, main/emission texture identity and offsets/scales, emission options,
and current lease membership. It reads exact ScrollingUVs component IDs, index,
rate, property name and private accumulated uvOffset. Geometry includes actual
submesh index counts and SHA256 over little-endian int32 indices, plus SHA256 over
float32 little-endian UV0 values. Bounds:4 material slots/submeshes,200000 vertices,
600000 indices,16 scrollers and256 existing lease resources. Missing or ambiguous
identity fails closed. These are managed observations; no Unity object is created.

`capture`/`play` may explicitly add `materialObservation:true` for an exact enemy
owner. Each end-of-frame pose then includes this metadata before PNG readback;
geometry hashes are included on the first frame. Ordinary captures omit this
metadata. A material observation reports its sampling phase, frame/game/realtime
and deltaTime. Native LateUpdate scheduling is unchanged. Multiple scrollers may
write one property; final offset must not be presumed equal to every scroller's
phase. Disabled renderer accumulation is only established by actual timed samples,
not by metadata labels or simulation alone.

The enemy recorder automatically requests this option when its selected renderer
has materialSlots. It requires all actual slots, owned materials and configured
owned PNGs, requested emission state and first-frame real submesh coverage. These
are identity/binding checks, not a visual or animation verdict. Runner and recorder
pin every per-slot texture alongside the GLB, profile catalog and deployed binaries;
a changed file stops the next dependent operation. Actions remain once-only.

The cubeA calibration authored by model_pipeline is the original two-layer
`two-original-layers.glb` fixture under scratch/cube-multislot-blender:72 vertices,
24 triangles split into two genuine12-triangle primitives, full native3-joint
palette. The exact CEL-relative native renderer is `enJellyCube`; native source
Scroller135154 targets slot1, `_MainTex`, rate(0,0.2). Serialized IDs are source
metadata, not live instance IDs. This is calibration geometry, not finished art.
Use the staged calibration profile/manifest from the content agent for actual
filenames, hashes and synthetic key; never guess an instance ID.

## Separate future lifecycle fixture (not implemented here)

A later explicitly authorized owned-object fixture can clone a dedicated test
renderer/scroller hierarchy through source→clone→grandclone, drive only the native
owned scrolling path, and measure each owner's private material identities and
lease membership before/after first enabled update. It must include disabled
renderer phase accumulation and enable transition, both destruction orders,
never-enabled clone cleanup, stable source/native references and final Unity-null
assets after ordinary production pruning. Prepare all objects under an owned
fixture root, exclude CEL/gameplay scripts and native avatar destruction, use
exact component/layout signatures, and clean only positively owned objects.
A fresh same-session strict Ready gate and independent source review are required
before implementing that mutation fixture. Current material-state/capture never
creates clones, invokes ownership methods, retains/releases leases or prunes.

Offline tests verify strict schema, mapping coverage, asset enumeration and observed
slot identity rejection. Unity decode/binding, timed scrolling and native lifetime
acceptance require separate live evidence on the reviewed candidate.

Frozen calibration staging: `scratch/runtime-profile-396-cube-materials-v1`, key
`ftkmf_modeltest_cubea_materialslots`, catalog SHA256
`cc6b381a8b8c0984c5a14e72c397735a38b267d7f62380cfa09a617301dd412f`.
Its receipt pins `cubea_material_layers_v1.glb` and the two
`cubea_material_layers_v1.slot0.png` / `.slot1.png` files. The portable source
fixture lives at `tools/ai-model-pipeline/multi-primitive-tests/cube-fixture/`.
The staged content binary requires the reviewed multi-slot Core candidate even
when registering legacy profiles; see the receipt's exact requiredCoreSha256 and
requiredContentSha256. Do not combine this catalog/content with the old deployed
Core. No part of this staging is itself a deployment or live acceptance.

## Repeat the timed scroll check

The [cubeA calibration evidence](../../../docs/evidence/cubea-multislot-calibration-v1/README.md)
contains three live combat captures with two distinct owned materials and textures.
Use the independent read-only verifier on a raw JSON capture or its lossless gzip:

```sh
python3 tools/ai-model-pipeline/runtime-test/verify_material_scroll.py CAPTURE.json.gz \
  --output scroll-verification.json
python3 -m unittest discover -s tools/ai-model-pipeline/runtime-test \
  -p test_verify_material_scroll.py
```

This verifier deliberately accepts only one enabled native `_MainTex` scroller,
2..4 real submeshes, and textures owned by the observed lease. It checks stable
owner/material/texture identities, unchanged offsets on other slots, exact target
identity, advancing time, and float32 phase recurrence using each observed deltaTime.
It rejects multiple writers, disabled renderers, and preserved unowned textures;
those configurations require separate evidence rather than an inferred pass.
The output pins the decompressed capture SHA256. Compare that hash with the
capture journal and deployment evidence before reusing a result.

All three cubeA captures passed 120 frames each. This establishes observed scroll
and current resource membership only. The death recording retains an enabled
renderer while the calibration blocks become visually indiscernible; analytical
skinning of observed native matrices shows downward movement, without measuring
terrain height. Finished creature appearance, clone independence, disabled-renderer
accumulation and final resource disposal remain separate validation requirements.

The separate [owned material lineage audit](../../../docs/evidence/material-lifecycle-native-v1/README.md)
now passes for the two-slot synthetic no-CEL fixture: clone/grandclone private
materials, native disabled-renderer phase accumulation and both final disposal
orders. Its explicit ownership setup is distinct from public GLB binding and
real avatar lifetime; those scopes must not be inferred from this result.
