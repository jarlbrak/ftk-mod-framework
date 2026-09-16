# Emberglass Bee

Original low-poly amber bee for the exact `beeA` renderer `Monster Bee` (121062). The segmented abdomen, six legs, compound eyes, antennae, opaque veined wings and overlapping wing sockets are authored parametric surfaces. No native surface or texture is included. Studio approval and live acceptance are separate gates.

![Hero](hero.png)
![Side](side.png)

Use `runtime-profile.json`: `ftkmf_modeltest_emberglass`, base `beeA`, combat profile `a34dceed30a67f62df142e0b62554fda51eb4be683804b903119503d90c0e73a`, `Monster Bee` → `emberglass.glb` + `emberglass_basecolor.png`. Minimum health is 64; visualScale 1 preserves the native CEL scale 1.25. This binding is not transferable to the mosquito merely because its hierarchy resembles the bee.

The full 47-name palette and native inverse bind matrices are retained. Native zero-weight RigNeck, RigRFeelerMain and RigLFeelerMain remain in the palette. The generator uses only bone names and inverse bind landmarks; `original-geometry-proof.json` verifies byte-identical source/palette regeneration with other native reference reads forbidden. Export validation separately reads local native comparison data.

This source's anatomical forward is **−meshY**, up **+meshZ**. The runtime renderer/skeleton establishes world orientation. The saved authoring scene preserves that exact binding frame. Only the separate studio display rotates the whole model for a conventional upright view; that rotation is applied after reopening and exporting the authoring scene.

First source material is `matBee` (60), with zero emission color despite its `_EMISSION` keyword. The second native `matBeeGlow` (63) has emission (2,2,2). The single-material original deliberately uses the first native material, `disableNativeEmission: false`, and opaque ivory/blue wings. It does not reproduce native second-slot glow. Live shader appearance remains a test gate.

## Rebuild

Run from the repository root after locally extracting renderer 121062 to `scratch/skeleton-audit/121062`:

```sh
scratch/model-venv/bin/python art-experiments/emberglass-bee/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/emberglass-bee/build_blender.py
scratch/model-venv/bin/python art-experiments/emberglass-bee/verify_original_geometry.py
scratch/model-venv/bin/python art-experiments/emberglass-bee/audit_native_poses.py --label pass
scratch/model-venv/bin/python art-experiments/emberglass-bee/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/3b84adbf86024705b35121e98c8e7730.json --label hit
scratch/model-venv/bin/python art-experiments/emberglass-bee/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/737dacf062de4524978da9c0fdafe220.json --label death
scratch/model-venv/bin/python art-experiments/emberglass-bee/finalize_manifest.py
```

`emberglass.blend` is editable; `emberglass-studio.blend` is for presentation. Saved-scene export and independent validation go to ignored `scratch/emberglass-roundtrip`. Direct/reopened audits preserve the exact palette and bind matrices. The original rest surface fits the native rest surface bounding box; this is not proof of animated culling or camera fit.

## Captured native pose studies

`native-{pass,hit,death}-pose-audit.json` pins each native diagnostic capture and original source hash. All 120 frames per capture are skinned offline; six fixed frames are shown in each contact sheet. Root_M motion is removed for articulation inspection; separate renderer-local travel bounds retain the actual lunge/displacement. These are original-surface offline images, not game renders or proof of ground contact. Each report identifies the largest stretched edge by piece/frame and actual lengths rather than treating a ratio as an automatic verdict.

Native baseline hit was ordinary 8 damage (58→50); native death entered a 16-body ragdoll and settled. That establishes source behavior only. The original still needs native idle/attack/hit/ragdoll, portrait, material, camera/culling fit and cleanup review. Fine antennae and veins may be small at combat distance; native FX can occlude review frames. No whole-family or alternate-controller coverage is claimed.

Parent reviewed the pinned hero, side, pass and death sheets and approved this version for live testing. The largest death stretch is an abdomen ring edge at frame 31: 0.013547→0.047522 mesh units (3.5079×), not a long attachment bridge. This remains an explicit live ragdoll review point.

## Native live V1

[Live archive](live-validation.json) records three complete120-frame captures on the exact native-scale beeA profile. Parent reviewed pass0/40, hit30 and death40/60: coherent amber insect, attached wings/legs, readable portraits, ordinary10damage58→48, and a corpse remaining together as it falls and lies on the floor. The silhouette is small/dark; native motion blur and green effects obscure some detail.

Death was an explicit kill fixture. Animator turns off and all16 bodies become dynamic at27. Frame60 looks near settled but still has nonzero recorded linear/angular velocity; both are zero at63 and119. IsSleeping was not recorded. One guarded Collect reaches strict Ready0/2 after an initial transitional query. The runtime first native Standard material uses the authored palette with zero emission; no second native glow slot is retained.

This is selected-pose body/portrait/hit/death appearance acceptance, not every clip, view, culling condition or bee-family variant. Some hit/attack clips have only partial first-cycle samples. Five PNGs and three120-frame MP4s are retained in `live-v1`; native effects remain game-owned. The source geometry and runtime profile were not changed for this test.

