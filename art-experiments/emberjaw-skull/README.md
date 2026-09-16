# Emberjaw

An original hovering skull for FTK's tested `skullA` chassis: a cracked ivory
mask, faceted obsidian cranium, two curved horns, ember eye sockets and a
separately articulated lower jaw. The open cheek and mandible preserve a skeletal
silhouette. All visible surfaces are original parametric geometry.

| Exact CEL-relative renderer | Runtime file | Full palette | Geometry |
|---|---|---:|---|
| `ChaosSkullTop` | `emberjaw_top.glb` | 22 joints | 5,127 split vertices / 1,709 triangles |
| `ChaosSkullBottom` | `emberjaw_bottom.glb` | 20 joints | 1,044 split vertices / 348 triangles |

Both use `emberjaw_basecolor.png`. `hero.png` and `side.png` are studio bind-pose
renders, not live gameplay. The original prototype has now been injected and
recorded in native combat; final artistic acceptance remains pending.

## Sources and reproduction

`build_geometry.py` creates the skull volumes, polygonal socket rims, teeth,
mandible and narrow mineral fissures. Native data provides only ordered joint
names and bind landmarks for authoring. Native surface vertices, faces, normals,
weights and textures are not copied. The independent validator reads the local
native reference for contract and bind comparison.

`build_blender.py` creates `emberjaw_top.blend` and `emberjaw_bottom.blend`, with
editable original meshes, vertex groups, complete native armatures and a packed
original palette. It saves, reopens and re-exports each file through the FTK
Blender bridge. `emberjaw-studio.blend` combines both parts with studio lighting;
export each renderer from its individual file because the combined scene has
two armatures. `*.source.json` and `*.pieces.json` preserve original mesh data
and named component ranges for procedural editing.

References for this build are renderer 121577 (top) and 121483 (bottom), under
ignored `scratch/skeleton-audit/<renderer>/reference.npz` and `skeleton.json`.
Verify IDs and source hashes against a fresh local inventory on another build.
Use the pipeline extractor/auditor to recreate these local-only references.

From the repository root:

```sh
scratch/model-venv/bin/python art-experiments/emberjaw-skull/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python art-experiments/emberjaw-skull/build_blender.py
scratch/model-venv/bin/python art-experiments/emberjaw-skull/finalize_manifest.py
```

Adjust Blender/Python paths for another machine. Rebuilding updates the original
artifacts and resets manual-review status in the generated manifest.

## Binding and bounds

The native skull faces Unity +Z, which becomes Blender -Y. Both exports retain
the exact complete palette and inverse bind matrices for their own renderer.
Cranium and horns follow `Root_M`; original socket-rim vertices use the native
eye landmark joints and each cavity/iris follows its eye root. The upper arch
and teeth use upper-lip joints. Jaw sides interpolate `Jaw_Bone` with the
corresponding lower lip, and the central chin uses `M_LowerLip`. This is authored
anatomical weighting, not nearest-surface transfer.

Both original bind-space bounding boxes fit within their corresponding native
surface bounding boxes. This is not proof of the animated culling envelope.
Production integration must preserve native animated renderer bounds. Native
idle, attack, received hit, death, socket motion, jaw/teeth intersections, material
appearance and culling still require live validation. Other skull/controller
variants and finished artistic acceptance are not implied by export success.

## Isolated registration recipe

`runtime-profile.json` supplies one entry for the existing test helper:
`ftkmf_modeltest_emberjaw`, base enemy `skullA`, with this build's verified
combat-profile fingerprint. Merge that entry into the intended isolated test
manifest rather than replacing unrelated entries. Copy only the two original
GLBs and `emberjaw_basecolor.png` into that isolated install's
`BepInEx/plugins/FTKModFramework_content/models/` when preparing the live test.
No deployment is performed by these source scripts.

For public-API content, after registering and configuring the custom enemy:

