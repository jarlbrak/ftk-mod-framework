# Moonreed Sylph

Original woodland fairy for exact fairyA `enFairy01` renderer121395. An ivory mask, dark eyes, teal tunic, lavender legs, copper flowing crest and four opaque patterned wings provide the silhouette. All geometry and palette pixels are authored here; native data supplies binding landmarks only. Native sparkle, weapon and spell effects remain game-owned.

![Hero](hero.png)
![Side](side.png)

`runtime-profile.json` uses key `ftkmf_modeltest_moonreed`, fairyA, exact combat profile `b63a0354dfdcab92ca25e71ac4960e0bb459863e30677d401965b5db00716ab7`, path `enFairy01`, `moonreed.glb` and `moonreed_basecolor.png`, minimum health64 and visualScale1. Native CEL scale is1. The canonical V2 archive below validates this exact profile and source assignment; it is still an isolated test profile rather than current game deployment.

Full66 palette/IBMs remain exact, including the native zero-weight RigSpine1, RigSpine6 and both arm collarbones. The original tunic follows the native spine; sleeves/legs/hair and four wing chains deform separately. Small blended shoulder/wing collars overlap attachment regions. Fingers follow native thumb/index/rest chains. The ten-joint crest is a continuous original surface rather than a rigid span across animated hair joints.

Anatomical up is mesh+Y and forward+Z. The native head-attached PortraitCam transforms to approximately(0,1.14543,1.32) in bind mesh coordinates, versus head(0,1.10543,0), supporting the authored face direction. This is a source orientation check, not an actual posed portrait pixel result. CameraRoot/EncounterCam exists but no alternate camera is selected. The source binding scene preserves native transforms; studio is a separate presentation.

First native matFairyA107 is white with zero emission. Second matFairyAWings108 has native pink tint and emission, which the one-material original does not preserve. Opaque ivory/lavender panels and indigo/copper veins use the first native slot; emission opt-out is false. Fresh canonical live inventory records Standard `matFairyA (Instance)` using `moonreed_basecolor.png` with emission disabled, zero emission color and no emission map. Portrait pixels and broader culling remain separate work.

## Rebuild and checks

From repository root, with ignored local reference121395 available:

```sh
scratch/model-venv/bin/python art-experiments/moonreed-sylph/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/moonreed-sylph/build_blender.py
scratch/model-venv/bin/python art-experiments/moonreed-sylph/verify_original_geometry.py
scratch/model-venv/bin/python art-experiments/moonreed-sylph/audit_native_poses.py --label pass
scratch/model-venv/bin/python art-experiments/moonreed-sylph/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/984eb2ce426948d1a10ecc5ca2bb82cc.json --label hit
scratch/model-venv/bin/python art-experiments/moonreed-sylph/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/4e53e1e68fac461d975a2586140792b1.json --label death
scratch/model-venv/bin/python art-experiments/moonreed-sylph/finalize_manifest.py
```

`moonreed.blend` is editable. It is saved and reopened before export to ignored `scratch/moonreed-roundtrip`; direct and independent reopened binary/skin validation are separate from artistic review. `original-geometry-proof.json` verifies source/palette reproduction when reference reads are restricted to bone names and inverse bind matrices. Native surface arrays are used only by separate validation/bounds checks, never the original authoring generator.

The rest surface fits the native surface bounding box. All120 frames of each native diagnostic capture are skinned offline; contact sheets show six samples. Root_M motion is normalized for deformation inspection and renderer-local travel bounds are reported separately. The death study's largest stretched torso edge is0.02646→0.03974 mesh units at32 (1.502×), not a detached long connector. These studies do not prove live camera, floor or culling fit.

The native diagnostic has animated fairy_die death, m_DoRagdoll false and Animator enabled. Two inactive weapon Break rigidbodies are equipment, not body ragdoll. The diagnostic's first death sample at normalized.141 misses the start. Original idle/attack/hit/death, palette, portrait, camera fit and native cleanup/progression remain live gates; native diagnostic coverage does not transfer automatic appearance acceptance to Moonreed or all fairy rows.

Parent reviewed the pinned hero/pass/death images and approved proportions for original live testing. Death sheets intentionally remove Root_M world rotation, so the figure remains upright there although the actual native diagnostic lies on the floor. This coordinate convention must not be interpreted as a death-pose mismatch or floor-fit proof.

## Original live V1

[Live validation](live-validation.json) records four complete120-frame captures. Parent reviewed six images: pass0/48 shows attached wings/face/hair/body and readable combat portraits; first attack30 visibly says DODGED withHP58 unchanged; the later hit30 shows ordinary5damage58→53; death40/60 shows a coherent prone corpse. The first wrapper's stopped result is preserved separately from these later successful observations.

Death uses explicit KillSingle, with m_DoRagdoll false and Animator enabled throughout. Pale wings receive warm yellow illumination; native pink spell, hero and sparkles obscure some frames. The postReady inventory had no matching Moonreed renderer, so this archive does not claim independent runtime material-property verification or infer emission from appearance. One Collect reaches strict Ready0/2.

This is scoped selected body/portrait/hit/death appearance acceptance for one exact fairyA original, not every animation/camera condition, normal lethal damage, all fairy rows or special fountain routes. Four full videos, six reviewed PNGs, summaries, source asset/binary/action pins and the original stopped wrapper are retained in `live-v1`. No source geometry or profile changes were made for this live trial.

## Canonical exact-source V2

The immutable [canonical V2 archive](live-validation-v2-canonical/README.md) repeats the current queue route for exact `fairyA / enFairy01 / renderer 121395`. Four complete 120-frame captures keep renderer instance -245544, owner 369188, CEL `enFairy(Clone)`, mesh `ftkmf_glb_moonreed.glb`, Root_M and joint signature `81c4105f144d33460cbc6c25586870c23a8df7398bc26d567845104d7b6498c7` stable while active, enabled and reported visible in all 480 frames.

The pass capture records native `AttackProf` and `fairy_attackProf` for 4 hero damage. The first ordinary zero-focus unmodified hammer attempt is preserved as native `Dodge` and `fairy_dodge` with enemy HP58 unchanged. The second attempt records ordinary 3 damage from HP58 to HP55, native `Damaged` and `fairy_damaged`. Later sampled enemy attacks include two 4-damage results and one native dodge. The separate explicit `KillSingle` fixture changes HP55 to HP0 for recorded 1000 damage and native `Death`; it is not ordinary lethal evidence.

Native `m_DoRagdoll` is false. The animator stays enabled while `fairy_die` persists from frame26 through119. Two recorded nonkinematic rigid bodies are inactive break props and receive no ragdoll credit. Thirty exact originals show the custom four-wing body coherent through hover, dodge, attacks, hit reaction, recovery and animated death. One guarded Collect reaches strict Ready0/2. The archive verifier passes 23 metadata mappings, 481 source-image pins, 480 capture-image pins, two assets, 30 selected PNGs, the root review and four videos.

This evidence is limited to the exact fairyA source and renderer. Native effects, hero overlap, small presentation, combat and loot UI, and late depth blur limit stated frames. It does not establish ordinary lethal behavior, every animation, collision, general corpse lifetime, cleanup, portraits, broader culling, sibling fairy sources, long-session resource lifetime, campaign completion or finished-art acceptance.
