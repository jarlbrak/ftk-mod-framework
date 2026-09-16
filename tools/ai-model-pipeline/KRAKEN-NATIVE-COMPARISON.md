# Native Kraken Animator comparison

`compare_kraken_native.py` reads evidence and emits requests/reports only. It never sends commands, deploys files or changes the game. Run it with the pipeline Python environment containing NumPy and UnityPy.

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/compare_kraken_native.py \
  --manifest /absolute/path/to/kraken-comparison-manifest.json \
  --output /absolute/path/to/selection.json
```

Only after capturing the actual modern `krakenHead` profile, assemble a manifest with these exact fields:

```json
{
  "version": 1,
  "session": "32-hex-character-helper-nonce",
  "profileKey": "ftkmf_modeltest_probe_krakenhead",
  "rendererInstanceId": -123456,
  "evidence": {
    "assets": {"path": "/absolute/path/to/resources.assets", "sha256": "actual-sha256"},
    "mapping": {"path": "/absolute/path/to/enemy-rig-mapping-reproducible.json", "sha256": "actual-sha256"},
    "profiles": {"path": "/absolute/path/to/frozen-model-test-profiles.json", "sha256": "actual-sha256"},
    "registration": {"path": "/absolute/path/to/frozen-model-test-registration.json", "sha256": "actual-sha256"},
    "journal": {"path": "/absolute/path/to/journal.jsonl", "sha256": "actual-sha256"},
    "inventory": {"path": "/absolute/path/to/full-enemy-inventory.json", "sha256": "actual-sha256"},
    "capture": {"path": "/absolute/path/to/native-capture.json", "sha256": "actual-sha256"},
    "deployment": {"path": "/absolute/path/to/deployment-receipt.json", "sha256": "actual-sha256"}
  },
  "binaries": {
    "framework": {
      "path": "/absolute/path/to/frozen-FTKModFramework.dll",
      "sha256": "actual-sha256",
      "deploymentPointer": "/actual/receipt/frameworkHashField",
      "journalPointer": "/0/data/actualFrameworkHashField"
    },
    "helper": {
      "path": "/absolute/path/to/frozen-FtkRuntimeModelTest.dll",
      "sha256": "actual-sha256",
      "deploymentPointer": "/actual/receipt/helperHashField",
      "journalPointer": "/0/data/actualHelperHashField"
    }
  }
}
```

These are placeholders, not accepted values. Paths can instead be relative to the manifest directory. The JSON pointers must identify actual SHA256 fields in the deployment receipt and journal identity record. The journal is parsed as an array of JSONL records, so its first pointer segment is a zero-based record index. The tool verifies each file hash, both binary hashes and both corresponding document pins. It cannot query loaded process memory; preserve the real initial deployment/process identity evidence, rather than inventing an identity record after capture.

The profile must have `baseEnemy: krakenHead`, no resource override, the exact mapped combat fingerprint for source renderer 121035, and exactly one `kraken2` renderer assignment. Registration and journal profile/session evidence must match. Full inventory supplies `animator.controller`, native bind matrices, mesh and bone signature. Capture frames must preserve the same renderer, owner, CEL, Animator and bone identity. `animator.name` is an Animator GameObject name, not the controller name. Forced-state `play` captures are rejected; use `capture` with `observed-runtime` provenance.

The selector reads controller 5973 and clip metadata from the hash-pinned source asset. It accepts only single-node states with speed 1, no speed/time/mirror/cycle parameter, zero offsets, matching state/clip loop flags and no mirror. In the inspected controller, IDLE/INTRO loop; attack/damage/disappear states clamp. `Snapshot.playing[].clipSeconds` is clip duration, not playback time. Selected seconds are computed from the verified state's normalized phase and quantized to Unity float32. The output records original normalized time, loop index or clamping, and exact requested seconds. No offsets or crossfade times are guessed.

Only positive-time, non-ragdoll, enabled frames with one layer, one clip, weight 1 and no transition qualify. Each selected frame must also have stable same-state neighbors and increasing game time. There must be at least two distinct explicit times per clip. Selection is evenly reduced to at most 32 times per clip. Appearance is excluded. The generated `requests` are data for the existing `kraken-sample-fixture` operation with `method: clip-playable`; run them through the guarded helper separately at Ready in the same session.

Once those exact sample results exist:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/compare_kraken_native.py \
  --manifest /absolute/path/to/kraken-comparison-manifest.json \
  --samples /absolute/path/to/attack-samples.json \
  --samples /absolute/path/to/idle-samples.json \
  --output /absolute/path/to/comparison.json
```

Inputs are revalidated and selection is recomputed. Sample session/source/controller/clip, initialization, manual controller-free graph configuration, cleanup, count and exact float32 time order must match. Output is `samples_required` until all selected clips are supplied, `reference_mismatch` if any comparison fails, or `reference_match` when every supplied native reference comparison passes. Each clip is independently reported; a capture with only idle does not validate attack/damage/disappear.

### Renderer-destroyed capture prefixes

By default, any failed capture is rejected. A manifest may explicitly set `"allowRendererDestroyedPrefix": true` to compare its retained prefix when the exact helper termination is `System.InvalidOperationException: Renderer destroyed during capture.` followed by one `RuntimeModelTest+<Capture>d__...MoveNext` stack frame. No other error, additional/inner exception, nonboolean opt-in, empty prefix or full-length failed capture is accepted. Requested duration/rate and frame counts must stay within helper bounds; retained frame IDs must increase and the result boundary must follow the last retained frame. All retained identity/provenance checks still apply.

This does not repair the original capture or set its `ok` flag to true. The report preserves the raw error, failed status, requested/retained frame counts and frame boundary in `captureBoundary`, with `completeCapture: false`. Output statuses are separately named `prefix_samples_required`, `prefix_reference_mismatch` and `prefix_reference_match`. The stable-neighbor rule still excludes the final retained frame. Matching a prefix proves only those selected earlier poses. The error category alone does not prove native cleanup caused destruction or that the full death animation was captured; those claims need separate evidence.

The comparison reconstructs local TRS matrices and composes the verified source hierarchy in common CEL-root coordinates. This excludes scene placement and the outer CEL object's scale. Per-local and composed matrix errors are both reported with tolerance 1e-4. The old adapter is not applied, and a match does not prove transitions, crossfade, runtime retargeting, native root-motion behavior or artistic acceptance. Bone names/order and bind matrices establish the source palette; the snapshot does not separately report per-bone runtime parent paths.

Tests:

```sh
scratch/model-venv/bin/python -m unittest discover -s tools/ai-model-pipeline \
  -p test_compare_kraken_native.py -v
```

The 21 offline tests cover clocks, ambiguity exclusions, sample limits, matrix matching/mismatch, exact times, nonce/configuration/cleanup requirements, file pins, profile/controller/renderer provenance joins, and the strict partial-prefix allowlist. The source reader was also run against the installed assets and resolved all 15 native states without unsupported clock settings. These software checks do not themselves supply native-capture comparisons; those require the separately hash-pinned runtime evidence.
