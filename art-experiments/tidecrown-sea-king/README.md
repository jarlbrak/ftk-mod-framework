# Tidecrown Sovereign

Tidecrown Sovereign is an original low-poly sea monarch for the exact
`seaKing` / `enSeaKing` renderer at source ID `121357`. It uses a storm-teal
royal robe, closed tide mantle, oxidized armor, coral reliquary, sea-glass eyes,
an ivory beard, a head-bound coral-and-gold crown, and fully articulated
gauntlet fingers. No native mesh positions, topology, UVs, normals, or texture
pixels are used.

![Hero studio view](hero.png)
![Side studio view](side.png)
![Portrait studio view](portrait.png)

## Binding design

`build_geometry.py` consumes only the exact 60 palette bone names and inverse
bind matrices. The robe deliberately stays within the observed Sea King body
floor envelope: the native `Knee_R` and `Knee_L` transforms exist in the palette
but carry no native surface weight, so this design does not invent an unsupported
below-floor leg section. The articulated arms use actual scapula, shoulder,
elbow, wrist, and finger chains. The cloak panels are closed volumes tied to the
spine, and the crown uses `Head_M` because the high `Hair_M` landmark has no
native surface weight and sits above the skull.

The one replacement is `enSeaKing`. The native trident, shield and breakable
props, tentacles, ragdoll components, portrait cache, and controllers remain
game-owned external objects. The profile preserves the exact Sea King combat
fingerprint, requests public visual scale `1.0`, uses the head `PortraitCam`
marker, holds the health fixture at 720, and disables the inherited `matLoot`
emission. Source-only checks do not establish accessory intersection, portrait
framing, or animation safety; the bounded live record below names what was
observed separately.

## Rebuild

Run from the repository root after the ignored skeleton extraction is available:

```sh
scratch/model-venv/bin/python art-experiments/tidecrown-sea-king/build_geometry.py
scratch/model-venv/bin/python art-experiments/tidecrown-sea-king/verify_original_geometry.py
scratch/model-venv/bin/python art-experiments/tidecrown-sea-king/verify_target_binding.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/tidecrown-sea-king/build_blender.py
scratch/model-venv/bin/python art-experiments/tidecrown-sea-king/finalize_manifest.py
```

The direct FTK GLB and independent Blender bridge export each pass the 60-bone
binary and inverse-bind contract. `original-geometry-proof.json` reruns the
authoring generator with native reads restricted to `bone_names` and
`bindposes`; `target-binding-proof.json` pins the target palette and bind
matrices. `studio-review.json` is a static source decision only.

## Live validation and reproduction

Stage the frozen profile into a fresh directory, then deploy that directory to
the isolated game only after recording its receipt:

```sh
python3 art-experiments/tidecrown-sea-king/stage_runtime_profile.py \
  --game-root scratch/mirewarden-game \
  --output scratch/runtime-profile-413-tidecrown-sea-king
```

The frozen stage was hash-verified into the isolated game using
`scratch/deploy-tidecrown-sea-king-413.py`, then exercised by the reusable
coverage runner. The resulting [V1 archive](live-validation-v1/README.md)
records exact binding of the authored GLB and PNG to one `enSeaKing` owner with
the expected 60-bone signature, `seaKingController`, native root scale 1.0, and
disabled emission. The native trident stayed external to the GLB.

A separate fresh no-focus exercise retained eight complete 120-frame attacks
at 720→720, each explicitly classified `no_hp_loss_unclassified`. The fresh
focus supplement then reached a measured same-target hit at attempt four,
720→719, before an explicit `KillSingle` fixture. That fixture retained 91
frames, entered dynamic ragdoll at frame 28, and ended when the native owner
renderer became inactive. Guarded progression reached strict Ready at level 0,
room 2 without a Collect submission. The focus result is not ordinary no-focus
damage, and the fixture is not ordinary lethal damage or final-cleanup proof.

Eight reviewed frames and two 12 fps presentation videos show the custom body
and external trident together at live Sea King scale. The giant encounter
camera, player, UI and effects obscure the head/crown, full robe and lower body,
so portrait pixels, all-angle culling, weapon intersection, material lifetime,
tentacles, breakables, other 60-bone humanoids, and finished-art acceptance
remain separate gates.

Verify the retained V1 archive after a checkout or archive transfer with:

```sh
python3 art-experiments/tidecrown-sea-king/verify_live_validation_v1.py
```

The newer [V2 canonical archive](live-validation-v2-canonical/validation.json)
repeats the exact `seaKing / enSeaKing / 121357` route in session
`8b09f62e7e9a411199a46fa6466a0556`. The unmodified-skill predecessor remains a
preserved stopped record with eight exact native Blocks at 720 HP. V2 uses the
documented disposable native skill-cap fixture: it changes only the equipped
`bluntSmithHammer` toughness augmentation from 0.81 to 0.95 with zero spent
focus. The first two zero-focus attempts still Block; attempt three records
native `Damaged`, one damage and HP 720 to 719.

Four complete 120-frame captures preserve settled `SeaKing_Idle`, native
`SeaKing_Attack2` and `SeaKing_Attack3`, both `SeaKing_Block` responses,
`SeaKing_Damaged`, recovery and another native attack. The 91-frame explicit
fixture-death prefix enters ragdoll at frame 28: the animator disables and all
12 active bodies become dynamic. The exact renderer remains active and visible
through frame 89, then becomes inactive and not visible at frame 90 before the
next requested sample reports destruction. Twenty-three inspected originals
show coherent sampled motion and collapse. No Collect occurs; strict Ready is
observed at level 0 room 2 with one active button.

The skill fixture and party fortification make test balance unrepresentative,
and the fixture death is not ordinary lethal evidence. The camera crops the
head and crown, while the hero, UI, smoke, lightning, impact effects and depth
blur obscure parts of the robe and limbs. Cleanup causality, general corpse
lifetime, final disposal, portrait pixels, all-angle culling, external prop and
tentacle behavior, and finished-art acceptance remain outside V2.

Verify the canonical archive with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py \
  art-experiments/tidecrown-sea-king/live-validation-v2-canonical \
  --check-video-metadata
```
