# Native reporting menu proof

Disposable, opt-in Spec A1 evidence tooling, separate from the shipped framework.
The invariant is that a framework-owned options submenu returns through the native
saved focus lifecycle while Options retains pause ownership. The existing native
Report Bugs button now opens this proof child. Its label,
layout, navigation, and serialized listeners remain intact; a narrow handler prefix
suppresses the old close-and-open-legacy-form path. This does not implement
reporting, capture, or submission in its original menu-only mode. The additional
framework integration mode below exercises the actual reporting implementation.

Build with `dotnet build tools/reporting-proof/FTKReportingProof.csproj -c Release`.
Override `FtkManagedDir` to the installed game's managed directory if required.
Build `tools/reporting-proof/steam-guard/SteamGuard.csproj` separately, setting
`TestGameRoot` to the disposable root containing BepInEx core. Install only its
`FTKReportingSteamGuard.dll` under that root's `BepInEx/patchers`. It rewrites the
two exact Steam wrappers to return false in memory, before game code loads. A
session receipt is required by the runtime plugin. The guard exits on failure;
it never saves a modified game assembly. Run its focused tests with
`dotnet run --project tools/reporting-proof/tests/SteamGuard.Tests.csproj -c Release -- <installed-firstpass-dll>`.

Only deploy the resulting proof DLL to the independently prepared disposable root.
Do not launch a second copy of the ordinary installed game for this proof.

On macOS, prepare a fresh copy with Python 3.11 or newer and `UnityPy==1.25.3`:

```sh
python tools/reporting-proof/isolate_game.py --source /path/to/installed/game \
  --destination /path/to/scratch/reporting-game --token uniqueproof1
```

The destination and profile identity must not already exist. The helper copies
the app and BepInEx core, changes only the copied application identity and
PlayerSettings company/product, and verifies source hashes. It does not launch.
Supply a dedicated `-logFile`, windowed mode, and record the owned process ID
when launching that copy's `run_bepinex.sh`. Verify the fresh readiness receipt
before commands. Stop only that exact owned process after the trial.

The plugin requires these environment variables:

- `FTK_REPORTING_PROOF=1`
- `FTK_REPORTING_ROOT`: exact BepInEx game root, directly under a `scratch` directory
- `FTK_REPORTING_BUNDLE`: actual bundle ID, beginning `com.ftkmf.reporting.proof.`
- `FTK_REPORTING_SESSION`: unique, nonempty run identity

Unity company must be `FTKReportingProof`; product must equal the final bundle ID
component. Persistent data must use that company/product or exact bundle identity.
Root symlinks are rejected. The isolation helper prepares the independent bundle;
its source installation is never a deployment destination. Steam initialization and
restart wrappers are suppressed before game initialization. Save paths point inside
`proof-saves` in the disposable root. Isolation failure exits the disposable process.

After `proof-output/ready.json` appears, atomically replace `proof-command.json`
inside the disposable root with a command such as:

```json
{"session":"unique-run","id":"state-1","op":"state"}
```

Each ID is at most 64 alphanumeric/hyphen characters and is consumed once. Requests
are limited to 4096 bytes. Responses appear in `proof-output/<id>.json`.
Operations are `state`, `screenshot`, `click`, and `cancel`. `click` additionally
requires `path` equal to one exact active button path returned by `state`; ambiguous
paths fail. `cancel` only closes the reporting child or its owning Options menu.
Screenshot waits for end of frame and writes `<id>.png`. State includes all scene
buttons (capped at 600), labels and navigation, focus ownership and selected object,
Options visibility, pause/time scale, reporting visibility, and initialization errors.
Use the native title Options button, then the existing Report Bugs button whose
exact path is returned as `reportingEntry`, then
`ReportingProofBack` to exercise the native submenu route. `setupGate` reports
existing map/character/camera readiness without advancing any native gate.
Prefer the normal creation controls for solo input proof. The framework Agent
bridge's `start_run` fixture can report in-world readiness before stable input
ownership; a delayed character-camera coroutine can then overwrite focus.
Its successful return is not proof that Options will restore usable gameplay
input. If used for a narrower fixture, assign and verify a unique listener port.

