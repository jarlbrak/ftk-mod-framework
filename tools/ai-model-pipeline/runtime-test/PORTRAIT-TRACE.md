# Passive portrait trace

This helper-only diagnostic requires the existing `FTK_MODEL_TEST=1` isolated-root
launch guard and an explicit current-session `portrait-watch` command:

```json
{"id":"unique","session":"current-nonce","op":"portrait-watch","enemy":"exact_registered_model_key"}
```

It validates the exact loaded catalog profile and synthetic DB row. Arming clears
prior diagnostic records. `portrait-watch-state` reads the bounded records;
`portrait-watch-stop` disarms while retaining them. Both read/stop requests contain
only id, session and op. The next arm clears old records. No command renders a
portrait. Arm before a separately authorized native encounter/portrait operation.

At most eight matching native portrait calls are retained; saturation stops
recording rather than overwriting first evidence. Each call is captured once,
immediately before DoRender, after native portrait posing and marker placement.
Snapshot overload and observed caller stack are separate from identity resolution.
Row scope forwards only to that exact row's source CEL; live identity requires the
matching EnemyDummy owner and registered row. Unknown nested calls never inherit
an outer watched identity. Unmatched calls produce no portrait record.

Records contain source/target CEL, mesh and material/texture IDs, names and counts,
read-only resource lease metadata, camera view/projection/world matrices, output
texture IDs/dimensions and the target's per-renderer bone locals/model/world
matrices. All names/paths describe observed Unity objects. Marker arguments are
observed at a last-priority SetTargetPosition prefix, with bounded candidate paths
before native placement; they do not independently prove native traversal order.
The pose note describes the native lowercase `portrait` search/sample-at-one-second
path, not a separate observation of SampleAnimation execution.

Bounds per source/target: 256 visited transforms, 32 SMRs, 16 shared materials per
renderer. Target bone snapshots share one total budget of 256. Truncation flags
are explicit; incomplete telemetry records carry an error and cannot establish
complete projection evidence. Storage contains serialized scalars/matrices only,
with no retained cloned Unity references or lease acquisitions. Snapshot scope
references end naturally with the call. Prefixes/finalizers contain telemetry
exceptions; finalizers preserve the original native exception. A stopped/changed
session/root disables observation. Existing native rendering and clone disposal
remain responsible for their own behavior.

The recorder never calls render, GetPixels, BakeMesh, or material-instantiating
`.material`/`.materials` getters, and does not dump mesh vertices/indices/weights.
Use the hashed authored GLB separately for offline projection/occlusion analysis.
`telemetryComplete` means the reads completed, not that rendering or visual
acceptance succeeded. `nativeSnapshotException` is reported by the scope finalizer.

Source review identified a separate row-preview mesh-application gap: that path
can clone the native prefab without the combat-only mesh swap. No Core fix is
included, and this is not established as the combat HUD portrait failure's cause.

Tests: `dotnet run --project tools/ai-model-pipeline/portrait-trace-tests -c Release`
links the actual scope/storage class and checks nested unresolved identity,
per-camera isolation, exception cleanup, saturation and reset. It is not a live
Harmony/Unity test; actual passive captures remain a deployment acceptance gate.
