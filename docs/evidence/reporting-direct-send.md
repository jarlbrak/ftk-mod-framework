# Direct-send reporting verification

## Scope

This record covers the Railway-backed reporting implementation in this change.
Historical browser-created issue #187 does not prove this transport. No new issue
has yet been created through the deployed service.

## Game-free checks

- Framework Release/net35 build passed with seven existing unassigned-field warnings.
- ReportingDraft, ReportingMetadata, ReportingRuntime and ReportingSession suites passed.
- ReportingDiagnostics passed 58 checks, including throttling, redaction, bounded
  snapshots, exact-session recovery, corrupt-slot fallback and owned cleanup.
- ReportingSubmission passed 60 checks, including diagnostics exclusion, UTF-8
  bounds, immutable retries, local helper process invocation, competing pending
  reports, receipt replay, missing-helper persistence and identity-checked discard.
- Service and launcher/helper `go test -race ./...`, `go vet ./...` and builds passed.
  Helper cross-builds passed for Windows amd64, Linux amd64, macOS amd64 and arm64.
- Release integrity metadata tests passed (four tests).

These checks use fake service/GitHub responses where applicable. They do not prove
live GitHub credentials, Railway deployment or supported gameplay on other platforms.

## Isolated macOS game observation

Framework SHA-256:
`87367ef4b9aa94d0909ad99846fbb55fd5105a574c3940bcea1f403ea9d37cba`.
The authorized disposable game copy used a unique profile and Steam-suppression
receipt. No other FTK process was running when this trial began. The fixture's
native command channel drove controls; this was not physical input qualification.

After the introductory screen, the title automatically opened the previous-session
report offer through Options. The panel owned native focus, retained the Options
blocker, used the native Mods panel styling, and displayed one optional description,
diagnostics inclusion, a detail toggle, Send report, Not now and Back to game.
A screenshot confirmed the public-sharing disclosure was legible without overlap at
1280 by 800. The expanded preview displayed the outgoing disclosure and payload.

A native `AkInitializer.OnApplicationFocus` null-reference error was observed during
startup and appeared in both bounded local diagnostic slots. This is positive
capture evidence, not a claim that the framework caused or repaired that game error.
Not now closed the report, removed the blocker and returned through Options to the
title screen. No pending upload file was created. The exact owned process then
quit normally; no game process remained.

Later small fixes to deferred-report recovery and explicit dismissal were rebuilt
but not covered by that captured binary's live trial.

## Repository deployment

Railway successfully built commit `e462cb06429af4bfa8186563c4a76ad73b7001d8`
from this repository's `docs/reporting-feasibility` branch and `/reporting-service`
root. The repository's reporting-service CI completed successfully. The checked-in
IaC plan was applied with no resource deletion, variable change or volume replacement.
The live `/healthz` returned HTTP 200 with `configured: false`; `/privacy` returned
its public disclosure. This proves deployment and HTTPS reachability, not issue
creation. The scoped GitHub service credential has not yet been configured.

## Remaining qualification gates

- Scoped service credential; GitHub requires interactive re-authentication.
- A real in-game Send producing a public issue with the correct diagnostic download;
  confirm report ID, exact-session content, opt-out and same-ID retry behavior.
- Steam-launched matched framework/helper installation, including receipt integrity.
- Active-session error offer and return-to-game behavior for this simplified panel.
- Fresh abrupt-exit/restart trial proving the previous session's error snapshot is
  attached through the service, plus Windows/Linux and co-op coverage.