The `uiOptionsMain.OnReportBugs` prefix always suppresses the legacy Options
handler in the armed disposable proof and routes a matching owner into `Open`.
The `uiOptionsMenu.Show` postfix binds the title/session scene's native button;
no additional reporting entry is cloned, no navigation links are changed, and no
prefab is mutated. Missing binding prerequisites visibly disable the native button
and report `injectionError`; a caught open failure also disables it. A later
successful Show preflight restores only that same proof-disabled button to its
recorded interactable value. Failed binding clears the stale owner/entry. The rest of
Options remains available. State exposes `legacyHandlerSuppressed`, `reportingEntry`,
and `reportingEntryInteractable`. A normal entry click increments the suppression
counter; closing through `ReportingProofBack` must not increment it because all
cloned persistent Back listeners are disabled before its close callback is installed.

Installed IL has no managed caller of `OnReportBugs`; its native button uses a
serialized event. The legacy handler calls `OnClose` then `SerializeGO.ShowBugForm`.
Independent `FTKVersion.OnClick` and `uiStartGame.RequestClientBugReport` also call
that legacy entry. An exact prefix on `SerializeGO.ShowBugForm(Action)` suppresses
the old capture coroutine before its flag mutation, screenshot, form, and callback.
A second exact prefix on `uiSystemDialog.ShowBugForm(Action)` suppresses direct form
entry, including its zero-argument wrapper. Neither callback is invoked and neither
route opens the framework child; unrelated dialogs and RPC state are untouched.
State records `legacyCaptureSuppressed` and `legacyFormSuppressed` separately.
The RPC method's preceding group-ID assignment still occurs; this proof does not
establish network behavior or remove unrelated native diagnostic code. During a
normal Options click, only `legacyHandlerSuppressed` should increment because the
old handler body never reaches either downstream entry. Those other source-backed
routes require separate live qualification.

For bounded rollback evidence, open Options normally and issue each fixed fault
point separately:

```json
{"session":"unique-run","id":"fault-blocker-1","op":"fault-open","point":"after-blocker"}
{"session":"unique-run","id":"fault-focus-1","op":"fault-open","point":"after-focus"}
```

`fault-open` requires this session's guard receipt and owned, visible, unblocked
Options focus with no popup or open proof child. It invokes the actual `Open`
path and throws only at the selected fixed point: immediately after blocker
activation or after a verified successful child focus transfer. The injection is
cleared in `finally`, including unexpected failures. No arbitrary method or fault
point can be supplied. Navigation locks can prevent reaching a fault, which is
reported as `expectedFaultObserved: false`, never a passing rollback.

The response contains primitive before/immediate/after states, the expected fault message,
`expectedFaultObserved`, `injectionCleared`, and `rollbackRestored`. The last flag
compares focus ownership, selected control, first modal, the parent's saved-state
reference and saved focus, blocker, child visibility/saved state, Options visibility,
and pause/time scale. The command channel stays busy while observing native frames
for up to one unscaled second (also capped at 120 frames); `observationFrames` and
`observationSeconds` describe the delayed check. No manual input/update call advances
the game. The native `SetFocus` clears current selection and queues its replacement
for later native updates, so a synchronous null selection alone is not proof of a
rollback bug. The immediate state remains visible alongside the settled result.
Invalidated owner references fail the comparison rather than continuing mutation.
State also exposes `firstModal`, `parentSavedFocus`, and
`openFaultArmed`. Unexpected exceptions use the normal error response; request a
fresh `state` afterward. These observations do not qualify all controller maps or
physical input.

After **each** passing fault, use the normal reporting entry `click`, child
`cancel`, reporting entry `click`, child `cancel`, and Options `cancel` sequence.
Verify the child opened each time, parent focus and pause survived each child
close, then the original pre-Options focus and time scale returned after root
close. Reopen Options normally for the next fault. Do not continue mutation after
a failed rollback; retain its state and stop only the owned disposable process.
Do not repeat `cancel` after root close: native `Close` can invoke `OnClose` even
when the focus is already lost. These probes do not exercise scene destruction.

