# Rimecrown Sentinel

An original five-part frost construct on `snowmanB`: a broad glacier pedestal, carved stern face, three-point ice crown, articulated crystal hands and burgundy scarf. Studio images show original surfaces in the native bind pose. Canonical V4 head, V5 scarf, V6 base, V7 hat and V8 middle-body routes cover all five exact renderer topologies for binding, appearance, native motion and gameplay at the observed native CEL scale. Finished-art and broad acceptance remain pending.

| CEL-relative renderer | Reference ID | Runtime asset | Full palette |
| --- | --- | --- | --- |
| SnowMan_Geo/enSnowmanHat | 121405 | rimecrown_hat.glb | 8 |
| SnowMan_Geo/enSnowmanHead | 121415 | rimecrown_head.glb | 11 |
| SnowMan_Geo/enSnowmanBase | 121557 | rimecrown_base.glb | 16 |
| SnowMan_Geo/enSnowmanScarf | 121639 | rimecrown_scarf.glb | 13 |
| SnowMan_Geo/enSnowmanmiddleBody | 121697 | rimecrown_body.glb | 31 |

All parts use `rimecrown_basecolor.png`. The profile initially targets Snowman B with `minimumBaseHealth: 64`. The existing exact combat profile `fe9f828784ae535bae62e5eca1ab1555c493414de30a1fb6e5c65089a52f9339` is shared by Snowman A and B. Their prior native calibration observations are separate cases; this does not establish authored-model acceptance for either variant. No controller difference is inferred from their names.

Native forward was checked before authoring using the front scarf chain (+Z) and head protrusion. The complete native inverse binds and palettes are retained per renderer, including unused joints. Original vertices, triangles and palette texture are generated parametrically; no native surface is copied or included in the Blender scenes.

The base is rigid to its actual native weighted joint `Root_M`, not `BotttomBall`. The chest uses `ChestBall`, head `HeadBall`, crown `Hat`; these surfaces share no triangles or manufactured connecting strip. The previous diagnostic Snowman connector used a fully weighted BottomBall-to-ChestBall span despite only 1/31 native BottomBall influence, producing a giant death strut. This authored model avoids that artificial span entirely. Native spelling `BotttomBall` remains in the palette but receives no authored chest weights. Local arm chains and scarf chains use ordinary joint-local weights and blends; scarf terminal joints with little or no native influence are not promoted to full-weight endpoints.

All five surfaces fit within their respective native bind-surface bounds. Native animation envelopes remain unchanged; this is not an animated culling proof. Live disassembly, scarf motion and hand articulation still require inspection.

`native-material-metadata.json` records the source asset hash and scalar/color properties only. All first material slots use 227 `matSnowman`, with white tint and zero emission. The head has a secondary emissive ice material, but the one-primitive replacement inherits only the first slot. The profile therefore leaves `disableNativeEmission` false for all five parts. Studio cyan eyes are palette colors, not emission effects; live appearance remains a separate check.

Rebuild from the repository root with the existing dependencies and local references:

```sh
scratch/model-venv/bin/python art-experiments/rimecrown-sentinel/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/rimecrown-sentinel/build_blender.py
scratch/model-venv/bin/python art-experiments/rimecrown-sentinel/finalize_manifest.py
```

The Blender script creates each editable rigged scene, saves it, reopens the saved file, exports through the FTK bridge and independently validates it. Reopened exports remain in ignored `scratch/rimecrown-roundtrip`; their hashes appear in the manifest. Export individual part scenes. The combined `rimecrown-studio.blend` is for presentation only. The finalizer checks direct/reopened exports and bind bounds, refreshes hashes and resets manual acceptance after regeneration. Material metadata can be refreshed with `inspect_materials.py --assets /absolute/path/to/resources.assets`.

The live records below preserve the exact five-renderer assignment. V4 grants canonical route credit only to the selected head renderer, V5 only to the scarf and V6 only to the base. Remaining checks include each unresolved companion renderer topology, full animation intervals, material readability, culling, collision/sleeping, final cleanup and Snowman A authored-model coverage. No game or catalog deployment is performed by the offline authoring scripts.

