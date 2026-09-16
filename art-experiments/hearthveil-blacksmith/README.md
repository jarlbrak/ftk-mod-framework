# Hearthveil Blacksmith

Hearthveil Blacksmith is an original six-mesh player avatar package for FTK's
exact `blacksmith_Female` skinset. It replaces the three always-present
SkinnedMeshRenderers and the three conditional apparel renderers that make the
Blacksmith outfit visible through the native `armorCloth1` equipment cycle.
No native surface, texture, UV, weight, or animation data is packaged or used
to author the art.

![Hearthveil default outfit in the authored studio](hearthveil-hero.png)

![Hearthveil equipped Gambeson branch in the authored studio](hearthveil-gambeson-hero.png)

The target is `Player_Blacksmith`, CEL `137599`, selected through the
`blacksmith_Female` skinset. The body path is `playerBlacksmith` with source
renderer `121067`; `hairTop` is `121083`, and `hairBottom` is `121178` with the
same six-bone binding profile represented locally by `121083`. The exact
apparel assignments are:

| Native branch | Renderer path | Expected native mesh | Original asset | Palette bones |
|---|---|---|---|---:|
| Unequipped default | `armorBlacksmithF(Clone)` | `armorBlacksmith` | `hearthveil-default-armor.glb` | 19 |
| Always present | `bootsBlacksmith(Clone)` | `bootsBlacksmith` | `hearthveil-boots.glb` | 10 |
| Equipped item59 | `armorGambesonF(Clone)` | `armorGambesonF` | `hearthveil-gambeson.glb` | 28 |

The native one-handed Blacksmith weapon uses `player_1H_Blunt_Combat` in
combat. The package intentionally leaves the native rigid backpack, helmet,
shield, and hammer alone because this player API replaces skinned renderers.

The read-only [player route preflight](route-preflight.json) pins the exact
`blacksmith_Female` avatar body/hair set, source renderer IDs, rig fingerprints,
and authored assets. Its apparel checks cover only declared assets and schema;
native equipment branches remain live evidence.

## Rebuild and source proof

The five local references are ignored scratch artifacts. The generator reads
only their `bone_names` and `bindposes`; original geometry, normals, UVs,
weights, palette, and all art landmarks are authored in
`build_geometry.py`.

Run from the repository root:

```sh
scratch/model-venv/bin/python art-experiments/hearthveil-blacksmith/build_geometry.py
scratch/model-venv/bin/python art-experiments/hearthveil-blacksmith/verify_original_geometry.py \
  --output-dir scratch/hearthveil-blacksmith-original-proof-new
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python art-experiments/hearthveil-blacksmith/render_studio.py
```

The proof command refuses to reuse an output directory. It reruns the generator
with every NPZ read limited to `bone_names` and `bindposes`, then compares all
six authored source/piece pairs and the shared palette byte-for-byte. The
studio renderer reads only the authored JSON and PNG. It previews the default
and equipped apparel branches separately; it is not motion evidence.

## Isolated profile transaction

The profile is [runtime-profile.json](runtime-profile.json). Stage it into a
fresh direct child of `scratch/` without touching the isolated game copy:

```sh
python3 tools/ai-model-pipeline/stage_custom_model_profile.py \
  --catalog-kind player \
  --game-root scratch/mirewarden-game \
  --profile art-experiments/hearthveil-blacksmith/runtime-profile.json \
  --asset-dir art-experiments/hearthveil-blacksmith \
  --pin art-experiments/hearthveil-blacksmith/manifest.json \
  --pin art-experiments/hearthveil-blacksmith/route-preflight.json \
  --pin art-experiments/hearthveil-blacksmith/build-report.json \
  --pin art-experiments/hearthveil-blacksmith/original-geometry-proof.json \
  --output scratch/hearthveil-blacksmith-stage

python3 tools/ai-model-pipeline/deploy_custom_model_stage.py \
  --catalog-kind player \
  --game-root scratch/mirewarden-game \
  --stage scratch/hearthveil-blacksmith-stage
```

Stop the isolated game before the dry review and keep it stopped through the pinned copy:

```sh
python3 tools/ai-model-pipeline/deploy_custom_model_stage.py \
  --catalog-kind player \
  --game-root scratch/mirewarden-game \
  --stage scratch/hearthveil-blacksmith-stage \
  --label hearthveil-blacksmith --execute
```

The deployment receipt pins the player catalog, all 355 prior model assets,
each six-mesh asset, and the authoring evidence. It makes a timestamped backup
inside the isolated game and refuses a live process, catalog drift, model drift,
symlinks, or a target outside `scratch/`.

## Live validation

The fresh isolated session `eeadf75d450540558db47a6908ae9667` deployed the
exact six authored assets and registered custom class ID 113. Both real avatar
owners retained five active package renderers: body, two hair parts, boots, and
the currently selected armor branch. The overworld owner settled at CEL
`-251870`; the combat owner settled at `-253630`; both held lease 7 with two
references.

The native `armorCloth1` item59 cycle moved from Body/Backpack `1/0` to `0/1`
and back to `1/0`. Each operation rebuilt both owners. The unequipped branch
selected `armorBlacksmithF(Clone)` with the original default armor, while the
re-equipped branch selected `armorGambesonF(Clone)`. Body, hair, and boots
remained custom throughout. Read-only watches then observed both retired leases
4 and 6 absent, with all 15 recorded assets Unity-null.

The Gambeson branch has a real 120-frame native attack capture with ordinary
enemy HP `72 → 62`; it samples `attack_blunt1H` and `damageLight_blunt1H`.
A separate native pass capture sampled two incoming damage responses and hero
HP `970 → 913`. The disposable encounter reached native Loot after an explicit
fixture death, then two guarded native Collect votes reached strict Ready at
level 0, room 3. Selected frames show the original model stable under the
native hammer, shield, backpack, helmet, effects, and combat UI.

The historical [V1 live archive](live-validation-v1/README.md) preserves the
combat, apparel-cycle, lifetime, and progression receipts. A later isolated
[V2 preview supplement](live-validation-v2/README.md) reached the actual native
Party Select screen, where class ID 113 was visibly assembled on the game-owned
pedestal. It records a 24-frame fixed-step `standardIdle_handsDown` capture of
the exact visible `playerBlacksmith` renderer. The V2 preview does not widen
the V1 evidence to every camera, culling condition, apparel branch, player
death, final-owner teardown, skinset, controller, portrait, multiplayer layout,
or final-art approval.

The [manifest](manifest.json) pins the exact target, assets, proof, live
outcomes, and current limits. Use the reusable
[FTK custom-model skill](../../skills/ftk-custom-models/SKILL.md) to repeat this
process for another player skinset or enemy skeleton.
