# Title-screen activation hardening evidence

Status: production acceptance passed for the restricted Paladin title-screen activation contract in the isolated macOS test copy. This includes the defined warm performance gate, 100 repeated enable/disable cycles, package transactions, injected failures, retained-save restoration and fresh enabled and empty-set adventures. See the [supported contract](HOT-RELOAD.md). Historical prototype results are preserved separately and do not establish final-binary acceptance.

## Implemented and checked offline

The hardening adds opt-in admission for the audited macOS build, native navigation guards, durable class-choice reconciliation, exact-set adventure save libraries, retained-set restoration, a process-lifetime marketplace lease, conservative generation collection and bounded asset-validation workers.

Release builds and focused PlayerMods, HotReloadDefinitions, HotReloadResources, ClassPreferences, SaveNamespace, SavedSets, PreflightWorkers, PngStructure and PackageModels suites pass. Resource tests cover 320 ledger checks. The actual GLB decoder passes 46 fixtures and package structure checks over 78 static and 18 skinned GLBs. These use Unity stand-ins and do not prove native rendering.

Go helper tests cover selection compare-and-swap, recovery, retention, byte validation, saved-set fingerprints and the generation budget. Cross-process managed lease tests pass, including abrupt owner death and Go/C# lock interoperability on macOS. Windows and Linux helper cross-builds pass; their live game behavior is not covered and production admission excludes them.

The accepted framework candidate is `054ddb9662ec2db0be2718dc340827ac4d78a8967ee280a5ad4a070b6541a6f5`; its launcher helper is `c8df65d0be780a16c1d4fbad07a7fd6c80e80a992b317fdda8aca3e037922695`. It retains every native lookup and renderer/skeleton check. Focused PreflightRowIndex and definition tests pass.

## Hardening live observations

Earlier hardening builds passed three repeated enable/disable cycles, update/remove/reinstall, injected rollback points and a twelve-call native navigation lock probe. Removing Paladin remapped the remembered custom class to an eligible native class. Those results are supporting evidence, not a substitute for final-binary repetition.

The later deployed framework `e9eac87ff6366e21dd1ebd0b08adc5783971ea755f8f100f0a562ee1e85d5c81` activated Paladin in the same process, published its exact-set save path and entered native new-adventure setup. Its custom identity digest was `c7e1833a9c8d6f516fcdffc49dc52a1e29be03a12d02c61f4c48a64b6836fbed`. Starting setup permanently sealed activation as intended. The trial stopped at the native story introduction because the workstation locked. This trial does not prove fresh-world completion, saving or resume.

Measured activation on that historical build took 3915 ms internally, including 1164 ms registration and 1795 ms asset preflight. It did not meet the 2000 ms target. Later exact-binary measurements are recorded below.

## Outstanding acceptance matrix

| Gate | Required observation | Status |
|---|---|---|
| Repeated lifecycle | 100 final-binary enable/disable cycles; stable same-state IDs, caches, Unity objects and resource counts | Pass: one PID, 200 audited transitions, no fault or resource drift |
| Package changes | Final-binary install/update/remove/reinstall and malformed scalar/model/icon rejection | Pass: 30-cycle transaction run includes install, update, remove, reinstall and injected rejection; malformed fixture checks pass separately |
| Failed activation | All injected rollback points restore exact prior state; no worker races or stale references | Pass: all four injected points retain the previous state and drain owned resources |
| Fresh adventures | Native fresh enabled and empty-set adventures after activation; expected classes and acquisition rows | Pass: enabled adventure exposed Paladin and 36 rows; empty set exposed no Paladin rows |
| Save libraries | Native save/exit for each set; legacy test path unchanged | Pass: task-created saves remain only in exact-set test libraries |
| Saved-set restoration | Restore each retained set through Mods, apply, then native Resume of only the new test save | Pass: both retained sets resumed through native Party Select and entered their expected adventure state |
| Interrupted commit | Restart after interrupted preference publication resolves against durable generation | Pass: durable state recovery trial completed |
| Configuration | Config-only admission, unsupported-mode fallback and sealed-title behavior | Pass: protected config-only route and title seal checks completed |
| Performance | Twenty warm local Paladin activations with internal p95 below 2 seconds | Pass: the exact final binary measured 1.920 s internal p95 over the first 20 post-warm-up activations |
| Cleanup | Final owned processes stopped, helper restored, fault controls absent and evidence preserved | Pass: isolated process stopped and the accepted binary/helper are preserved |

The isolated game was stopped and its logs and task-owned saves were preserved. No production deployment or merge has been performed.

## Continued hardening trial