## First live run: camera-fit failure

[V1 evidence](live-validation-v1.json) archives all five exact assignments and
three complete 120-frame, unpaused captures. Parent reviewed pass0/50, hit30
and death40/60. The crown overlaps HUD at idle and upper body/head rise above
the screen during native attack while the base stays. This is a fit failure;
a separate scale 0.75 catalog is prepared but its live fit remains untested.

- [Attack](live-v1/attack.mp4)
- [Ordinary hit](live-v1/nonlethal-hit.mp4),58 to48, normal 10 damage.
- [Kill-fixture death](live-v1/kill-fixture-death.mp4),48 to0.

During snowman_deathDirect upper parts fall while the base remains anchored.
Native base renderer 121556 has all7683 vertices100% weighted to Root_M;
source weight and clip-binding evidence is hashed separately. Separation alone
is not a weighting or binding bug, and no artificial connector is added. Two
guarded Collect actions reach Ready0/2. Videos replay12fps, not real-time
performance; full visibility, corrected fit and final art remain pending.

## Scale0.75 correction verified in selected views

[V2 archive](live-validation-v2.json) preserves the same geometry under the
exact corrected deployment hashes. Idle crown clears HUD and the attack crown
is onscreen at previously cropped frame50. Native health-banner/effect overlap
remains. Normal 8 damage 58 to50 is captured but dust/hero obscure frame30.
Death50 to0 retains native upper-part falloff and anchored rootweighted base,
without obvious new stretch in reviewed 40/60. All three captures complete 120
unpaused frames; two Collect actions reach Ready0/2. This is selected-frame
fit correction, not full visibility or culling acceptance. V1 failure remains.

[Attack](live-v2/attack.mp4), [hit](live-v2/nonlethal-hit.mp4),
[death](live-v2/kill-fixture-death.mp4).

## Fresh catalog-411 V3 live supplement

The [V3 archive](live-validation-v3/README.md) records session `35ba69223a7c4ee1a20c781289b181e4` after the 0.75 scale correction. Hat, head, base, scarf and middle body bound to one `snowmanB` owner. Root reviewed idle, attack, native smoke/falloff and loot frames; the crown remains inside the corrected camera, ordinary HP changes 58 to 50, explicit `KillSingle` completes 120 frames, and two native Collect actions reach strict Ready at level 0, room 2.

This is selected-frame binding, scale, motion and gameplay evidence. Native effects and hero occlusion limit fine review; the explicit death fixture is not ordinary lethal damage, and full culling, collision/sleeping, material fidelity, finished-art quality and final resource lifetime remain separate checks. The V1 fit failure and V2 correction records remain unchanged.

## Canonical head route: V4

The [V4 head archive](live-validation-v4-head/README.md) is the canonical exact-source record for topology `02a6f31412bd52ab`, direct-enemy source `snowmanB`, renderer `SnowMan_Geo/enSnowmanHead` and source renderer 121415. The isolated process observed native CEL scale 1.0. The same owner bound the authored hat, base, scarf and middle-body parts, but V4 grants no route credit to those four companion topologies.

Pass, ordinary attack and kill-fixture captures each retain 120 frames. The ordinary zero-focus attack used the native blunt smith hammer without a skill-cap or damage fixture and produced `Damaged` for 13 HP, from 58 to 45. The profile's `minimumBaseHealth: 64` and the disposable party maximum-HP fortification make the trial unsuitable as a balance sample. Explicit `KillSingle` then produced recorded 1000 damage, HP 45 to 0 and native `Death`; this is fixture death evidence, not ordinary lethal gameplay.

The source has `m_DoRagdoll=false` and zero rigidbodies. The reviewed `snowman_damage` and `snowman_deathDirect` sequences are animated stacked-part separation and fall-apart, not ragdoll behavior. The exact head remains active, enabled, reported visible and identity-stable through all 360 retained telemetry frames. Smoke, airborne travel, camera framing and the loot overlay obscure or move it offscreen in later images, so the archive does not establish a readable corpse, later corpse lifetime, cleanup causality or final disposal.

