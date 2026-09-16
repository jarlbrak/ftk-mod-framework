# Bramblecoil Viper

Bramblecoil Viper is an original low-poly jungle serpent for the exact
`snakeJungleC` / `enJungleSnakeC` renderer at source ID `121424`. It uses an
articulated jade body, coral-and-amber bramble thorns, gold scale bands, paired
rainleaf hood panels, a faceted viper head, luminous eyes, a separate ivory
jaw, and a seven-bone orchid tongue. No native mesh positions, topology, UVs,
normals, skin weights, material pixels, or animation curves are used.

![Hero studio view](hero.png)
![Side studio view](side.png)
![Portrait studio view](portrait.png)

## Binding design

`build_geometry.py` consumes only the exact 44 palette bone names and inverse
bind matrices. The tapered body follows the real tail-to-head sequence
`BackRib_End` through `BackRib_01`, `Root_M`, and `FrontRib_01` through
`FrontRib_11`; each ring is bound to the matching joint. The hood crosses from
the upper front-rib chain to `Head`, while the lower jaw, tongue, eyes, cheek
thorns, and crown thorn use their actual head, jaw, tongue, and `joint13`
bindings. This makes idle, bite, venom, hit, and DeathLight tests meaningful
for more than a root-bound static shell.

The one replacement is `enJungleSnakeC`. The profile preserves the exact
`snakeController` combat fingerprint, applies no extra visual scale to the
native root scale of `1.0`, uses the native head `PortraitCam` marker, and
turns off inherited `matMasterJungle` emission on its transaction-owned
material instance. Its explicit `fallOffPolicy: "preserve-custom-body"` asks
the framework to retain that transaction-owned renderer only when the native
fall-off component targets it with a live explicit lease. Native cameras,
rigidbodies, joints, and fall-off pieces remain game-owned and do not appear in
the GLB.

Jungle C has a nonstandard normal-death hand-off. With no policy, its native
`FallOffLimb` path hides `enJungleSnakeC` and activates 13 native
`quetzalPiece_*` rigid mesh fragments. Bramblecoil does not copy or replace
those fragments. The frozen `live-validation-v1` archive now records a paired
same-binary `KillSingle` fixture: the opted body stayed active, visible, and
enabled through `Snake_DeathBig` while 26 native ragdoll bodies became dynamic;
the policy-omitted control hid the exact custom renderer by capture frame 25
and activated the native 13-piece fall-off body. `KillSingle` remains an
explicit fixture, not ordinary lethal-damage evidence.

A raw `Animator.Play` capture of `Base Layer.DEATHLIGHT` / `Snake_Death` can
fire `DeathFallOff` without setting the CEL trigger state. That endpoint is
therefore not semantic DeathLight evidence. The archive instead includes a
fresh `EnemyDummy.PlayAnim(DeathLight)` fixture that establishes the native CEL
`DeathLight` branch and keeps the exact custom body visible across all 24
fixed-step frames. The public opt-in only applies after the explicit runtime
lease owns the exact `FallOffLimb` renderer; all other fall-off renderers retain
native behavior.

The 46-bone Desert Snake family is a distinct rig and cannot consume this
44-bone asset. Its `Tongue_08` and `Tongue_End` palette entries alone make an
exact Desert authoring package necessary.

## Rebuild

Run from the repository root after the ignored Jungle C skeleton extraction is
available:

```sh
scratch/model-venv/bin/python art-experiments/bramblecoil-jungle-snake/build_geometry.py
scratch/model-venv/bin/python art-experiments/bramblecoil-jungle-snake/verify_original_geometry.py
scratch/model-venv/bin/python art-experiments/bramblecoil-jungle-snake/verify_target_binding.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/bramblecoil-jungle-snake/build_blender.py
scratch/model-venv/bin/python art-experiments/bramblecoil-jungle-snake/finalize_manifest.py
```

The direct FTK GLB and independent Blender bridge export each pass the 44-bone
binary and inverse-bind contract. `original-geometry-proof.json` reruns the
authoring generator with native reads restricted to `bone_names` and
`bindposes`; `target-binding-proof.json` pins the target palette and bind
matrices. `studio-review.json` is a static source decision only.

