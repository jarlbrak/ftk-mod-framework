# Resinmaw Bogling

An original amber slug-toad for `acidBlobA`, exact renderer 121344 at CEL-relative `enAcidMonster`. The silhouette combines a broad resin tail, folded haunches, moss-colored brows, golden eyes, and four articulated ivory tusks. The two jaws move independently. An overlapping jaw-pivot throat closes the neck connection without a rigid bridge between the jaws.

`build_geometry.py` creates every surface and palette pixel from original parametric forms. Native data supplies only the exact32-joint palette, inverse bind matrices, joint landmarks, usage metadata, and fitting bounds. `native-binding-audit.json` records positive native influence counts, including minor terminal weights. The four fang endpoints joint25/31/28/34 have zero positive weights; they remain in the palette without added diagnostic geometry. Rear tail and front-facing feet/fangs establish Unity +Z as front.

`resinmaw.blend` is the editable armature scene; `resinmaw-studio.blend`, `hero.png`, and `side.png` are presentation artifacts. `build_blender.py` saves, reopens, exports through the FTK bridge, and independently validates the saved scene. Full palettes and inverse binds are preserved. Original bind vertices stay inside the native reference box; that does not prove animated culling or camera fit.

Source material34 matAcidBlobA has white tint and red emission `(1,0,0)`. The exact assignment in `runtime-profile.json` opts out of native emission so amber, moss, and black pupils remain authored colors. Minimum base health is64. No native effects, cameras, controllers, bounds, or rigid accessories are replaced.

The pose studies skin only original vertices using recorded native bone matrices from120 frames each of pass, hit, and death. They normalize Root_M motion for inspection and use fixed framing across each study. Maximum triangle-edge stretch ratios are1.871,2.003,2.851 respectively; these are diagnostics, not quality thresholds. The largest death stretch occurs at the left folded haunch during native joint movement. Studio surfaces are connected and the sampled death poses fold coherently; full live motion remains an acceptance gate.

The source prefab includes native fxFollowAcid and fxAcidBubbles. DeathDirect and DeathIndirect have DeathFallOff events at different timings. Brown chunks and a puddle observed in the native baseline remain native effects: this asset does not claim to replace them. The baseline capture is not a live test of Resinmaw. Authored attack, hit, death, portrait readability, material, camera fit, culling, and cleanup remain pending.

Reproduce from repository root with local121344 extraction available:

```sh
.venv-3dgen/bin/python art-experiments/resinmaw-bogling/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/resinmaw-bogling/build_blender.py
.venv-3dgen/bin/python art-experiments/resinmaw-bogling/audit_native_poses.py
.venv-3dgen/bin/python art-experiments/resinmaw-bogling/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/77a5277153b64b209d6e82260853e780.json --label hit
.venv-3dgen/bin/python art-experiments/resinmaw-bogling/audit_native_poses.py --capture scratch/mirewarden-game/model-test-output/1c7ffa01a02049eca9ca1b292a54be98.json --label death
.venv-3dgen/bin/python art-experiments/resinmaw-bogling/finalize_manifest.py
```

Pose studies additionally require the indicated local captures. Rebuilding resets manual approval; review changed surfaces again before deployment.

## First live trial

[Live archive](live-validation.json): three 120-frame captures; reviewed idle/attack and both HUD portraits are readable, own palette/emission settings verified, and ordinary 5 damage 81→76 observed. Explicit kill fixture76→0 and two guarded Collect actions returned strict Ready0/2. The initial accepted start followed by a read-only busy500 and a once-only resume are preserved; journal inspection confirms one initial start request and none during resume.

Death art remains visually limited. All120 samples retain an active/enabled renderer and unit bone scales. Comparing frames28–119 against native AcidBlobA at identical death phases gives maximum renderer-local bone-matrix difference1.43e-7 across all32 bones. Hips moves backward/downward, consistent with the native sink and occlusion; the narrow visible form at40 is not evidence of scale collapse. Native chunks/puddle remain at60. Matching motion does not establish readability of all death surfaces or a whole-sequence artistic PASS.

## Fresh live V2

[The V2 archive](live-validation-v2/README.md) records a fresh catalog-411 run in session `9b795beba7674d6e887bf6faef3529ba`. The exact acidBlobA `enAcidMonster` renderer (121344) bound to owner `369188` with the expected 32-joint signature at public visual scale `1.0` (captured native CEL scale basis `0.75/0.8/0.8`). Selected idle and attack views keep the amber body, eyes, overlapping jaws and tusks connected and readable. The runtime material uses the authored basecolor, has emission disabled/black, no emission map, and explicitly opts out of inherited native emission.

Pass, ordinary attack and explicit `KillSingle` captures are complete 120-frame recordings. The ordinary attack changes HP `81→73` with `cheat=None` and no focus. The fixture reaches native Victory and strict Ready at level 0 room 2; the encounter exposes a Ready vote directly, so no Collect action is claimed. The renderer remains active through the last captured frame, but native UI/effects and the victory overlay limit fine jaw/fang detail, full death deformation, settled-ragdoll, culling, portrait/resource lifetime and finished-art acceptance.