```csharp
Content.SetEnemyBodyMeshesFromGlb(enemy,
    new EnemyRendererMesh("ChaosSkullBottom", "emberjaw_bottom.glb", "emberjaw_basecolor.png"),
    new EnemyRendererMesh("ChaosSkullTop", "emberjaw_top.glb", "emberjaw_basecolor.png"));
```

Use the [strict multipart API](../../docs/MODEL-RENDERER-API.md), confirm both
actual custom renderer identities, and gather normal combat evidence. Successful
helper registration and studio renders are separate from live acceptance.

## First native live iteration

Both original parts bound in native `skullA` combat, session
`6dd2bce53bfa4af2907d96d4ef59ba1a`, stage
`7992f3d02fd849e7b17441040bec9111`. The reviewed front-facing floating skull is
readable and its independent jaw opens wide. Native purple effects strongly
change the studio colors and obscure the mouth; horns approach the HUD/upper
edge, and the hero partly occludes hit motion. This is an integrated usable
prototype observation, not completed art polish or culling validation.

Native attack, ordinary hit (observed53 to48 HP, not an assumed64 HP), and
explicit KillSingle death fixture (48 to0 HP) each produced120 unpaused frames.
Attack sampled `attackScream_skull`; hit sampled `damageLight_skull` and
`attackStare_skull`. `deathDirect_skull` is recorded, but the skull disappears
inside the native purple burst before reviewed frame30. **Full visible death
deformation is not established.** Two guarded Collect actions reached Ready0/3.

- [Native attack MP4](live/attack.mp4)
- [Nonlethal hit MP4](live/nonlethal-hit.mp4)
- [Kill-fixture death MP4](live/kill-fixture-death.mp4)
- [Exact live evidence and hashes](live-validation.json)

Eight selected PNGs and three capture summaries are retained in `live/`. Videos
replay120 screenshots at12fps; they are not real-time performance benchmarks.
The evidence preserves original stage asset hashes independently of source edits.

## Native-scale regression (unchanged model)

[Native-scale evidence](live-validation-native-scale.json) preserves the original
asset hashes under framework9e533f89, sessionb3478c0c658f47c6a3ba82b94b9a6f2a.
Neutral factor1 preserves native scale0.75; both renderer world axes measure0.75.
The front face and independently moving jaw remain coherent at this size in
reviewed views. This is a scoped fit regression pass with prototype art limits.

- [Attack](live-native-scale/attack.mp4), top renderer, reviewed frames0/50.
- [Ordinary hit](live-native-scale/nonlethal-hit.mp4), bottom renderer, HP69 to64, frame30.
- [Kill-fixture death](live-native-scale/kill-fixture-death.mp4), HP64 to0, frames30/60.

All three captures completed120 unpaused frames. Death is already hidden by
native effects at the reviewed frames, so complete visible death deformation
is not established. Two guarded Collect actions reached strict Ready0/3.
Videos replay120 captured frames at12fps; no real-time performance inference.
Culling, other variants and final art polish remain unverified. Earlier
old-framework evidence remains in live-validation.json.

## Fresh catalog-411 multipart run

[V2 evidence](live-validation-v2/README.md) records a fresh session at public
visual-scale factor `1.0` (captured native CEL scale `0.75`). Both authored
parts bind to the same skullA enemy owner: `ChaosSkullBottom` uses the jaw GLB
and `ChaosSkullTop` uses the cranium GLB, with separate observed bone
signatures. The upper cranium, horns, eye sockets, teeth and independent jaw
remain coherent and readable in the selected idle and attack views.

The pass and ordinary attack captures contain 120 unpaused frames. Ordinary
damage is 69 to 59 with `cheat=None` and no focus; explicit `KillSingle`
completes 120 frames, two native Collect actions are accepted and strict Ready
is observed at level 0 room 2. Native `chaosBeastBody` emission and purple
effects brighten the live palette, so color/readability is recorded as a
material limitation; exact live color matching, complete jaw/death
deformation, culling-envelope, portrait/resource lifetime and finished-art
acceptance remain open.

