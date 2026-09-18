# Six-pair bank sampler runner

`run_kraken_bank_suite.py` coordinates only the approved `24c9596a…` helper's
input fixture: appear, damaged, damaged-heavy, death, death-light, then intro.
Each scenario receives one first and one repeat request, followed by independent
numerical verification before the next scenario. The five existing scenarios
require241 frames and Intro requires361. It never stages enemies, starts a run,
advances Ready, clears dialogs, restarts or deploys. It uses local helper files;
no bridge port or HTTP action is involved.

After the operator deploys the reviewed helper, provide the exact current session,
current catalog hash, and current deployment receipt plus its SHA256. The receipt
must name the exact owned root and contain `new` relative-path SHA pins including
the catalog and all three plugin DLLs. The runner checks every receipt entry and
both native source hashes; no prior catalog/session is embedded in the runner.

First run offline validation, which makes **no writes or helper calls**:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/runtime-test/run_kraken_bank_suite.py \
  --root /absolute/project/scratch/mirewarden-game \
  --output /absolute/project/scratch/kraken-two-bank-six-pairs-v1 \
  --session <exact-current32hex> \
  --catalog-sha256 <exact-current-catalog64hex> \
  --deployment /absolute/project/scratch/current-deployment-receipt.json \
  --deployment-sha256 <exact-receipt64hex>
```

Once validation succeeds and the operator has sole helper control at strict native
Ready, repeat the same command with `--execute`. The default per-operation timeout
is600 seconds; overrides must be finite and positive. The runner preserves an
exclusive session claim and refuses an existing output directory or unresolved
same-session helper command. Do not run another helper client concurrently; the
claim coordinates copies of this runner, not arbitrary clients.

Execution records measured on-disk binary/source pins (not loaded-memory proof),
raw deployment/session files, immutable request/result files and an append-only
journal. Every delivered command has a journaled intent before the atomic rename.
Readiness must preserve root/session, the exact Ready snapshot and queued dungeon
metadata. Every fixture's native dungeon instance pin must also remain identical.
File/source pins are rechecked before delivery, when a result arrives, and before
verification. Pending polling only reads session/result files. Result bytes are
copied without rewriting their JSON.

Any timeout, identity change, helper rejection, failed readiness or numerical
mismatch stops the sequence. No command is resubmitted. The journal and stopped
report retain the pending command ID and completed pairs. After an uncertain
submission, the operator may read the existing result file later; the runner has
no automatic resume, retry, claim removal or next-scenario path. It does not mark
partial results successful or silently skip a difficult case.

Each scenario folder contains first/repeat raw reports and requests, readiness
observations, the six-pin `kraken-modern-input-mixer-v1` manifest, and full verifier
output. A complete suite writes `suite-result.json`. This is owned input-source
recertification only: no endpoint/skin, attack callbacks, complete 15-state support,
real-avatar lifecycle or production adapter acceptance follows.

Offline tests use temporary files and simulated helper results. They verify exact
explicit deployment/catalog/session guards, uncertain no-retry, pending-operation
refusal, raw result identity/bytes, changed pins, invalid timeouts and Ready changes.
They do not establish Unity/native execution.
