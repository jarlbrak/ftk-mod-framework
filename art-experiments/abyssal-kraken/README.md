# Abyssal Kraken

This package is a reproducible original-model exercise for three modern Kraken
renderer profiles. It keeps the old five-bone `enkrakenhead` resource experiment
outside this package because that resource is not the production `krakenHead`
combat prefab.

| Profile | Exact target | Original assets | Evidence state |
| --- | --- | --- | --- |
| Abyssal Crown V4 | `krakenHead`: skinned `kraken2` (121035) plus rigid `Root_M/base/body/neck/eye/kraken2_eye` (101310) | `abyssal-crown-kraken-head-v2.*` plus `abyssal-crown-kraken-eye-v4.*` | The canonical [V5 exact-source archive](live-validation-head-v5-canonical/README.md) records both same-owner renderer bindings, 331 identity-stable motion-renderer frames, reviewed idle, native attack, ordinary hit, animated fixture disappearance, and strict Ready. The rigid eye is required profile context without separate skinned-topology credit. Finished-art approval remains pending. |
| Sargassum Lash V2 | `krakenTentacle` and `krakenTentacleMirror`: `krakenTentacle` (121595) | `sargassum-kraken-tentacle-v2.*` | The canonical [exact primary archive](live-validation-sargassum-primary-v2-canonical/README.md) records 331 identity-stable frames, reviewed idle, three native attacks, ordinary hit, animated fixture death, and Ready for `krakenTentacle`. The older [combined archive](live-validation-v2-kraken-tentacles/README.md) retains scoped primary and mirrored-controller evidence. Neither archive approves finished art. |
| Royal Sargassum V3 | `seaKingTentacleA` and `seaKingTentacleB`: `KrakenGodTentacle` (121315) | `royal-sargassum-seaking-tentacle-v3.*` | [Archived](live-validation-v3-seaking/README.md) scoped technical evidence for both rows. It is not final-art approval. |

![Abyssal Crown studio view](abyssal-crown-kraken-head-v2-hero.png)
![V4 static eye studio view](abyssal-crown-kraken-eye-v4-hero.png)

The head and two tentacle designs use original geometry, UVs, normals, weights,
and texture pixels. No native mesh surfaces, topology, normals, UVs, texture
pixels, skin weights, or animation samples are source material for these
assets. The exact hierarchy, bone names, inverse-bind matrices, renderer paths,
and material behavior are runtime compatibility metadata.

## Why the V4 head has two assignments

`kraken2` is the seven-bone skinned head. `kraken2_eye` is a separate rigid
`MeshRenderer` with one `MeshFilter`, parented beneath the animated `eye` bone.
Replacing only the skinned renderer left the native emissive eye surface in
front of the original head. V3 demonstrated that a full-size rigid globe could
replace it technically but overwhelmed the silhouette. V4 replaces it with an
original compact sea-glass eye in the child filter's local space.

The canonical V5 archive proves both exact renderer types, the one-filter static
mesh, authored textures, shared owner, and sampled idle, attack, hit and
animated-disappearance states. At the tested combat camera, the compact rigid
eye remains registered beneath the larger crown shell during the sampled
motions. This is a practical technical result, not a final aesthetic
endorsement. The older V4 archive and original colored placement probes remain
historical evidence: deeper offsets are occluded by the skinned head, while
large offsets dominate the silhouette.

## Rebuild and offline checks

Run from the repository root after the ignored skeleton references are present:

```sh
scratch/model-venv/bin/python art-experiments/abyssal-kraken/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python art-experiments/abyssal-kraken/build_blender.py -- head
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python art-experiments/abyssal-kraken/build_blender.py -- tentacle
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python art-experiments/abyssal-kraken/build_blender.py -- seaking
scratch/model-venv/bin/python art-experiments/abyssal-kraken/build_static_eye_v4.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python art-experiments/abyssal-kraken/build_static_eye_v4_blender.py
scratch/model-venv/bin/python art-experiments/abyssal-kraken/verify_original_geometry.py
scratch/model-venv/bin/python art-experiments/abyssal-kraken/verify_target_bindings.py
```

