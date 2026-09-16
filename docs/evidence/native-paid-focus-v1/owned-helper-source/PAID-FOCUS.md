# Native paid focus (candidate; not live validated)

Adds `paid-focus-state` and `paid-focus-submit` to the isolated test helper. One paid slot for an already-selected normal Attack per native combat turn. This does not submit Attack or guarantee a hit/critical. It never writes focus counters, selects an action, invokes the payment callback, refunds, or falls back to a cheat.

Read state with `{id:<unique>,session:<exact nonce>,op:"paid-focus-state"}`. A usable response contains the full `identity`, a helper-issued `ticketId`, five-second/600-frame expiry and explicit guards. Immediately submit `{id:<different unique>,session:<nonce>,op:"paid-focus-submit",ticketId:<returned>,identity:<exact returned object>}`. Standard helper command/result protocol applies; do not use command.py's unsupported operation list or guess instance IDs. A future orchestration CLI must read and submit immediately. No CLI live acceptance is claimed.

The helper rechecks the exact owned root/nonce, on-disk Core/helper/native assembly identities, living SP hero, reciprocal combat dummy/CEL/current target, entered dungeon/room, native fight-order leader/attacker/turn counter, normal Attack profile and usable native button, real input focus/selected object, native Wait For Stance, no modal/popup, and available slot/payment state. It invokes native `uiBattleStanceButtons.FocusSlot` once. The permanent claim excludes target/button/profile/slots/focus counts: changing selection or issuing a new ticket cannot buy another slot this turn. Maximum32 claims per helper session. Native acquisition state is never created by this observer.

The operation is installed before invocation, including a synchronous native completion. `operation.status=submitted` only means the native request returned. Read `paid-focus-state` to observe the same `submissionId`. `payment-complete` requires an exact observed native `FocusSlotAnimateFinish` before/after pair: same ownership/turn/profile, callback baseline matching submission counts, available focus decreases1 and spent increases1, no interrupt or native exception. The native HUD decrements its animation counter *after* this callback, so payment evidence may have animationCount1. Wait for a fresh state with focusingfalse/animationCount0 and actual hero readiness before any separately authorized normal attack. Never retry a submission after uncertainty; a missing result remains unknown. An interrupted or otherwise failed callback retains its claim.

The raw callback evidence is historical. A later action change can naturally refund focus; report fresh counts rather than treating the old payment as currently available or buying another slot. An unavailable read may return prior snapshots with their original frames; it is not fresh proof. No native timeout cancellation/cleanup or direct FocusInterrupt is performed. Existing lifetime watcher stays armed and untouched.

Tests use the actual shipped Newtonsoft assembly and linked pure policy, covering expiry, identity/pin changes, blocked gates, target/button replay resistance, native exception claim retention, delayed/synchronous completion, interruption and exact debit. They do not prove Unity/UI integration. Candidate source/build is entirely under this scratch directory; deployed and frozen helper artifacts remain unchanged.

Reproduce offline validation:

```sh
dotnet run --project scratch/native-paid-focus-candidate/tests/PaidFocusTests.csproj -c Release
dotnet build scratch/native-paid-focus-candidate/runtime-test/RuntimeModelTest.csproj -c Release -p:TestGameRoot=/absolute/owned/scratch/game -o /absolute/new/candidate/output
```

The existing bridge's forced dungeon readiness may bypass native stance Initialize. The raw initialized flag is metadata only. This operation instead requires an actually visible, selected and focused native Attack UI. If those surfaces are absent, it refuses rather than creating them or calling focus against a stale profile.

Native weapon ID/instance and exact table reference are pinned; normal Attack profile must match its slots/skill/noFocus fields. Duplicate matching callbacks permanently invalidate payment acceptance; up to8 raw callback records are retained with explicit truncation/count. Late callbacks after an uncertain submission remain evidence only and cannot upgrade it to success.
