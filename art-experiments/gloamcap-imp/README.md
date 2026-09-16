# Gloamcap Trickster

An original fungal creature on the tested resource Imp rig: broad plum mushroom cap, carved ochre face, pale eyes, long articulated hands and small root legs. The new surface is authored geometry, not a calibration probe. Studio and authored-model live acceptance are separate checks.

Exact target: native row `impA`, resource prefab `enbaseyimp`, CEL-relative renderer `enBaseyImp`, reference renderer **121117**, combat profile `780022dbce47a2f317f9224e6f5949ad8efad7393113b46477726ab3ad63d5f0`. `runtime-profile.json` preserves this resource override and supplies `minimumBaseHealth: 64`. The runtime assets are `gloamcap.glb` and `gloamcap_basecolor.png`.

Native +UnityZ forward was checked from the toe chain and the majority-weight Head_M surface envelope (Z -0.149 to +0.338). The authored face points +Z, equivalent to Blender -Y. The full 37-joint palette and exact inverse binds are retained. The cap and face follow Head_M; native Hair_M has no positive surface weights and receives no invented cap weights. Trunk rings follow consecutive spine joints, arms and legs blend at local joints, and fingers/toes use their corresponding native chains. Original geometry and palette are generated parametrically; no native surface or texture is included in the source or Blender scenes.

All original vertices are within the native bind-surface box. Native animation bounds remain unchanged; this is not proof of animated culling coverage. The broad cap, collar and long hands still require native motion and intersection review.

The source material audit records `grey50` (1076), no emission keyword, zero emission RGB and native tint RGB 0.5. The profile leaves `disableNativeEmission` false. The fresh catalog-411 run read back native Standard `grey50 (Instance)` with the authored basecolor, black emission and no emission map; native tint and lighting still limit exact palette comparison. `native-material-metadata.json` includes the source asset hash and scalar/color properties only, and `inspect_materials.py --assets /absolute/path/to/resources.assets` reproduces it.

Rebuild from repository root with the existing local references and dependencies:

```sh
scratch/model-venv/bin/python art-experiments/gloamcap-imp/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/gloamcap-imp/build_blender.py
scratch/model-venv/bin/python art-experiments/gloamcap-imp/finalize_manifest.py
```

The Blender script creates and saves the editable rigged scene, reopens the saved file, exports through the shared FTK bridge and independently validates it. Reopened exports remain in ignored `scratch/gloamcap-roundtrip`; their hashes are recorded in the manifest. Export `gloamcap.blend`; `gloamcap-studio.blend` adds presentation-only floor, lights and cameras. Regeneration resets manual acceptance to pending.

The prior resource Imp calibration documented native attack cycles, hit and physics-driven death plus Ready progression. Those observations establish a tested chassis, not acceptance of this new model. The V3 archive below supplies exact live resource assignment, native-scale readability, sampled idle, attacks, dodges, hit recovery, physics death, material appearance and Ready evidence for this model. Extended culling, cleanup causality, portraits, collision, other Imp clips and final art-direction approval remain outside that archive. No game, framework, helper or catalog changes are performed by the build scripts.

## First live original baseline

[Live evidence](live-validation.json) records exact resource Imp binding and
deployment hashes in session59a504bceafc4d3c99c206d07f0f8a59. Parent reviewed
attack0/50, both hit attempts30, and death40/60. The small native-size body and
face portraits are readable; native grey darkens the palette at50 but remains
legible, with no obvious deformation in selected views. Hero obscures the lower
body during the successful hit. This is scoped appearance/motion acceptance,
not unoccluded all-motion, culling or flawless-art acceptance.

- [Attack](live/attack.mp4)
- [First hit: dodged](live/dodged-hit.mp4),58HP unchanged.
- [Second hit: normal10](live/nonlethal-hit.mp4),58 to48.
- [Kill-fixture death](live/kill-fixture-death.mp4),48 to0.

All four captures complete120 unpaused frames. Native death disables Animator
at26 with11 rigidbodies and ragdoll enabled; deathHeavy_imp freezes at0.055
while the body collapses. Two guarded Collect actions reach strict Ready0/2.
Videos replay12fps, not real-time performance; both hit attempts are preserved.

## Fresh live validation V2

The [catalog-411 V2 archive](live-validation-v2/README.md) records a fresh session `f83c72d3f15f4be7a96cd409c9acc91e` using the exact `impA` chassis at visual scale `1.0`. The authored mesh bound to `enBaseyImp` under one native enemy owner with stable bone signature `455829c18e8514b56ff76611cb48b0962646dadc987d56f39aa8c889f8af3e70`; the stable catalog renderer is `121117` and the runtime instance was `-245388`.

Pass, ordinary attack and explicit `KillSingle` were independently recorded as complete 120-frame captures. The ordinary attack changed the same target from HP 58 to 48 with `cheat=None` and no focus. Two guarded native Collect calls were accepted and strict Ready was observed at level 0, room 2. The archive preserves the separate one-shot exercise attempts that stopped on random no-damage outcomes; the successful damage proof comes from the standalone recorder in the same fresh session.

The selected views show the cap, face, eyes, hands and root legs connected at native combat distance. Native targeting/UI/effects, foreground hero occlusion and victory depth blur limit fine surface, full death deformation, settled-ragdoll, culling, portrait/resource-lifetime and finished-art acceptance.

## Canonical exact-source live validation V3

The [V3 archive](live-validation-v3/README.md) records fresh session `db8b9c9d636e4d98a0670294d9f7010d` on the exact `impA` resource override `enbaseyimp`, CEL-relative renderer `enBaseyImp`, source renderer 121117 and native scale `1.0`. One observed owner bound `gloamcap.glb` through runtime renderer instance `-245554` with the expected 37-joint signature `455829c18e8514b56ff76611cb48b0962646dadc987d56f39aa8c889f8af3e70`.

All five captures complete 120 retained frames. The pass capture preserves settled `cidle_impUnarmed` and two `attack_impUnarmed` cycles. Two bounded ordinary no-focus trials stay at 58 HP and visibly exercise `dodge_imp`; the third changes the same target from 58 to 48 HP, visibly exercises `damageHeavy_imp`, returns to idle and records another native counterattack. A separate explicit `KillSingle` fixture at 48 HP drives `deathHeavy_imp` with `m_DoRagdoll=true`; all 11 recorded rigidbodies become nonkinematic at frame 26, the body reaches the floor by frame 45 and remains partly visible through frame 119. This fixture is not ordinary lethal-damage evidence, and no later corpse lifetime is claimed.

Twenty-nine exact original PNGs accept the custom mushroom silhouette through the sampled motions and physical fall. The exact renderer stays active, enabled, visible and identity-stable in all 600 retained frames. Fresh material inventory records native Standard `grey50 (Instance)` with the authored base-color texture, emission disabled, zero emission color and no emission map. One guarded native Collect action reaches strict Ready at level 0 room 2. UI, foreground overlap, effects, particles, loot overlay, depth blur and camera motion limit fine inspection. This record credits only the exact resource-prefab route; it does not credit a direct enemy row, sibling Imp sources or the Mirewarden troll route that shares the topology group.