Two guarded native Collect actions reach strict Ready at level 0, room 2. Root review covers 17 selected original PNGs; 343 other captured PNGs remain preserved by hash but were not individually reviewed. Native effects, combat UI, hero overlap, smoke and motion blur limit fine art inspection. V1, V2 and the cross-topology V3 supplement remain historical records rather than canonical evidence for the unresolved companion routes.

## Canonical scarf route: V5

The [V5 scarf archive](live-validation-v5-scarf/README.md) is the canonical exact-source record for topology `1322fec3354db550`, direct-enemy source `snowmanB`, renderer `SnowMan_Geo/enSnowmanScarf` and source renderer 121639. The isolated process observed native CEL scale 1.0. The same owner bound the authored hat, head, base and middle body, but V5 grants no route credit to those four companion topologies.

Pass, ordinary attack and kill-fixture captures each retain 120 frames. The ordinary zero-focus attack used the native blunt smith hammer without a skill-cap or damage fixture and produced `Damaged` for 13 HP, from 58 to 45. The profile's `minimumBaseHealth: 64` and disposable party maximum-HP fortification make the trial unsuitable as a balance sample. Explicit `KillSingle` produced recorded 1000 damage, HP 45 to 0 and native `Death`; this is fixture death evidence, not ordinary lethal gameplay.

The burgundy neck wrap and long front tail remain aligned in settled idle, articulate with the torso through native `snowman_attack3`, and recover coherently after `snowman_damage`. `m_DoRagdoll=false` and zero rigidbodies make the later `snowman_deathDirect` separation an animated fall-apart rather than ragdoll behavior. The exact scarf stays active, enabled, reported visible and identity-stable through all 360 telemetry frames, while smoke, airborne travel, camera framing and loot obscure or move it offscreen in later images. The archive therefore does not establish a readable corpse, later corpse lifetime, cleanup causality or final disposal.

Two guarded native Collect actions reach strict Ready at level 0, room 2. Root review covers 17 selected original PNGs and leaves 343 hashed captures unreviewed. Native effects, combat UI, hero overlap, smoke and motion blur limit fine art inspection. At the V5 boundary, hat, base and middle body remained unresolved; V6 below covers the base separately, and V4 covers the head.

## Canonical base route: V6

The [V6 base archive](live-validation-v6-base/README.md) is the canonical exact-source record for topology `8c73c366b064726a`, direct-enemy source `snowmanB`, renderer `SnowMan_Geo/enSnowmanBase` and source renderer 121557. The isolated process observed native CEL scale 1.0. The same owner bound the authored hat, head, scarf and middle body, but V6 grants no route credit to those companion topologies.

Pass, ordinary attack and kill-fixture captures each retain 120 frames. The ordinary zero-focus attack used the native blunt smith hammer without a skill-cap or damage fixture and produced `Damaged` for 10 HP, from 58 to 48. The profile's `minimumBaseHealth: 64` and disposable party maximum-HP fortification make the trial unsuitable as a balance sample. Explicit `KillSingle` produced recorded 1000 damage, HP 48 to 0 and native `Death`; this is fixture death evidence, not ordinary lethal gameplay.

The broad faceted glacier pedestal uses its actual native weighted root `Root_M`. It stays coherent at the target footprint through settled idle, `snowman_attack2`, the ordinary `snowman_damage` response and recovery, and later `snowman_attack3`. With `m_DoRagdoll=false` and zero rigidbodies, the persistent base and departing upper stack in `snowman_deathDirect` are animated behavior rather than ragdoll. The exact base stays active, enabled, reported visible and identity-stable through all 360 telemetry frames. Selected pre-loot frames keep it readable, but the record does not establish exact terrain contact, collision, later corpse lifetime, cleanup causality or final disposal.

One guarded native Collect action reaches strict Ready at level 0, room 2. Root review covers 17 selected original PNGs and leaves 343 hashed captures unreviewed. Native effects, combat UI, hero overlap, smoke and motion blur limit fine art inspection. At the V6 boundary, hat and middle body remained unresolved; V7 below covers the hat, while V4 and V5 cover head and scarf separately.

