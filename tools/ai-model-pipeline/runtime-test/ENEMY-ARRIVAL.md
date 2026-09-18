# Passive native enemy arrival capture

This optional helper records one already staged native enemy's arrival and first action. It does not change initiative, AI, abilities, health, visibility or native animation. The broader HUD lifetime observer is excluded from this candidate.

At strict native Ready, first successfully submit the existing `stage-next-enemy` for an eligible Enemy slot. Then submit one command through the ordinary isolated file protocol:

```json
{"id":"NEW_32_HEX_ID","session":"EXACT_CURRENT_NONCE","op":"enemy-arrival-arm","enemy":"EXACT_REGISTERED_PROFILE_KEY","catalogSha256":"EXACT_CATALOG_SHA256","rendererPath":"EXACT_CEL_RELATIVE_PATH"}
```

The response's distinct `captureId` is the sole later capture result ID. Preserve the immutable arm response. Only after an accepted arm may the operator separately submit the existing native `ready` operation once. This command never submits Ready or any other game action. Poll the predetermined capture result file; after an uncertain Ready submission, do not retry Ready or arm another capture. No ordinary recorder or other helper coroutine may run concurrently.

The fixed request is `seconds:10`, `fps:12`: a nominal target of 120 samples, with width at most 960 and preserved aspect ratio, `fixedStep:false`. Capture works toward that sample count. Readback and encoding overhead can substantially extend wall time and affect game-time advancement; neither 10 actual wall-clock seconds, 10 game seconds nor unperturbed native timing is guaranteed. Use the recorded per-frame timestamps to determine the observed spans. The unconsumed arm expires after 90 real seconds. The first PNG represents its recorded end-of-frame, not necessarily the first visible spawn frame.

For example, completed capture `3dcae051935d428bb2a62178954f8a67` contains 120 samples whose first-to-last timestamps span 64.234680 wall-clock seconds and 6.283203 game seconds, despite the nominal 10-second request. This timing measurement does not establish full visual or animation coverage.

`enemy-arrival-state` takes only the standard id/session/op envelope and rechecks the arm's root, nonce, row, party, catalog, every GLB/PNG (including all material-slot textures), and Core/helper/content/game assembly pins. `enemy-arrival-clear` cancels only an unconsumed arm; it cannot stop a running capture or mutate native objects. A terminal ticket cannot be reused or retargeted within that helper session.

Arming pins the already staged sole row, native Ready dungeon/level/room, exact registered profile, source prefab and living party FIDs. Native init prefix/finalizer correlate the exact dummy and fresh reciprocal CEL. The helper's next Update requires that same FID in the real encounter map, live HP, a single target, exact custom mesh identity and bone/bind palette, and actual acquired/applied lease membership for its owner, target renderer and mesh. It never calls retention/pruning methods to create this proof.

Passive events are bounded to 32. Actual `PlayAttackSequence` entry copies all supplied damage-info scalars, including explicit null auxiliary inputs; its finalizer records completion, exception and post-attack health modifier. Actual synchronous `TakeSecondaryDamage` records arguments and before/after HP/alive state. Native exceptions are returned unchanged. An attack observed before launch stops with `missed-before-action`; a later attack before the first PNG remains explicitly marked. `currentAttackInfo` and `postAttackHealthMod` in ordinary snapshots may be stale fields from a reused dummy and are never treated as new attack proof.

Each frame adds direct CEL child visibility (maximum 64) and existing renderer visibility (maximum 64), alongside the normal pose/animator data. Overflow or telemetry errors invalidate the result permanently. Native renderer destruction preserves the ordinary raw error and any completed frame prefix/PNGs. Cancellation disposes the inner capture coroutine so its temporary framebuffer resources and timing cleanup run; partial sampled records are retained. There is no action retry, hero action, KillSingle, loot collection, encounter progression or process control in this feature.

