# Lichenfang Prowler

Lichenfang Prowler is an original low-poly lichen-armored wolf for the exact
resource-prefab route `enbaseywolf`: base enemy `wolfA`, CEL-relative renderer
`Wolfie` (`120975`), and `wolfController` (`6007`). It is deliberately separate
from the direct `wolfA` body and from every other wolf-family resource prefab.

The sculpture has a dark spruce and slate coat, layered living-lichen ruff,
copper leaf ears and claws, amber eyes, broad articulated paws, and a
four-segment tail with closed leaf vanes. `build_geometry.py` authors its
geometry, UVs, normals, influences, palette and named pieces from an original
brief. During authoring it reads only the exact ordered `bone_names` and
`bindposes` from the ignored local reference. The export validator separately
reads that reference to reject malformed binary/skin output; no native mesh
surface, texture, UV, weight, material, or animation data informs the asset.

`build-report.json` records the strict 33-bone GLB contract and all-palette
influence coverage. `route-preflight-health64.json` is a separate read-only proof that
the declared profile matches this resource prefab, base chassis, renderer,
controller fingerprint, and declared assets. Neither file establishes runtime
appearance or art acceptance.

## Scoped live result

[Live validation V1](live-validation-v1/README.md) now preserves one fresh,
exact `enbaseywolf` / `wolfA` / `Wolfie` trial of this original asset. The
bound `lichenfang.glb` remained visible through native `cidle_wolf`,
`attackProf_wolf`, `damaged_wolf`, and `deathHeavy_wolf` motion. A no-focus,
ordinary same-target hit lowered the spawned enemy from 58 to 48 HP; a separate
explicit `KillSingle` fixture drove the native death clip. The run then reached
strict Ready. The archive pins the complete captures, root-reviewed frames,
source hashes, immutable catalog and registration snapshots, and a verifier.

The result is limited to this exact resource-prefab source pair and is not a
final art review. Portraits, culling, long-session asset lifetime, all ability
variants, ordinary lethal-damage behavior, ragdoll behavior, and final
art-direction approval remain separate gates.

## Rebuild and inspect

Extract the exact local reference first. It belongs under ignored `scratch/`
and must never be committed or distributed.

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/extract_reference.py \
  --assets "$FTK_DATA/resources.assets" --renderer-id 120975 \
  --output scratch/basey-wolf-reference-v1
scratch/model-venv/bin/python art-experiments/lichenfang-basey-wolf/build_geometry.py
/opt/homebrew/bin/blender --background --factory-startup --python-exit-code 1 \
  --python art-experiments/lichenfang-basey-wolf/render_studio.py
scratch/model-venv/bin/python art-experiments/lichenfang-basey-wolf/verify_original_geometry.py
python3 tools/ai-model-pipeline/validate_resource_model_profile_route.py \
  --profile art-experiments/lichenfang-basey-wolf/runtime-profile.json \
  --asset-dir art-experiments/lichenfang-basey-wolf \
  --output art-experiments/lichenfang-basey-wolf/route-preflight-health64.json
```

The studio images are authored bind-pose review aids. They do not establish
native FTK renderer selection, materials, motion, culling, portraits, gameplay,
resource lifetime, or final artistic acceptance.

## Isolated integration sequence

Stage this profile only into the isolated game copy, review the dry deployment,
then execute it after the isolated process is stopped:

```sh
python3 tools/ai-model-pipeline/stage_custom_model_profile.py \
  --game-root scratch/mirewarden-game \
  --profile art-experiments/lichenfang-basey-wolf/runtime-profile.json \
  --asset-dir art-experiments/lichenfang-basey-wolf \
  --pin art-experiments/lichenfang-basey-wolf/build-report.json \
  --pin art-experiments/lichenfang-basey-wolf/original-geometry-proof.json \
  --pin art-experiments/lichenfang-basey-wolf/route-preflight-health64.json \
  --output scratch/lichenfang-basey-wolf-stage
python3 tools/ai-model-pipeline/deploy_custom_model_stage.py \
  --game-root scratch/mirewarden-game \
  --stage scratch/lichenfang-basey-wolf-stage \
  --label lichenfang-basey-wolf
```

After a reviewed execution, start a fresh isolated run and preserve exact native
binding, visible idle, ordinary native attack, nonlethal received hit, explicit
fixture death, and native Ready/progression evidence in a new immutable archive.
Direct `wolfA` or other resource-prefab records do not substitute for this
`enbaseywolf` source pair.

The runtime profile sets `minimumBaseHealth: 64` only on the isolated custom
clone. It preserves any higher native base health and gives the normal no-focus
hit a nonlethal observation window; it does not change FTK balance or the
authored asset.

The older `route-preflight.json` is retained as immutable evidence for the
pre-floor profile. New staging uses `route-preflight-health64.json`, which pins
the current profile bytes.

This package has offline export, route-preflight and reproducibility evidence,
plus the scoped V1 live archive described above. Portraits, culling, lifetime,
other ability variants, ordinary lethal damage, ragdoll behavior, and final art
review remain pending.
