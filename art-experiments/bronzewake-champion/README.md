# Bronzewake Champion

An original bronze-and-teal lamellar champion for the verified `bossGladiator` chassis. Four independent skinned surfaces replace body, compact nape hair, armor/trousers and boots. Studio images show the native bind pose without proprietary accessories. The exact `armorBossGladiator` topology has reviewed canonical live evidence in V4, the exact `enBossGladiator` body topology has reviewed canonical live evidence in V5, and the exact `bootsBossGladiator` topology has reviewed canonical live evidence in V6. These reviews remain scoped and do not constitute final art-direction approval of every surface or animation.

| CEL-relative path | Reference renderer | Runtime file | Full joint palette |
| --- | --- | --- | --- |
| enBossGladiator | 121272 | bronzewake_body.glb | 25 |
| hairBottomBossGladiator | 121500 | bronzewake_hair.glb | 8 |
| armorBossGladiator | 121522 | bronzewake_armor.glb | 26 |
| bootsBossGladiator | 121661 | bronzewake_boots.glb | 11 |

All parts use `bronzewake_basecolor.png`. `runtime-profile.json` supplies exact assignments and the verified combat profile. Native helmet and weapon/shield accessories remain attached by the game; this model does not replace or export those rigid objects. The short braids and fitted head were designed with the observed faceguard in mind, but actual helmet/hair/weapon intersections still require live inspection.

Native forward is Unity +Z, corroborated by the toe chain and facial extent before authoring. The original tubes and polygonal volumes use native joint centers as binding references; they copy no native surface. Arms blend locally at elbows, torso follows successive spine joints, trousers follow hips and knees, and boots follow knees, ankles and toes. Plates use their corresponding local spine or limb joints. Tassets are separate per hip. Each renderer retains its own complete native palette and inverse binds.

The source material metadata explains the boots-only `disableNativeEmission: true`: the first boots material is `matLoot` (1056), with emission enabled and RGB 1.4. The first body/hair/armor materials have zero emission. The runtime clones only the first native material for each one-primitive export. Native tint and shader appearance are separate live checks; the studio palette does not predict their final appearance.

All four original surfaces fit inside their respective native bind-surface bounds. Production animation envelopes remain unchanged; containment is not proof of animated culling coverage. No local extracted native reference geometry or texture is packaged. The metadata file contains only material names, identities and scalar/color properties.

Rebuild from repository root with existing local references and dependencies:

```sh
scratch/model-venv/bin/python art-experiments/bronzewake-champion/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/bronzewake-champion/build_blender.py
scratch/model-venv/bin/python art-experiments/bronzewake-champion/finalize_manifest.py
```

The generator emits explicit mesh-source JSON, original palette, per-piece ranges and FTK GLBs. The Blender script creates each editable armature scene, saves and reopens it, exports through the shared bridge and independently validates it. Reopened exports remain in ignored `scratch/bronzewake-roundtrip`; their hashes appear in the manifest. Export individual part scenes; `bronzewake-studio.blend` combines all four for presentation only. Regeneration resets manual approval to pending.

Remaining acceptance outside V4, V5 and V6: the hair source topology represented by the multipart package, additional proficiencies, DeathLight, portraits, collision, extended culling, long-session resource lifetime, full campaign completion and final art-direction approval.

## First live original baseline

[Live evidence](live-validation.json) records four exact custom bindings under
framework900f/helper434, sessionb3322a5d049e4638acecf30928de8d06. Reviewed armor
and body fit and articulate, without obvious intersection/stretch in selected
views. Native helmet/weapons remain. Native color/environment darkens the
palette, which remains readable; this is scoped prototype review, not flawless
or all-animation art acceptance. Actual boots emission is off, RGB black/mapnull.

- [Armor attack](live/attack.mp4),120 frames; reviewed0/50.
- [Ordinary body hit](live/nonlethal-hit.mp4),58 to48,120frames; reviewed30.
- [Kill-fixture death](live/kill-fixture-death.mp4),48 to0; reviewed40/60.