The generic rigid-child route check replaces the old package-specific inventory
step. Generate it from the same local `resources.assets` file pinned by the
enemy mapping, then preflight the current profile document:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/inventory_static_renderers.py \
  --assets "$FTK_DATA/resources.assets" \
  --mapping scratch/enemy-rig-mapping-reproducible.json \
  --output scratch/static-renderer-inventory.json
python3 tools/ai-model-pipeline/validate_custom_model_profile_route.py \
  --static-inventory scratch/static-renderer-inventory.json \
  --profile art-experiments/abyssal-kraken/runtime-profiles.json \
  --asset-dir art-experiments/abyssal-kraken \
  --output art-experiments/abyssal-kraken/route-preflight-v4-head-static.json
```

The record proves the current direct routes and declared original bytes only.
It does not reuse the V4 archive for a later profile or establish rigid-eye art,
culling, controller coverage, or lifetime acceptance. For a changed profile,
write a new, non-overwriting preflight filename beside this historical record.

For a new rigid child, author original positions in the selected `MeshFilter`
local space and export it through the generic strict writer:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/export_ftk_glb.py \
  --static --source my-original-rigid-part.json --output my-rigid-part.glb
```

A static GLB has one triangle primitive and no skin, `JOINTS_0`, or
`WEIGHTS_0`. It does not take a skeleton reference. A skinned GLB remains a
separate export using its own exact palette and inverse-bind reference.

## Isolated deployment and evidence

All deployment scripts reject a non-`scratch` game root and a running isolated
game. The historic V4 stage and deployment receipts pin the original
configuration. The current isolated game was later restored from the placement
probe with a catalog-only transaction: it restored catalog hash
`51f22c64607feebb3241961023b7c642c3e30f30b3b7f5478794da399253df79` and made
no model-file changes.

To reproduce that specific restore only when the isolated catalog is the
recorded probe profile:

```sh
python3 art-experiments/abyssal-kraken/stage_restore_v4_from_static_probe.py \
  --game-root scratch/mirewarden-game \
  --output scratch/abyssal-kraken-v4-restore-stage
python3 art-experiments/abyssal-kraken/deploy_restore_v4_from_static_probe.py \
  --game-root scratch/mirewarden-game \
  --stage scratch/abyssal-kraken-v4-restore-stage
python3 art-experiments/abyssal-kraken/deploy_restore_v4_from_static_probe.py \
  --game-root scratch/mirewarden-game \
  --stage scratch/abyssal-kraken-v4-restore-stage --execute
```

For any new profile revision, create a new pinned stage from the current
catalog, review its hashes before execution, launch a fresh isolated process,
and retain the stage result, pass, ordinary attack, explicit fixture-death
prefix, static inventory, Ready state, raw PNG hashes, and a human visual
verdict. The [canonical V5 archive](live-validation-head-v5-canonical/README.md)
is the current reference for a combined skinned and rigid replacement. Its pass
and ordinary-hit captures retain all requested 120 frames. Its fixture-death
capture retains 91 frames: `krakenDisappear` plays from frame 29 through frame
89, frame 90 records the same renderer inactive and not visible, and the next
sample reports renderer destruction. This proves the sampled animated
disappearance, not cleanup causality, corpse lifetime or final disposal.

The canonical Sargassum primary route is the reference for a tall animated
tentacle that removes itself during death. Its Pass and ordinary Attack
captures each retain all 120 requested frames. The fixture-death capture keeps
the exact renderer for 91 frames: `Tentacle_Death` runs through frame 89,
frame 90 records the renderer inactive and not visible with its animator
disabled, and the next sample reports renderer destruction. Accept this prefix
only with the recorded termination and limits. It proves the sampled animated
withdrawal, not full death duration, cleanup causality, corpse lifetime or final
disposal.
