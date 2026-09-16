# Verdigrin Coffer

An original mimic chest with verdigris panels, wood walls, brass borders, ivory teeth, a hollow mouth and a wine-colored tongue. The chest base, lower mouth, hinged lid and tongue retain separate native articulation. The canonical V4 supplement records exact binding, selected appearance, idle, attack, hit, animated death and gameplay evidence; studio approval and the remaining scoped art and lifetime checks stay open.

The initial exact target is `mimicA`, CEL-relative path `mimic01`, reference renderer **121192**, combat profile `a78ca2526fd86c3dcd283858b58448b98081356e758ddbe3e2081eba1ebc7ebe`. Mimic A/B/C share this recorded exact profile; no cross-variant live coverage is inferred. `runtime-profile.json` uses Mimic A with `minimumBaseHealth: 64`. Runtime assets are `verdigrin.glb` and `verdigrin_basecolor.png`.

No indexed mimic live baseline was found when authoring. The skeleton register lists the offline mimicController representative and the current catalog supplies the exact assignment. Native diagnostic validation remains pending separately.

Native forward is +UnityZ: the lower-chest majority-weight surface centers at Z +0.273, while the hinged lid centers at Z -0.564. The lid is upright at the rear in the native bind pose; the tongue chain rises vertically along Y. The art preserves this pose rather than presenting a pre-closed chest or a tongue posed forward. Root_M carries the base, mid the lower chest and teeth, lidHinge the lid and upper teeth, and the native tongue chain carries the tongue. Native lidTop has no positive surface weights and remains unweighted, but all nine palette joints and exact inverse binds are retained. No surface triangles bridge the lid and lower mouth.

Every original vertex fits within native bind-surface bounds. Native animation envelopes remain unchanged; this is not proof of animated culling. Lid closure, tooth interleaving, tongue flexion and body motion require live inspection. Original boxes, faceted volumes, palette and topology are generated parametrically; no proprietary surface or texture is copied or packaged.

**Material override:** native first material 1056 `matLoot` enables emission with RGB 1.4. The profile explicitly sets `disableNativeEmission: true`. The source has additional wood and mimic materials, but this one-primitive export inherits only the first native slot. The source asset hash and scalar/color metadata are recorded in `native-material-metadata.json`; refresh with `inspect_materials.py --assets /absolute/path/to/resources.assets`.

Rebuild from repository root using existing local references and dependencies:

```sh
scratch/model-venv/bin/python art-experiments/verdigrin-mimic/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/verdigrin-mimic/build_blender.py
scratch/model-venv/bin/python art-experiments/verdigrin-mimic/finalize_manifest.py
```

The Blender script creates and saves the editable rigged scene, reopens it, exports through the FTK bridge and independently validates the result. Reopened outputs remain in ignored `scratch/verdigrin-roundtrip`; their hashes are recorded in the manifest. Export `verdigrin.blend`; `verdigrin-studio.blend` adds presentation-only floor, lights and cameras. Regeneration resets manual acceptance after refreshing mechanical checks and hashes.

Pending after canonical V4: studio approval, exhaustive hinge and tongue intervals, floor collision and sleeping, full culling, portraits, resource lifetime, final disposal, sibling mimic source pairs and finished-art acceptance. No game, framework, helper or catalog changes are performed by the authoring scripts.

Live baseline: [hashed evidence](live-validation.json) and [attack video](live/attack.mp4), [normal-hit video](live/nonlethal-hit.mp4), [death video](live/kill-fixture-death.mp4). All three captures contain 120 unpaused samples. Parent reviewed readable chest/teeth and both HUD portraits; normal damage 58→52, then animated closed-chest death 52→0. Private material emission was disabled and the original texture retained. Hero/effect occlusion and an early clipped death interval limit motion coverage. Two Collect actions reached strict Ready 0/2. The first pass request was refused for a profile-hash mismatch before action and remains preserved.

## Fresh live trial V2

Session `956d5a3eecae4fc0b9aee0bf11d2293f` used the exact catalog-411 profile `ftkmf_modeltest_verdigrin` on `mimicA`, renderer `mimic01` (121192), owner `369188`, bone signature `7e11ccbe7c3c9a43758519923854adc1952724a0b1763ab6235e1796beaa76ba`, and visual scale `1.0`. The fresh pass, ordinary attack and explicit `KillSingle` captures each completed 120 frames. The ordinary attack changed HP 58→54 with `cheat=None` and no focus; one guarded Collect was accepted.

