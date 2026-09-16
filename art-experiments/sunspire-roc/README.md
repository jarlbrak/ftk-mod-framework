# Sunspire Roc

Sunspire Roc is an original, stylized eagle built for the exact `rocA` / `enRoc01` skeleton at renderer `121238`. Its midnight flight feathers, copper breast, sun-gold crown, ivory hooked beak, articulated talons, and three-chain tail are authored as new low-poly surfaces. No native mesh positions, topology, UVs, normals, or texture pixels are used.

`build_geometry.py` consumes only the target palette's bone names and inverse bind matrices, writes a direct FTK GLB and a nearest-sampled authored palette, and validates the binary/skin contract. `verify_original_geometry.py` reruns the generator while refusing all native surface reads. `build_blender.py` creates an editable armature scene, independently bridge-exports it, and renders a separate studio scene.

![Hero studio view](hero.png)
![Side studio view](side.png)

## Rebuild

Run these commands from the repository root after the ignored skeleton extraction is available:

```sh
scratch/model-venv/bin/python art-experiments/sunspire-roc/build_geometry.py
scratch/model-venv/bin/python art-experiments/sunspire-roc/verify_original_geometry.py
scratch/model-venv/bin/python art-experiments/sunspire-roc/verify_target_binding.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/sunspire-roc/build_blender.py
```

The direct GLB, Blender re-export, and their validation records are separate checks. `runtime-profile.json` is the intended additive test catalog entry: it preserves the native `rocA` combat profile, replaces only `enRoc01`, opts out of native emission, leaves visual scale at `1.0`, and retains the unique head-attached native `PortraitCam` marker. The source root already scales to `0.9`; no GLB or public-scale compensation is applied.

The serialized `rocController` retains `IDLE`, attack, damage, dodge, death, and victory state motion. Its attack clips include forward/reverse native slide events, and `DeathFallOff` happens early in both death clips; source inspection found no Roc rigid accessories or ragdoll components. The model therefore follows every available wing, head/jaw, leg/talon, central-tail, and side-tail bone, but only live capture can establish how the actual cleanup boundary looks.

## Live evidence

Two independent fresh catalog-412 combat runs bind the same original GLB to the
exact `rocA` `enRoc01` renderer under native owner `369188`, with the expected
36-bone signature and preserved 0.9 root scale. The cloned `Standard` material
uses `sunspire-roc_basecolor.png`, with native emission disabled and black.

- [V1 focused supplement](live-validation-v1/README.md) records `Attack(focus)`
  changing the same target from HP 81 to 71, complete 120-frame pass/attack/
  explicit-death captures, two guarded native Collect actions, and strict Ready
  at level 0 room 2.
- [V2 ordinary supplement](live-validation-v2/README.md) independently records
  ordinary no-focus `Attack` changing the same target from HP 81 to 71, with
  the same complete capture and progression boundaries. Its nine reviewed
  frames show the head, beak, torso, wings, talons, and tail connected through
  pass, hit, native attack motion, and the visible death sequence.
- [V3 native row-portrait supplement](live-validation-v3/README.md) starts a
  separate fresh session, completes the ordinary combat boundary, then invokes
  one constructed native `uiEnemyEncounterPortrait.Initialize` call. Its exact
  custom 36-bone `enRoc01` clone uses `ftkmf_glb_sunspire-roc.glb` and returns a
  reviewed 328 by 280 portrait PNG; the recorded temporary clone, UI texture,
  and preview lease assets are released afterward.
- [V4 canonical exact-source archive](live-validation-v4/README.md) reconciles
  the historical and current catalog rows, re-reviews original combat and
  portrait pixels, pins all 360 combat images plus the portrait, and records
  the required source-specific binding, appearance, idle, attack, hit, death,
  fixture-action, ordinary-damage, and Ready fields in one verified archive.

The focused run remains deliberately separate from ordinary damage evidence.
V4 preserves the V2 and V3 provenance instead of rewriting either historical
record. The overall catalog hash changed as unrelated profiles were added, but
the selected Sunspire Roc profile, authored assets, source renderer, and motion
renderer are identical. This makes V4 the repeatable example for repairing an
older evidence layout without replaying complete live actions.

The V3 portrait fixture is a constructed native UI caller, not an opened
encounter menu or live combat HUD. The combined evidence establishes this one
exact assignment, not ordinary lethal damage, every portrait layout or cache
reuse path, full camera or culling coverage, settled corpse state, ragdoll,
final resource lifetime, every animation interval, completed art direction,
`rocB`, jungle Roc variants, or other bird rigs.