## Canonical hat route: V7

The [V7 hat archive](live-validation-v7-hat/README.md) is the canonical exact-source record for topology `963e53f60643b796`, direct-enemy source `snowmanB`, renderer `SnowMan_Geo/enSnowmanHat` and source renderer 121405. The isolated process observed native CEL scale 1.0. The same owner bound the authored head, base, scarf and middle body, but V7 grants no route credit to those companion topologies.

Pass, ordinary attack and kill-fixture captures each retain 120 frames. The ordinary zero-focus attack used the native blunt smith hammer without a skill cap or damage fixture and produced `Damaged` for 13 HP, from 58 to 45. The profile's `minimumBaseHealth: 64` and disposable party maximum-HP fortification make the trial unsuitable as a balance sample. Explicit `KillSingle` produced recorded 1000 damage, HP 45 to 0 and native `Death`; this is fixture death evidence, not ordinary lethal gameplay.

The three-point crown remains centered around the head in settled idle, follows it through `snowman_attack2`, preserves its shape during the ordinary `snowman_damage` response and recovery, and travels with the upper assembly in later attacks and `snowman_deathDirect`. With `m_DoRagdoll=false` and zero rigidbodies, that separation is animated behavior rather than ragdoll. The exact hat stays active, enabled, reported visible and identity-stable through all 360 telemetry frames. Later smoke, camera framing, foreground overlap and loot obscure the crown, so the record does not establish exact collision, a readable corpse, later corpse lifetime, cleanup causality or final disposal.

Two guarded native Collect actions reach strict Ready at level 0, room 2. Root review covers 17 selected original PNGs and leaves 343 hashed captures unreviewed. Native effects, combat UI, hero overlap, smoke, motion blur and the loot overlay limit fine art inspection. At the V7 boundary, the middle body was the only unresolved Rimecrown topology; V8 below closes it, while V4 through V6 cover head, scarf and base.

## Canonical middle-body route: V8

The [V8 middle-body archive](live-validation-v8-middle-body/README.md) is the canonical exact-source record for topology `a158ab62f9dd430f`, direct-enemy source `snowmanB`, renderer `SnowMan_Geo/enSnowmanmiddleBody` and source renderer 121697. The isolated process observed native CEL scale 1.0. The same owner bound the authored hat, head, base and scarf, but V8 grants no route credit to those companion topologies; they already have separate canonical records.

Pass, ordinary attack and kill-fixture captures each retain 120 frames. The ordinary zero-focus attack used the native blunt smith hammer without a skill cap or damage fixture and produced `Damaged` for 8 HP, from 58 to 50. The profile's `minimumBaseHealth: 64` and disposable party maximum-HP fortification make the trial unsuitable as a balance sample. Explicit `KillSingle` produced recorded 1000 damage, HP 50 to 0 and native `Death`; this is fixture death evidence, not ordinary lethal gameplay.

The faceted torso, articulated arms and crystal hands remain coherent in settled idle, pose through `snowman_attack1`, separate without a long deformation during the ordinary `snowman_damage` response, recover, and articulate through later `snowman_attack3` and `snowman_attack1`. They travel with the upper assembly during `snowman_deathDirect`. With `m_DoRagdoll=false` and zero rigidbodies, that separation is animated behavior rather than ragdoll. The exact middle body stays active, enabled, reported visible and identity-stable through all 360 telemetry frames. Later smoke, distance, camera framing, foreground overlap and loot obscure it, so the record does not establish exact collision, a readable corpse, later corpse lifetime, cleanup causality or final disposal.

Two guarded native Collect actions reach strict Ready at level 0, room 2. Root review covers 17 selected original PNGs and leaves 343 hashed captures unreviewed. Native effects, combat UI, hero overlap, smoke, distance, motion blur and the loot overlay limit fine art inspection. Together, V4 through V8 provide separate canonical evidence for all five Rimecrown renderer topologies.
