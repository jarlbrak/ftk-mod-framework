# Sablevine Serpent

Sablevine Serpent is an original low-poly serpent for the exact resource-prefab
route `enbaseysnake`: base enemy `snakeJungleA`, CEL-relative renderer
`enSnake_Basey` (`121488`), and `snakeController` (`6002`). This is a 44-bone
resource rig and is not interchangeable with the direct Jungle Snake assets.

The authored creature has a long midnight body of slate, moss, lichen plates,
closed leaf vanes, a copper jaw, amber eyes, a crown horn, and a fully
articulated eight-joint rose tongue. `build_geometry.py` reads only ordered
`bone_names` and `bindposes` from the ignored local reference while authoring.
Its geometry, UVs, normals, weights, palette and named pieces are original. The
independent exporter validator reads the reference only to reject malformed
binary/skin output; native mesh surfaces, UVs, texture pixels, weights,
materials and animation samples never inform the art.

`build-report.json` records the strict 44-bone GLB result and all-palette
coverage. The resource route preflights prove the declared resource load path,
chassis, CEL, renderer, controller/rig fingerprint and assets before staging.
They do not establish a live result.

## Live revisions

### V2 scale 0.55: limited combat-camera fit accepted

[Live validation V2](live-validation-v2-scale055/README.md) preserves a fresh
isolated `enbaseysnake` / `snakeJungleA` / `enSnake_Basey` session using
`runtime-profile-scale055.json`. V2 reuses the exact V1 `sablevine.glb` and
`sablevine-palette.png` bytes; the only authored-profile revision is the public
`visualScale` factor `0.55` under a fresh profile key.

Registration records native prefab root scale `[1, 1, 1]`; the binding probe and
all 360 captured frames record spawned CEL root scale
`[0.550000011920929, 0.550000011920929, 0.550000011920929]`. The selected
normal idle, native Attack, hit, and death frames keep the whole readable
serpent inside the combat camera. The trial also preserves the exact 44-bone
binding through native motion, an ordinary no-focus nonlethal hit from 58 to 45
HP, a separate explicit fixture death, and strict Ready recovery at level 0,
room 2.

This is a usable **limited-art** result for the exact route. Portraits, culling,
collision, long-session resource lifetime, other native ability variants,
ordinary lethal damage, ragdoll behavior, and final art-direction approval
remain open.

### V1: camera-fit rejected and retained

[Live validation V1](live-validation-v1/README.md) preserves the original
scale-1.0 trial. Its authored mesh remained bound through native idle, attack,
ordinary hit, and explicit fixture death, but the upright upper body was cropped
by normal combat framing. V1 remains an immutable rejection; V2 does not
retroactively upgrade it.

## Rebuild and inspect

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/extract_reference.py \
  --assets "$FTK_DATA/resources.assets" --renderer-id 121488 \
  --output scratch/basey-snake-reference-v1
scratch/model-venv/bin/python art-experiments/sablevine-basey-snake/build_geometry.py
/opt/homebrew/bin/blender --background --factory-startup --python-exit-code 1 \
  --python art-experiments/sablevine-basey-snake/render_studio.py
scratch/model-venv/bin/python art-experiments/sablevine-basey-snake/verify_original_geometry.py
python3 tools/ai-model-pipeline/validate_resource_model_profile_route.py \
  --profile art-experiments/sablevine-basey-snake/runtime-profile-scale055.json \
  --asset-dir art-experiments/sablevine-basey-snake \
  --output art-experiments/sablevine-basey-snake/route-preflight-scale055.json
```

The studio imagery is bind-pose review only. It cannot establish native resource
selection, binding, materials, animation, culling, gameplay, portraits,
lifetime or final art acceptance.

## Isolated V2 integration sequence

```sh
python3 tools/ai-model-pipeline/stage_custom_model_profile.py \
  --game-root scratch/mirewarden-game \
  --profile art-experiments/sablevine-basey-snake/runtime-profile-scale055.json \
  --asset-dir art-experiments/sablevine-basey-snake \
  --pin art-experiments/sablevine-basey-snake/build-report.json \
  --pin art-experiments/sablevine-basey-snake/original-geometry-proof.json \
  --pin art-experiments/sablevine-basey-snake/route-preflight-scale055.json \
  --pin art-experiments/sablevine-basey-snake/profile-revision-scale055.json \
  --output scratch/sablevine-basey-snake-scale055-stage
python3 tools/ai-model-pipeline/deploy_custom_model_stage.py \
  --game-root scratch/mirewarden-game \
  --stage scratch/sablevine-basey-snake-scale055-stage \
  --label sablevine-basey-snake-scale055
```

If the runtime helper or content plugin changed, build those artifacts first and
use `deploy_isolated_test_binaries.py` while the isolated game is stopped. Then
launch a fresh isolated run and preserve exact native binding, visible idle,
ordinary attack, nonlethal received hit, explicit fixture death, native
Ready/progression, actual spawned scale, and human camera review in a new
immutable archive. Existing `snakeJungleA`, `snakeJungleC`, or sibling
resource-prefab evidence does not substitute for `enbaseysnake`.

The profile's `minimumBaseHealth: 64` is an isolated clone-only test floor. It
preserves a higher native base health and permits a normal nonlethal-hit trial;
it does not alter the shipped enemy, asset bytes, or game balance.

The older `route-preflight.json` is retained as immutable evidence for the
pre-floor profile. `route-preflight-health64.json` records the V1 test floor;
V2 staging uses the separately pinned `route-preflight-scale055.json`.
