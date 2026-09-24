# Direct-send reporting verification

## Scope

This record covers the Railway-backed reporting implementation on 2026-09-24.
Historical browser-created issue #187 does not prove this transport. The new
issues below were created by the actual game panel, verified native helper and
repository-owned service, without browser form submission or manual attachments.

## Game-free checks

- Framework Release/net35 build passed with seven existing unassigned-field warnings.
- ReportingDraft, ReportingMetadata, ReportingRuntime (normal and busy owner), and
  ReportingSession suites passed.
- ReportingDiagnostics passed 58 checks covering bounded collection, redaction,
  throttling, exact-session recovery, corrupt-slot fallback and owned cleanup.
- ReportingSubmission passed 117 checks, including diagnostic exclusion, UTF-8
  bounds, frozen retries, real local helper subprocesses, child-only injector
  environment cleanup, exact-request receipt hashes, legacy receipt fallback,
  competing pending reports, missing-helper persistence and checked discard.
- Runtime regression checks cover immediate retirement of a submitted saved draft,
  preserving an unrelated draft, and creating a fresh report afterward.
- Service and launcher/helper tests with race detection, vet and builds passed.
  Helper cross-builds passed for Windows amd64, Linux amd64, macOS amd64 and arm64.
- Release integrity metadata tests passed (four tests).
- All GitHub checks passed for deployment commit `322d05c3`. Later client fixes
  have the focused local checks above; their CI status is tracked by PR #188.

Fake responses in offline tests do not establish live behavior. The following
observations are separate evidence.

## Live service and game chain

The authorized disposable macOS game had a unique profile, save root, process
identity and per-launch Steam-suppression receipt. No other FTK process was running
when these trials began. The harness invoked native button callbacks; these are
programmatic game tests, not comprehensive mouse/controller qualification.

