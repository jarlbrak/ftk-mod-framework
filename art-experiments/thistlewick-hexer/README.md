# Thistlewick Hexer - original body and canonical live V3 route

A compact woodland trickster body for the exact36-bone `scourgeG` chassis: moss-green coat panels, charcoal underlayers, restrained copper cuffs, a pale carved-wood imp face with a pointed nose, dark inset eyes and long articulated fingers. The shape should read as a nimble, troublesome forest hexer at FTK combat distance. Broad coat and face masses take priority over small ornament. No new combat mechanics or balance claims accompany this visual replacement.

The active native rigid `enLuckysHat` stays on Hair_M. This example replaces the body only: no copied native hat, duplicate headwear, hidden accessory or automatic weapon replacement. Keep the original head top modest so the retained hat seats cleanly. Native weapon, sounds, effects and scourge progression stay outside the original-body scope.

Exact calibration selection already exists in catalog398: `ftkmf_modeltest_probe_scourgeg`, base `scourgeG`, renderer121222 at CEL-relative `enScourgeLeprechaun`, native scale1, full36-joint palette. Combat profile `7b38982bd4cc216ce1f587e9235d4fb740c40c26a3bb2e73f7ad30c5a71beccf`; actual native weapon/prefab controller5934 `LeprechaunController`. Ordinary `leprechaunA/B` use another topology and are not substitutes. The source row is Fergus, a scourge; a controlled cloned-row battle does not validate global haunt progression.

`thistlewick-bind-setup.blend` contains the exact rest armature and binding metadata, with no creature mesh or native surface guide. It remains the reusable authoring setup. The original body now lives in `thistlewick.blend` and `thistlewick.source.json`; the fresh V2 trial below binds the exported candidate in an additive catalog-411 encounter. Recorded native pass, dodge, critical-hit and death poses inform fitting. The V2 runtime verifies body material readback and selected live views; native hat clearance, culling and portrait/resource lifetime remain bounded.

Source metadata establishes a single body material203, white color, zero emission and1× texture scale. Only a head-attached PortraitCam was located. Native prefab has no Rigidbody, CharacterJoint, FallOffLimb or scrolling component; the initial source typetree did not resolve CEL ragdoll flags. The later runtime death records m_DoRagdollfalse/Animator enabled throughout, with a separate kinematic weapon body at0–24 and no rigidbodies25–119. Heavy/light imp death clips have different events and generic handler guards. Exact source findings and native reference stay in ignored `scratch/scourge-leprechaun-topology-analysis`.

