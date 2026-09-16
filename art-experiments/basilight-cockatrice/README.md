# Basilight Cockatrice

Original low-poly jade bird-dragon for native `cockatriceC`: hooked ivory beak, amber crest, muted red flight feathers and wattles, articulated wings and a long scaled tail. The editable mesh is authored from scratch; no native surface or texture is packaged.

`runtime-profile.json` selects `enChicken` on exact renderer121484, 50-joint palette, controller5949 and combat profile `5f5dc029684fca74cada33fdde5e250b536ed38335a869d1e637db9990226cb1`. Representative121328 has exactly equal ordered bone names and inverse binds. Native CEL scale0.55 is preserved with visualScale1.0; minimumBaseHealth64 is a test minimum, not a balanced gameplay design.

`basilight.blend` contains the editable original mesh, all50 vertex groups and native rest armature. `basilight-studio.blend` adds presentation lighting and camera. Export the tagged mesh from the former. `build_geometry.py` generates original parameterized surfaces; `original-geometry-proof.json` demonstrates identical regeneration with native reads restricted to bone names and inverse binds. Zero-weight palette entries remain intact. Source mesh is Y-up, front+Z; Blender uses `(x,-z,y)`. Palette UV is top-origin for the FTK loader.

The trunk, neck, tail and leg strips follow native local chains. Neck transitions and knees/toes include blended weights; wing feathers and head crest attach to their specific native bones. Small overlapping shoulder volumes cover the wing root. No invented hierarchy connector spans independent animated parts. The native rest bounds contain the authored mesh; that is not proof of an animated culling envelope, and the runtime must preserve native animation bounds.

Native material169 `matMasterJungle` has white emission RGB1.646 with `_EMISSION`; this profile explicitly disables native emission to preserve the opaque jade/ivory palette. Native material slots, effects, death feathers/pink glow and controller remain game-owned. Actual replacement-material properties, combat lighting and portrait readability still require live validation. Both source PortraitCam and EncounterCam exist; this version retains the default head-attached portrait marker.

The [native calibration evidence](../../docs/evidence/cockatricec-diagnostic-v1/validation.json) covers three120-frame recordings and six root-reviewed poses, normal5 damage72→67, explicit fixture death, and two collects to strict Ready0/3. Native Animator stayed enabled and m_DoRagdoll false throughout death. It does not validate this original mesh, ordinary lethal damage, every clip, native Cockatrice A controller5948, boss bind variants or resource121693.

`audit_native_poses.py` skins only the original geometry through all120 frames of each recorded native capture. The three depth-buffer sheets show selected poses with Root_M motion removed so joint deformation is easy to inspect. Consequently the death sheet can appear upright while the actual native corpse lies on the floor. Separate numerical travel bounds retain renderer-local movement; neither sheet reproduces live culling, materials, terrain or portrait projection. Read each audit's exact worst-edge piece, frame and lengths; do not treat a maximum ratio as an unexplained quality verdict.

Rebuild from repository root (local extracted references stay ignored):

```sh
scratch/model-venv/bin/python art-experiments/basilight-cockatrice/build_geometry.py
scratch/model-venv/bin/python art-experiments/basilight-cockatrice/verify_target_binding.py
scratch/model-venv/bin/python art-experiments/basilight-cockatrice/verify_original_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender -b --python art-experiments/basilight-cockatrice/build_blender.py
scratch/model-venv/bin/python art-experiments/basilight-cockatrice/audit_native_poses.py --label pass
scratch/model-venv/bin/python art-experiments/basilight-cockatrice/audit_native_poses.py --label hit --capture scratch/mirewarden-game/model-test-output/abbd367333e341478f033709069ea2ee.json
scratch/model-venv/bin/python art-experiments/basilight-cockatrice/audit_native_poses.py --label death --capture scratch/mirewarden-game/model-test-output/c60e92c48dcb48e68af9dbd81440195f.json
scratch/model-venv/bin/python art-experiments/basilight-cockatrice/finalize_manifest.py
```

