# Cinderbloom

Current source, GLBs and studio views are **version 2**, with the face toward
native Unity +Z. Version 1 live evidence is archived separately below.

An original multipart carnivorous flower for FTK's verified `plantA` skeleton:
a dark curved stalk supports a broad copper corolla, an ember-colored throat,
short ivory teeth and an articulated lower cup. Four broad root leaves anchor
the silhouette; two smaller leaves belong to the separate native leaf renderer.
These are authored surfaces, not enlarged skeleton markers.

| Exact CEL-relative renderer | Runtime file | Palette joints | Geometry |
|---|---|---:|---|
| `enPlant01` | `cinderbloom_body.glb` | 45 | 5,568 split vertices / 1,856 triangles |
| `enPlant01Leaves` | `cinderbloom_leaves.glb` | 16 | 504 split vertices / 168 triangles |

Both parts use `cinderbloom_basecolor.png`. Register them together with
`Content.SetEnemyBodyMeshesFromGlb` using the [strict renderer API](../../docs/MODEL-RENDERER-API.md).
This directory does not deploy or register content. The v1 prototype has now
been injected and captured in native combat; its facing review failed as detailed
below. Version2 subsequently passed its targeted live facing fix; final polish
and broader coverage remain pending.

## Editable sources and reproduction

`build_geometry.py` creates original parametric surfaces and authored weights.
It reads only joint names and inverse-bind joint centers for geometry generation;
no native surface vertices, faces, normals or textures are copied. The separate
independent validator reads the ignored native reference for contract comparison.
`build_blender.py` creates the two editable armature scenes, re-exports each
through the Blender bridge, and creates the combined studio presentation.

- `cinderbloom_body.blend` and `cinderbloom_leaves.blend`: independent editable
  bind-pose meshes with vertex groups, native armatures and packed original PNG.
- `cinderbloom-studio.blend`: both parts with studio lighting and camera. Export
  from each individual part file; the studio has two armatures.
- `*.source.json`: original mesh-local positions, normals, palette UVs and skin.
- `*.pieces.json`: named original component ranges for editing the generator.
- `hero.png`, `side.png`, `turntable.mp4`: studio bind-pose presentation.

The current reference IDs are 120953 (body) and 121072 (leaves). Verify them
against a fresh local inventory if the game build changes. Local references
remain at ignored `scratch/skeleton-audit/<renderer>/reference.npz` and
`skeleton.json`; use the pipeline extractor/auditor to recreate them.

From the repository root:

```sh
scratch/model-venv/bin/python art-experiments/cinderbloom-plant/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python art-experiments/cinderbloom-plant/build_blender.py
ffmpeg -framerate 12 -i scratch/cinderbloom-roundtrip-v2/turntable/%04d.png \
  -frames:v 48 -c:v libx264 -crf 19 -pix_fmt yuv420p -movflags +faststart \
  art-experiments/cinderbloom-plant/turntable.mp4
scratch/model-venv/bin/python art-experiments/cinderbloom-plant/finalize_manifest.py
```

Use the appropriate local Blender and Python paths on another machine. Existing
renders may be overwritten by rebuilding; FFmpeg requests confirmation before
replacing an existing movie. Raw studio frames and independent Blender re-export
files remain in ignored scratch.

## Binding and review limits

Stem rings interpolate adjacent joints along the actual curved bind chain.
The paired broad petal mantles follow the two native `Bone018`–`Bone023` chains;
the lower cup follows `Jaw`, and mouth details follow `Head`. Ground leaves use
the body's root-leaf bones. The separate middle leaves use `Leaf5` and `Leaf6`
in their own complete palette. This explicit anatomical mapping avoids nearest-
surface transfer and retains exact native palettes and inverse bind matrices.

Both original bind-space bounds fit inside their respective native surface
bounds. That is a fit check, not proof of the full animation/culling envelope.
The production API must preserve native animated bounds. The broad petals,
short teeth, jaw opening and leaf/stem intersections still need live idle,
attack, hit and death review. Other plant/controller variants are not covered.
Studio lighting does not reproduce FTK's material, emission or camera conditions.

The manifest separates binary/skin validation, Blender roundtrip and studio
review from pending live acceptance. A pleasant still is not a completed
in-game model validation.


## Isolated live-test recipe

`runtime-profile.json` is a single named-model entry for the existing runtime
helper: key `ftkmf_modeltest_cinderbloom`, base enemy `plantA`, and this build's
verified combat-profile fingerprint. Merge its entry into the intended isolated
helper profile manifest; do not replace a larger manifest unintentionally.
Stage these exact original files under that isolated install's
`BepInEx/plugins/FTKModFramework_content/models/`:

- `cinderbloom_body.glb`
- `cinderbloom_leaves.glb`
- `cinderbloom_basecolor.png`

For ordinary public-API content, after creating/configuring the custom enemy:

```csharp
Content.SetEnemyBodyMeshesFromGlb(enemy,
    new EnemyRendererMesh("enPlant01", "cinderbloom_body.glb", "cinderbloom_basecolor.png"),
    new EnemyRendererMesh("enPlant01Leaves", "cinderbloom_leaves.glb", "cinderbloom_basecolor.png"));
```

Use a fresh isolated process or a verified native Ready slot within the owned
session. Confirm both exact runtime renderer identities, then capture idle,
attack, received hit, death, loot and next Ready progression. Studio approval
is not live acceptance, and successful registration alone is not binding proof.

## First live iteration: v1 facing failure

Both original parts bound in native `plantA` combat, session
`6dd2bce53bfa4af2907d96d4ef59ba1a`, stage
`4293589eef3d4d1ab932b966a93ecc3f`. Native attack, ordinary hit (58 to48 HP), and
explicit KillSingle death fixture (48 to0 HP) each produced120 unpaused frames.
The reviewed flower opens its petals, flexes its stem, bends on hit and collapses;
native effects partly occlude it and small teeth/mouth details are unreadable at
the normal camera. One guarded Golden Root collection reached strict Ready0/2.

**Facing review failed:** v1 mouth/teeth/throat were authored toward mesh-local
minusZ while the native head/jaw forward is plusZ. Captured skin transforms keep
the face away. Valid bindings and fitted bounds did not prevent this anatomical
error. This is an integrated original prototype, not final visual acceptance.
A targeted source correction and new native retest are required. These archived
v1 results remain tied to their original GLB/PNG hashes even after source edits.

- [Native attack MP4](live-v1/attack.mp4)
- [Nonlethal hit MP4](live-v1/nonlethal-hit.mp4)
- [Kill-fixture death MP4](live-v1/kill-fixture-death.mp4)
- [Live evidence and hashes](live-validation-v1.json)

Nine selected PNGs and three capture summaries are retained in `live-v1/`.
Videos replay120 screenshots at12fps, not a real-time performance benchmark.


## Version 2 facing correction

The first live run exposed an authoring error: version 1 placed the mouth,
teeth and jaw toward Unity -Z. Native head and jaw surfaces extend primarily
along +Z, and the recorded head transforms kept our -Z face pointing away from
the combat view. The coherent stem and petal motion did not establish correct
face orientation. This was an original-art fitting error, not a loader failure.

Version 2 rebuilds the central crown, throat, teeth and jaw cup toward +Z and
moves the studio camera to that side. It does not rotate the skeleton or the
entire creature. The stem, four root leaves, root bulb, paired articulated
mantles and veins remain byte-identical in source mesh data. Every original
vertex's joint indices and weights, full bone palettes and inverse bind matrices
remain unchanged. The separate leaf GLB and palette PNG are also byte-identical.
Both updated surfaces still fit their respective native bind-space bounds.

The complete pre-fix source/GLB/manifest/studio package is preserved locally at
ignored `scratch/cinderbloom-v1-before-facing-fix`. The change audit is
`scratch/cinderbloom-v2-change-audit.json`. Current exports and saved Blender
roundtrips pass. The subsequent version2 live test below verifies the targeted
facing correction on its new body hash; version1 evidence remains unchanged.

## Version 2 live facing fix passed

Both parts bound in session `42f40ae3067344b786e959a92f35cbf2`, stage
`d04e99bce0a841199203f0dfbffc5b52`, with body SHA-256
`ab9cec051208a0dd9527ae70ee596504e6dd46dc827473c512b93deae02c4d46`.
The leaf GLB and palette match v1. Reviewed mouth, teeth and ember throat now
correctly face the player. Petal opening, stem flex, hit bending and collapse
are coherent without obvious exploding geometry in reviewed areas. **The targeted
facing fix passes live.** This is an integrated usable original prototype;
small-detail polish, hero/effect occlusion, full culling and other variants remain
outside acceptance.

Attack, ordinary hit (58 to48 HP), and explicit KillSingle death (48 to0 HP)
each recorded120 unpaused frames over about10.9083 game seconds. One guarded
Collect reached strict Ready0/2. Body telemetry covers attack/death and leaf
telemetry covers hit; do not infer complete per-renderer animation coverage.

- [V2 native attack MP4](live-v2/attack.mp4)
- [V2 nonlethal hit MP4](live-v2/nonlethal-hit.mp4)
- [V2 kill-fixture death MP4](live-v2/kill-fixture-death.mp4)
- [V2 evidence and hashes](live-validation-v2.json)

Seven reviewed PNGs and three summaries are archived in `live-v2/`. Videos are
12fps replays of120 captured screenshots, not real-time performance benchmarks.
The v1 facing failure and its immutable artifacts remain in `live-v1/`.

## Unchanged v2 under corrected native scale

The same v2 body/leaf/texture hashes passed a native-scale regression in session
`61d9e44ebed54190ae8b9e4e8b503d6f`, corrected framework `9e533...`. Neutral
factor1 retained native scale1.6. Reviewed front, stem and mouth remained readable
without frame escape, and recoil/collapse were coherent. This is a regression
pass for this prototype, not a v3 geometry revision or full culling/variant pass.
The previous v2 old-framework evidence remains separate.