Strict Ready did not appear before the bounded runner stopped, so this V2 record does not claim full loot or next-room progression. Root reviewed the open and lowered lid, ivory teeth, wine tongue, lower panels, attack overlays and native item-choice surface. The explicit death is fixture evidence, not ordinary lethal damage; hero/effect occlusion, hinge/tongue coverage, collision/sleeping, culling, portrait/resource lifetime and finished-art acceptance remain open.

The [V2 validation record](live-validation-v2/validation.json), [lossless archive README](live-validation-v2/README.md), [root frame review](../../scratch/verdigrin-root-visual-review.json) and [archive byte review](../../scratch/verdigrin-root-archive-review.json) preserve the raw case, journals, helper responses, 360 source-image pins, six selected originals and three presentation videos. Rebuild from the repository root with:

```sh
python3 art-experiments/verdigrin-mimic/live-validation-v2/archive.py
```

The script is offline-only and refuses a completed destination. It excludes game DLLs and native payloads while retaining authoring/runtime manifests and asset hashes.

## Fresh live trial V3

The [fresh V3 archive](live-validation-v3/README.md) records one clean catalog-411 process (`abb0e9c1bb414743b45603efa7c7dd05`) with exact `mimicA`/`mimic01` binding (renderer 121192, owner 369188, visual scale 1.0, nine-joint signature `7e11ccbe7c3c9a43758519923854adc1952724a0b1763ab6235e1796beaa76ba`). The authored coffer remains attached and readable through complete 120-frame pass, ordinary attack and explicit death-fixture captures.

The ordinary attack resolves HP 58→52 with `cheat=None` and no focus. `KillSingle` then records HP 52→0 across 120 frames. Native Loot accepts two guarded Collect responses: the first leaves the same current button and no reward delta, while the second advances to strict Ready at level 0 / room 2. One guarded Ready vote advances the next Enemy encounter; the fresh state shows Jelly Cube plus the registered cultist probe. This closes the V2 progression gap while preserving the limits: fixture death is not ordinary lethal damage, and hero/effects limit fine lid, tooth and tongue review. Floor collision/sleeping, full culling, portrait/resource lifetime, final disposal and finished-art acceptance remain open.

The [V3 validation record](live-validation-v3/validation.json), [standalone case](../../scratch/verdigrin-v3-standalone-case.json), [root frame review](../../scratch/verdigrin-v3-root-visual-review.json) and [native Loot/Ready continuation](../../scratch/verdigrin-v3-native-loot-ready.json) preserve the raw captures, helper responses, source-image pins and selected originals. Rebuild from the repository root with:

```sh
python3 art-experiments/verdigrin-mimic/live-validation-v3/archive.py
```

The V3 archive script is offline-only and refuses to overwrite a completed destination. It excludes game DLLs and native payloads while retaining authoring/runtime manifests and asset hashes.

## Canonical live validation V4

The [canonical V4 archive](live-validation-v4/README.md) closes the source-specific idle and hit gaps for exact `mimicA / mimic01 / renderer 121192`. Its retained session `e087741594c041d29c7a5f19adf364ce` uses the same profile, catalog, GLB, texture, source assignment and motion renderer as the current campaign. Only the generated queue and stage-readiness hashes changed after other routes completed, so the immutable runner was preserved and reconciled rather than replayed.

Three complete 120-frame captures record settled `cidle_mimic`, native `attack_mimic` dealing 14 hero damage, ordinary no-focus HP 58→52 with `damageSmall_mimic`, later `chompAOE_mimic`, and explicit-fixture `deathHeavy_mimic`. The exact renderer remains active, enabled, visible and identity-stable in all 360 retained frames. It reports `m_DoRagdoll=false`, so the death is animated rather than a body ragdoll. Fixture death is not ordinary lethal-damage evidence.

V3 remains the separate source for two guarded native Collects, strict Ready at level 0 room 2 and progression into the next Enemy room. V4 pins all 360 capture images, seven root-reviewed originals, three verified videos, its runner-reconciliation record and 25 gzip-lossless metadata mappings. Hero, UI, effects and depth blur limit fine review, and the Victory frame does not establish a precise corpse lifetime.
