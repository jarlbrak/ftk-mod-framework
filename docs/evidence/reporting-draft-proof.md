# Explicit local draft recovery

## Scope

This slice adds one explicit local saved draft and immutable reviewed-export files
to the native reporter. Save local preserves all structured narrative fields,
report/capture identity and report-creation/previous-session metadata. Recovery never
collects replacement metadata, restores review approval or enables metadata sharing.
The UI confirms before Resume saved replaces current in-memory edits. A different
report cannot silently overwrite the saved slot.

The [draft filesystem checks](../../FTKModFramework/Tests/ReportingDraft/README.md)
cover normal restart, immutable provenance, detached cache copies, same-report
updates, different-report rejection, UTF-8 limits, shared quota, expiry, corrupted
latest publication and released-lease rejection. Runtime checks also cover exact
reviewed export bytes, optional diagnostics files, manifest publication, main-thread
completion and unavailable ownership. Session/metadata regressions and the net35
Release build passed; the build has seven existing EnemyVisualPatch warnings.

## Live build

The isolated macOS FTK 1.1.00 / Unity 2017.2.2p2 route used production reporting,
with proof-only Steam suppression and separate save/report roots. Four foreign
FTK processes were recorded before launch and excluded from input, deployment
and signals.

- Framework SHA-256: `cbeee88ea2b78a9cfb8e77e5f14185aa129427bc312863b392ac0294947ab909`
- Proof SHA-256: `9af74676d0a207d93ef9d4ba4c574370483a7b3470e60a48ab4e991e87b2fea3`
- Steam guard SHA-256: `005c6e8c2e0803be027bf836b4ab23050ed45fd64dbbe50488025a4baa6a4854`

## Live observations, 2026-09-24 UTC

Programmatic native button/input fixtures passed these checks on the build above:

- Save local persisted a synthetic Unicode narrative and reported success only
  after worker completion.
- Editing after saving did not change the saved version. Resume saved first asked
  for confirmation while preserving the newer text; confirmation restored the
  saved narrative and original report/capture identities and metadata bytes.
- Resumed metadata inclusion was off and Copy reviewed was disabled.
- After normal Application.Quit and relaunch, native Options > Report Bugs restored
  the same saved narrative, report/capture IDs and metadata bytes without review
  approval or metadata-sharing consent.

A screenshot confirmed readable status and the Save local/Resume saved controls
without overlapping native Options captions. These assertions use guarded native
calls and InputField assignments, not complete physical-input qualification.
No clipboard copy, browser launch, upload or remote issue was requested.
The test process was stopped normally. All four protected FTK processes retained
both their original executable identity and start time. Raw receipts and captures
remain in ignored scratch storage.

## Structured GitHub form UI smoke, 2026-09-24 UTC

The structured-form build was deployed only to the authorized isolated app copy
(`com.ftkmf.reporting.proof.e5d6reporting`) with its separate persistent and save
roots and Steam guard. Live-smoke framework SHA-256:
`6d21bf6e1c30e7b8aee9d13fd16c959b1a80c6c9d3d0422abf688bb69e1348dc`.

The native Report Bugs action opened the framework panel with reporting focus and
the Options blocker active. The live form exposed the three field selectors and the
cannot-reproduce choice. Synthetic summary/expected-actual values were entered and
reviewed; all seven explicit field-copy buttons and the Back control were visible.
With metadata excluded, the review status correctly said no diagnostic file was
saved. The GitHub action was not pressed, no report was saved, and no browser or
network destination was opened. The exact disposable process was stopped with a
normal quit, and the other running FTK process identities remained unchanged. This
checks the native UI composition and programmatic field path, not controller or
complete physical keyboard coverage. A final game-free build after this UI smoke
also changed title prefilling to use the first summary line and clarified the
shortening notice; its framework SHA-256 is
`30af5262583cf88fe425b5adefb213c84785bf829dae1d68eb3e71224f9f16b3`. The focused
runtime test and net35 build pass on that final binary; the small title-text change
was not repeated in-game.

## Remaining gates

Explicit local deletion is implemented with a tombstone but is not live-qualified
in this evidence record. Saving a restart report does not acknowledge the incident;
Dismiss remains its explicit disposition. Seven-day retention is cleanup while the
framework runs, not a background deletion service. Hashes detect damaged bytes;
they do not establish trust, secrecy or power-loss durability. Windows/Linux,
forced exit during draft publication, complete physical-input routes and scene
teardown remain unqualified. The browser request and local attachment files exist,
but browser rendering, sign-in recovery, public upload behavior and ordinary-account
submission remain unqualified. After this draft test, a separate user-requested
synthetic report completed the live in-game handoff and created
[issue #187](https://github.com/jarlbrak/ftk-mod-framework/issues/187). That
maintainer-session submission did not upload the diagnostics attachment and does not
close the outstanding browser qualification gates; see
[`reporting-browser-proof.md`](reporting-browser-proof.md).