Death remains a failed partial capture:91 of120 requested frames before
RendererDestroyed, with native ragdoll and11 final rigidbodies. No extra frames
complete the replay, and no full death or destruction-causality claim follows.
All retained frames are unpaused. Native progress automatically reaches strict
Ready0/2, with no Collect. Videos replay at12fps, not real-time performance.

## Native live V2

[The fresh V2 archive](live-validation-v2/README.md) repeats the exact
`bossGladiator` assignment in session `9df826f02881492aa5be8b9098937942` at
public visual-scale factor `1.0` (captured native CEL scale `1.1`). Body, hair,
armor and boots all bound to owner `369188` with separate observed bone
signatures. Selected idle and attack frames retain the native helmet and
weapon/shield accessories while the authored four-part silhouette stays
coherent.

Pass and ordinary attack captures are complete 120-frame recordings. Ordinary
damage changes HP `58→48` with `cheat=None` and no focus. The explicit
`KillSingle` fixture reaches the native victory transition, but the renderer is
destroyed during frame 91 of the requested 120-frame death capture. That
expected prefix is archived as a boundary, not as full death, settled ragdoll
or destruction-causality acceptance. The encounter reaches strict Ready at
level 0 room 2 and exposes a Ready vote directly, so no Collect action is
claimed.

All four runtime materials report no active emission; the boots assignment also
opts out of inherited native emission. Native lighting darkens the palette but
keeps it readable. Fine plate/hair intersection, complete death deformation,
culling, portraits, resource lifetime and finished-art acceptance remain open.

## Fresh live trial V3

The [fresh V3 archive](live-validation-v3/README.md) repeats the exact four-part
`bossGladiator` binding in session `591ba03f086341369926e56e8f4dc444` at public
visual scale `1.0` (captured native CEL scale `1.1`). `enBossGladiator`,
`hairBottomBossGladiator`, `armorBossGladiator` and `bootsBossGladiator` all stay
on owner `369188`; the native helmet and weapon/shield accessories remain
attached by the game.

Pass and ordinary attack are complete 120-frame recordings. Ordinary damage
changes the same target from HP `58` to `48` with `cheat=None` and no focus. The
explicit `KillSingle` fixture commits HP `48` to `0`, while the native renderer
is destroyed during frame `91` of the requested 120-frame death capture. That
prefix is preserved as a cleanup boundary, not as full corpse, settled-ragdoll
or destruction-causality acceptance.

The post-death native surface is strict Ready at level `0` / room `2`, with no
Collect vote available. One guarded native Ready click advances the dungeon to
room `2`; the next live state contains the normal Jelly Cube and the registered
cultist probe. All four materials report the authored basecolor and no active
emission, with inherited boots emission explicitly disabled. Native UI/effects
and the cleanup boundary still limit fine plate/hair, culling, portraits,
resource lifetime, collision/sleeping and finished-art review.

## Canonical live V4

The [V4 archive](live-validation-v4/README.md) certifies topology
`11742c19aa67e6d0` through the exact native `bossGladiator` /
`armorBossGladiator` / renderer `121522` route. Session
`4049537c607648bfbf525e75570102ed` bound the selected authored armor mesh with
bone signature
`ea49e68f33a132d821b8f00fd60d358e4634b2360c05b9a4ccd776dbe59eab6f`.
The same stage also bound the package's body, hair and boots assignments to the
same native owner; those sibling source topologies receive no coverage credit
from this archive.

Pass and ordinary attack each retained all 120 requested frames. The enemy's
native `AttackProf` motion and the ordinary no-focus `DamagedHeavy` response
kept the multipart silhouette coherent; measured target health changed from
`11` to `1`. The explicit `KillSingle` fixture produced a finalized exact-target
native `Death`, then retained 90 frames through `2HandWield_Death` before the
ragdoll controller became unresolved. The helper records that point as an
unsuccessful partial capture with a separate, default-off terminal-boundary
classification. It does not promote the prefix to a complete 120-frame death
capture or ordinary lethal gameplay evidence.

Twenty-one original PNGs were inspected at original resolution. They cover
settled idle, approach, impact, recovery, heavy-hit response, death entry and
the last retained ragdoll frame. No sampled tearing, renderer loss,
viewport-edge clipping or multipart separation was visible. Native equipment,
combat UI, effects, foreground occlusion, depth blur and camera motion limit
fine surface inspection. The encounter still reached strict Ready at level `0`
room `2`, with no Collect action required.

