# In-game reporting feasibility

> Historical browser-handoff design and evidence. The current direct-send flow,
> automatic error capture and sharing disclosure are described in [Reporting](REPORTING.md).
> Its service contract lives in [reporting-service](../reporting-service/README.md).
> Browser-only, logs-off and mandatory review-step requirements below are superseded
> for that flow. Historical proof results do not qualify the new transport.

Discovery for [epic #161](https://github.com/jarlbrak/ftk-mod-framework/issues/161)
and [Spec A, #162](https://github.com/jarlbrak/ftk-mod-framework/issues/162).
This is a design and evidence record, not a shipped reporting feature.

## Invariant and observable outcome

Opening and cancelling framework reporting must preserve the native options
menu's pause and input ownership, route the existing Report Bugs action to the
framework reporter instead of the legacy game form,
and make no save, content, or multiplayer changes. A player must be able to
open, cancel, reopen, and return from an external browser without losing input.
Opening the editor automatically collects bounded allowlisted metadata for the
report, with optional logs and review before external sharing. A restart after
a detected unexpected exit offers to review a report using preserved prior-session
metadata. See the [reporting contract](REPORTING-CONTRACT.md#automatic-metadata-and-restart-offers).
Opening a browser never proves submission.

## Native button ownership decision

The user approved taking over the existing **Report Bugs** button on 2026-09-23.
This supersedes the earlier requirement to retain the legacy reporter alongside
a separate framework/mod entry. The new entry keeps the native caption, placement
and navigation. Its action opens the framework reporter, with the destination and
framework/mod scope explained inside the panel. The original handler must not
close Options, unpause the session or open the legacy form.

Published planning text still needs this explicit reconciliation before closure:
epic #161's planning note, Spec A #162 FR-1/simplicity constraints, and Spec C #164
background/FR-1 currently describe preserving a separate vanilla route. Replace
that requirement with native-button takeover, a suppressed legacy handler and no
duplicate entry. Preserve unrelated native behavior, prefab safety and fail-safe
menu/input ownership. This document records the approved product decision; older
trial results below remain historical evidence of the previous entry design.

## Evidence boundary

Repository baseline: `40529301`.
The first slice is A1's assembly-backed discovery and candidate lifecycle design.
The framework now contains startup/shutdown tracking and the native report editor;
see [runtime progress](REPORTING-CONTRACT.md#runtime-foundation-progress) and the
[integrated restart proof](evidence/reporting-restart-proof.md). This is unreleased
work, and its live qualification is recorded separately from the earlier
menu-only prototype. The separate
[live proof record](evidence/reporting-menu-proof.md) tracks disposable trials,
including failures. Controller, browser, attachment, authentication, and co-op
qualification must not be inferred from that bounded proof.

Follow the [live verification workflow](../.agents/skills/ingame-smoke/SKILL.md)
in a configured disposable installation before claiming menu feasibility.
The [bounded proof helper](../tools/reporting-proof/README.md) supplies a separate
test plugin and macOS copy preparation. It does not ship with the framework.
Its application identity, preferences, persistent data, save paths, logs, and
command channel must be independent of any concurrently running game. Steam
initialization and relaunch are blocked in the proof process before game assembly
execution; this intentionally does not qualify Steam-connected behavior.
A3 additionally needs a designated test repository and an ordinary account
without repository write access. Production issues are not a test destination.

## Existing framework seams

- [ModsButtonPatch](../FTKModFramework/Core/UI/ModsButtonPatch.cs) clones a title
  menu cell, disables persistent click listeners, removes runtime listeners,
  and prevents duplicate injection. Those precautions are reusable; the scene
  hierarchy and navigation assumptions need separate verification for Options.
- [ModsPanel.Open](../FTKModFramework/Core/UI/ModsPanel.cs) records an opening
  `StartGameFE.MainScreen` and transfers focus. It does not establish an active
  session reporting lifecycle. Do not open that panel as the reporting editor.
- [ModRegistry](../FTKModFramework/Core/Data/ModRegistry.cs) separates `Enabled`
  from `PendingEnabled`; registration is not proof of successful content loading.
  [ContentLoader.Load](../FTKModFramework/Core/Data/ContentLoader.cs) registers
  discovered manifests before content registration. A2 must trace outcome
  producers before B records per-mod load results.
- [MarketplaceRuntime](../FTKModFramework/Core/Marketplace/MarketplaceRuntime.cs)
  exposes separate `Active` and `Pending` snapshots and can publish hot-reload
  changes. A later capture must copy the current authorities at observation time,
  rather than equating startup selection with successfully loaded content.

## Installed-assembly anchors

Read-only inspection used `ilspycmd` against the installed macOS
`Assembly-CSharp.dll`, SHA-256
`94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`.
These findings establish control flow for those bytes, not scene layout or live
behavior. No game binary or decompiled source is included here.

| Anchor | Observed behavior | Consequence |
| --- | --- | --- |
| `uiOptionsMenu.Show` | Calls `PauseGame(true)`, marks showing, configures title/session buttons and explicit links ending at `m_ReportBugs`, then transfers focus to `m_MainOptions` | Bind the current scene owner after Show; retain its native title/session navigation |
| `uiOptionsMenu.PauseGame` | Solo pause and multiplayer behavior differ | Preserve native ownership; do not pause peers or directly set simulation time |
| `uiOptionsMain.OnReportBugs` | Closes Options before calling the vanilla bug form | Suppress the legacy handler before its close/form side effects; route to the framework child |
| `uiOptionsMain.OnClose` | Restores main-canvas interaction, hides system canvas, resets stats time, unpauses, and clears showing/blocker state | Do not call it when opening or cancelling the reporting submenu |
| Native submenu actions on `uiOptionsMain` | Set `m_SubBlocker`, then transfer focus to a child with saved input state | Use the submenu ownership pattern, subject to live proof |
| `uiOptionsAudio.Awake`, `OnSetFocus`, `OnClose` | Marks option-submenu behavior, assigns cancel to `Close`, activates on focus, deactivates before base close | Reference lifecycle for a framework-owned reporting submenu |
| `FTKInput.SetFocus` | Captures input state before losing parent focus, assigns saved state/callback, then invokes pre-focus, focus, and post-focus hooks | Restore through native saved state rather than a guessed title screen |
| `FTKInput.SetFocus` early branches | Popup waiting can return without transition; same-focus handling does not run the normal pre/post hooks | Verify actual focus ownership before claiming the editor opened |
| `FTKInput.Close`, `LostFocus` | Close clears matching first-modal state, loses owned focus, then calls `OnClose`; lost focus deselects and clears current ownership | Use `Close`, not direct `OnClose`, for normal cancellation |
| `FTKInputFocus.OnClose`, `FTKInputState` | Restore saved input maps, controllers, mouse mode, prior focus and selection, then the callback | Do not separately restore gameplay input while Options remains open |
| `FTKInputFocus.SetFocus`, `Update`, `ExternalUpdate` | SetFocus stages a new selection and clears current selection; later native UI updates resolve it | Immediate selection-null after restore is inconclusive; observe a bounded native settling interval without invoking updates manually |
| `uiOptionsMain.OnPreSetFocus` | Clears submenu blocker on return | Cancelling the child should return to Options while retaining its pause ownership |

### Native handler takeover and submenu transaction

Use a narrow postfix on `uiOptionsMenu.Show` to bind the current scene-owned
Options menu and its existing `m_ReportBugs` button. Preflight the parent focus,
blocker and native submenu reference. Do not add a second entry or rewrite the
native navigation links. Never mutate a shared prefab.

Intercept `uiOptionsMain.OnReportBugs` before the original method closes Options
and opens its form. Only a verified current owner may open the framework child.
While the reporting replacement is active, a missing prerequisite must not fall
through to the removed legacy reporting route. Keep Options usable; make an
unavailable action visibly disabled or otherwise provide an explicit unavailable
result. Rebinding on later Show calls must recover when prerequisites return.
Opening the production child starts bounded metadata collection, but no account
or browser operation. The current menu proof does not implement collection.

Suppress the shared `SerializeGO.ShowBugForm(Action)` entry before it sets its
showing flag or starts its screenshot/report coroutine. Installed managed callers
include `FTKVersion.OnClick` and `uiStartGame.RequestClientBugReport`, in addition
to the Options handler. Also suppress the exact `uiSystemDialog.ShowBugForm(Action)`
form entry, including its zero-argument wrapper, without altering general dialogs.
These unrelated callers do not open the framework reporter. The existing RPC may
still assign its group ID before calling the suppressed method; do not alter
network state or invoke its legacy callback. This disables the inspected legacy
form paths, not arbitrary third-party reporting code.

Open a framework-owned option submenu using the native saved-input-state route.
Copy the native submenu's scene-configured modal and close-on-lost-focus flags
before transferring focus. The proof found that leaving the new child nonmodal
can take a different `FTKInput.SetFocus` branch and corrupt the enclosing menu's
restore target. A rollback must preserve both the parent's input-state reference
and the previous focus held inside that state, not just the current focus owner.
Check popup and framework navigation locks first. Set the blocker only as part
of the opening transaction. Distinguish a rejected transfer, which leaves the
parent untouched, from failure after parent focus has already been released.
The latter must restore the captured native input/focus state while its owner
remains valid, as well as hide the child and restore the previous blocker value.
Do not blindly close a child whose saved state was never initialized. The proof
must establish a recovery path for each mutation point, including interference
from existing hot-reload focus/close guards, before production implementation.

Back/cancel uses `FTKInput.Close(child)` once, deactivating the child before its
base close restores Options. Closing Options afterwards remains native behavior.
Browser focus loss must not close the child, unpause independently, or recapture
diagnostics; returning should retain the review and current focus owner. This
browser behavior is a requirement awaiting proof, not an assembly finding.

Scene destruction invalidates the captured owner. Cleanup must discard live UI
references and owned controls without restoring stale focus into a
destroyed scene. Preserve any copied narrative separately; do not retain Unity
objects in background capture work. Scene transitions, interrupted focus transfer,
repeated close, and browser return remain explicit proof cases.

## Candidate context and input matrix

All entries below are proposed scope, not release support claims.

| Context | Candidate behavior | Evidence still required |
| --- | --- | --- |
| Title | Existing Report Bugs opens framework reporting | Takeover passed ten cycles, bounded faults and targeted keyboard checks; full input/browser/destruction remain open |
| Responsive startup/mod-load failure | Same entry only when native Options and input are usable | Partial initialization, missing sources, text-only recovery |
| Solo overworld | Keep Options responsible for native pause | Earlier extra-entry proof passed; repeat with native-button takeover |
| Solo combat | Await explicit evidence before enabling | Turn transitions, input ownership, cancel and browser return |
| Loading or scene transition | Do not offer entry without a stable owning menu | Destruction and interrupted-editor cleanup |
| Co-op host | Await explicit evidence; disclose that play continues | Two-client host reporting and authority checks |
| Co-op client | Await explicit evidence; disclose that play continues | Two-client client reporting and authority checks |

For each enabled context, mouse selection, keyboard navigation and confirm/back,
and controller navigation and confirm/back need live trials. Initial text-entry
design assumes a physical keyboard; controller-only text entry is unproven.
Windows, macOS, and Linux/Proton are separate qualification targets, all currently
unverified for this feature. Unsupported contexts recover by returning to a
usable menu/title when safe and filing through GitHub. Restart offers require
the separate lifecycle producer now wired through Plugin and the reporting worker.
The menu-only proof does not qualify that integration; use the integrated restart
record for its actual observations and outstanding gates.

## Next gates

1. Complete A1 input and lifecycle qualification beyond the
   [recorded title and solo proof](evidence/reporting-menu-proof.md). Native-button title cycles, targeted keyboard and two synchronously injected fault
   probes now pass. Requalify active solo under the new route. Full input
   coverage, interrupted transitions, scene destruction, and browser return
   remain open. Preserve the recorded build/hash/context boundaries.
2. A2's [internal contract](REPORTING-CONTRACT.md) defines the versioned envelope
   and authority table with synthetic fixtures.
   Review binds an exact draft revision; recapture has a distinct capture identity.
   Missing outcomes stay unknown. Numeric capture budgets belong to B.
   The [runtime foundation](REPORTING-CONTRACT.md#runtime-foundation-progress) now
   has collector/store/worker tests and startup/menu integration. The editor supports
   automatic metadata, bounded review/copy, in-memory resume and one explicit local
   saved draft; deferred restart offers support durable dismissal. Saved recovery
   restores no sharing consent. The fixed GitHub handoff now saves a recovery copy
   before requesting a bounded prefill or template-only URL; file attachments,
   real browser journeys and issue submission remain open. Integrated live qualification is recorded separately.
3. A3's [browser proof preparation](evidence/reporting-browser-proof.md) supplies
   synthetic cases; live trials must prove signed-in and signed-out browser
   recovery, field prefills, Unicode,
   oversized fallback, actual attachment, and final submission in a test repository.
4. A4 reviews the independent menu and browser decisions and reconciles B/C/D.
   Neither decision is a go until its mandatory live gates pass.

No child issue is complete on the strength of this document.
