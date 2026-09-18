# Reefstrider - original fishA01 candidate

A turquoise fish biped with a cream belly, rounded fish muzzle, dark lateral eyes, deep-blue gill panels, restrained coral crown and broad articulated flipper hands/webbed feet. The design uses original closed low-poly surfaces rather than armor or human clothing. Native unarmed attacks, effects and ragdoll remain unchanged.

Exact fishA01 renderer 121695 enFishA uses34 native palette entries and unarmed controller5958. The32 native-positive joints are weighted; MiddleFinger4_L/R remain unused but retained. Native scale 1 is preserved. No fishA02/A03 controller inheritance is claimed. The runtime profile explicitly disables native matLoot emission for the intended palette; fresh live readback is recorded in the V2 supplement below.

The generator reads only native bone names and bind matrices for landmarks; independent regeneration reproduces original source/palette/pieces exactly. Native geometry remains ignored. Direct/reopened exports, closed-piece positive volume and native bind bounds are checked. The initial intersecting belly badge is preserved in offline-history/initial-bind; the current cream belly is coloring on the continuous torso surface.

Completed native pass and dodged-attempt captures provide240 pose samples. The dodged attack is not hit evidence. Published sheets normalize Root_M travel for anatomy; no original live acceptance is inferred. The later hit and11-body ragdoll fitting are recorded below.

Run build_geometry.py, verify_original_geometry.py and audit_surfaces.py with scratch/model-venv/bin/python. Run build_blender.py with Blender --background --python for editable/reopened source and hero/side renders. audit_native_poses.py accepts explicit capture and six frame indices. The final manifest and runtime profile are frozen after the reviews below; deployment remains pending.

## Confirmed hit and ragdoll fitting

The pre-ragdoll candidate is preserved intact in offline-history/before-ragdoll-fit. The unchanged original mesh was applied to all120 confirmed normal-hit poses and all120 explicit-death poses, separately from the earlier dodge. The full death sheet retains renderer-local travel; a second sheet examines the physics transition28/30/32/34/36/40, including the worst shoulder stretch. Long legs and shoulders remain connected in the inspected projections; this is not numerical floor contact or an all-angle seam guarantee.

The largest hit-capture edge is the left flipper at79, .147186 to.312468 (2.12294×), during later motion. The largest death edge is the right shoulder at32, .103001 to.218840 (2.12464×). Original head, eyes and glints share exactly Head_M weights, checked independently; no differential skinning can displace them relative to each other. This does not prove eye visibility from every camera angle.

Native diagnostic observations report all11 bodies dynamic and Animator off at26, with measured motion/speed zero60–119. No IsSleeping or floor-contact inference is made. Root image review is recorded below; fresh original-body live tests are recorded in the V2 supplement.

## Reviewed offline package

Root reviewed all five pose sheets, with shoulders/hips/long legs attached and eyes seated where the face is visible. manifest.json freezes the exact original source, editable/reopened scenes,480 unique captured poses plus the dedicated ragdoll-transition view, attachment checks and preserved earlier candidates. The native11-body ragdoll/colliders are retained; rendered geometry fitting is not an original collision-model or contact proof. Exact fishA01 only, with native scale 1×factor 1 and explicit emission opt-out. No A02/A03 acceptance.

## Fresh live validation V2

The fresh catalog-411 run in session `b5ec7aa40751424d8e428ade47ea8ec5` bound `reefstrider.glb` to `enFishA` under one `fishA01` owner (renderer 121695, owner369188, bone signature `4670c40a093faef41fab67e69e846f992d7686b45f667a5a7a4ce42f050d8479`). The runtime readback saw `matLoot (Instance)` with the authored `ftkmf_reefstrider_basecolor.png`, Standard shading, black emission and no emission map.

Pass, ordinary attack and explicit `KillSingle` fixture captures completed 120 frames each. The ordinary attack reduced the same target from HP 58 to48 with `cheat=None` and no focus. Selected idle, attack and death-prefix frames show the fish body, cream belly, crown, flippers and webbed feet remaining connected in combat staging; the native UI, foreground hero and victory depth blur limit fine deformation and settled-ragdoll review. Two guarded native Collect calls were accepted and strict native Ready was observed at level0 room2.

The reproducible archive is [live-validation-v2](live-validation-v2/), with validation SHA256 `7e7174a268a0995d1ac29a88ab4fbd6ce8afa69e5b1bd7ea344f41983d6896f2`. It contains 370 source-image pins, 230 gzip-lossless metadata mappings, six selected originals and three 120-frame presentation videos. `archive.py` refuses overwrite unless `FTK_ARCHIVE_REBUILD=1`; native payloads and DLLs are excluded.

## Canonical exact-source validation V3

[The canonical V3 archive](live-validation-v3/README.md) pins fresh session
`a67c2432ebcc410c820dfc2f7ef3e2f1` against the exact `fishA01 / enFishA /
121695` assignment and expected 34-bone signature. One owner completes three
120-frame captures: settled idle plus native `AttackCrit`, an ordinary no-focus
HP 58 to 48 hit with native `Damaged`, and an explicit `KillSingle` fixture with
native `Death`. Two guarded Collect actions reach strict Ready at level 0 room
2.

Eighteen reviewed original PNGs accept the authored head, seated eyes and mouth,
cream belly, flipper arms, long legs, webbed feet, attack motion, hit recovery,
and a coherent ragdoll corpse that remains sampled behind the loot panel. The
hero and bright effects obscure the causal hit and death-impact frames, while
later recovery and collapse frames remain readable. The death fixture is not
ordinary lethal evidence, and no fishA02, fishA03, or sibling topology receives
credit. Indirect death, other native attacks, portrait, collision, extended
culling, long-session lifetime, full campaign completion, and final art
approval remain outside this archive.

Rebuild this immutable supplement through the generic reviewed-case workflow:

```sh
python3 tools/ai-model-pipeline/archive_model_validation_case.py \
  art-experiments/reefstrider-fish/live-validation-v3-plan.json
python3 tools/ai-model-pipeline/verify_model_validation_archive.py \
  art-experiments/reefstrider-fish/live-validation-v3 \
  --check-video-metadata
```
