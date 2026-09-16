# Duneshade Asp

Duneshade Asp is an original low-poly desert serpent for the exact
`snakeDesertA` / `enDesertSnakeA` renderer at source ID `121552`. It has a
moon-violet articulated body, lapis scarab plates, ember-orange sunspines,
paired turquoise-and-gold sunshield hoods, a faceted sandstone head, glassy
venom eyes, a limestone jaw, a forked nine-joint orchid tongue, and three
sunstone tail bells. No native mesh positions, topology, UVs, normals, skin
weights, material pixels, or animation curves are used.

![Hero studio view](hero.png)
![Side studio view](side.png)
![Portrait studio view](portrait.png)

## Binding design

`build_geometry.py` consumes only the exact 46 palette bone names and inverse
bind matrices. The duneshell follows the native tail-to-head sequence
`BackRib_End` through `BackRib_01`, `Root_M`, and `FrontRib_01` through
`FrontRib_11`; every body ring is bound to the matching joint. The hood spans
upper front ribs and `Head`; the jaw and nine-joint tongue use their actual
`Jaw`, `Jaw_End`, `Tongue_01` through `Tongue_08`, and `Tongue_End` joints.
That makes the idle, bite, venom, hit, and death animation checks meaningful
for animated anatomy instead of a root-bound shell.

The one replacement is `enDesertSnakeA`. The profile preserves the exact
`snakeDesertA` combat fingerprint, leaves public `visualScale` at `1.0`, and
therefore retains the chassis's native root scale of `0.65` without adding a
second authored scale. It uses the native `PortraitCam` marker and turns off
inherited emission on the transaction-owned material instance. Native cameras,
rigidbodies, and joints remain game-owned and do not appear in the GLB.

Desert A has the ordinary skeleton-ragdoll death path: source inspection found
no `FallOffLimb` component or external native fragment-mesh handoff. The
canonical `live-validation-v2-canonical` archive confirms the expected
custom-body behavior under an explicit `KillSingle` fixture. The exact body
stays active, visible, enabled, and identity-stable through all 120 death frames.
Its 13 native skeleton bodies stay kinematic through frame 27, then the animator
disables and all 13 become dynamic at frame 28. Measured motion begins at frame
29 and reaches zero by frame 50. One native Collect reaches strict Ready. The
death remains a fixture, not ordinary lethal-damage evidence.

The 44-bone Jungle C family is incompatible with this asset. Its palette lacks
`Tongue_08` and `Tongue_End`, so an exact Jungle C package such as Bramblecoil
must be authored and validated separately.

## Rebuild

Run from the repository root after the ignored Desert A skeleton extraction is
available:

```sh
scratch/model-venv/bin/python art-experiments/duneshade-desert-asp/build_geometry.py
scratch/model-venv/bin/python art-experiments/duneshade-desert-asp/verify_original_geometry.py
scratch/model-venv/bin/python art-experiments/duneshade-desert-asp/verify_target_binding.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/duneshade-desert-asp/build_blender.py
scratch/model-venv/bin/python art-experiments/duneshade-desert-asp/finalize_manifest.py
```

The direct FTK GLB and independent Blender bridge export each pass the 46-bone
binary and inverse-bind contract. `original-geometry-proof.json` reruns the
authoring generator with native reads limited to `bone_names` and `bindposes`;
`target-binding-proof.json` pins the target palette and bind matrices.
`studio-review.json` is a static source decision only.

## Live validation V2 canonical

[`live-validation-v2-canonical/validation.json`](live-validation-v2-canonical/validation.json)
pins the successful current-profile session, original GLB and PNG, exact
`snakeDesertA / enDesertSnakeA / 121552` source assignment, owner, renderer,
46-joint signature, native scale, material state, all 360 capture images, 17
reviewed originals, and three presentation videos. The complete captures cover
settled `Snake_Idle`, native `Snake_BiteAttack`, an ordinary zero-focus HP
`58 to 48` `Damaged` response with `Snake_HitSmall`, recovery, a later bite, and
the separate HP `48 to 0` `KillSingle` fixture with `Snake_DeathBig` followed by
the measured skeleton-ragdoll transition. One guarded Collect reaches strict
Ready at level 0, room 2.

The fixture does not prove ordinary lethal damage. Later loot UI hides most of
the grounded body, so exact collision, general corpse lifetime, cleanup cause,
and final disposal remain open. Portrait pixels, other cameras, all-angle
culling, every animation, sibling snake sources, long-session material and
resource behavior, full campaign progression, and finished-art acceptance are
also outside this archive.

## Historical live validation V1

[`live-validation-v1/validation.json`](live-validation-v1/validation.json)
preserves the earlier fresh catalog-417 session, deployment receipt, original
GLB/PNG, exact owner/renderer/bone signature, transaction-owned material,
native root scale, raw captures, native Collect responses, strict Ready state,
and selected-frame review. It records one native pass, one accepted ordinary
attack with same-target HP `58 → 48`, and an explicit `KillSingle` ragdoll
fixture. The ordinary attack measurement is deliberately limited to observed
target HP; it does not infer a combat cause. V1 remains historical evidence and
does not replace the current-profile V2 canonical route.

For a fresh repeat against a reviewed isolated deployment, run the bounded
native exercise with the exact profile and renderer:

```sh
python3 scratch/run-coverage-batch.py --run \
  --profile ftkmf_modeltest_duneshade_desert_asp:enDesertSnakeA \
  --attack-attempts 8
```

The fixture does not prove ordinary lethal damage. Portrait pixels, all-angle
culling, material lifetime, corpse presentation quality, and full campaign
progression remain separate gates.
