# Cairnfire Troll B

Cairnfire Troll B is an original, low-poly basalt, copper, and ember body for
FTK's exact `trollB` target. It uses the native Troll B bone palette and inverse
bind matrices, but no native surface, texture, UV, weights, or animation data.

The exact live target is `trollB` on `enTroll02`, CEL component path `137393`,
with SkinnedMeshRenderer source `121256` at `enTroll01`. Its observed combat
controller is `trollController` (`6006`), represented by combat fingerprint
`ab549dcfcad738d283caac671d43a8b7053e64a95ae0805babf218599efe09a2`.

![Cairnfire Troll B bind-pose studio preview](cairnfire-trollb-hero.png)

## Rebuild

The local reference is intentionally ignored at
`scratch/trollb-reference/reference.npz`. Extract it only from the isolated
game copy, then run these commands from the repository root:

```sh
scratch/model-venv/bin/python art-experiments/cairnfire-troll/build_geometry.py
scratch/model-venv/bin/python art-experiments/cairnfire-troll/verify_original_geometry.py \
  --output-dir scratch/cairnfire-troll-original-proof-new
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python art-experiments/cairnfire-troll/render_studio.py
```

`build_geometry.py` consumes only `bone_names` and `bindposes` from the local
reference. The proof command refuses to overwrite an existing proof directory,
so each provenance run remains reviewable. The studio renderer reads the
authored JSON and PNG only. It is a visual review of the bind pose, not evidence
of in-game animation.

## Isolated deployment

The profile is [runtime-profile.json](runtime-profile.json). Stage it into a
fresh directory without changing the game copy:

```sh
python3 tools/ai-model-pipeline/stage_custom_model_profile.py \
  --game-root scratch/mirewarden-game \
  --profile art-experiments/cairnfire-troll/runtime-profile.json \
  --asset-dir art-experiments/cairnfire-troll \
  --pin art-experiments/cairnfire-troll/manifest.json \
  --pin art-experiments/cairnfire-troll/build-report.json \
  --pin art-experiments/cairnfire-troll/cairnfire-trollb.validation.json \
  --pin art-experiments/cairnfire-troll/original-geometry-proof.json \
  --output scratch/cairnfire-trollb-stage
```

Review the stage first, then deploy only to the stopped isolated copy:

```sh
python3 tools/ai-model-pipeline/deploy_custom_model_stage.py \
  --game-root scratch/mirewarden-game --stage scratch/cairnfire-trollb-stage
python3 tools/ai-model-pipeline/deploy_custom_model_stage.py \
  --game-root scratch/mirewarden-game --stage scratch/cairnfire-trollb-stage \
  --label cairnfire-trollb --execute
```

The deployer checks the catalog and every prior model hash before copying the
two original assets and the catalog. It makes a timestamped backup in the
isolated game copy. It refuses a running game, a stale stage, symlinks, and any
non-isolated root.

## Current validation state

The offline GLB and skin contract pass. The source-provenance proof also passes.
The first isolated live run confirmed binding, complete captures, ordinary hit,
fixture death, and native cleanup, but visual review rejected its vertically
inverted palette. The revised PNG reverses its authored tile rows to match the
runtime V flip. A fresh corrected run then showed the exact custom mesh and
texture on `enTroll02(Clone)/enTroll01`, full 120-frame pass, attack, and death
windows, native `attack_troll` and `deathHeavy_troll` motion, ordinary HP 72 to
62, fixture HP 62 to 0, and one native Collect returning to strict Ready 0/2.
Art approval is still pending. See [manifest.json](manifest.json) for the
pinned target and revision facts. The immutable [corrected-palette V2 live
archive](live-validation-v2-palette-corrected/README.md) pins the exact profile,
asset hashes, complete source-frame hashes, selected review frames, videos, and
the V1 rejection history.