Binary metadata distinguishes loaded assembly identity from measured on-disk SHA256; it does not claim a hash of loaded memory. The candidate requires the source-audited game Assembly-CSharp SHA256 and isolated helper guards. No native game assets are exported by this observer apart from the already authorized scratch runtime screenshots.

Validation before a live trial: helper net35 build; 36 linked shipped-Newtonsoft policy assertions; four integration tests including executing the exact extracted capture-wrapper method with a fake inner iterator to check normal completion, cancellation, error disposal and immutable existing results. These are source/offline checks, not Unity event-timing or native self-sacrifice acceptance. The required live gate remains actual selected proficiency plus synchronous health-event evidence, original PNG chronology, and review of the available frames.

## Reusable once-only runner

From an existing eligible native Ready Enemy slot, use `arrival_case.py`. `--profile-sha256` means the SHA256 of the **entire current model-test-profiles.json**, not a single descriptor. Supply the current helper nonce and the exact profile renderer path:

```sh
python3 tools/ai-model-pipeline/runtime-test/arrival_case.py \
  --root /absolute/project/scratch/owned-game --port 8788 \
  --session EXACT_32_HEX_NONCE --enemy EXACT_REGISTERED_KEY \
  --renderer-path EXACT_CEL_RELATIVE_PATH \
  --profile-sha256 EXACT_64_HEX_CATALOG_SHA256 --level 0 --room 2 \
  --capture-timeout 360
```

The runner first waits read-only for the requested strict native Ready slot. It creates its permanent once-only claim only after that preflight succeeds, then requires one living hero, stages once, applies the existing 999-HP Ready fixture, arms once, and submits native Ready once. Fresh Ready/party checks precede each dependent setup step. `arrival-case-result.json` preserves the `readyPreflight` receipt. A preflight timeout means no arrival claim, staging, arm or gameplay action was issued. It reuses Runner registration freshness, asset/binary/session guards and additionally pins its Python sources and registration report. Legacy-singular profiles are refused: arrival's native bound-lease check currently expects the explicit plural path. Registration or profile metadata is not a plural decode/binding acceptance claim; the native arm/capture owns that check.

An exclusive session claim is retained after any failure that occurs after the preflight claim. Do not rerun to recover an uncertain stage, arm or Ready. After Ready, no further mutating helper command, hero action, Collect, restart or next-room operation is submitted. The predetermined result file is observed under unchanged pins. Optional `--post-ready-level` and `--post-ready-room` arguments permit bounded read-only `fixture-state` polling after a terminal capture; `--post-ready-timeout` controls only that observation. A missing post-arrival Ready changes the terminal status to `terminal_capture_observed_post_ready_pending` and never retries gameplay. Capture and Ready observation timeouts accept finite values from1 through 1800 seconds. A capture-timeout report has `terminal:false`, retains `rawCapturePath`, and does not mean capture failed or ended. Any later evidence must be read from that same ID under the original pins, not by resubmitting this runner.

`arrival-case-result.json` retains the preflight receipt, immutable arm, Ready outcome or uncertainty, optional post-arrival Ready observation, journal path, exact terminal raw SHA256/ok/error and retained frame count. Raw files are never rewritten. Terminal means a matching result file was observed, not full frame/animation/art acceptance. Timing is the nominal120-sample request described above; actual wall/game spans must be measured from frames. The caller remains the sole helper writer throughout the invocation.

`run_execution_queue_route.py` dispatches a queue route whose selected workflow is `passive_enemy_arrival`. Its native setup can end in either an already observed hero-turn boundary or a pre-hero-turn self-removal boundary. When the hero acts first, the orchestrator records one native Pass before starting arrival observation. The target itself is not a reliable setup clock: retained Tamarind executions reached different native boundaries, and neither armed arrival. Preserve those stopped records. The dispatcher is offline-tested, while a fully completed orchestrated passive route still requires its own live evidence before it can be called validated.
