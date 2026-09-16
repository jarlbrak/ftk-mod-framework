# Tideglass Crab

An original tidal crab with a broad teal carapace, raised amber eyes, six walking legs and substantial copper-and-ivory claws. Upper and lower pincers are independently articulated surfaces. This is original authored geometry, not a calibration probe. The exact `crabB / enCrabWizard / 121411` source now has scoped canonical live evidence in V3; studio presentation and final art-direction approval remain separate.

The exact catalog target for reference renderer **121411** is **crabB**, CEL-relative path **enCrabWizard**, combat profile `482b8f324233045b4effc0527f997283b7620993d2a4a90fccbf264a24d5c85a`. This is not crabA's renderer 121579. `runtime-profile.json` preserves the exact mapping with `minimumBaseHealth: 64`; runtime files are `tideglass.glb` and `tideglass_basecolor.png`.

The earlier body/portrait baseline and the fresh catalog-411 trial below both use the exact Crab B assignment. The skeleton register still lists the offline crabController representative; no cross-variant live support is inferred.

Native forward is +UnityZ, verified from claw/pincer chains and the rearward -Z tail chain. Original carapace rings follow the body joints, walking legs follow each of the six native leg chains, eyestalks follow proximal antenna joints, and claw arms follow their actual three-joint chains. Each pincer and its teeth are rigid to its own top/bottom pincer bone, with no connecting triangles across the opening. The low rear shell ridge uses only proximal tail joints; geometry is not invented for every unused palette joint. The full 63-joint native palette and exact inverse binds are retained.

All original vertices fit within native bind-surface bounds. Native animation envelopes remain unchanged; bind containment does not prove animated culling. Native claw opening/closing, eye tracking and leg articulation still require live inspection.

**Material override:** the source uses only material 1056 `matLoot`, with `_EMISSION` enabled and emission RGB 1.4. The profile explicitly sets `disableNativeEmission: true` to preserve the intended non-glowing palette. Source scalar/color properties and source asset hash are recorded in `native-material-metadata.json`. Refresh with `inspect_materials.py --assets /absolute/path/to/resources.assets`. Studio colors remain separate from final native shader/tint acceptance.

Rebuild from the repository root with existing local references and dependencies:

```sh
scratch/model-venv/bin/python art-experiments/tideglass-crab/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/tideglass-crab/build_blender.py
scratch/model-venv/bin/python art-experiments/tideglass-crab/finalize_manifest.py
```

The Blender script creates and saves the editable rigged scene, reopens it, exports through the FTK bridge and independently validates the result. Reopened exports remain in ignored `scratch/tideglass-roundtrip`; their hashes are recorded in the manifest. Export `tideglass.blend`; `tideglass-studio.blend` adds presentation-only floor, lighting and cameras. Regeneration resets manual approval after refreshing checks and hashes. No native mesh surface or texture is copied or packaged.

Pending outside V3: studio approval, native-scale readability beyond the selected live views, other proficiencies, `Crab_DeathInDirect`, portraits, collision, extended culling, long-session resource lifetime, full campaign completion and final art-direction approval. No game, framework, helper or catalog changes are performed by these scripts.

## First live original baseline

[Live evidence](live-validation.json) pins exact deployment and assets in
session0b8becf223394f3386b42a7b92b22de4. Idle body/pincers and portrait are
readable. A native purple wizard hat remains on the body and portrait, though
absent in studio. Actual private matLoot emission is off, RGB black/mapnull
with the original texture. Parent reviewed pass0/50/70, hit30 and death40/60.

- [Attack](live/attack.mp4): effects obscure50;70 shows motionblur/return.
- [Normal2 hit](live/nonlethal-hit.mp4):58 to56; hero/effects obscure body.
- [Kill-fixture death](live/kill-fixture-death.mp4):56 to0; hat hides part of collapse.

All three captures complete120 unpaused frames; two Collect actions reach
Ready0/2. Native Crab_DeathDirect plays with ragdoll flagfalse; recorded
rigidbody counts0/1 do not prove ragdoll or identify the accessory involved.
This is scoped body/portrait review, not unoccluded all-motion or culling
acceptance. Videos replay12fps, not real-time performance.