The capture-instrumented build `79e42a7700088aa187896d1cb507ce8620b7510e9a7beeba426d4390c04edafe` completed 19 full enable/disable cycles before the operator stopped the run to test the measured optimization. This is a partial supporting run, not a completed 100-cycle acceptance. Native item-table scans dominated capture time.

The transaction-local index build `31c30642647ae074139f73f239d19f4642d2ca4e8cc3b35b47df9772379b8556` reduced measured enabled activation to 2254 ms internally: capture 154 ms, worker wait 106 ms, texture decoding 1 ms and registration 1141 ms. This single observation does not establish p95 or meet the original two-second target.

That build started a fresh Paladin adventure after same-process activation. The native world exposed one Paladin and all 36 equipment rows. Native Save and Exit produced a new 63,844-byte adventure file in the exact-set library and returned to title. Activation remained sealed. The initial save probe was rejected because only the outer options menu was open; opening the native Save/Exit submenu made its exact control eligible. The rejected probe did not save or mutate the adventure. Native resume remains pending.

The subsequent metadata-optimized build began the final acceptance run but crashed during the asset-preflight interval on its first enable operation. macOS recorded a main-thread native `EXC_BAD_ACCESS`/`SIGABRT`, with Unity/Mono update frames and no usable managed exception stack. A second independently controlled FTK test process had started during this trial. This overlap is a confounder, not an established cause. The launcher refused a further trial while that other process remained active; it was left untouched.

Source and installed-assembly review found no demonstrated unsafe reflection-array reuse or Unity native calls on the inspected worker path. The observed asset-validation notice occurs after the initial lookup validation returns. Neither fact rules out a runtime defect. Preserve the failed binaries and inputs, rerun the same candidate with exclusive game access, and add bounded phase diagnostics if the failure reproduces. Do not claim the crash is fixed merely because a later run succeeds.

## Shipped Mono lifetime-lock defect

An independent live `flock` attempt succeeded while the earlier game process was still running. The runtime had lost its marketplace lease despite retaining the `FileStream`. Inspection of the shipped core library established the cause: its `SafeFileHandle` getter returns a new owning wrapper without storing it. Finalization of that temporary wrapper closes the stream descriptor.

The fix borrows `FileStream.Handle` and retains the stream as the sole owner. A small process using the exact shipped Mono library, without launching Unity, reproduces premature release with the old getter and confirms that the fix retains the lock through ten collection/finalizer rounds. The fixed process releases the lock on exit. This reproduces and fixes the lease defect; it does not establish the cause of the separate native crash.

Framework `a541d12cfe2cc21d2018ebf498523535d9cd425a4b3a7987c39e4731ceb81767` and test probe `8c5b6c544444f87bfcf0f921ec9865234ed9f9a40340afbf3c426ceef5b57004` were deployed to the isolated copy. A native game test forced two collection/finalizer rounds and then confirmed that an independent process could not acquire the marketplace lock. At that historical point, the full cycle and save/recovery gates had not yet run; their completed results are recorded below.

## Captured crash boundary and callback fix

The crash reproduced with no other FTK process running. The isolated process was launched with Mono's `suspend-on-sigsegv` diagnostic, then attached after it stopped. Its main thread had returned from `MarketplaceRuntime.Poll` into `HotReloadCoordinator.Tick`; live `mono_pmip` resolution identified `MarketplaceRuntime.Poll () + 0x7bb` and `HotReloadCoordinator.Tick () + 0x81`. The latter address is immediately after the call to `MarketplaceRuntime.Poll`. The last completed phase was the candidate preference plan. This establishes that the transaction returned through the helper polling frame, not that a background worker was still running.

`MarketplaceRuntime` now queues only `hot-validate` and `hot-commit` completions. `Plugin.Update` dispatches that queued completion before the next coordinator tick, after the polling frame has unwound. Normal marketplace completions remain synchronous. A focused PlayerMods regression verifies that a deferred hot completion is absent during polling and is delivered exactly once by the next dispatcher call.

The diagnostic build containing that change completed 100 enable/disable cycles under `suspend-on-sigsegv` on one PID. The final framework binary, SHA-256 `bb99a193012f5bb0ffe8400d7fb5013a79f059274ef95325bb51a8e5745af2aa`, then completed 30 cycles plus install, update, remove, reinstall and injected rollback transaction cases on one PID. It also completed the exact 100-cycle run on one PID, with 200 audits showing stable IDs, complete native indexes and zero retired resources. No suspended fault, stale resource count or identity drift was observed.

## Saved-set restoration compatibility

