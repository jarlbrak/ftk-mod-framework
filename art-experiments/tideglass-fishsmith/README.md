# Tideglass Fishsmith

Tideglass Fishsmith is an original three-mesh player package for FTK’s exact
`blacksmith_Fish` skinset. It covers a distinct Fish avatar contract: the body
uses the 25-bone `playerFIsh` rig, the crest uses its six-bone `hairTop` rig,
and the animated kelp mantle uses the seven-bone `hairBottom` rig. No native
surface, texture, UV, weight, or animation data was used to shape the art.

![Tideglass Fishsmith in the authored studio](tideglass-hero.png)

![Tideglass Fishsmith from the authored studio side](tideglass-side.png)

The package targets FTK’s shared `Player_FishPerson` avatar through
`blacksmith_Fish`, CEL `139636`, with these exact assignments:

| Renderer path | Source renderer | Palette bones | Original asset |
|---|---:|---:|---|
| `playerFIsh` | 121366 | 25 | `tideglass-body.glb` |
| `hairTop` | 121490 | 6 | `tideglass-hair-top.glb` |
| `hairBottom` | 121521 | 7 | `tideglass-hair-bottom.glb` |

The [build report](build-report.json) and [manifest](manifest.json) pin local
references, original runtime assets, target paths, and current limits.
[Original-geometry proof](original-geometry-proof.json) reruns the deterministic
authoring generator while each local NPZ is restricted to `bone_names` and
`bindposes`; the regenerated source and palette bytes match exactly.
The read-only [player route preflight](route-preflight.json) separately pins the
complete exact `blacksmith_Fish` body/hair set, source renderer IDs, rig
fingerprints, and V2 asset hashes before staging.

## Rebuild and inspect

```sh
python3 art-experiments/tideglass-fishsmith/build_geometry.py
python3 art-experiments/tideglass-fishsmith/verify_original_geometry.py \
  --output-dir scratch/tideglass-fishsmith-original-proof-rerun
blender --background --factory-startup --python-exit-code 1 \
  --python art-experiments/tideglass-fishsmith/render_studio.py
```

The Blender presentation reads only authored source JSON and the authored
palette. V2 reduces the oversized pale mask treatment to a compact dark visor, tapered
teal snout, brass eye slits, and restrained lantern core. The V1 root inputs
and native preview evidence remain immutable in [live-validation-v1](live-validation-v1/);
studio review does not substitute for FTK appearance or animation review.

## Stage for a fresh isolated run

```sh
python3 tools/ai-model-pipeline/stage_custom_model_profile.py \
  --catalog-kind player \
  --game-root scratch/mirewarden-game \
  --profile art-experiments/tideglass-fishsmith/runtime-profile.json \
  --asset-dir art-experiments/tideglass-fishsmith \
  --pin art-experiments/tideglass-fishsmith/manifest.json \
  --pin art-experiments/tideglass-fishsmith/route-preflight.json \
  --pin art-experiments/tideglass-fishsmith/build-report.json \
  --pin art-experiments/tideglass-fishsmith/original-geometry-proof.json \
  --output scratch/tideglass-fishsmith-stage-<unique-label>
```

Stop the isolated game before dry deployment and keep it stopped through the
explicit deploy. Launch a fresh isolated process, navigate normally to the
active offline Create Game screen, invoke `native-create-character-screen`,
and select the registered custom class through visible Party Select UI. Record
strict `player-preview-state` before capturing the actual native preview.

This V2 Fish package declares no conditional apparel. `blacksmith_Fish`
uses class-specific native armor and boots, so a future extension must first
establish a fresh live outfit inventory. Follow with separate overworld,
combat idle/attack/hit, progression, equipment, and lifetime evidence.
