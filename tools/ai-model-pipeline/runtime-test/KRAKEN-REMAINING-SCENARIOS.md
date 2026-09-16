# Owned Kraken scenarios and input observations

These are isolated experiments, with no gameplay adapter or attack callbacks.
Existing appearance and original-marker requests retain their manifest, camera,
12 image indices,4 Bake indices and1e-5 tolerance.

For Gloamfin `damaged`, `damaged-heavy`, `death` or `death-light`, stage the
reviewed `gloamfin-four-scenario-capture-plan-v1.json` alongside the unchanged
Gloamfin manifest/GLB/PNG. Its SHA256 is
`1229bb59766cc1b5e47bdcb21448d85adeb07290df0c8c60dd1b450b07dbe21b`.
The original manifest SHA remains
`8ae418087754cb73da2e73d42ad31802565a36adb6849d720b226e3b121db84e`.
Source plan, generator and full-trajectory bounds proof are in
`scratch/gloamfin-four-scenario-plan-v1/`.

Under the existing owned-root/session/strict Ready guards, arm one scenario:

```json
{"id":"NEW_UUID_HEX","session":"CURRENT_NONCE","op":"kraken-skin-probe-arm","scenario":"damaged-heavy","variant":"gloamfin-v1","manifestSha256":"8ae418087754cb73da2e73d42ad31802565a36adb6849d720b226e3b121db84e","capturePlanSha256":"1229bb59766cc1b5e47bdcb21448d85adeb07290df0c8c60dd1b450b07dbe21b"}
```

Consume it once through the unchanged endpoint request:

```json
{"id":"ANOTHER_NEW_UUID_HEX","session":"CURRENT_NONCE","op":"kraken-controller-fixture","scenario":"damaged-heavy","endpointPolicy":"main-appearance-local-v1"}
```

Use separate arms/requests for the first and repeat run. The plan hash is
rechecked at consumption. Appearance rejects a companion plan; the old marker
variant does not gain nonappearance support. The companion fixes12 PNG/4 Bake
steps per scenario: heavy includes its short pure-damage95..110 window, and death
includes late motion at191. Death cameras are fixed diagnostic framing fitted
to both full241-frame trajectories, not the real gameplay camera.

The existing `verify_kraken_skin_probe.py` evidence manifest accepts one extra
`evidence.capturePlan` pin (`path` and `sha256`) for these four cases. It joins
that plan to the original asset identity, arm/result/skin identity, exact camera
and sample schedules; full weighted vertex/Bake and endpoint checks remain at
1e-5. Preserve all original inputs, raw frames, PNGs, cleanup results and visual
review separately. Offline tests and a build are not new live scenario evidence.

For the independent source-mapping question, issue a **separate observation-only**
request, without a skin arm or endpointPolicy:

```json
{"id":"NEW_UUID_HEX","session":"CURRENT_NONCE","op":"kraken-controller-fixture","scenario":"appear","observeExistingDrivers":true}
```

This adds `identity.oldExistingDriverRest` and each frame's
`old.existingDrivers`: existing paths, required/missing paths and only present
native driver poses. It never adds missing nodes. Inspect with:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/inspect_kraken_existing_drivers.py \
  --request scratch/driver-request.json --report scratch/driver-result.json \
  --output scratch/driver-comparison.json
```

The inspector validates the native report's fixed clocks, source hashes,
initialization, transforms, cleanup and Ready result, then compares native
surface clocks/weights before measuring local/model differences. Missing paths
remain `unavailable_missing_native_paths`; measured differences do not establish
a live source adapter. Do not feed observation-only requests to the unchanged
exact-schema endpoint verifier. Native attack states remain excluded because
`fireEvents=false` does not suppress their StateMachineBehaviours.
