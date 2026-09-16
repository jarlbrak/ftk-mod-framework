# Native scheduled owned material lifetime

The independent raw-data audit passes both fixed lineages:44 EndOfFrame samples,
100 native phase calculations, and nine owned resources per lineage. This is an
owned original no-CEL material fixture, separate from actual public GLB binding or
native player/enemy avatar integration.

The unchanged raw request/report and before/after Ready snapshots are pinned by
[manifest.json](manifest.json). The exact deployment and reviewed helper source
are included. The report ID is `d7f21ee0fd7a43d98a73c2be4ae6670e`, session
`62ffa7254a5348b8b2107b86809aebbe`, Core `19ece392…`, helper `b9fc9374…`.
The deployment receipt identifies measured on-disk files; it is not a hash of
loaded process memory. The pre-deployment helper review's `deployed:false` field
is preserved as historical build metadata; the deployment's `new` fields record
the selected files.

The verifier reconstructs these relationships from the raw observations, without
using the helper's assertion booleans as its acceptance oracle:

- Source, clone, grandclone and never-enabled renderer have distinct stable
  component/bone identities and exact native-clone parent relationships.
- Mesh and two textures remain shared. Source materials stay stable; enabling
  clone and grandclone adds two distinct private materials each, matching their
  serialized provenance. Resource counts advance5→7→9.
- Disabled renderers accumulate their own native phase without private material
  allocation. Their still-shared offsets agree with the ancestor's material
  writes; those offsets are not evidence of a disabled-renderer write.
- Enabled slot1 offsets follow each owner's phase; slot0 remains unchanged.
  All100 raw phase recurrences match float32 arithmetic exactly, with maximum
  error0 at the unchanged absolute `1e-5` tolerance. Consecutive frame IDs and
  positive actual deltaTime are required; no fixed-framerate assumption is used.
- Descendants-first and source-first teardown show the expected surviving owner
  sets and lease references. The grandclone continues updating after source and
  clone disappear. Final disposal records join to all nine historical resource
  IDs per lineage, report Unity-null, and report absent leases5 and6.
- Before/after snapshots retain the same session, party, native avatar IDs,
  dungeon indices and strict native Ready surface.

Final Unity-null/lease absence are source-reviewed runtime measurements, not a
second independent engine observation. The direct invalid-slot prefix test has
only formatted assertion rows, so this audit does not independently re-prove its
atomic before/after state. It remains source-reviewed and runtime-reported.
Never-active GameObjects, actual Unity failed-commit rollback, public GLB
transaction behavior, native avatar lifetime, art and animation are not covered.
No game actions were issued during this offline verification.

Reproduce from the repository root, writing a new output file:

```sh
python3 tools/ai-model-pipeline/verify_material_lifecycle.py \
  --manifest docs/evidence/material-lifecycle-native-v1/manifest.json \
  --output scratch/material-lifecycle-independent-recheck.json
python3 -m unittest discover -s tools/ai-model-pipeline \
  -p test_verify_material_lifecycle.py -q
```

[verification.json](verification.json) contains the independently reconstructed
result. Thirteen corruption tests cover wrong phases despite passing helper
assertions, bad offsets, frame gaps/paused clocks, aliasing, wrong serialized or
retired resource IDs, extra disabled allocations, teardown, session and binary
pin changes, and shifted/overlapping lineage timelines. No Unity scheduling simulation is claimed by these offline tests.

The architect reviewed the independent verifier and reran all13 corruption tests.
[audit-receipt.json](audit-receipt.json) pins the verification tool, tests and result.
A standalone copy of [the verifier](verify_material_lifecycle.py) is included;
archived tests are exact source copies whose repository command is shown above.