The first live Send found a real helper-launch defect: BepInEx's inherited
`DYLD_INSERT_LIBRARIES` made the standalone helper exit before HTTP. The report
remained saved locally. Clearing game-only injector variables from the child
process fixed the launch without changing the game's environment. After a normal
quit and restart, the panel recovered the same frozen report and its explicit Retry
created [issue #189](https://github.com/jarlbrak/ftk-mod-framework/issues/189).
The issue and public bundle contained the originally captured startup errors and
metadata, not replacement diagnostics from the retry launch.

| Trial | Result |
| --- | --- |
| Automatic detected-error offer, explicit Send/Retry | [#189](https://github.com/jarlbrak/ftk-mod-framework/issues/189), HTTP 201, with diagnostic excerpt and verified public download. |
| Diagnostics explicitly excluded | [#190](https://github.com/jarlbrak/ftk-mod-framework/issues/190), description only; no excerpt or download link, diagnostic URL returned 404. |
| Verified isolated process forcibly terminated, then restarted | The title offered the previous session automatically; explicit Send created [#191](https://github.com/jarlbrak/ftk-mod-framework/issues/191). Download contained the exact previous session ID and persisted error timestamps, separately from the new launch's errors. The pending incident cleared only after confirmed submission. |
| Final-build manual Options > Report Bugs | [#192](https://github.com/jarlbrak/ftk-mod-framework/issues/192), fresh report identity and automatic diagnostics, HTTP 201. |
| Exact service replay of #189 | HTTP 200 returned #189 again. Changed content under its ID returned 409. This was an HTTP replay check, separate from the game's retry test. |
| Migrated saved-draft retirement | Final build resent the exact #190 content through the game, received HTTP 200/#190, retired the matching legacy draft, and reopened an empty manual report with a new ID. That new report became #192. |

Successful reports showed their issue number and View issue in the native panel.
The latest local receipt contains a SHA-256 of the exact frozen request: changed
text under the same ID cannot reuse an old success locally. Legacy receipts without
that hash go through the service's idempotency check. The saved-draft regression
was found during this trial and fixed before the final manual report.

Framework SHA-256 for initial service/restart trials:
`5e10591102bfc5673fbf74abb5ce6bdff956a11a926d3dcaf3b0e913d4110046`.
Final framework SHA-256 for migrated-draft retry and issue #192:
`d5d40fc53399a6c5b1a2e58ce4bb15afe35a695b220d9043c903be4515c0bf03`.
Native macOS arm64 helper SHA-256 for these trials:
`b27912873d2607adae500f347c3152a74f1809938fc3497f6cad0ba09ead1b0f`.

The logs include an observed `AkInitializer.OnApplicationFocus` null-reference
error and the fixture's expected unavailable-Steam message. These qualify capture
and transport, not a claim that the framework caused or repaired a native error.
Metadata explicitly marked unavailable plugin/managed inventory sources and partial
coverage. This does not prove complete inventory coverage for every installation.

## Native UI and local Steam installation

Screenshots at 1280 by 800 showed the native Mods-style panel, legible public-sharing
disclosure, diagnostic toggle and expandable preview. The original edit button
covered the description; the corrected compact Edit button sits beside it. A
keyboard Escape trial ended editing and restored focus to Edit, with native cancel
semantics. The final screenshot showed entered text unobscured. Not now dismissed
an automatic error offer without creating an outgoing report. Closing returned to
the owning Options menu/title. The last isolated process quit normally.

The final framework/helper pair and matching verification records were installed
into the local Steam game after confirming it was stopped. The prior four files
were backed up. The helper's actual `prepare-launch` check returned
`Framework verified.` without launching or requiring repair. No saves, unrelated
plugins or game configuration were replaced. This is installation verification;
the new build has not been exercised in an authenticated Steam-launched game here.

## Repository deployment

Railway builds the feature branch from this repository's `/reporting-service`
root. The checked-in IaC was applied without deleting resources, changing secrets
or replacing the receipt volume. Repository-scoped Railway GitHub App authorization
registered the push trigger; pushing commit `322d05c3` automatically built and
deployed it. The separate fine-grained GitHub token grants Issues read/write only
to the framework repository and is stored as a Railway service secret. It is absent
from game files and source control. The initial credential expires 2026-10-24 and
must be rotated before then; automatic credential refresh is not implemented.

The service returned `/healthz` HTTP 200 with `configured: true`, served `/privacy`,
and created the real issues above. Configuration readiness alone was not used as
proof of GitHub access.

## Expanded logs and managed drafts

Follow-up implementation `cb7ee059` addresses the thin diagnostics in real issue
[#193](https://github.com/jarlbrak/ftk-mod-framework/issues/193). That older report
said no matching error log was captured; an exception-only buffer could not explain
a freeze without an exception. The new buffer records recent informational/debug
messages, warnings, errors and stack traces from the running game and framework,
up to 128 KiB per current/prior session. It does not recover logs that the old build
never captured. Fresh manual opens take fresh snapshots; saved drafts retain their
original snapshots and explicit diagnostic choices.

Game-free checks passed: ReportingDraft 91 checks, ReportingDiagnostics 76 checks,
ReportingSubmission 124 checks, ReportingRuntime normal/busy, ReportingMetadata,
ReportingSession, the Release/net35 build, service/helper race tests and vet, four
helper platform builds, service build, infrastructure typecheck and diff checks.
The larger-payload tests cover more than 128 KiB through the payload, helper and
service, receipt restart, readable download, opt-out/expiry 404 and encoded receipt
size rejection. This is separate from the smaller live startup capture below.

Two isolated macOS launches exercised these native flows:

- Diagnostics defaulted on in an error offer and fresh manual reports. Draft A
  retained on; draft B retained an explicit off choice across a normal restart.
- Save, Open, Keep editing, Save and continue, and Discard edits worked. Discard
  preserved the saved version. Ordinary descriptions restored exactly, without
  adding the legacy narrative format's blank lines.
- Both drafts appeared after restart. Keep draft cancelled deletion; confirming
  Delete removed only B. A retained its original report/capture and metadata time.
- Native keyboard navigation reached A's Open button and Return opened it. Native
  screenshots showed the editor, expanded preview and draft list without overlap.
- A fresh Options > Report Bugs opened an empty default-on report with a new ID and
  current observation time, rather than silently reopening an old capture.
- Sending restored A created [issue #194](https://github.com/jarlbrak/ftk-mod-framework/issues/194).
  During Send, Back and reopening Report Bugs retained the same in-flight identity;
  confirmation then showed #194 and retired the matching saved draft.

Issue #194 links both JSON and a readable `.log` attachment. Both returned HTTP 200.
The 12,193-byte, 120-line uploaded log exactly matched A's saved capture, including
framework Info/Debug and Unity Warning/Error/Exception entries from the first
launch. No second-launch entries replaced it. The `.log` response was 12,534 bytes
including its capture/coverage headings. These are real game-generated startup
logs, not an injected long-payload fixture or a manual GitHub attachment. The
existing opt-out issue #190 returned 404 for both `.log` and `.json` downloads after
the upgrade. Repository-triggered Railway deployment succeeded and health remained
configured. No existing report, including #193, was backfilled with new logs.

Final framework SHA-256:
`799365744f34b15272b8992867517b49c99705c41705054c9b524f2ec5ac974a`.
Final macOS arm64 helper SHA-256:
`a0dce4fefe2a09f388f4c086de31c8ff0c341f0997f051d5965a3c2d3fb7b9b2`.

The isolated game quit normally. These binaries and matching helper/installation
hash records were installed into the stopped Steam installation, with all four
prior files backed up. Hashes were verified after copying. A separate foreign FTK
process was running when `prepare-launch` was called; the helper correctly declined
to perform its preparation checks. That process was left untouched. The earlier
`Framework verified.` observation applies to the preceding build, not this update.
Authenticated Steam launch of this final pair remains a user-facing test gate.

## Credential-label filtering follow-up

Final merge review found that the explicit labels `credential` and `credentials`
were missing from filtering. Synthetic cases reproduced the gap in both the client
and service before the fix. The client now omits matching messages, and the service
omits matching strings and structured credential fields. Entire matching strings
are omitted because values can contain spaces and line breaks.

The client diagnostics suite passed 80 checks. The service race suite passed,
including a fake-GitHub submission that checks issue text, stored payload, public
JSON and readable log downloads for exclusion of synthetic values. Service vet,
build and infrastructure typecheck passed, as did the framework Release/net35 build
with seven existing warnings. This is offline filtering evidence, not another live
report submission. The earlier binary hashes describe those earlier trials.

This change does not retroactively rewrite retained uploads. Historical test issues
were deleted on request; removing an issue does not remove its service copy. Cleanup
of those copies remains a separate operational task. No real credential exposure
was established by the synthetic regression.

## Remaining qualification gates

- Authenticated Steam-launched manual test of the installed final pair.
- Active-session error offer, pause ownership and return-to-game behavior for this
  simplified panel; combat/co-op and Windows/Linux runtime qualification.
- Spoof-resistant client identity at Railway ingress. `TRUSTED_PROXY_HOPS` remains
  zero; the five-per-hour limit can apply to shared proxy addresses. Do not assume
  a proxy hop count without evidence or claim broad public-rollout readiness.
- A native crash is not distinguished from force quit or power loss. This trial
  proves abrupt-exit recovery with persisted logs, not crash-dump collection.
- If disk failure/quota prevents the saved-draft tombstone, or exit interrupts that
  write, the legacy draft can return on another launch. Edited same-ID content is
  rejected safely instead of falsely confirmed; durable retirement recovery still
  needs separate failure-path qualification.