Recreate the empty setup after local121222 extraction:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/thistlewick-hexer/create_bind_setup.py
```

Remaining art gates: full original-row/portrait/hat readability, culling and cleanup limits, and finished-art acceptance. Full-palette, original-only, direct/reopen, bind-bounds and pass/death offline checks are recorded below. The canonical V3 route record is archived in [live-validation-v3](live-validation-v3/README.md); the earlier three-process evidence remains immutable in [live-validation-v2](live-validation-v2/README.md).

## Canonical live trial V3

The exact `scourgeG / enScourgeLeprechaun / renderer 121222` route now has one
machine-readable record for every required source-specific core gate. A fresh
complete pass capture records `cidle_impUnarmed`, native
`attackProf_leprechaun`, upright recovery, and robbery/flee removal while the
enemy remains at HP `58`. The proficiency deals `20` hero damage and steals
`11` gold before the custom renderer becomes translucent and disables. This is
native flee behavior, not death.

The retained ordinary no-focus capture separately records HP `58→48` and
`damageHeavy_imp`. The immutable V2 evidence supplies the ordinary HP `58→0`
result, explicit `KillSingle` `deathHeavy_imp` capture, two guarded native
Collect actions, and strict Ready at level `0` / room `2`. The fixture death is
separate from ordinary lethal-damage evidence. `m_DoRagdoll=false`, so it is an
animated death rather than a body ragdoll.

The [V3 archive](live-validation-v3/README.md) pins 240 incremental capture
images, 14 reviewed originals, four 120-frame videos, the exact current runner,
and the prior archive hash. It also records the reusable workflow rule: capture
an ordinary hit before a pass that can remove this enemy, preserve the native
flee as its own terminal outcome, and use a separate fresh process for an
explicit death fixture and loot/Ready progression. Do not automatically replay
a terminal self-removal action.

## Fresh live trial V2

The exact `scourgeG` / `enScourgeLeprechaun` chassis bound `thistlewick.glb` under owner `369188` at public visual scale `1.0`; the observed 36-bone signature is preserved in the archive. Three independent fresh processes keep the behavior boundaries explicit: native pass/flee removal, a separate ordinary attack resolving HP `58→0` without a cheat, and an explicit `KillSingle` death capture with `deathHeavy_imp` across all 120 frames. The kill process then accepted two guarded native Collect votes and reached strict Ready at level `0` / room `2` after one guarded Ready vote.

Six selected originals and presentation videos are in [live-validation-v2](live-validation-v2/README.md), with the lossless evidence and hashes in [validation.json](live-validation-v2/validation.json). The pass/removal capture is not classified as death; the ordinary lethal HP result is kept separate from death-animation acceptance. Native effects, foreground hero occlusion and the Victory surface limit fine surface, settled-body, culling, portrait/resource and finished-art claims.


## Original surfaces and current checks

`build_geometry.py` creates the pale wooden mask/nose/ears, inset eyes, moss coat and shoulder yokes, copper clasps/cuffs, long articulated fingers and charcoal boots from analytic original tubes and ellipsoids. Both shoulder yokes overlap the chest and sleeve and follow Chest/Scapula/Shoulder joints. The35 body-weighted bones include the separate left terminal toe; Hair_M remains unweighted exactly as in the native body and still controls the separate native hat. All36 palette entries and inverse binds are retained.

`verify_original_geometry.py` reruns generation with reference access restricted to names and bindposes, reproducing original geometry/palette/piece metadata exactly. Direct and saved/reopened export checks pass. `audit_surfaces.py` separately checks positive signed volume for each of31closed original pieces; this supplements normal agreement and does not claim a watertight union. Bind bounds fit native reference; animation/culling envelopes remain unverified.

The native pass sheet selects0/42/48/54/60/80 from all120recorded poses, removing Root_M world travel for anatomy. The report separately retains renderer-local travel bounds. Maximum measured edge stretch is a coat torso edge at51, .068526→.174771 (2.55043×). The underlying native exercise stopped after theft/flee-consistent removal; no death is inferred. This study does not include the retained native hat, Unity materials or camera/portrait framing.

```sh
scratch/model-venv/bin/python art-experiments/thistlewick-hexer/build_geometry.py
scratch/model-venv/bin/python art-experiments/thistlewick-hexer/verify_original_geometry.py
scratch/model-venv/bin/python art-experiments/thistlewick-hexer/audit_surfaces.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/thistlewick-hexer/build_blender.py
scratch/model-venv/bin/python art-experiments/thistlewick-hexer/audit_native_poses.py
scratch/model-venv/bin/python art-experiments/thistlewick-hexer/finalize_manifest.py
```


## Death-first offline fitting

The original mesh is unchanged after fitting all120frames of native death capture `9e85b32a18e64139a7886a1191c336f8`. Explicit KillSingle starts from58HP. `deathHeavy_imp` plays25–119; all120keep Animator enabled/m_DoRagdollfalse. A single active kinematic native weapon body appears0–24 under WEAPON_HOLDER;25–119have zero bodies. This is animated body death, not body ragdoll.

The normalized anatomy sheet remains upright by design. `native-death-travel-pose-study.png` retains Root_M rotation/travel and shows the body tumbling onto its back. Maximum torso edge stretch at33 is .068526→.124898 (1.82263×). Root reviewed the original hero, normalized anatomy sheet and travel sheet and judged the silhouette/collapse suitable to continue; native hat clearance still needs live testing. No geometry changed. Prior pass-only and intermediate manifest states are preserved in `offline-history/`. Critical-hit fitting is recorded below.

```sh
scratch/model-venv/bin/python art-experiments/thistlewick-hexer/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/9e85b32a18e64139a7886a1191c336f8.json --label death --steps 0 24 28 30 60 119
scratch/model-venv/bin/python art-experiments/thistlewick-hexer/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/9e85b32a18e64139a7886a1191c336f8.json --label death-travel --steps 0 28 30 33 60 119 --keep-root-motion
```

Native death-first diagnostic evidence is archived separately in [fergus-diagnostic-death-v1](../../docs/evidence/fergus-diagnostic-death-v1/validation.json); it is explicit KillSingle, not ordinary lethal damage or original-body live acceptance.

## Dodge and critical-hit fitting

The first fresh hit attempt was dodged, then followed by theft/flee-consistent removal. Its full capture and root review remain separately archived in [dodge/flee evidence](../../docs/evidence/fergus-diagnostic-dodge-flee-v1/validation.json); it is not a successful hit. The unchanged original mesh was fitted through all120 poses, with separate normalized and travel sheets.

A distinct encounter records an ordinary attack landing CRITICAL13 damage, HP58 to45, and `damageHeavy_imp`25–31. The all120-frame original-mesh audit and six-view critical-hit sheet show connected shoulders, coat, cuffs and fingers. Root approved these selected offline views for live testing. The maximum coat edge stretch is .068526 to.176791 (2.57990×) at114 during the later native attack, not the recoil. Subsequent explicit KillSingle45 to0 and two guarded Collect submissions reached strict Ready0/3 Trap1; [critical-hit/death evidence](../../docs/evidence/fergus-diagnostic-critical-hit-v1/validation.json) preserves both raw captures and reviews. Light-hit variation, ordinary lethal damage and global Fergus progression are not established.

```sh
scratch/model-venv/bin/python art-experiments/thistlewick-hexer/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/8fd5813208864d7385026500a46296c9.json --label dodge --steps 0 21 24 27 30 33
scratch/model-venv/bin/python art-experiments/thistlewick-hexer/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/fb869d7abbe84f5e833730e944a510c4.json --label critical-hit --steps 0 25 27 30 31 85
```
