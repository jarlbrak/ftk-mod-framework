# Paladin gear observation summary

This offline tool reads existing isolated fixture snapshots. It never connects
to the game or changes snapshots, package assets, gear or game state.

```sh
python3 tools/ai-model-pipeline/paladin-gear-evidence/test_summarize.py
python3 tools/ai-model-pipeline/paladin-gear-evidence/summarize.py \
  --output scratch/paladin-gear-evidence-v1.json
```

Choose a new output name for each pass. Inputs are matching
`paladin-gear-<set>-<1h|2h>-renderers.txt`, `-guardian-state.txt` and
`-equipment-inventory.txt` files. The three snapshots must share a session.
The report retains frame numbers, input hashes, native hero/dummy identities,
equipped slot IDs, exact renderer paths, visibility flags and resolved model
assets. `--fixture-receipt` pins the actual fixture content and its historical
production source content; current production metadata is labeled separately and
used only for asset lookup. Existing fixture asset hashes must remain unchanged. Hero joins use dummy and overworld instance IDs, not array order.

Runtime mesh names encode `package-model:SHA256(modGuid + newline + assetPath +
newline + assetSHA256)`. The summarizer reproduces that identity from the current
production package and fails for unknown package mesh identities. It records
asset hashes and declaring row assignments beside each resolved renderer. A mesh
assignment is stronger evidence than a screenshot alone, but does not establish
fit, animation, native break behavior or resource disposal. Texture application
and icon appearance are not established by this mesh-only summary.

The gear fixture grants all 36 items at creation. Its in-combat `equip_item`
actions bypass ordinary equipment action costs. Neither inventory nor renderer
coverage constitutes normal acquisition evidence. Disabled break fragments
establish assigned original meshes only. Sequential snapshots are not atomic;
keep each trio close together without intervening equipment changes. Both body
variants must be identified by their actual mapped assets, not preview labels.

## Historical observations and current scope

Early custom-body captures showed mismatched female/male apparel assignments.
Those records remain historical evidence. The current package retains native FTK
bodies, faces, hair and backpacks and authors equipment only, following the user’s
revised scope. A native backpack is therefore expected and is not a missing
original asset. See [current acceptance scope](../../../docs/paladin/VALIDATION.md).

The summarizer still verifies the exact model identities in its supplied fixture;
a historical original-body capture cannot establish current native-body fit.
Use the pinned revision-specific render and combat evidence for that claim.

## Isolated helper prerequisite

Package-only mode still requires an explicit `model-test-profiles.json` in the
isolated root, containing `{"version":1,"profiles":[]}` when no model fixtures
are wanted. Missing that file caused the first gear-copy helper activation to
fail. Preserve that failure separately from the subsequent working session;
never infer registration or visual coverage from an inactive helper.

## Corrected v4 captures

Keep corrected captures separate from the initial mixed-variant trial. The task's
scratch capture driver accepts `--output-prefix corrected-gear` and defaults to
its historical `paladin-gear` prefix. It refuses existing output files before any
live equipment action, so reruns require a fresh prefix. Only the live trial
operator should execute that driver.

For the offline summary, bind the exact immutable fixture copy and receipt:

```sh
python3 tools/ai-model-pipeline/paladin-gear-evidence/summarize.py \
  --prefix corrected-gear \
  --package scratch/paladin-gear-fixture-v4/package \
  --fixture-receipt scratch/paladin-gear-fixture-v4/receipt.json \
  --expected-session ACTUAL_CORRECTED_SESSION \
  --output scratch/paladin-gear-corrected-summary.json
```

The lookup content must match either the receipt's fixture content or its original
production source. A later edited production package cannot silently supply the
historical assignment metadata. An empty capture selection fails rather than
producing a success-looking empty coverage report. Framework/helper pins for the
prepared corrected run are recorded separately; a snapshot session must still be
associated with the actual loaded binaries by the trial operator.