## Fresh catalog-411 live validation V2

The repeatable run used session `0b328315443b47e4bc9515cfa4808a65`, Crab B,
renderer `enCrabWizard` (121411), owner `369188`, and bone signature
`e51916d0e0a577fa1442bdcf75dff8a51102a2eaf3d2edc36c11ddaf8c441e96`. The
authored surface was visible and bound in the pass, ordinary attack and
explicit `KillSingle` fixture captures. The ordinary hit changed the same
target from HP 58 to 56 with no focus and `cheat=None`; the fixture completed
120 frames, accepted two native Collect actions and reached strict native Ready
at level 0 room 2.

Root review covers two idle frames, two attack frames and the fixture
endpoints. The teal shell, eye stalks, six legs and separated pincers remain
readable, with the native purple wizard hat retained. UI, hero and effects
limit fine deformation and material inspection; the fixture endpoint is the
native victory/item-choice surface, not corpse or floor-contact acceptance.

The byte-pinned supplement is
[`live-validation-v2/validation.json`](live-validation-v2/validation.json),
with its offline-only archive script at
[`live-validation-v2/archive.py`](live-validation-v2/archive.py). The archive
validation SHA is `5d1b65316b9e86e7f17304ff75cc5087dd14986d3c53511bdea472b325af392d`;
it retains 360 source PNG hashes, six selected originals and three 120-frame
presentation videos while excluding native payloads and DLLs. Re-running the
script refuses to overwrite the completed destination unless
`FTK_ARCHIVE_REBUILD=1` is explicitly set.

## Canonical exact-source live validation V3

The [V3 archive](live-validation-v3/README.md) certifies topology
`791b63f3064c2f62` through the exact native `crabB / enCrabWizard / 121411`
route. Session `a7ecd64edd784ef28493feee9ed55a57` bound
`tideglass.glb` to renderer instance `-245434` on owner `369188`, with observed
bone signature
`e51916d0e0a577fa1442bdcf75dff8a51102a2eaf3d2edc36c11ddaf8c441e96`.
The private material uses the authored base texture with emission disabled,
black emission color and no emission map.

All three captures retain their complete 120 requested frames. The pass covers
`Crab_Idle`, `Crab_Attack1` and recovery. The ordinary no-focus player action
changes the same target from HP `58` to `56`, with `Crab_Hit`, recovery and a
subsequent native `Crab_Attack2` sampled in the same capture. The exact renderer
stays active, enabled and visible in all 360 retained frames.

Death uses the separate explicit `KillSingle` fixture at 56 HP and is not
ordinary lethal gameplay evidence. The native Death trigger begins at retained
frame 23, followed by `Crab_DeathDirect` from frame 27 through frame 119. The
controller reports `m_DoRagdoll=false`, so this is an animated death path and
not a ragdoll. The renderer remains visible through the sampled 120-frame
window; no later corpse lifetime is claimed. One native Collect reaches strict
Ready at level `0` room `2`.

Twenty-one exact originals were inspected at original resolution. They show a
coherent teal shell, eye stalks, legs and separated pincers through idle,
attack, hit, recovery, animated death and the sampled corpse window. The native
purple wizard hat remains attached during live motion. Native UI, effects,
foreground hero overlap, loot overlay, depth blur and camera motion limit fine
surface inspection. No sampled renderer loss, mesh explosion, detached authored
section, viewport clipping or floor fall-through was observed.

Reproduce the exact bounded route after staging the documented profile into an
isolated game copy:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_execution_queue_route.py \
  --root . \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/mirewarden-game \
  --topology-group 791b63f3064c2f62 \
  --route-kind directEnemy \
  --profile-document art-experiments/tideglass-crab/runtime-profile.json \
  --motion-renderer-path enCrabWizard \
  --run \
  --output scratch/model-route-791b63f3064c2f62-directenemy-NEW-run.json
```

Every rerun needs a fresh output filename. Never overwrite or reinterpret the
V3 session artifacts; build a new reviewed archive for new evidence.
