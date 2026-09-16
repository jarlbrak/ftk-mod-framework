# Copperveil Weaver

An original eight-legged spider: midnight-blue body, broad copper-marked abdomen, amber eye cluster and ivory articulated fangs. This is an authored surface model on the documented 65-joint Spider B rig, with mechanical export complete and fresh live trial evidence recorded.

Exact target: `spiderB`, CEL-relative renderer `enSpiderB`, native reference renderer **121386**, combat profile `ac86fda01c15eea07fdc783ef52ea95fb759f6fbfb9e1d706fcd55e7330ecb5a`. `runtime-profile.json` uses these exact values with `minimumBaseHealth: 64`. Runtime assets are `copperveil.glb` and `copperveil_basecolor.png`. Spider A has a different recorded combat profile; no cross-variant coverage is implied.

No indexed spider live baseline was located in `docs/model-runtime-validation.json` when authoring. The skeleton register lists the offline spiderController representative, and the catalog identifies the exact Spider B assignment. The fresh V2 trial below is the first current exact-renderer live evidence; offline compatibility and an original studio render alone do not establish live support.

Forward is +UnityZ, verified before authoring from Head_M and the fang chains, with the tail/abdomen chain extending -Z. Original thorax and abdomen rings follow the corresponding spine/tail joints. All eight legs follow their own named native leg chains; short copper knee sleeves follow local leg3 joints. Fang and palp surfaces follow their respective chains. Terminal tips are inset from bone endpoints where endpoints exceed the native surface bounds. The complete 65-joint palette and inverse binds remain intact, including unused joints. No native vertex surface, topology or texture is copied.

Every original vertex fits within the native bind-surface box. Native animation envelopes remain unchanged, and bind containment does not prove animated culling. The abdomen overlays and articulated leg sleeves require live motion inspection for separation/intersections.

Source material 232 `matSpiderB` has white tint and zero emission with no emission keyword, so the profile leaves `disableNativeEmission` false. `native-material-metadata.json` records the source asset hash plus material identity and scalar/color properties. Refresh it with `inspect_materials.py --assets /absolute/path/to/resources.assets`. Studio eyes are palette colors, not emitted light.

Rebuild from the repository root using the existing local references and dependencies:

```sh
scratch/model-venv/bin/python art-experiments/copperveil-spider/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/copperveil-spider/build_blender.py
scratch/model-venv/bin/python art-experiments/copperveil-spider/finalize_manifest.py
```

The Blender script creates and saves the editable rigged scene, reopens the saved file, exports through the FTK bridge and independently validates it. Reopened exports remain in ignored `scratch/copperveil-roundtrip`; their hashes are recorded in the manifest. Export `copperveil.blend`; the combined `copperveil-studio.blend` contains presentation-only floor, lights and cameras. Regeneration resets manual acceptance after refreshing mechanical checks and hashes.

Remaining art checks: studio review, native-scale readability across all motion intervals, full culling and cleanup. No game, framework, helper or catalog changes are performed by these scripts.

## First live original baseline

[Live evidence](live-validation.json) pins all assets and deployment in session
98049d8298334aeda9e92d2725e4944d. Body, legs, copper joints and eyes read under
darker native lighting without an emission issue. Raised native attack stays
onscreen with effects. Parent reviewed attack0/50, each hit30, and death40/60.
All hit views are hero-occluded; scoped art approval does not establish
unoccluded body deformation, all-motion or culling acceptance.

- [Attack](live/attack.mp4)
- [Blocked hit1](live/blocked-hit1.mp4),63HP unchanged.
- [Blocked hit2](live/blocked-hit2.mp4),63HP unchanged.
- [Successful normal2 hit](live/nonlethal-hit.mp4),63 to61.
- [Kill-fixture death](live/kill-fixture-death.mp4),61 to0.

All five captures complete120 unpaused frames. Upturned legs follow native
spiderDeathDirect, with Animator enabled throughout, zero rigidbodies and no
ragdoll flag. Its first sampled normalized time0.1222 leaves the early endpoint
unseen. One Collect reaches Ready0/2. Replays are12fps, not real-time
performance. The earlier separate SpiderB probe blocked-hit result remains
historical and is not retroactively converted into successful damage evidence.

## Fresh live validation V2

[The reproducible archive](live-validation-v2/README.md) pins session
`115d691fdeec4e829088462912641932`, exact renderer `enSpiderB` (121386), owner
`369188`, and the 65-joint bone signature. The authored spider stayed readable
through two idle samples and two native poison-attack samples. The ordinary
attack changed the same target from HP 63 to 61 with no cheat or focus. The
explicit `KillSingle` fixture completed 120 frames, one native Collect was
accepted, and strict native Ready was observed at level 0 room 2.

The fixture death is separate from ordinary lethal damage. Native effects and
the hero foreground limit fine leg, abdomen and material inspection, and the
terminal view is the native victory/item-choice surface rather than a detailed
corpse or floor-contact view. Rebuild the archive from the repository root with:

```sh
PYTHONDONTWRITEBYTECODE=1 python3 art-experiments/copperveil-spider/live-validation-v2/archive.py
```

The script refuses to overwrite a completed archive; set
`FTK_ARCHIVE_REBUILD=1` only when deliberately regenerating it from the same
captured sources.

## Canonical exact-source validation V3

[The canonical V3 archive](live-validation-v3/README.md) pins fresh session
`1e61830193c74dc092ad870ca523e023` against the exact `spiderB / enSpiderB /
121386` assignment and the expected 65-bone signature. One owner completes
three 120-frame captures: settled idle plus native `AttackProf`, an ordinary
no-focus HP 63 to 59 hit with native `Damaged`, and an explicit `KillSingle`
fixture with native `Death`. One guarded Collect reaches strict Ready at level
0 room 2.

Eighteen reviewed original PNGs accept the authored abdomen, face, eight-leg
silhouette, attack motion, hit recovery, airborne death, and a coherent
collapsed custom-mesh corpse that remains sampled behind the loot panel. The
hero and bright effects obscure the causal hit and death-impact frames, while
later recovery and collapse frames remain readable. The death fixture is not
ordinary lethal evidence, and no Spider A or sibling topology receives credit.
Indirect death, other native attacks, portrait, collision, extended culling,
long-session lifetime, full campaign completion, and final art approval remain
outside this archive.

Rebuild this immutable supplement through the generic reviewed-case workflow:

```sh
python3 tools/ai-model-pipeline/archive_model_validation_case.py \
  art-experiments/copperveil-spider/live-validation-v3-plan.json
python3 tools/ai-model-pipeline/verify_model_validation_archive.py \
  art-experiments/copperveil-spider/live-validation-v3 \
  --check-video-metadata
```
