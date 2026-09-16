# Once-only native trap runner

`trap_case.py` wraps the reviewed helper's `trap-state`/`trap-submit` protocol. It uses only the isolated command files; no bridge port, generic action, unsupported command.py operation, or UI automation is involved.

```sh
python3 tools/ai-model-pipeline/runtime-test/trap_case.py \
  --root /absolute/project/scratch/owned-game \
  --session EXACT_32_HEX_NONCE --option Proceed --level 0 --room 3 \
  --catalog-sha256 EXACT_MODEL_TEST_PROFILES_SHA256 \
  --helper-sha256 EXACT_DEPLOYED_HELPER_SHA256 --outcome-timeout 180
```

Only `Disarm` or `Proceed` is accepted. Root must be an absolute nonsymlink isolated directory immediately under `scratch`. Catalog/helper are explicit expected hashes; the current Core binary and Python sources are measured and held unchanged. These are on-disk hashes, not loaded-memory hashes. Helperac9b0278 or a source-compatible reviewed candidate is required.

The runner creates an exclusive session/level/room claim before reading a ticket. It submits the exact identity/ticket immediately if the entire observation roundtrip took less than four seconds; the helper independently requires its native five-second/frame TTL and all actual trap/hero/UI guards. One submission only. Changing option cannot bypass the local claim. Native helpers preserve an additional trap-room claim.

After confirmed `submitted`, the runner polls only `fixture-state`, requiring the same sole living hero and session, expected level and room bounds, and actual strict Ready at room+1. Native trap disappearance is normal; it does not keep waiting for a usable trap-state. Fixture-state does not expose a native dungeon instance ID, so final evidence proves its observed hero/session/level/room Ready boundary; it does not assert an independently measured final dungeon object ID. The operator must remain the sole helper writer and perform no concurrent game operations.

Every command has an exclusive request file in `trap-case-UUID`, its predetermined native result path and journal entry before delivery. Result IDs/nonces and raw hashes are preserved. A helper timeout or malformed/mismatched result retains that exact pending ID and immediately stops all further submissions, including observations. Never rerun the wrapper or try a different option to recover uncertainty. An operator may later inspect only that preserved result under the original pins; no automatic action inference/retry occurs.

`trap-case-result.json` distinguishes `native_ready_observed` (`terminal:true`) from `observation_timeout_outcome_pending` (`terminal:false`) and command uncertainty (`pending` retains exact result path). Native rejection/error text is preserved in the command journal and submission result. Native roll failure, damage or death is not overridden. A failed living-owner guard stops observation without labeling the trap successfully traversed. No native roll, art, lifetime or end-to-end CLI acceptance is claimed by the offline tests.