## Native live V2

[The fresh V2 archive](live-validation-v2/README.md) repeats the exact `beeA`
`Monster Bee` binding in session `59cde8c216d54a79b5d22fab5ac82d0c` at public
visual-scale factor `1.0` (captured native CEL scale `1.25`). The authored body
stayed attached under owner `369188`; selected idle and attack frames retain the
abdomen, legs, antennae and paired wings. The first native Standard material
uses the authored basecolor with a black emission color and no emission map;
the native second-slot glow is not claimed.

Pass and ordinary attack captures are complete 120-frame recordings. Ordinary
damage changes HP `58→45` with `cheat=None` and no focus. The explicit
`KillSingle` fixture reaches the native victory transition, but the renderer is
destroyed during frame 104 of the requested 120-frame death capture. That
expected prefix is archived as a boundary, not as full death or settled-ragdoll
acceptance. The encounter reaches strict Ready at level 0 room 2 and exposes a
Ready vote directly, so no Collect action is claimed.

This fresh run verifies the exact authored beeA assignment only. The small/dark
combat silhouette, native UI/effects and renderer-destroyed boundary limit fine
antenna/vein detail, complete death deformation, culling, portraits, resource
lifetime and finished-art acceptance.

## Fresh live trial V3

The [fresh V3 archive](live-validation-v3/README.md) repeats the exact `beeA`
`Monster Bee` binding in session `72ea10881809499883a38b7137a49480` at public
visual scale `1.0` (captured native CEL scale `1.25`). The authored bee remains
on owner `369188`; abdomen, legs, antennae and paired wings stay attached to
the one native renderer.

Pass and ordinary attack are complete 120-frame recordings. Ordinary damage
changes the target from HP `58` to `53` with `cheat=None` and no focus. The
explicit `KillSingle` fixture commits HP `53` to `0`, while native cleanup
destroys the renderer during frame `94` of the requested 120-frame death
capture. That prefix is preserved as a cleanup boundary, not full corpse,
settled-ragdoll or destruction-causality acceptance.

The post-death surface is strict Ready at level `0` / room `2`, with no Collect
vote available. One guarded native Ready click advances room `2`, where the
normal Jelly Cube and registered cultist probe appear. The first native
Standard material uses the authored basecolor with black emission and no
emission map; the separate native glow slot remains outside this claim.

## Canonical exact-source live V4

The [immutable V4 archive](live-validation-v4/README.md) pins a fresh execution-
queue run to topology `6fdb7ff148451731` and the exact `beeA / Monster Bee /
renderer 121062` source assignment. Session
`6b24887886c84815a06146b2cfa90494` binds `ftkmf_glb_emberglass.glb` under owner
`369188` with the expected 47-bone signature
`14b49cbaf8d8e62092f5ea4062498c3b4eaa16b7f2bdf1121ecc2296da33e2a9`.
Public visual scale `1.0` preserves the native CEL scale `1.25`. The native
Standard `matBee` instance uses the authored base-color texture with black
emission color and no emission map.

Four accepted captures preserve every ordinary attempt instead of discarding a
zero-loss result. The 120-frame pass records `BeeIdle`, `AttackProf`, and
`beeStingerAttack`. Ordinary attempt 1 keeps HP at `58`, with a native `Dodge`
trigger, `dodge_bee`, and the visible DODGED label. Ordinary attempt 2 measures
HP `58` to `50`, enters `BeeTakeDamage`, and returns to idle; both ordinary
captures also sample `attack2_bee`. The separate `KillSingle` fixture changes
HP `50` to `0`, enters `BeeDie`, and switches all 16 recorded rigid bodies from
kinematic to dynamic at frame 27 while `m_DoRagdoll=true`. The model reaches the
floor by frame 40 and remains visibly grounded through frame 102. Frame 103
records the renderer inactive and terminates the capture, so V4 claims only the
observed physics fall and bounded corpse interval, not ordinary lethal damage,
cleanup causality, full requested death duration, or later corpse lifetime.

Twenty-four exact original PNGs were reviewed across the four captures. The
archive independently verifies 22 metadata mappings, 465 source-image pins,
all 464 retained capture images, two source assets, the root visual review, 24
selected originals, and four videos. Combat reaches strict Ready at level 0,
room 2 without a usable Collect action. Fine insect detail remains limited by
the native combat camera, foreground hero, effects, and UI. Portrait, collision,
extended culling, clone and long-session material lifetime, other native attacks,
full campaign completion, and final art approval remain outside V4.

To repeat this exact route after changing the authored source, rebuild the GLB
and texture with the commands above, regenerate the execution queue and stopped-
game stage-readiness ledger, then run the campaign command for topology
`6fdb7ff148451731`. Use new non-existing run, review, plan, and archive paths.
Create the plan with `make_execution_queue_archive_plan.py`, freeze it with
`archive_model_validation_case.py`, and recheck it with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/emberglass-bee/live-validation-v4 --check-video-metadata
```

The route proves one canonical `beeA` representative. It does not credit the
two remaining exact-source representatives in the shared topology.