Reproduce the exact bounded route after staging the documented profile into an
isolated game copy:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_execution_queue_route.py \
  --root . \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/mirewarden-game \
  --topology-group 11742c19aa67e6d0 \
  --route-kind directEnemy \
  --motion-renderer-path armorBossGladiator \
  --run \
  --output scratch/model-route-11742c19aa67e6d0-directenemy-NEW-run.json
```

Every rerun needs a fresh output filename. Never overwrite or reinterpret the
V4 session artifacts; build a new reviewed archive for new evidence.

## Canonical body live V5

The [V5 archive](live-validation-v5/README.md) certifies topology
`73ef97795cc54f57` through the exact native `bossGladiator` /
`enBossGladiator` / renderer `121272` route. Session
`eb80d5aa8b494998ba63acfdac8a4b56` bound `bronzewake_body.glb` to renderer
instance `-245382` on owner `369188`, with observed bone signature
`e7c491e2041e744c1757fa0535b2d1c431aae261803adf64349798087d8e80f4e`.
The same stage also bound the package's hair, armor and boots assignments to the
same native owner; those sibling source topologies receive no coverage credit
from this archive.

Pass and ordinary attack each retained all 120 requested frames. The body stayed
visible through native `CombatIdle`, `BasicAttack`, `HitSmall` and recovery
motion. Ordinary no-focus damage changed the same target from HP `11` to `6`.
The material readback confirmed the authored `_MainTex`, black emission color,
no emission map and emission disabled.

The explicit `KillSingle` fixture started from HP `6`; it is not ordinary lethal
gameplay evidence. The retained 90-frame prefix includes native `Death` and
`2HandWield_Death`. Because `m_DoRagdoll=true`, 15 rigid bodies were observed
through frame 25, 11 remained from frame 26, and all 11 active survivors were
nonkinematic from frame 28. The final reviewed sample remained a coherent low
ragdoll pose. The helper then reached a controller-unresolved boundary, so the
archive does not claim a complete 120-frame capture, later corpse lifetime or
cleanup causality.

Eighteen exact original PNGs were inspected at original resolution. The body,
hair, armor and boots remain aligned through idle, attack, hit, recovery and the
retained ragdoll prefix. No sampled tearing, renderer loss, floor fall-through,
viewport clipping or multipart separation was visible. Native equipment,
effects, foreground hero occlusion, depth blur and camera framing still limit
fine inspection. The encounter reached strict Ready at level `0` room `2`, with
no Collect action required.

Reproduce the exact bounded route after staging the documented profile into an
isolated game copy:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_execution_queue_route.py \
  --root . \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/mirewarden-game \
  --topology-group 73ef97795cc54f57 \
  --route-kind directEnemy \
  --motion-renderer-path enBossGladiator \
  --run \
  --output scratch/model-route-73ef97795cc54f57-directenemy-NEW-run.json
```

Every rerun needs a fresh output filename. Never overwrite or reinterpret the
V5 session artifacts; build a new reviewed archive for new evidence.

## Canonical boots live V6

The [V6 archive](live-validation-v6/README.md) certifies topology
`b65252b48fd9609d` through the exact native `bossGladiator` /
`bootsBossGladiator` / renderer `121661` route. Session
`e9d57811cedf4bc695c773ab87f5ff5d` bound `bronzewake_boots.glb` to renderer
instance `-245188` on owner `369188`, with observed bone signature
`9dab517a577b96ba9923e06782faa1786306469392eab546ff016c4c9112b04e`.
The same stage separately verified the package body, hair and armor assignments;
those sibling source topologies receive no coverage credit from this archive.

Pass and ordinary attack each retained all 120 requested frames. The authored
boots remained active, enabled and visible through `2HandWield_CombatIdle`,
`2HandWield_CriticalAttack`, recovery, ordinary `2HandWield_HitBig`, and native
`2HandWield_BasicAttack`. Ordinary no-focus damage changed the same target from
HP `11` to `1` with `DamagedHeavy`. The exact material readback confirmed the
authored base texture, black emission color, no emission map and emission
disabled.