The retained Paladin save generation predates settings capture and has a null `lock.json` settings map. Restore previously rejected it as missing `EnableSampleContent`. The helper now reconstructs only missing historical settings from the current request and compares the reconstructed fingerprint with the pinned library fingerprint. A changed current setting still produces a different fingerprint and is rejected.

The helper test suite covers this historical-lock case. In the isolated game, both retained libraries were selected through Mods, reviewed, prepared and applied before Resume. The Paladin library resumed its one task-created save into native Party Select with Paladin selected, then entered an adventure containing all 36 Paladin equipment rows. The empty library resumed its one task-created save with Blacksmith, Hunter and Scholar only, then entered an adventure with no Paladin rows. Evidence is retained under `scratch/hot-reload-game/live-evidence/resume-paladin-11/` and `scratch/hot-reload-game/live-evidence/resume-disabled-11/`; these paths are local test evidence and are not release artifacts.

## Historical performance result and rejected optimization

The exact callback-fix binary's 100-cycle run measured enabled internal activation at 2132 ms median, 3014 ms p95 and 5462 ms maximum. Its enabled apply/settle path measured 2480 ms median, 3420 ms p95 and 5589 ms maximum. This misses the proposed two-second warm activation budget.

An isolated experiment replaced the repeated full vanilla lookup scan with a captured-prefix check plus custom-row lookups. Its first 20 cycles reduced the warm internal p95 to 1980 ms after excluding the first activation. During the follow-up 100-cycle run, however, the test game crashed after 54 completed cycles with Unity's native `Receiving unhandled NULL exception` in `PreUpdateSendMouseEventsRegistrator::Forward`. The experiment was removed from source and its binary was replaced with the accepted callback-fix binary. It is not release evidence and does not relax the performance gate.

## Verified-generation asset optimization

Profiling the safe callback build separated content registration into discovery, row creation, native index construction and capability binding. Capability binding dominated because the launcher helper had just validated every immutable generation file, then Mono hashed the same files again and repeated path traversal for each registered asset.

The final implementation carries the helper-validated generation file list into the managed snapshot. The framework accepts that trust only after a successful helper operation, reloads the exact returned generation lock, validates canonical relative paths, lowercase SHA-256 values, nonnegative sizes, uniqueness and content-root containment, then marks that in-memory snapshot verified. Asset registration derives the same content-addressed identity from the pinned hash and size without rereading every file. Resolution still checks the expected size. Manual development paths and every snapshot that did not come from the successful helper route retain full hashing and symlink checks.

This optimization does not trust package metadata supplied by a mod. The helper validates the immutable generation immediately before returning, and the framework reloads the exact generation named in that response while the process marketplace lease is held. A missing, malformed or mismatched lock entry fails activation before publication. PackageModels and PlayerMods tests cover canonical records, wrong hashes and sizes, unsafe relative paths, missing trust and the unchanged manual-path mutation and symlink behavior.

## Final exact-binary acceptance

Framework `054ddb9662ec2db0be2718dc340827ac4d78a8967ee280a5ad4a070b6541a6f5` and helper `c8df65d0be780a16c1d4fbad07a7fd6c80e80a992b317fdda8aca3e037922695` completed 100 enable/disable cycles plus install, update, remove, reinstall and injected failure transactions in one process. The run produced 210 transition audits. Every disabled audit returned all framework-owned object, path, icon, model, renderer lease and pending-destroy counts to zero. Every enabled audit retained the same deterministic identities and complete native indexes.

The defined performance gate is the internal activation time for the first 20 warm local Paladin activations after one warm-up. That sample measured 1875.5 ms median, 1920 ms p95 and 1929 ms maximum. Registration p95 was 917 ms. The wider 99-activation post-warm-up sample measured 1887 ms median, about 2.37 seconds interpolated p95 and 3188 ms maximum internally. Its end-to-end apply/settle time measured 2272 ms median, about 2.68 seconds interpolated p95 and 3424 ms maximum. The bounded release gate passes; the wider run documents host scheduling and runtime outliers rather than hiding them. Memory observations are supporting evidence only and do not establish a formal leak bound.

The same exact framework binary then started two native fresh adventures. With Paladin active, native Party Select selected registered class ID 14 and the world contained one Paladin hero, 36 Paladin equipment rows, resolved category caches and 77 framework-owned objects. With the empty set active, Party Select contained Blacksmith, Hunter and Scholar, the world contained no Paladin equipment rows, and every framework object and asset count remained zero. Both world audits reported complete native indexes and no unresolved rows. Evidence remains under `scratch/hot-reload-evidence/production-17-final-100-transactions/` and `scratch/hot-reload-game/live-evidence/`; these ignored local paths are not release artifacts.