## Exact top-renderer route

[V3 top-renderer evidence](live-validation-v3-top/README.md) isolates the
`skullA / ChaosSkullTop / renderer 121577` topology route. The fresh run bound
`emberjaw_top.glb` with bone signature
`ceefd5e5f8af79aea68956212570ab8131db6694a32ce054b4b47e8ef543f30b`
at native CEL scale `0.75`. This record credits only the upper renderer; the
separately visible `ChaosSkullBottom` jaw requires its own exact route archive.

Three complete 120-frame captures retain the exact top mesh through settled
idle, native `Attack`, an ordinary `Damaged` response and recovery, native
`AttackProf` counterattack, and explicit-fixture `Death`. Thirty original PNGs
were reviewed. The upper cranium and horns remain coherent and aligned to the
lower jaw throughout the sampled motion. The ordinary no-focus attack reduced
the enemy from 69 to 61 HP, and strict Ready was observed at level 0 room 2.

The native death effect deactivates the skull between reviewed capture indices
24 and 26 and replaces it with a purple particle burst. V3 therefore accepts
the clean destruction handoff without claiming an articulated corpse pose.
Death still comes from the explicit `KillSingle` fixture, so ordinary lethal
damage remains unproven. Portrait, collision, extended culling, long-session
resource lifetime, additional abilities, and final art approval remain open.

The exact route can be repeated from a current staged campaign with:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_execution_queue_route.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/mirewarden-game \
  --topology-group 8f027145e71c9525 \
  --route-kind directEnemy \
  --profile-document art-experiments/emberjaw-skull/runtime-profile.json \
  --motion-renderer-path ChaosSkullTop \
  --attack-attempts 8 \
  --run \
  --output scratch/model-route-8f027145e71c9525-directenemy-run.json
```

Always use the current generated campaign command rather than assuming this
historical topology identifier remains valid after an inventory change.

## Exact lower-jaw route

[V4 lower-jaw evidence](live-validation-v4-bottom/README.md) isolates the
`skullA / ChaosSkullBottom / renderer 121483` topology route. The fresh run
bound `emberjaw_bottom.glb` with bone signature
`68ca14bd21428d7421aec38e3c89c94195458104a3167e3b79033e09080b4bb4`
at native CEL scale `0.75`. Together with V3, the two exact archives cover both
renderer assignments in the Emberjaw package without transferring evidence
between their distinct bone palettes.

Three complete 120-frame captures retain the lower-jaw mesh through settled
idle, native `Attack` and `AttackProf` motion, an ordinary `Damaged` response
and recovery, native `AttackCrit` counterattack, and explicit-fixture `Death`.
Thirty-four original PNGs were reviewed. The jaw remains aligned while closed,
opens widely with a clean hinge and continuous tooth row, then returns to its
settled position. The ordinary no-focus attack reduced the enemy from 69 to 61
HP, and strict Ready was observed at level 0 room 2.

The same native death boundary applies: the jaw is present at reviewed index 24
and deactivated into the particle burst by index 26. This accepts the renderer
cleanup path without claiming an articulated corpse. The death action remains
an explicit fixture, and portrait, collision, extended culling, resource
lifetime, additional ability variants, and final art approval remain open.

The exact lower-jaw route can be repeated from a current staged campaign with:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_execution_queue_route.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/mirewarden-game \
  --topology-group b4ccd4e96e3a224b \
  --route-kind directEnemy \
  --profile-document art-experiments/emberjaw-skull/runtime-profile.json \
  --motion-renderer-path ChaosSkullBottom \
  --attack-attempts 8 \
  --run \
  --output scratch/model-route-b4ccd4e96e3a224b-directenemy-run.json
```

Use the command regenerated by the current campaign when inventory or profile
inputs change.
