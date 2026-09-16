# Wildbloom Herbalist

Wildbloom Herbalist is an original three-mesh player package for FTK's exact
`herbalist_Female` skinset. It is the first authored package for the player-only
seven-bone `hairBottom` topology: the body uses the exact 24-bone
`player_Herbalist` rig, the crown uses its distinct six-bone `hairTop` rig, and
the shoulder braids use the seven-bone `hairBottom` rig. No native surface,
texture, UV, weight, or animation data was used to shape the art.

![Wildbloom Herbalist in the authored studio](wildbloom-hero.png)

![Wildbloom Herbalist from the authored studio side](wildbloom-side.png)

The package targets the `Player_Herbalist` avatar through the
`herbalist_Female` skinset, CEL `137648`, with these exact assignments:

| Renderer path | Source renderer | Palette bones | Original asset |
|---|---:|---:|---|
| `player_Herbalist` | 121202 | 24 | `wildbloom-body.glb` |
| `hairTop` | 121088 | 6 | `wildbloom-hair-top.glb` |
| `hairBottom` | 121003 | 7 | `wildbloom-hair-bottom.glb` |

The [build report](build-report.json) and [manifest](manifest.json) pin the
three local reference files, all runtime assets, exact target paths, and current
limits. [Original-geometry proof](original-geometry-proof.json) reruns the
deterministic authoring generator while each local NPZ is restricted to
`bone_names` and `bindposes`; the regenerated source and palette bytes match.
The read-only [player route preflight](route-preflight.json) separately pins the
complete exact `herbalist_Female` body/hair set, source renderer IDs, and rig
fingerprints before staging.

## Rebuild and inspect

```sh
python3 art-experiments/wildbloom-herbalist/build_geometry.py
python3 art-experiments/wildbloom-herbalist/verify_original_geometry.py \
  --output-dir scratch/wildbloom-herbalist-original-proof-rerun
blender --background --factory-startup --python-exit-code 1 \
  --python art-experiments/wildbloom-herbalist/render_studio.py
```

The Blender presentation reads only the package's authored source JSON and
palette. It is a studio review, not live FTK evidence.

## Native preview evidence

[Live validation V1](live-validation-v1/README.md) records a fresh isolated
Party Select session in which the visible Player 1 class was Wildbloom
Herbalist. The game-owned preview owner used the exact `herbalist_Female`
skinset, native pedestal, and all three expected custom meshes. Its body was
then captured for 24 fixed-step frames while native
`standardIdle_handsDown` advanced for 1.916 seconds of measured game time.

That record confirms the native preview and idle join only. It does not claim
overworld or combat behavior, item/equipment compatibility, progression,
teardown, portrait behavior, multiplayer behavior, or final art approval.

## Stage for a fresh isolated run

Use a fresh stage directory; replace `<unique-label>` before each new stage:

```sh
python3 tools/ai-model-pipeline/stage_custom_model_profile.py \
  --catalog-kind player \
  --game-root scratch/mirewarden-game \
  --profile art-experiments/wildbloom-herbalist/runtime-profile.json \
  --asset-dir art-experiments/wildbloom-herbalist \
  --pin art-experiments/wildbloom-herbalist/manifest.json \
  --pin art-experiments/wildbloom-herbalist/route-preflight.json \
  --pin art-experiments/wildbloom-herbalist/build-report.json \
  --pin art-experiments/wildbloom-herbalist/original-geometry-proof.json \
  --output scratch/wildbloom-herbalist-stage-<unique-label>
```

Stop the isolated game before dry deployment and keep it stopped through the
explicit deploy. Then launch a fresh isolated process, navigate normally to the
active offline Create Game screen, invoke `native-create-character-screen`, and
select the registered custom Herbalist through the visible Party Select UI.
Record a strict `player-preview-state` before capturing the actual native
preview. Follow with separate overworld, combat idle/attack/hit, progression,
equipment, and lifetime evidence; this package declares no conditional apparel
assignment yet.