The explicit `KillSingle` fixture started from HP `1`; it is separate from the
ordinary nonlethal hit. The retained 90-frame prefix includes native `Death`,
`2HandWield_Death`, and a physics transition with `m_DoRagdoll=true`. The body
set changes from 15 rigid bodies to 11 at frame 26, and all 11 survivors become
nonkinematic at frame 28. Reviewed samples preserve attached, correctly aligned
footwear through the fall and the last retained floor pose. The helper then
reaches the typed controller-unresolved boundary, so full-duration death, later
corpse lifetime and cleanup causality remain unproven.

Nineteen exact original PNGs were inspected at original resolution. Across all
330 retained frames the exact renderer identity remains stable, active, enabled
and visible. Native body, hair, armor, equipment, combat UI, effects, foreground
hero overlap, depth blur and camera motion limit fine inspection. The encounter
reaches strict Ready at level `0` room `2` with no Collect action.

Reproduce the exact bounded route after staging the documented profile into an
isolated game copy:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_execution_queue_route.py \
  --root . \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/mirewarden-game \
  --topology-group b65252b48fd9609d \
  --route-kind directEnemy \
  --motion-renderer-path bootsBossGladiator \
  --run \
  --output scratch/model-route-b65252b48fd9609d-directenemy-NEW-run.json
```

Every rerun needs a fresh output filename. Never overwrite or reinterpret the
V6 session artifacts; build a new reviewed archive for new evidence.

## Canonical hair live V7

The [V7 archive](live-validation-v7/README.md) certifies topology
`fb84ec3e6e18f459` through the exact native `bossGladiator` /
`hairBottomBossGladiator` / renderer `121500` route. Session
`80a9400b41074c35bf82c590526ffdbd` bound `bronzewake_hair.glb` to renderer
instance `-245268` on owner `369188`, with observed bone signature
`8564fa4a8438235e6bdbd73077f284de0bed317efc657541c16ca4be91c4357d`
at native CEL scale `1.1`. The same stage separately verified the body, armor
and boots assignments; those sibling source topologies receive no coverage
credit from V7.

Pass and ordinary attack each retained all 120 requested frames. The authored
red crest and lower hair accents remained active, enabled and visible through
`2HandWield_CombatIdle`, `2HandWield_BasicAttack`, recovery, ordinary
`2HandWield_HitBig`, and `2HandWield_CriticalAttack`. Ordinary no-focus damage
changed the same target from HP `11` to `1` with `DamagedHeavy`. The exact
material readback records `matGladiator_hair` with the authored base texture,
black emission color, no emission map and emission disabled.

The explicit `KillSingle` fixture started from HP `1`; it is separate from the
ordinary nonlethal hit. The retained 90-frame prefix includes native `Death`,
`2HandWield_Death`, and a physics transition with `m_DoRagdoll=true`. The body
set changes from 15 rigid bodies to 11 at frame 26, and all 11 survivors become
nonkinematic at frame 28. Reviewed samples keep the crest aligned through the
fall and final retained floor pose. The typed controller-unresolved boundary
leaves full-duration death, later corpse lifetime and cleanup causality open.

Twenty exact original PNGs were inspected at original resolution. Across all
330 retained frames the exact hair renderer identity remains stable, active,
enabled and visible without sampled tearing, renderer loss, hair separation,
viewport clipping or floor fall-through. Native body, armor, boots, helmet,
equipment, combat UI, effects, foreground hero overlap, depth blur and camera
motion limit fine inspection. Strict Ready follows at level `0` room `2` with
no Collect action.

Reproduce the exact bounded route after staging the documented profile into an
isolated game copy:

```sh
python3 tools/ai-model-pipeline/runtime-test/run_execution_queue_route.py \
  --root . \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/mirewarden-game \
  --topology-group fb84ec3e6e18f459 \
  --route-kind directEnemy \
  --motion-renderer-path hairBottomBossGladiator \
  --run \
  --output scratch/model-route-fb84ec3e6e18f459-directenemy-NEW-run.json
```

Every rerun needs a fresh output filename. Never overwrite or reinterpret the
V7 session artifacts; build a new reviewed archive for new evidence.
