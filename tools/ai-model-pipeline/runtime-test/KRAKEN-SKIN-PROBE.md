# Owned original Kraken skin probe

This optional helper experiment binds five original rigid octahedral markers to
existing endpoint OUTPUT bones. Its separate owned SMR uses the native binding
renderer's full transform from the copied output tree. The output remains
Transform-only. No native/controller/sampler geometry, material or texture is
cloned. Native graph inputs, exact scalar SLERP and numerical endpoint fields are
unchanged. This is not creature art, soft-skin/seam testing or production rig support.

## Fixed asset handoff

Source directory: `scratch/kraken-owned-skin-probe-v1/`.
Stage these three files under the isolated content `models/` directory only when
the parent authorizes deployment:

- `kraken-owned-skin-probe-v1.glb`, SHA256 `acbc612980c87f8b1fab0347b94dcd27a55cc154ffdf77b29f937f49170d6d9e`
- `kraken-owned-skin-probe-v1.png`, SHA256 `5088ed5fcedf49d8c71546e03537c4cb6de4af26efcd026d04693a5b591ae1d1`
- Source `manifest.json` renamed to `kraken-owned-skin-probe-v1.manifest.json`,
  unchanged bytes/SHA256 `df046f883286b3cf1723ec7bd87454e99e131a1673221c09aced61fd7d833e17`.

The pinned manifest contains original authoring/generator hashes, independently
checked octahedron offsets, five rigid bone groups, exact old121260 palette/IBMs,
byte-identical reproduction and fixed camera envelope audit over both earlier
verified 241-frame appearance runs. Native reference assets remain local scratch.
The loader reads native binding metadata only; baked/exported vertices are solely
from this hash-pinned original 120-vertex/120-index mesh.

## One-shot arm and unchanged endpoint request

At the exact native Ready/session intended for the experiment:

```json
{"id":"unique-arm-id","session":"current-session","op":"kraken-skin-probe-arm","scenario":"appear","manifestSha256":"df046f883286b3cf1723ec7bd87454e99e131a1673221c09aced61fd7d833e17"}
```

Use standard 32-hex request IDs for evidence verified offline. Only one arm may
exist. `kraken-skin-probe-stop` with id/session/op clears it. The next controller
fixture consumes/disarms it before allocation; mismatched session/root/Ready,
scenario or absent endpoint policy rejects and clears the stale arm. A failed
run never retries the skin experiment implicitly. No generic queued actions.

Then issue the normal, unchanged endpoint request:

```json
{"id":"unique-controller-id","session":"current-session","op":"kraken-controller-fixture","scenario":"appear","endpointPolicy":"main-appearance-local-v1"}
```

Repeat with a new arm and controller ID. No pending arm preserves previous helper
behavior. Initially only appearance is supported; other scenarios require their
later explicit capture/framing acceptance.

## Isolation and sampling

Fixed PNG steps: 0,16,28,40,41,80,104,105,112,119,120,240. Bake steps: 0,28,80,112.
Every image is 512 square. The immutable orthographic camera uses the pinned
model-top framing, near .1/far40/size6.5. It is disabled and has no native scripts,
lights or CEL. Before each capture the selected culling layer must have no other
scene Renderer, CanvasRenderer or Terrain. No native object is relayered.

The original renderer is disabled outside the synchronous BakeMesh/Camera.Render
interval and disabled in finally before any coroutine yield. RenderTexture.active
is restored in a nested finally. Native graphs and output poses are rechecked
without advancing graph time. Every allocated mesh/material/texture/RT/camera/SMR
object gets an independent cleanup attempt, and deferred Unity-null completion
joins existing Ready/engine-error success conditions.

Skin data lives in separate `originalSkinProbe` records. Selected baked vertices
are renderer-local, with same-step output pose, renderer/world inverse, bone world
matrices, original IBMs, camera matrices and image hashes. Broad owned-only culling
bounds avoid truncating the moving probe; actual baked bounds are recomputed and
recorded separately. Images go to the unique controller-ID `-original-skin` output
directory. Neither output nor prior evidence is overwritten.

## Independent checks and acceptance

Run the unchanged endpoint verifier first. The new
`verify_kraken_skin_probe.py` reruns it through an explicitly pinned endpoint
manifest, then checks original assets/geometry, arm/session/Ready provenance,
fixed captures, PNG hashes/dimensions/variation, cleanup and skin coordinates.
Expected vertices are computed without fitting:
`inverse(rendererWorld) * boneWorld * originalIBM * originalVertex`, summed by
original GLB weights. Fixed numerical tolerance is 1e-5. It reports coordinate,
BakeMesh or clipping failure rather than silently correcting it.

Skin verification manifest schema is `kraken-original-skin-v1`, with exactly:

- `evidence`: pins `{path,sha256}` named `endpointManifest`, `assetManifest`, `glb`,
  `png`, `firstArmRequest`, `firstArmResult`, `repeatArmRequest`, `repeatArmResult`.
- `images`: `first` and `repeat` arrays of twelve `{path,sha256}` pins in fixed
  step order. Endpoint report/request/assets/game pins remain in the unchanged
  nested endpoint manifest.

Run `scratch/model-venv/bin/python tools/ai-model-pipeline/verify_kraken_skin_probe.py
--manifest ... --output scratch/...json`. New output files only.
Tests: `scratch/model-venv/bin/python -m unittest discover -s tools/ai-model-pipeline
-p test_verify_kraken_skin_probe.py` (20 original-asset/algebra/provenance/negative cases).

The first live pair is preserved in `scratch/kraken-skin-live-v1`. Both helper
runs completed, and all four BakeMesh samples in each run matched the independent
skinning formula within `1e-5` (maximum about `7.63e-6`). The full verifier rejected
the camera inverse check: native prefab placement near world coordinates
(100.8, 0, 132.82) amplified float rotation nonorthogonality into a roughly
`2.99e-5` inverse translation residual. This is retained failed evidence.

For recapture, only the optional skin presentation uses an endpoint-plan-owned
Transform parent whose translation cancels the native output top local position.
`SetParent(..., false)` preserves the output's local TRS, and endpoint local/model
checks remain unchanged. Native graph and independent sampler trees stay in their
original placement. Both the fixed camera and standalone probe renderer derive
world placement from the translated output tree; there is no independent camera
or renderer correction. Identity and every capture report the immutable parent
matrix, original top placement, instance IDs and ownership. The endpoint plan,
not skin disposal, owns and verifies destruction of the parent.

The camera inverse equation and `1e-5` verifier tolerance are unchanged. A fresh
live pair must pass them; no recapture success is inferred from this source fix.
Numerical matches precede human/model image inspection for marker movement,
occlusion/clipping and branch-boundary behavior.
A mechanics match does not establish visual continuity, native skin equivalence,
soft tissue, attack events or a production adapter.

The verifier binds all five renderer bone IDs to each frame's actual output-local
IDs, derives fixed camera world/projection matrices from the pinned framing, and
checks every capture against them. Its view conversion includes Unity's documented
[negative-Z camera convention](https://docs.unity3d.com/2017.4/Documentation/ScriptReference/Camera-worldToCameraMatrix.html);
a per-frame camera shift cannot pass merely by keeping the markers in view.