## Live validation v1

[`live-validation-v1/validation.json`](live-validation-v1/validation.json)
pins the successful fresh isolated runs, deployed catalog and DLLs, original
GLB/PNG, raw captures, source screenshots, and selected-frame review. The
semantic DeathLight fixture records the exact owner, renderer, 44-bone
signature, transaction-owned material with emission off, and CEL trigger.
The paired policy fixture records the normal native death hand-off under both
policy states. A separate opted run then used one actual `VoteButton.OnLeftClick(Collect)`
call and reached strict native Ready at level 0 / room 2.

Reproduce the narrow policy exercise against a recorded deployment receipt:

```sh
python3 art-experiments/bramblecoil-jungle-snake/run_deathlight_trigger_v1.py \
  --deployment-receipt scratch/bramblecoil-policy-control-idle-gate-419/receipt.json
python3 art-experiments/bramblecoil-jungle-snake/run_falloff_fixture_v2.py \
  --variant opted --deployment-receipt scratch/bramblecoil-policy-control-idle-gate-419/receipt.json
python3 art-experiments/bramblecoil-jungle-snake/run_falloff_fixture_v2.py \
  --variant native-control --deployment-receipt scratch/bramblecoil-policy-control-idle-gate-419/receipt.json
python3 art-experiments/bramblecoil-jungle-snake/verify_falloff_fixture_v2.py \
  --opted scratch/bramblecoil-falloff-opted-v2-*.json \
  --native-control scratch/bramblecoil-falloff-native-control-v2-*.json \
  --output /tmp/bramblecoil-falloff-policy-comparison.json
python3 art-experiments/bramblecoil-jungle-snake/run_falloff_cleanup_v1.py \
  --deployment-receipt scratch/bramblecoil-policy-control-idle-gate-419/receipt.json
```

The existing ordinary and focused attack diagnostics each left the exact
86-HP target unchanged across eight accepted attacks. They remain
`no_hp_loss_unclassified`; neither the fixture nor these records proves an
ordinary hit or ordinary lethal death. Portrait pixels, all-angle culling,
material lifetime, corpse presentation quality, and full campaign progression
remain separate gates.

## Live validation v2 canonical

[`live-validation-v2-canonical/validation.json`](live-validation-v2-canonical/validation.json)
supersedes the unresolved V1 combat boundary with one fresh, exact-source
campaign. Four complete 120-frame captures preserve settled `Snake_Idle`, the
native `Snake_BiteAttack`, an ordinary native Block at HP 86→86, a later
fixture-assisted ordinary hit at HP 86→85 with `Snake_HitSmall` and recovery,
and explicit-fixture `Snake_DeathBig`. The exact custom renderer remains active,
enabled, and reported visible in every retained frame.

The explicit `KillSingle` changes HP 85→0. The animator disables at frame 28,
and the 13 active skeleton rigidbodies become dynamic at that frame. Measured
motion continues through frame 58 and is zero from frames 59 through 119. The
coiled corpse remains visible through the sampled loot frame 119. That sample
does not establish its later lifetime or cleanup. One actual guarded Collect
reaches strict Ready at level 0 / room 2.

This route uses two disposable hero-side fixtures after repeated native Blocks.
It caps the equipped weapon's actual skill at FTK's native maximum, then raises
the native weapon maximum above the decompiled Jungle Snake C armor boundary.
The receipt records physical augmentation 0→23 and maximum damage 12→35. Native
combat awards XP 0→110 and level 0→2; restoration preserves that progression,
removes only the 23-point augmentation, and verifies the level-adjusted maximum
of 12. The HP 86→85 action remains an ordinary zero-focus native action, but its
balance is unrepresentative. The explicit kill is a fixture and is not ordinary
lethal evidence.

Human review covers 23 exact original PNGs across idle, bite, Block, hit,
recovery, death, ragdoll transition, moving and settled corpse, and final loot.
The fixed camera and health UI partly crop or cover the tall head, while native
effects, the hero, UI, and depth blur obscure some poses. Collision, portraits,
all-angle culling, sibling sources, resource lifetime, every clip, later corpse
cleanup, and finished-art acceptance remain open.