The saved/reopened Blender export is independently validated in ignored `scratch/basilight-roundtrip/`. The initial studio approval pinned exact reviewed images and authorized the live test recorded below.

Root approved the pinned hero/pass/death images for the next live test. The torso’s worst attack edge at frame57 grows from0.6424625381 to1.2836276334 mesh units (1.99798×); the tail’s worst death edge at frame32 grows from0.3908768112 to0.7517071287 (1.92313×). The extra pass-worst sheet covers frames54–59 around that torso transition and shows the original neck/body remaining connected in the author review. This is selected-pose evidence, not a blanket full-pose art PASS.

## Scoped live trial

[Live validation](live-validation.json) records three complete 120-frame captures
on exact native `cockatriceC`: enemy action, a normal 10-point player hit (72 to
62 HP), and explicit KillSingle death. Two native Collect actions returned the
game to strict Ready at level 0, room 3. The recorded death includes
`Cockatrice_DeathHuge`; all 120 frames retained the active renderer and enabled
Animator.

Five reviewed PNGs show the jade body, ivory beak, amber crest and red wattles,
custom head in both portrait surfaces, connected hit recoil and backward fallen
body with curled tail. Native feather effects, hero occlusion and camera blur
limit fine surface review. The requested emission opt-out produces a colored
body in these views, but no independent material-property measurement is claimed.
Raw captures, journals, three verified videos and the separate visual verdict
are preserved in `live-v1/`. Other cockatrice controllers, bind variants and
resource rigs remain separate validation cases.

## Fresh live V2

[The V2 archive](live-validation-v2/README.md) records a fresh catalog-411 run in session `db97671239c04b4e899fb14569fd72e7`. The exact cockatriceC `enChicken` renderer (121484) bound to owner `369188` with the expected 50-joint signature at public visual scale `1.0` (captured native CEL scale `0.55`). Selected idle and attack views retain the jade body, crest, beak, wing panels, legs and claws. The runtime material uses the authored basecolor, has emission disabled/black, and no emission map.

Pass, ordinary attack and explicit `KillSingle` captures are complete 120-frame recordings. The ordinary attack changes HP `72→62` with `cheat=None` and no focus. The fixture reaches the native Victory loot surface; one guarded native Collect is accepted and strict Ready is observed at level 0 room 2. Native UI/effects and the victory overlay limit fine crest/feather detail, full death deformation, settled-ragdoll, culling, portrait/resource lifetime and finished-art acceptance.

## Canonical exact-source validation V3

[The canonical V3 archive](live-validation-v3/README.md) pins fresh session
`cacb6f33c8324199b1ad09f921b5f6ae` against the exact `cockatriceC / enChicken /
121484` assignment and expected 50-bone signature. One owner completes three
120-frame captures: settled idle plus native `AttackProf1` and `AttackProf`, an
ordinary no-focus HP 72 to 62 hit with native `Damaged` and
`Cockatrice_HitSmall`, and an explicit `KillSingle` fixture with native `Death`
and `Cockatrice_DeathHuge`. One guarded Collect reaches strict Ready at level 0,
room 2.

Eighteen reviewed original PNGs accept the hooked beak, amber crest, jade body,
articulated wing panels, long legs, claws, tail, two native attack variants, hit
recovery, and coherent recoil, backward fall, and prone corpse through the
sampled loot handoff. Native feathers, the foreground hero, and bright effects
obscure the impact frames. The native death keeps the Animator enabled with
`m_DoRagdoll=false`, so this is an animator-driven death rather than a physics
ragdoll. The explicit fixture is not ordinary lethal evidence. Other native
attacks, portrait, collision, extended culling, long-session lifetime, later
corpse lifetime, full campaign completion, and final art approval remain outside
this archive.

Rebuild this immutable supplement through the generic reviewed-case workflow:

```sh
python3 tools/ai-model-pipeline/archive_model_validation_case.py \
  art-experiments/basilight-cockatrice/live-validation-v3-plan.json
python3 tools/ai-model-pipeline/verify_model_validation_archive.py \
  art-experiments/basilight-cockatrice/live-validation-v3 \
  --check-video-metadata
```
