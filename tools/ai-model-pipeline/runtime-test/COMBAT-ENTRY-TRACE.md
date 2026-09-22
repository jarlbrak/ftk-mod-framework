# Combat entry trace

For an isolated runtime-test launch only, set `FTK_MODEL_TEST_COMBAT_ENTRY_TRACE=1`
in addition to the existing root/test environment. No command enables it in a
running process. It records at most 256 events to
`model-test-combat-entry.jsonl` and the helper log. Each record includes its session,
frame, sequence, and UTC timestamp; appended sessions remain distinguishable.

The observer patches entry and finalizer exit of native `CharacterDummy`
`InitDummyForCombat`, `ResetForCombat`, `_preAttackDummyReset`, and `CreateAvatar`,
plus the `EncounterSession.InitPlayerDummiesForCombat` loop. Records include
Harmony patch owners, class keys, source/clone identities, and cached versus
freshly obtained Animator component identities. The player-loop record includes
all current party actors and the number of registered player dummies. It does not
call Animator state or controller getters, create or repair objects, advance the
FSM, retain leases, or swallow original exceptions. It closes the trace file after
each record to preserve the last completed boundary if the process crashes.

Compare traces from the same combat-entry route in a vanilla-content control and
in the package trial. Native player initialization registers each dummy only after
its initialization returns; the intro coroutine is not awaited before the next
player or enemy initialization. A missing exit identifies a boundary requiring
investigation, not proof that its Animator or model is defective. Managed exception
text in the finalizer can explain a partial loop. A native crash may bypass every
finalizer. Patching can also change JIT behavior, so an instrumented successful run
must be compared with an uninstrumented run before declaring the problem fixed.

`test_combat_entry_trace_readonly.py` checks the diagnostic mutation boundary.
The helper build checks native signatures. Neither proves combat initialization or
reproduces the crash.

## Isolating the hook effect

Keep the same binary, save, entry route, and tutorial preparation between fresh
processes. `FTK_MODEL_TEST_COMBAT_ENTRY_TARGETS` accepts a comma-separated subset
of the five exact names listed above. Omit it to select all five; unknown,
whitespace-padded, or duplicate names fail before trace hooks are installed.
Start with `CreateAvatar`, then `InitDummyForCombat`, then the other individual
methods. A successful subset narrows the dependency; it is not itself a repair.

Set `FTK_MODEL_TEST_COMBAT_ENTRY_QUIET=1` with the same selected hooks to suppress
all per-call observation and file writes. Configuration and installed patch owners
are still recorded at startup. A quiet success with the same subset supports a
hook/JIT effect over per-call logging or component-inspection timing, but still
requires an uninstrumented comparison.

For the earlier finalizer-based framework build only, a separate, explicitly mutating test-process diagnostic is
`FTK_MODEL_TEST_COMBAT_LEASE_FINALIZER_SKIP=1`. It removes only the exact
`PlayerCombatMeshLeasePatch.Finalizer` method from `CharacterDummy.CreateAvatar`
after verifying its framework owner and signature. It records patch sets before
and after and checks that other patch counts remain unchanged. It also works with
tracing disabled, allowing a direct untraced control. It deliberately disables
the framework's inactive combat-clone lease retention fallback in that process;
use the no-custom-model control first. It does not modify production source or
files. Restart without this variable to restore the normal patch set. Do not
interpret a passing diagnostic with retention removed as a production-safe fix.

The corrected framework retains clones through a narrow post-assignment IL insertion,
so the finalizer-skip diagnostic intentionally rejects it: the old finalizer no
longer exists. Run corrected builds with both trace and skip disabled for the
production-path comparison.
