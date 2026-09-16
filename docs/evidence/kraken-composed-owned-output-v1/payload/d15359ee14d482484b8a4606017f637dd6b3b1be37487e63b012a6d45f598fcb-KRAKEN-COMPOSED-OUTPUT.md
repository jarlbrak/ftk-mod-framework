# Fixed input and original output composition

This opt-in experiment combines the separately verified fixed-four modern sampler
with the authored old-five-bone endpoint policy. It is not a production adapter.
The frozen43529 helper, original `two-clip-raw-v1`, `fixed-four-raw-v1` and
`main-appearance-local-v1` protocols/evidence remain separate.

Use a fresh command ID and current owned-helper nonce at strict native Ready:

```json
{"id":"<fresh32hex>","session":"<current32hex>","op":"kraken-controller-fixture","scenario":"damaged","endpointPolicy":"fixed-main-appearance-local-v1"}
```

The composed request internally creates the fixed-four graph. Do not also supply
`modernInputMixer` or `observeExistingDrivers`. The report explicitly identifies
that internal sample; it was not a separately submitted mixer command. Supported
scenarios remain appear, damaged, damaged-heavy, death and death-light. Attacks
and their native callbacks are unsupported and explicitly rejected.

For each same-step native snapshot:

- With no appearance role, freeze the fixed sampler's first pose and compute four
  target models as `S_fixed × inverse(S_rest) × T_rest`. Current/next source clocks
  and raw weights retain the fixed sampler's independently verified contract.
- With any appearance role, including raw weight0, retain the previous independent
  full-strength main and old-appearance samples. Convert their target models to
  locals under the actual old root and blend four locals exactly once with raw
  appearance weight and explicit shortest-arc SLERP. Never retarget an already
  appearance-mixed sampler pose and blend that output again.
- Preflight all resulting local TRS before committing to the separate owned
  output. Copy actual native old Root_M and native jaw LOCAL into that output.
  Native inputs are unchanged; previous output is never a baseline. Native jaw
  model-space equivalence is not claimed.

The endpoint samples must match the frozen fixed frame's roles, clip identities,
state, clocks and raw weights. All241 frames, first/repeated sampler poses, output
readbacks and cleanup observations remain available. Numerical mismatch does not
become a mechanics or art pass.

## Optional original Gloamfin skin

Arm before each composed first/repeat command, using a distinct ID and the same
nonce/scenario. The optional arm policy must explicitly match the new policy:

```json
{"id":"<arm32hex>","session":"<current32hex>","op":"kraken-skin-probe-arm","scenario":"appear","variant":"gloamfin-v1","manifestSha256":"8ae418087754cb73da2e73d42ad31802565a36adb6849d720b226e3b121db84e","endpointPolicy":"fixed-main-appearance-local-v1"}
```

For the other four scenarios also include the unchanged `capturePlanSha256`:
`1229bb59766cc1b5e47bdcb21448d85adeb07290df0c8c60dd1b450b07dbe21b`.
Cross-policy mismatches reject before consuming the pending arm. Omitted arm policy
retains the legacy protocol. The new composed skin arm requires explicit original
Gloamfin; the legacy marker and appearance arms retain their existing contracts.
The original5640 vertices, five bone palette/IBMs, weights, per-scenario fixed
camera,12 image steps and4 BakeMesh steps are unchanged.

## Independent verification

An endpoint manifest has schema `kraken-composed-endpoint-v1` and exactly these
`evidence` pins, each `{path,sha256}`: first, firstRequest, repeat, repeatRequest,
assets, gameAssembly and historicalEndpointManifest. The historical manifest is
the previously verified same-scenario `kraken-endpoint-policy-v1` pair.

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/verify_kraken_composed_fixture.py \
  --manifest scratch/<case>/composed-manifest.json --output scratch/<case>/verification.json
```

The verifier validates the exact original composed request, then uses explicitly
labeled internal views for shared numerical validators. Original request hashes
remain pinned. It independently checks the fixed input against native poses,
recomputes endpoint/output algebra and verifies ownership separation. Historical
comparison requires exact source/rest/controller/state/clock/raw-weight agreement
before comparing all241 output poses at1e-5. Only process-specific object IDs are
ignored. There is no clock shift, pose fit or tolerance increase.

For skin, use schema `kraken-composed-gloamfin-v1`, `images` with first/repeat image
pins, and `evidence` containing composedManifest, assetManifest, glb, png,
firstArmRequest, firstArmResult, repeatArmRequest, repeatArmResult, plus capturePlan
for the four companion scenarios. The unchanged full original-weight/Bake/camera
checks run only after composed endpoint validation succeeds.

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/verify_kraken_composed_skin.py \
  --manifest scratch/<case>/skin-manifest.json --output scratch/<case>/skin-verification.json
```

Five first/repeat scenario pairs and selected visual review remain required for
this composition's empirical acceptance. Live synchronization, synchronous portrait
rendering, attack coverage/events and real-avatar clone ownership remain separate
future gates. No Core or live hook is added by this fixture.
