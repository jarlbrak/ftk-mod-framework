# Rivenquill Cockatrice

Rivenquill Cockatrice is an original cobalt, jade, and copper bird-dragon for
two distinct Basey resource-prefab routes:

- `enbaseycockatriceboss` → `bossCockatrice`, `enBaseyCockatrice` (`121693`)
  under `cockatriceController` (`5949`);
- `enbaseycockatricesmall` → `cockatriceC`, `enBaseyCockatrice` (`121694`)
  under the same controller asset but a separate serialized source pair.

The ignored boss and small references have equal ordered 50-bone palettes and
byte-identical inverse bind matrices, verified by `build_geometry.py`. That
allows one original GLB and palette to be declared in two profiles. It does not
merge their resource selection, controller behavior, runtime binding, captures,
or acceptance: each profile needs a fresh native trial.

The model itself is original geometry: a compact slate body, copper belly,
feathered neck, crown skull, hooked beak, articulated gobble chains, broad
closed primary and secondary wing volumes, raptor legs, and eleven-segment
plume tail. The generator reads only local `bone_names` and `bindposes`; the
export validator separately reads the local reference to reject malformed
output. No native mesh surface, UV, texture, material, skin-weight, or animation
sample is used in the authored sculpture.

## Rebuild and inspect

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/extract_reference.py \
  --assets "$FTK_DATA/resources.assets" --renderer-id 121693 \
  --output scratch/basey-cockatrice-boss-reference-v1
scratch/model-venv/bin/python tools/ai-model-pipeline/extract_reference.py \
  --assets "$FTK_DATA/resources.assets" --renderer-id 121694 \
  --output scratch/basey-cockatrice-small-reference-v1
scratch/model-venv/bin/python art-experiments/rivenquill-basey-cockatrice/build_geometry.py
/opt/homebrew/bin/blender --background --factory-startup --python-exit-code 1 \
  --python art-experiments/rivenquill-basey-cockatrice/render_studio.py
scratch/model-venv/bin/python art-experiments/rivenquill-basey-cockatrice/verify_original_geometry.py
python3 tools/ai-model-pipeline/validate_resource_model_profile_route.py \
  --profile art-experiments/rivenquill-basey-cockatrice/runtime-profile.json \
  --asset-dir art-experiments/rivenquill-basey-cockatrice \
  --output art-experiments/rivenquill-basey-cockatrice/route-preflight-health64.json
```

Studio renders are bind-pose review aids. They do not establish live resource
selection, renderer binding, materials, culling, animation, gameplay, portrait
behavior, lifetime, or final art acceptance.

## Isolated integration sequence

Stage both profiles as one atomic catalog transaction, then review the dry
deployment before execution:

```sh
python3 tools/ai-model-pipeline/stage_custom_model_profile.py \
  --game-root scratch/mirewarden-game \
  --profile art-experiments/rivenquill-basey-cockatrice/runtime-profile.json \
  --asset-dir art-experiments/rivenquill-basey-cockatrice \
  --pin art-experiments/rivenquill-basey-cockatrice/build-report.json \
  --pin art-experiments/rivenquill-basey-cockatrice/original-geometry-proof.json \
  --pin art-experiments/rivenquill-basey-cockatrice/route-preflight-health64.json \
  --output scratch/rivenquill-basey-cockatrice-stage
python3 tools/ai-model-pipeline/deploy_custom_model_stage.py \
  --game-root scratch/mirewarden-game \
  --stage scratch/rivenquill-basey-cockatrice-stage \
  --label rivenquill-basey-cockatrice
```

Run one fresh isolated trial for the boss profile and another for the small
profile. Preserve exact binding, visible idle, ordinary attack, nonlethal hit,
fixture death, and native Ready/progression separately. No result may transfer
between the two source pairs or to direct Cockatrice rows.

The reviewed `scratch/rivenquill-basey-cockatrice-v1-stage/receipt.json`
records that both profile keys and both original asset files are already present
byte-for-byte in the current isolated catalog. That stage makes no game-copy
change; its purpose is to pin the exact catalog and show that the two resource
routes can be exercised separately without replacing an existing asset.

Both profiles set `minimumBaseHealth: 64` only on their isolated custom clones.
The floor preserves any higher native base health and makes a normal nonlethal
hit observable without changing shipped balance, source assets, or either
resource-pair identity.

The older `route-preflight.json` is retained as immutable evidence for the
pre-floor profiles. New staging uses `route-preflight-health64.json`, which
pins the current profile bytes.

`live-validation-v1-boss/` is the immutable, independently verifiable archive
for a fresh `bossCockatrice` / `enbaseycockatriceboss` trial. It records the
exact runtime binding, complete idle/native-attack and ordinary-hit captures,
the measured no-focus HP change from 540 to 539, an explicit native-Death
fixture, strict Ready at level 0 room 2, and selected original combat-camera
frame review. Fixture death ends after expected native renderer cleanup; it is
not ordinary lethal-damage or ragdoll acceptance.

`live-validation-v1-small/` separately records a fresh
`cockatriceC` / `enbaseycockatricesmall` trial. It preserves complete
idle/native-attack, ordinary-hit (HP 72 to 62), and native-Death fixture
captures, strict Ready at level 0 room 2, and selected original combat-camera
review. The small prefab's observed CEL-root scale is `0.3499999940395355` on
all 360 retained frames, while the boss archive records its own `0.9` native
scale. These are two independently measured source-pair results, even though
they share authored assets and bind data.

Both resource routes now have bounded technical motion, gameplay, and
limited-art camera acceptance archives. Portraits, culling, collision,
long-session lifetime, every ability variant, normal lethal damage, ragdoll
behavior, and final art direction remain open for each exact route.