For a bounded real-browser focus trial, open the report child normally, record its
settled state, and issue:

```json
{"session":"unique-run","id":"browser-focus-1","op":"browser-focus"}
```

The command requires this session's guard receipt and the visible owned report
child holding native focus, with the Options blocker active and no popup. It calls
`Application.OpenURL` with the fixed read-only repository issues page:
`https://github.com/jarlbrak/ftk-mod-framework/issues`. There is no configurable URL,
query, issue form, or report payload. `browserRequestStatus: requested` means only
that the native call returned; it is not browser navigation or submission success.
A thrown exception uses the normal error response.

State exposes `applicationFocused`, `applicationFocusLosses`, and
`applicationFocusGains`. The boolean starts from Unity's current focus property;
counters count this plugin's subsequent native focus callbacks. These observations
do not mutate gameplay or input. Observe the actual browser page separately, return
to the exact owned proof application, and request a fresh state. Require observed
loss and gain callbacks and compare settled child focus, selection, first modal,
parent saved focus, blocker, and pause/time scale with the pre-browser baseline.
Then close the child normally and verify Options/root close recovery. An external
browser that stays in the background does not prove application-focus recovery.
This trial does not establish GitHub form prefill, authentication, attachment,
submission, or cancellation behavior for the reporting feature.

The file commands supply programmatic native-call evidence. They do not qualify
mouse, keyboard, or controller input; separately observed targeted keyboard trials
are recorded in the public evidence document. Outstanding gates include browser recovery; combat/co-op; Windows/Linux; scene transitions;
or every interrupted-focus recovery path. Builds are only offline evidence. Do not
publish a feasibility go decision from this proof alone. Keep generated captures,
saves, game binaries, and decompiled source out of git.

The disposable offline fixture returns unavailable (`false`, with zero/false out
values) from the game's Steam-specific `GetStatValue` and `GetAchievementValue`
adapters. Those installed methods call Steam without checking initialization;
their write methods already refuse when Steam is unavailable. Read counters appear
in state output. This fixture cannot establish authenticated Steam, cloud, platform
statistics, achievements, migration from an existing profile, or co-op behavior.

## Framework integration mode

Set `FTK_REPORTING_NATIVE=1` in the same guarded disposable setup and install the
framework under test into that disposable copy. The proof plugin retains its
isolation and observation helpers but does not install its own Report Bugs or
legacy-form interception patches. The framework's ReportingMenu and ReportingPanel
own that route. Do not mix menu-only suppression counters or fault injection
results with evidence for this mode.

The current direct-send panel offers one optional description, a diagnostics toggle,
an expandable payload preview, and Send report. It routes through the trusted native
helper to the repository-owned Railway service. Error and restart offers require
explicit Send; merely opening or dismissing them uploads nothing. Retry keeps the
same frozen bytes and report ID. See [Reporting](../../docs/REPORTING.md).

Earlier evidence below and in historical records exercised browser handoff, local
export, and multi-field narrative editing. Those paths do not qualify the direct-send
implementation. Do not reuse their success claims for service submission.

`state` exposes `nativeReporting` and `nativeReport`, including tracking availability,
fixed status text, pending/report/capture identities, draft readiness/saved identity,
metadata inclusion, metadata and visible panel text.
These outputs can contain locally collected metadata and must remain disposable
private evidence. `text` requires the framework panel to own focus and exactly one
active InputField. Supply `text`, or `oversized: true` for the fixed 8,193-character
ASCII rejection probe. `quit` records state and requests `Application.Quit` in the
owned disposable process. These are programmatic tests, not physical-input proof.

Use [the integrated restart evidence record](../../docs/evidence/reporting-restart-proof.md)
for the tested build, observations and remaining gates. Test normal quit, owned
forced termination, prior/current provenance, deferred offers, durable dismissal,
review invalidation and draft reopening separately. Never infer those outcomes from
a successful build or the historical menu-only trials.