Four120-frame captures record native attack, player-scoped attack (enemy58 to50),
enemy-scoped hit (50 to42), and KillSingle collapse (42 to0), all unpaused. One
Collect reached Ready0/3. The player clip does not approve original player art:
item59 was unequipped, but default native clothes still conceal the custom body.

- [Native attack](live-native-scale/attack.mp4)
- [Player-scoped attack](live-native-scale/player-attack.mp4)
- [Nonlethal hit](live-native-scale/nonlethal-hit.mp4)
- [Kill-fixture collapse](live-native-scale/kill-fixture-death.mp4)
- [Regression evidence and hashes](live-validation-native-scale.json)

Eight reviewed PNGs and four summaries accompany the12fps screenshot replays.

## Fresh catalog-411 multipart run

[V3 evidence](live-validation-v3/README.md) records a fresh session at
public visual-scale factor `1.0` (captured native CEL scale `1.6`). Both
authored parts bind to the same plantA enemy owner: `enPlant01` uses the
corrected-facing body and `enPlant01Leaves` uses the separate leaf GLB, with
separate observed bone signatures. The corrected mouth, teeth, ember throat
and jaw cup face the combat camera; the selected idle and attack views retain
the stem, corolla, root leaves and separate leaf renderer without obvious
multipart separation.

The pass and ordinary attack captures contain 120 unpaused frames. Ordinary
damage is 58 to 50 with `cheat=None` and no focus; explicit `KillSingle`
completes 120 frames, one native Collect is accepted and strict Ready is
observed at level 0 room 2. Root review covers two idle frames, two attack
frames and the fixture endpoints. Native UI/effects and the victory item
surface limit fine teeth, jaw, leaf-intersection, complete deformation,
culling-envelope, portrait/resource lifetime and finished-art acceptance.

## V4 exact leaf-renderer route

[V4 leaf evidence](live-validation-v4-leaves/README.md) isolates the exact
`plantA / enPlant01Leaves / renderer 121072` route in session
`e4890060b24a4e8e90968268898bcdf4`. The archive records the authored
`cinderbloom_leaves.glb` on bone signature
`5986c6bf9143cd84d7d81a10c360af1a16ebc5cfb2ce604d91ef010fb074438a`
through three complete 120-frame captures. Structured native events identify
settled idle, `AttackProf1`, ordinary `DamagedHeavy`, recovery, and the
explicit-fixture `Death` sequence while the exact leaf mesh identity remains
stable.

One ordinary no-focus hit reduced the same target from 18 to 8 HP. The death
capture used the separate `KillSingle` fixture and therefore is not ordinary
lethal-damage evidence. The fixture completed native progression to strict
Ready at level 0 room 2.

Sixteen hash-pinned original PNGs were reviewed. They retain the complete
Cinderbloom silhouette and connected leaf assembly through the sampled idle,
attack, hit, recovery, collapse, and settled frames. The native close attack
camera briefly overlaps the hero and target, and the final loot view limits
surface detail. The independently verified archive contains all 360 capture
images, three presentation videos, lossless source metadata, the root review,
and source asset pins.

This V4 archive is the canonical exact-source representative for the leaf
topology only. It does not cover `enPlant01` body renderer 120953. That body
renderer requires its own exact route archive. Portraits, collision, extended
culling, long-session resource lifetime, other native ability variants, and
final art-direction approval also remain open.

## V5 exact body-renderer route

[V5 body evidence](live-validation-v5-body/README.md) isolates the exact
`plantA / enPlant01 / renderer 120953` route in session
`572176f3391e44e192e7112d6ed3b5ee`. The authored
`cinderbloom_body.glb` remains on bone signature
`0e515f0853f8fff145982ba036d438588431e0b6046651a9b69a8199ddd569ad`
through three complete 120-frame captures. Structured native events identify
settled idle, native `Attack`, ordinary `Damaged`, recovery, and the
explicit-fixture `Death` sequence while the exact body mesh identity remains
stable.

One ordinary no-focus hit reduced the same target from 18 to 10 HP. The death
capture used the separate `KillSingle` fixture and therefore is not ordinary
lethal-damage evidence. The fixture completed native progression to strict
Ready at level 0 room 2.

Nineteen hash-pinned original PNGs were reviewed. They retain the corrected
forward-facing mouth, crown, stem, base, and complete silhouette through the
sampled idle, attack, recoil, recovery, collapse, and settled frames. Native
hit effects and the foreground hero obscure parts of the impact interval, and
the final loot view limits surface detail. The independently verified archive
contains all 360 capture images, three presentation videos, lossless source
metadata, the root review, and source asset pins.

This V5 archive covers the body topology only. The separate V4 archive covers
the `enPlant01Leaves` renderer. Portraits, collision, extended culling,
long-session resource lifetime, other native ability variants, and final
art-direction approval remain open for both records.
