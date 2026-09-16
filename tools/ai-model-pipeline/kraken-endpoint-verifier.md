# Offline Kraken endpoint-policy verification

`verify_kraken_endpoint_fixture.py` checks the optional
`endpointPolicy: "main-appearance-local-v1"` report from
`kraken-controller-fixture`. It does not issue game commands. Use two independently
submitted requests in the same helper session, for the same scenario and Ready slot.
Every run must contain all 241 sequential manual steps and successful cleanup.

Create a JSON manifest with exactly these fields:

```json
{
  "schema": "kraken-endpoint-policy-v1",
  "evidence": {
    "first": {"path": "/absolute/scratch/first.json", "sha256": "<64 lowercase hex>"},
    "firstRequest": {"path": "/absolute/scratch/first-request.json", "sha256": "<64 lowercase hex>"},
    "repeat": {"path": "/absolute/scratch/repeat.json", "sha256": "<64 lowercase hex>"},
    "repeatRequest": {"path": "/absolute/scratch/repeat-request.json", "sha256": "<64 lowercase hex>"},
    "assets": {"path": "/absolute/scratch/game-copy/FTK_Data/resources.assets", "sha256": "<actual source hash>"},
    "gameAssembly": {"path": "/absolute/scratch/game-copy/FTK_Data/Managed/Assembly-CSharp.dll", "sha256": "<actual source hash>"}
  }
}
```

Use the actual Mac bundle paths on macOS. The verifier accepts only the explicitly
supported shipped resources and game-assembly hashes; it reads native rest transforms
and controller state/clip clock rules from those assets. Generate hashes from the
original immutable files. Do not add provenance to historical reports after capture.

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/verify_kraken_endpoint_fixture.py \
  --manifest /absolute/scratch/endpoint-manifest.json \
  --output /absolute/scratch/endpoint-verification.json
```

NumPy, SciPy and UnityPy are required. The output must be a new file under scratch.
Missing pins, invalid schema, frame gaps, wrong sampler identities/settings, bad
clocks/weights, or incomplete cleanup fail closed. Numerical mismatches produce an
`endpoint_policy_mechanics_mismatch` report with individual failures and nonzero exit.

The verifier reconstructs every pose from local TRS, recomputes immutable-rest
retargeting, derives four hierarchical locals under the actual native old root,
and independently uses linear position/scale interpolation and shortest-arc SLERP
with the raw appearance weight. It compares output readback, complete old model
chains, preserved native root and jaw **local**, and repeated-run poses. Source
and output instance IDs are checked within each run; owned trees must be disjoint.

Pure-state endpoint samples are compared to both observed native graph trees at
their actual clocks. Mixed-clock samples remain constructed policy endpoints.
Appearance requires observed entry, pure appearance, exit, and subsequent pure idle;
other scenarios require pure idle, pure target and transition coverage. No exact
native transition frame counts are assumed beyond the fixed 241-step recording.

A matching result establishes this bounded mechanics/baseline comparison only.
It does not establish native mixed-pose equivalence, mesh deformation, visible
continuity, jaw model equivalence, or a production adapter. Runtime flags for input
immutability and within-step repeated commit are reported assertions, not independent
recovery of unrecorded before/after states. Cross-run samples/output are compared
independently. Full synthetic tests contain nonidentity rest transforms, compound
rotations and real clock/alpha transitions; they do not substitute for live evidence.
