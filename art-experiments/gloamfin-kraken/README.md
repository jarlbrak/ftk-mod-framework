# Gloamfin Kraken blockout

Original five-bone cephalopod head for the old resource `enkrakenhead`, renderer 121260 at `krakenHead`. It began as a deformation blockout for the owned endpoint fixture and now has an exact production adapter plus a canonical route archive. It remains separate from the modern Kraken and is not finished creature art.

## Generic integration boundary

The current resource-route preflight records seven controller binding hashes
missing from `enkrakenhead` beyond the direct `krakenHead` baseline. The
repository therefore classifies this exact resource route as
`controller_incompatible` and does not create a generic `runtime-profile.json`
for it. The implemented explicit adapter preserves the five-bone source identity
and native combat authority. Do not reuse seven-bone Kraken evidence or the
constructed endpoint fixtures as production evidence.

The adapter is now specified by the machine-readable
[production contract](../../docs/evidence/kraken-production-adapter-design-v1/contract.json)
and its [review guide](../../docs/evidence/kraken-production-adapter-design-v1/README.md).
It keeps the real CEL and weapon controller authoritative, samples the modern
four-endpoint chain on a separate event-disabled hierarchy, maps those endpoints
onto the old four articulated bones, and preserves the old jaw and appearance
path. The generic staging exclusion remains in force. The production
implementation and listed gates are now reconciled by the canonical archive.

## Canonical production route V2

The [canonical archive](live-validation-v2-canonical/validation.json) closes the
exact `krakenHead / enkrakenhead / krakenHead / 121260` route. The two-owner
production campaign supplies native attacks, all four Kraken proficiencies,
ordinary lethal death, enemy victory, and natural teardown. A separate visual
exercise supplies exact binding, reviewed appearance, idle, attack, ordinary
hit, fixture death, sampled camera and culling, and strict Ready0/2.

The original blank initiative tile is preserved in the pre-fix review. A fresh
first-snapshot follow-up with framework
`60917286ce044bfc57480ac34596cf54a596e6db0ec8a179c2518502f0847510`
uses a bounded disposable portrait-clone scale of `0.8447812795639038` while
leaving the source scale at `1.0`. The reviewed native 204x172 texture shows the
complete face and upper body and is used by six active initiative images. Other
projections, a persistent corpse, all-camera culling, sibling sources, and
finished-art approval remain outside the archive.

The broad teal mantle, watchful pale eyes, copper beak and two curled facial feelers establish a recognizable silhouette. One continuous neck/mantle surface has deliberately graded joint1/neck/head/topHead weights. The lower jaw articulates independently. A flexible head/jaw-weighted dark throat maintains the mouth cavity as it opens. Feelers are head-rigid shape landmarks, not independently animated tentacles.

All five original palette names and native inverse binds remain exact. No native vertices, triangles, normals, UVs or texture pixels are distributed. `verify_original_geometry.py` reruns the authoring generator with native array access restricted to names and inverse binds; source, piece metadata and palette reproduce byte-identically. Independent full GLB validation is separate from that authoring provenance check.

`gloamfin.blend` is editable, with the original mesh tagged for the FTK bridge. `build_blender.py` saves and reopens it before exporting and independently validating the saved scene. `gloamfin-studio.blend`, hero and side are studio presentations. The direct and saved-reopened exports pass, with bind vertices inside native reference bounds; this does not prove live culling or animation-envelope fit.

`audit_endpoint_poses.py` applies complete original weights to pinned output matrices from both241-step appearance runs in `scratch/kraken-skin-live-v2`. It does not resample or change the endpoint policy. It records all482 pose bounds, triangle area/edge ratios, soft-versus-rigid displacement and a fixed front three-quarter camera in old prefab-root coordinates. The full 241-step depth-buffered movie is `endpoint-study.mp4`; the contact sheet selects the fixture's12 image steps. This is offline skinning, not a live BakeMesh comparison or an artistic quality verdict.

The initial quick painter render produced false jagged mouth edges. Independent depth rendering removed those artifacts but exposed an actual teal neck region between the separate mouth pads. An original blended black throat now fills that visual gap. `endpoint-depth-comparison.png` shows steps0/40/41/112 after correction. These checks omit Unity materials, lighting and backface-culling behavior; those remain live gates.

The future fixture contract is `gloamfin-kraken-blockout-v1.manifest.json`, variant `gloamfin-v1`. It pins GLB/PNG/source/generator hashes, counts, palette/IBMs, explicit weight distribution, provenance and camera. Existing five-marker assets are unchanged. The initial blockout required a separately reviewed helper allowlist extension; the later appearance trial below uses that approved extension. Preserve the1e-5 full-weight verifier, isolated origin/camera, one-shot arm, fixed steps and resource cleanup. Damage, heavy damage, death, death-light, standalone attack and production integration remain separate later gates.

Reproduce from repository root with local121260 binding reference and pinned endpoint captures available:

```sh
.venv-3dgen/bin/python art-experiments/gloamfin-kraken/build_geometry.py
/Applications/Blender.app/Contents/MacOS/Blender --background --python art-experiments/gloamfin-kraken/build_blender.py
.venv-3dgen/bin/python art-experiments/gloamfin-kraken/verify_original_geometry.py
.venv-3dgen/bin/python art-experiments/gloamfin-kraken/audit_endpoint_poses.py
.venv-3dgen/bin/python art-experiments/gloamfin-kraken/check_depth_render.py
ffmpeg -nostdin -y -loglevel error -framerate 60 -i scratch/gloamfin-endpoint-poses/%04d.png -c:v libx264 -pix_fmt yuv420p art-experiments/gloamfin-kraken/endpoint-study.mp4
.venv-3dgen/bin/python art-experiments/gloamfin-kraken/build_fixture_manifest.py
.venv-3dgen/bin/python art-experiments/gloamfin-kraken/finalize_manifest.py
```

Regeneration changes hash pins and resets manual review. Do not substitute regenerated files into an already approved fixture silently.

## Owned appearance fixture V1

The helper's separately reviewed `gloamfin-v1` allowlist extension has now run this exact blockout. [Live fixture evidence](live-validation-appearance-v1.json) preserves first and repeat runs:241 endpoint samples,12 sparse512×512 images and4 BakeMesh observations per run. Independent full-weight validation reports maximum BakeMesh error7.48114104e−7 and endpoint error2.36390344e−6 against unchanged tolerance1e−5. Repeat image bytes match. Ready0/2 is unchanged and owned cleanup reports complete.

Parent reviewed all12 first-run images: coherent teal hood, neck and beak/throat, with no visible detached gaps or clipping in these samples. Open mouth40/41 remains connected. This accepts selected **appearance blockout deformation and fit**, not a finished character, continuous animation quality, other scenarios, game encounter replacement or production adapter. Existing marker V1 camera-precision failure and V2 marker results remain separate and unchanged.

The archived `.json.gz` files preserve the full first/repeat telemetry byte-exactly; the contact sheet has12 discrete samples and is not a continuous movie. Fixed image steps are0/16/28/40/41/80/104/105/112/119/120/240; BakeMesh steps are0/28/80/112. The record pins the one-shot arm requests, exact approved asset manifest, full original weights/IBMs, origin-isolated camera and unchanged Ready identity.

Read-only reproduction of the independent verification, using the pinned source paths preserved in the manifest:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/verify_kraken_skin_probe.py --manifest scratch/gloamfin-skin-live-v1/skin-manifest.json --output scratch/gloamfin-skin-live-v1/verification-recheck.json
```

The verifier intentionally retains `pending_visual_review`; the separately hashed root review supplies only the limited visual verdict above. Next gates are the same weighted blockout under damage/heavy damage/death/DeathLight and standalone attack through a reviewed fixture extension, followed separately by production integration and finished-art work. Do not silently change assets or reuse the appearance-only arm for an unreviewed scenario.

## Owned damage and death fixture pairs V1

The same immutable original blockout now has four additional completed first/repeat pairs. These supersede the corresponding pending scenario gates above; they do not extend the scope to gameplay or finished art.

| Constructed scenario | Evidence | Maximum BakeMesh error | Maximum endpoint error |
| --- | --- | --- | --- |
| Damaged | [Pair record](live-validation-damaged-v1.json) | 8.41541257e−7 | 2.63679005e−6 |
| Damaged-heavy | [Pair record](live-validation-damaged-heavy-v1.json) | 7.09405358e−7 | 2.78328996e−6 |
| Death | [Pair record](live-validation-death-v1.json) | 1.52573364e−6 | 2.62060982e−6 |
| Death-light | [Pair record](live-validation-death-light-v1.json) | 1.52573364e−6 | 2.62060982e−6 |

Each pair preserves 241 endpoint observations, 12 PNGs and four BakeMesh samples per run, unchanged tolerance 1e−5, byte-identical repeat images, unchanged pinned Ready identity and reported owned cleanup. Raw first/repeat JSON is archived as lossless gzip, alongside all 24 images, arm requests/results, deployment pins, source verification manifests, the fixed companion capture plan, original verifier result and root review. Original absolute source paths remain in immutable evidence; the pair record maps each raw report and image to its archived path and SHA256. Decompress raw reports before reconstructing the original verifier input layout; do not edit the hash-pinned manifests in place.

The independent verifier's `original_skin_numerical_match_pending_visual_review` status remains unchanged. Root separately reviewed all 12 first-run images for each scenario: the hood, eyes, beak, jaw, neck and feelers remained connected and framed in those samples. Death and death-light show the intact blockout descending through the wider fixed camera. Their reviewed appearance is similar; these results do not establish distinct animation behavior. No native floor or encounter is present, so descent is not proof of corpse placement.

These are sparse, constructed deformation observations of an original **blockout**. Continuous/all-view quality, standalone attack, a production rig adapter, actual gameplay fit/materials/portrait/progression and finished-art polish remain separate gates. The records live only in the `kraken_original_skin_trials` ledger and do not increase ordinary enemy topology coverage. Earlier marker failure history, the appearance archive, and all authored asset manifests remain unchanged.
