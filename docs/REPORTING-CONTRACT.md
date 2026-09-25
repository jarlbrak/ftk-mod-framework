# Internal reporting contract, schema 1

> Historical browser-handoff design and evidence. The current direct-send flow,
> automatic error capture and sharing disclosure are described in [Reporting](REPORTING.md).
> Its service contract lives in [reporting-service](../reporting-service/README.md).
> Browser-only, logs-off and mandatory review-step requirements below are superseded
> for that flow. Historical proof results do not qualify the new transport.

Design for [Spec A2](https://github.com/jarlbrak/ftk-mod-framework/issues/162),
against repository baseline `40529301`. This is an internal data contract and
synthetic proof, with the current internal implementation described under
[runtime progress](#runtime-foundation-progress). It is not a public mod API. The
[menu evidence](REPORTING-FEASIBILITY.md) does not yet qualify all entry contexts.
B owns the collection allowlist, budgets, sanitization, persistence and retention;
C owns narrative validation and UI; D owns outgoing formatting and browser
transport; E owns release qualification. This document owns identity, timing,
state transitions and authority. A4 remains the production implementation gate. The approved entry now takes over
the native Report Bugs action; see the [ownership decision](REPORTING-FEASIBILITY.md#native-button-ownership-decision). Automatic metadata and restart offers are also approved product requirements.
They supersede the earlier explicit-capture-only and no-crash-recovery scope;
external sharing still requires review and an explicit action.

## Identity and observation

A draft has `schemaVersion: 1`, an opaque locally generated `reportId`, positive
monotonic `revision`, UTC `createdAt`, narrative, player suspicion (`framework`,
`mod`, `not_sure`), minimal `entryContext`, sharing choices, and a nullable selected
capture reference. IDs never enter saves or multiplayer state. A suspected mod
is a user choice, never evidence of causation. Restored choices are labelled old.

Each accepted narrative, suspicion, sharing-choice or capture mutation advances
revision and clears review. Opening a new report automatically requests a bounded
allowlisted metadata snapshot in memory and selects usable results for inclusion
by default. There is no initial Capture button prerequisite. Logs stay off by
default, and opening never contacts a service. The player can inspect or exclude
metadata before export; collection failure preserves a text-only route. Context has its own `observedAt`,
phase, adventure identifier and role, with null for unknown values and a section
status. Entry and capture observations are independent; neither overwrites the
other. Title has no adventure or session role. Missing role is not solo.

Each automatic initial capture or explicit refresh attempt creates a distinct opaque `captureId`, request time,
nullable start/completion times, and section results. Timestamps describe observed
wall-clock times, not ordering authority: use revisions and attempt identity for
ordering because clocks can move. A running attempt has no terminal result;
completion time is null until terminal. Rejected concurrent requests do not create
an attempt. Workers receive only bounded copied primitives, never Unity objects,
mutable registry rows, PlayerPrefs, or live marketplace collections.

Every terminal section has `status`, `reason`, `observedAt` (null if never observed),
nullable payload and nullable truncation counts. Counts are integers only when
known; an unenumerated total is null, not zero. Reason codes are fixed and contain
no exception text, paths, player names or metadata. Initial vocabulary:

| Status | Meaning | Allowed reasons |
| --- | --- | --- |
| complete | Requested bounded observation finished; not proof of healthy game/mod | none |
| partial | Some requested evidence missing or truncated | source_error, scene_changed, limit_reached, transition_in_progress, deadline_exceeded |
| unavailable | No trustworthy source observation | source_absent, not_initialized, unsupported, transition_in_progress, runtime_faulted |
| omitted | User or policy excluded collection | user_declined, policy_excluded |
| cancelled | Explicit cancellation or superseded attempt | user_cancelled, superseded |
| failed | Attempt could not produce usable evidence | source_error, deadline_exceeded |

The terminal envelope also carries overall `status` and `reason` from the same
vocabulary. Complete means every requested section completed (user-omitted
sections do not lower it). Partial means at least one requested section supplied
usable copied evidence and another failed, was unavailable, or was truncated.
A cooperative deadline preserves finished evidence as partial with
`deadline_exceeded`; without usable evidence it is failed. If no source could be
observed the overall result is unavailable; all declined is omitted. Explicit
cancellation is cancelled regardless of completed sections. Only complete or
partial envelopes with usable requested evidence are selectable. A successful
empty inventory is usable evidence; unknown outcomes do not make it failed.
Cancellation and supersession terminate the local attempt immediately even if
its worker has not returned, allowing text-only review or a replacement request.
Worker completion cannot revive a terminal attempt. B must serialize actual worker
execution: if cancelled work has not drained, keep text-only review available but
reject a new capture as busy until it does. The fixture models completion delivery
after termination, not parallel workers.

Per-mod outcomes are a separate vocabulary: `registered`, `partial`, `failed`,
`disabled`, `blocked`, `unknown`. Any non-unknown outcome requires an identified
producer, observation time and scope. `registered` means only that the identified
registration stage succeeded, never that behavior, assets or the session work.
A complete inventory may legitimately contain only unknown outcomes.

## Capture, review and recovery transitions

1. New reports start with metadata inclusion enabled and logs disabled. Automatic
   initial collection uses the same bounded attempt lifecycle as explicit refresh.
   Editing preserves narrative even if diagnostics are absent. Capture request
   advances revision, clears review, and starts an attempt with current sharing
   choices. Keep an older selected capture explicitly labelled previous while
   replacement runs; it is not eligible for export during capture.
2. Completion is accepted only for the currently owned attempt and collection
   input generation. Narrative-only edits advance draft revision and clear review
   but preserve the in-flight metadata attempt: typing cannot defeat automatic
   collection. Sharing/source changes and explicit cancellation invalidate its
   collection authority; late completion is discarded. Completion merges only
   metadata into the current draft, never an older narrative. Cancellation terminates the attempt and advances
   revision. Cancellation cannot preempt a blocked getter or file operation.
3. A usable replacement is selected only through a visible completion transition
   advancing revision. A failed replacement retains the prior capture for recovery
   but clears diagnostic sharing consent. The editor shows both the failed attempt
   and the previous observation time. A user must explicitly reselect that previous
   snapshot or choose text-only, then review again. It never silently becomes fresh.
4. Review shows the full sanitized outgoing narrative and selected files, including
   D's prepared fields/title shortening/fallback. Approval binds report ID, exact
   revision, selected capture ID or null, sharing choices and exact prepared bytes.
   Any edit, capture mutation, selection or sharing change invalidates approval.
5. Export/clipboard/file-location/browser actions require this exact approval and
   explicit user action. Text and diagnostics carry matching schema/report/capture
   IDs. Text-only uses null capture identity and no diagnostic attachment; previous
   files are not candidates for attachment. File hashes check byte integrity, not
   trust or secrecy. Disabling logs removes them from every outgoing representation;
   enabling logs requires a new capture, never a disk read at export time.
6. Recovery restores narrative and labelled prior observations but no review or
   sharing consent. An explicitly saved unreviewed narrative draft is local-only,
   not an export. Browser retry uses the same reviewed revision without recapture.
   Handoff state is `requested` or `failed`, never `submitted` or `issue_created`.
   A browser request does not prove a window opened. GitHub file selection may
   upload publicly before the player submits; C must disclose this.

The editor states are editing, capturing, review, local recovery and handoff
requested/failed. They do not imply a remote issue lifecycle. Storage failure
preserves editable text and an explicit reviewed text-only/copy recovery route.
B publishes staged revisions with validated lengths/hashes and a publication
marker; this contract makes no power-loss atomicity promise.

## Automatic metadata and restart offers

Every newly created report attempts to bundle framework/game versions, bounded
OS/runtime/architecture fields, current and pending mod selection, supported
plugin metadata, known loader outcomes and timestamped game context. Use the
source authorities below: unknown outcomes remain unknown, and absent sections
carry a reason. Never include usernames, machine IDs, absolute paths, saves,
credentials, full configs or raw exception text. Optional sanitized logs require
separate opt-in. Refresh is explicit; browser focus return never recollects.
The same rules apply to reports started from an unexpected-exit offer.

Restart detection owns a separate reporting-root OS lease acquired early in
`Plugin.Awake`, before reading or rotating prior session state. Follow
`Core/Marketplace/MarketplaceRuntimeLease.cs`'s shipped-runtime `flock` and
`LockFileEx` pattern, including borrowed `FileStream.Handle` lifetime constraints.
Do not reuse the marketplace lease, which is acquired later, or infer ownership
from PID/FileShare alone. If the reporting lease is busy or unsupported, disable
restart tracking for that launch without inspecting or changing another owner's
records. Ordinary manual reporting remains available. Separate configured roots
remain independent; never scan another installation for incidents.

Under the lease, a session has an opaque session ID, start observation, phase,
latest completed checkpoint ID and nullable shutdown observation. Persist only
bounded allowlisted detached metadata after B normalization/redaction, including
untrusted mod names and versions, during normal execution, initially and at
known initialization/context changes. Stage unique files and validate lengths,
schema and hashes before publishing immutable checkpoints. Incomplete or invalid
records are ineligible; never infer a crash from corruption. Retain at most one
pending prior incident plus the current session, each within the existing 256 KiB
structured cap. Keep only the latest committed checkpoint per retained session;
remove superseded checkpoints and abandoned temporary files under the lease.
All checkpoints, manifests and temporary files count toward B's 5 MiB store quota
and seven-day retention. Check capacity before staging. Preserve the pending
incident until durable disposition or retention expiry, whichever comes first;
expiry removes the offer as well as its evidence. If capacity or publication
fails, skip new tracking rather than overwrite an unexpired pending incident.
No logs are persisted by this feature. Recoverable previous-session logs require
a separately consented, bounded, sanitized producer; `LogOutput.log` is not one.

A valid prior started session without a shutdown observation is a possible
unexpected exit. `OnApplicationQuit` records shutdown observation; `OnDestroy`
alone does not. A shutdown callback does not prove process exit completed, and
an unexpected exit does not prove a crash: force quit and power loss look alike.
Failures before tracking begins are uncovered. No crash handler, stack capture
or exception suppression is implied.

Queue one offer at a stable native title menu after splash completion, with no
other modal or input transition: **The previous game session ended unexpectedly.
Would you like to report it?** Actions are **Review report** and **Dismiss**.
Neither opens a browser or uploads anything. The offer waits while title is not
ready. It never pauses or interrupts a new active run. Review creates a local
report bound to the incident's session/checkpoint IDs; durable report linkage
makes retry idempotent. Dismiss durably acknowledges the incident. Failed writes
suppress the offer for this launch and may cause it to return next launch; show
that limitation. Merely showing the offer does not acknowledge it.

Restart reports label previous-session metadata and its last observation time
separately from automatically collected current-launch metadata. A changed mod
set on restart must never replace the prior evidence. Missing checkpoints remain
unavailable, not reconstructed. Existing review/revision rules bind both sources.
Recovery of an existing draft restores no review or sharing consent; a new
restart report defaults to metadata included and logs off like any new report.

Spec B must implement lease ownership, checkpoint retention and automatic
collection; C must implement the deferred offer and metadata preview; D must
preserve previous/current provenance in all output forms; E must qualify normal
quit, forced termination, early startup failure, concurrent launches, invalid
records, failed writes, repeated restart/dismiss and changed mod sets. These
requirements amend the earlier planning scope before issue closure.

## Source authority

All runtime snapshots below are copied on the Unity thread after the relevant
initialization, without invoking discovery, activation, saves or config writes.
An absent producer is unavailable, not a successful empty result. Export order is
ordinal identity order; registration order is untouched.

| Category | Authority and scope | Availability and failure semantics |
| --- | --- | --- |
| Narrative/suspicion/choices | Explicit editor input, timestamped draft revision | Never inferred from logs or blamed mods |
| Framework version/settings | Plugin assembly metadata and existing named config entries in `Plugin.cs` | B allowlists EnableDataContent, EnableBehaviorLoading, EnableCampaignEngine, RunSelfTests, DiagnosticsEnableGate; configured is distinct from applied, which is unknown without an application observation |
| Environment | Verified version source, Unity version, runtime/OS/architecture primitives | B restricts fields; no machine/user identifiers or environment dump |
| Discovery/current selection | `Core/Data/ModRegistry.cs`: Entries, Register, RegisterManaged, IsEnabled | Rows include disabled mods. Enabled is current selection, compatibility is separate. Unknown GUID's permissive IsEnabled result is not evidence. Main-thread collection wrapper and mutable PendingEnabled must be copied |
| Manual pending preference | `ModRegistry.SetEnabled`, row PendingEnabled | Next-launch preference, not current selection or outcome. Null means no differing preference. Collector never calls SetEnabled or reads arbitrary PlayerPrefs |
| Managed active/pending | `Core/Marketplace/MarketplaceRuntime.cs`: Active, Pending, BootstrapVerified, PollHelperCompletion | Selected generations/packages only. BootstrapVerified verifies helper activation. Pending can lag polling; no existing freshness timestamp. Record capture observation time without claiming helper freshness |
| Loader aggregates | `Core/Data/ContentLoader.cs`: LoadInternal, CollectEntries, LoadResult | Cached count is phase-1 rows; total is parsed pending entries, excluding several failures/gates. Equality is not successful mod load. Local ValidationReport is discarded and has unstructured errors/warnings |
| Per-mod outcomes | No retained reliable producer today | Unknown. `BehaviorLoader.LoadOne` knows GUID, but reflection/duplicate failures and registration counts do not reliably encode success. Never parse log strings to invent ownership |
| Reload authority | `Core/HotReload/HotReloadCoordinator.cs`: candidate load, Committed, Rollback, Fault; DefinitionState | Candidate registry may replace live rows before Active changes. During transition mark inventory/outcomes unavailable or partial. Successful rollback preserves prior committed evidence, labelled prior observation. Fault means runtime authority unknown. PublishHotReload may throw after Active assignment; no atomic snapshot can be assumed |
| External plugins | BepInEx 5.4.20 `Chainloader.PluginInfos` metadata dictionary, after bootstrap | GUID/name/version only; dictionary insertion precedes AddComponent, failed load removes entry. Presence is bootstrap registration evidence, not ongoing health. Never read Instance, Location or config. Unavailable if that version/source is not verified |
| Optional logs | No verified current-session file-tail authority today | BepInEx DiskLogListener can append prior sessions/use fallback filenames; LogOutput.log alone is insufficient. A bounded listener registered by B can cover only events since registration, not full startup. Label coverage and synchronize callbacks; opt-in export and sanitization still required |

Repository anchors are relative to [Core](../FTKModFramework/Core/) and
[Plugin.cs](../FTKModFramework/Plugin.cs). BepInEx inspection describes the resolved
5.4.20 net35 dependency, not every installed BepInEx version.

B's smallest outcome producer is a bounded internal observation builder in
LoadInternal, with typed GUID observations only where discovery, phase-1,
phase-2/capability and behavior call sites already know ownership. Keep unattributed
problem counts separate. Candidate results remain private until verified commit
and generation publication. Preserve the prior result after verified rollback;
mark transition/fault unavailable rather than expanding the reload transaction.
No data loading configured or initialization never reached is distinct from a
completed discovery with zero rows. This is a scoped handoff to B, not code added
by A2.

## Installed context sources

Read-only inspection used the same Assembly-CSharp SHA-256 recorded in the
[feasibility evidence](REPORTING-FEASIBILITY.md). `uiStartGame.Instance` directly
returns `guiStartGame`; `m_GameStarted` becomes true during EnterFahrulRPC before
entry work completes. It cannot establish a settled overworld. Use `title` only
from a known title owner, guarded `in-session` otherwise, and `unknown` when
neither is established. `EncounterSession.m_IsInCombat` is a coarse flag, not a
complete phase model. `GameLogic.gAction` does not enumerate all transitions.

`GameLogic.m_GameMode` distinguishes SinglePlayer, Multiplayer and
LocalMultiplayer only in a known active session. LocalMultiplayer is not silently
labelled solo. Host/client remains unknown unless native network initialization
and room membership are already established. Arbitrary `PhotonNetwork` access
can run its static constructor and create a persistent object/network peer;
never probe it during startup to discover whether networking exists. No player,
peer or room identifiers enter the snapshot.

`GameLogic.GetGameDef()` returns a nullable active definition. Its raw
`m_DisplayName` is a label/key, not yet a verified stable adventure identifier;
leave adventure identity unavailable until B has an approved identifier mapping.
Do not use save filenames, save IDs, mod paths or localized display lookup as an
identifier. Existing `FTKVersion.m_VersionNum`, `m_SilentID` and `m_BuildType` are
primitive version sources; GetVersionFull traverses UI/platform state and Create
instantiates UI. Read existing references with null checks. GameLogic,
EncounterSession, GameFlow and FTKVersion Instance getters perform discovery and
cache assignment; use existing backing references or direct bounded discovery
without changing singleton caches. No getter that creates an object is permitted.

## Budgets and measurement fixture

The adjacent specs remain the policy owners. B proposes 32 KiB total narrative,
64 KiB formatted report, 256 rows per inventory group, 256 KiB structured diagnostics,
128 KiB/500 complete log lines, 512 KiB combined shareable payload, and 256 UTF-8
bytes per metadata scalar. C's individual narrative caps must fit B's total.
D's 6,000 ASCII-byte URL cap is a product choice, not a proven GitHub universal
limit. Count final UTF-8 bytes including JSON escapes/correlation; store quota
also includes manifests and temporary files. Overlarge narrative edits are rejected
visibly, not truncated. Diagnostic truncation is explicit with known counts only.

Reference measurement host: Apple M5, 32 GiB RAM, arm64, macOS 26.6.2 (25G83),
Unity 2017.2.2p2, game build 12395049; framework baseline above. B must record its
actual build and dependency hashes when measuring. No timing or memory budget is
proved here. Proposed targets are 10 ms cumulative main-thread work, 2 seconds
preparation excluding review, 8 MiB peak capture memory increase, and a cooperative
5-second deadline.

The [synthetic fixture generator/check](../tools/reporting-contract/check.py)
produces the semantic cases and maximum-input fixture deterministically. The
stress case has 256 rows in each of discovery, active, pending and external plugin
groups, 256-byte UTF-8 scalars including JSON-escaped characters, and 500 complete
log lines totaling 128 KiB. It intentionally can exceed the structured/combined
output caps: B must truncate inventory deterministically and record counts, not
claim every maximum fits at once. No runtime capture performance is measured by
this Python fixture check. B should measure cold and warmed captures, log-off and
log-on, cancellation and late completion, with managed allocation/peak memory and
cumulative Unity-thread sampling separated from worker/file time. Record raw
sample counts, maxima and percentiles; retain no player data in public evidence.

## Evidence and outstanding gates

Run `python3 tools/reporting-contract/check.py` for fixture correlation and state
checks. Use `--write DIR` to materialize synthetic JSON for downstream tests.
Run `python3 tools/reporting-contract/restart_check.py` for automatic metadata
defaults, detached previous/current provenance, pending-incident priority and
lease/readiness/disposition policy examples. Those examples supply synthetic
ownership and validity inputs; they do not test OS locks, record validation,
durable acknowledgement, shutdown callbacks or the native prompt.
Fixtures cover complete, text-only, partial loader initialization, missing source,
unknown role/title, changed entry/capture context, selected/pending versus unknown
loaded outcomes, failed recapture and explicit reuse, and invalid stale approval
or mismatched identities. They are contract examples, not a production parser,
sanitizer, persistence implementation or net35 performance test.

The earlier architecture review on 2026-09-23 found no remaining blockers after correcting
terminal cancellation ownership, overall capture status and partial observation
timestamps. The fixture check, deterministic JSON materialization, relative-link
checks and whitespace checks passed. This review covers the contract slice, not
production runtime validation. Precise assembly context anchors appear above. A1 physical input/lifecycle, A3 ordinary-account browser/attachment proof and
A4 go/no-go remain open. No production implementation or epic/spec closure follows
from game-free contract checks.

## Runtime foundation progress

The internal [reporting sources](../FTKModFramework/Core/Reporting/ReportingSources.cs)
copy bounded current selections and audited existing version/context references.
The [report factory](../FTKModFramework/Core/Reporting/ReportingReport.cs) collects
metadata automatically once per new report and retains separate previous-session
strings. Free-form names/descriptions are currently excluded; machine identifiers
use conservative syntax and sensitive-pattern exclusion. Load outcomes remain
unknown, logs are absent and native started state means `session_or_transition`.
This is a narrower first producer, not full coverage of the source table.

The [session store](../FTKModFramework/Core/Reporting/ReportingSessionStore.cs)
uses a dedicated OS lease and hashed immutable current/pending state publications.
It implements bounded checkpoints, shutdown observation, dismissal, report linkage
and expiry. A caller must durably save a draft before linking an incident; the
store cannot prove that a draft or GitHub issue exists. Acknowledged incidents do
not block later unexpected exits. Unknown files are preserved and count toward
quota. Corrupt latest state disables tracking without guessing an older crash.

[ReportingRuntime](../FTKModFramework/Core/Reporting/ReportingRuntime.cs) is now
wired into `Plugin.Awake`, `Update` and `OnApplicationQuit`. A dedicated background
worker acquires the independent lease under `BepInEx/Reporting`, serializes store
operations and coalesces checkpoint work. Early startup metadata uses basic
framework/environment primitives, without game singleton or registry reads.
Successful content initialization, observed title/session phase changes and stable
hot-reload epoch changes request later checkpoints. Unity sources are copied on
the main thread; storage runs on the worker. Tracking begins when the worker
acquires ownership, so earlier startup failures remain uncovered. Shutdown waits
at most two seconds for the worker to record the quit callback; blocked writes can
leave an offer on the next launch. `OnDestroy` does not mark a normal exit.

The native [menu route](../FTKModFramework/Core/UI/ReportingMenu.cs) and
[report panel](../FTKModFramework/Core/UI/ReportingPanel.cs) now take over Report Bugs
and suppress the legacy capture/form entries. A new editor automatically captures
immutable metadata with inclusion enabled. Description edits and reopening the
same in-memory draft do not recollect it or replace its report identity. Metadata
is available as a bounded, literal, paginated preview behind View metadata, with
prior/current provenance. Available Resume and Delete actions are grouped under Draft
options when a saved draft exists; Save local remains directly available.
The editor collects summary, reproduction steps, expected vs actual, and an explicit
cannot-reproduce choice. The combined narrative limit is 8,192 UTF-8 bytes; an
oversized edit remains visible for correction and cannot be reviewed or saved. A
new report defaults to metadata included; recovered drafts restore no sharing
consent. Logs remain off. Explicit review prepares one immutable plan containing
the exact fields, title, URL mode, report text and optional diagnostics JSON.
Edits or sharing changes invalidate it. Nothing is silently truncated; oversized
URLs use the template-only route and every form field has an explicit copy control.

Restart offers wait for stable native title focus after splash completion and do
not interrupt an active run. Review creates a report using preserved previous
metadata and a fresh current-launch snapshot. Review alone does not acknowledge
the incident. Save local explicitly persists the editor state, but does not link
or acknowledge the restart incident, so the offer can return next launch. Unsaved
edits remain in memory and are lost on scene-owner replacement or process exit. Dismiss writes
a durable disposition on the worker and returns its result to the main thread.
A failed dismissal reports that the offer may return. A manual draft is not replaced
by a delayed automatic offer. Continue to GitHub requires an explicit reviewed plan.
It first saves the local editor draft, then publishes the exact report text and
optional metadata file with a validated manifest under the reporting lease. A
failure keeps browser navigation closed and leaves the per-field and full-report
copy routes available. Full metadata never enters the URL; only compact
report/capture references and sharing notes appear in the diagnostics field. The
form uses a valid `Not sure` frequency choice, and its environment field refers to
the separate optional file. URLs are fully encoded and limited to 6,000 ASCII bytes;
oversized URLs open the blank template without truncating the plan. Once the reviewed
file exists, its local diagnostics path can be copied for explicit selection in
GitHub. Selecting it uploads publicly before the player submits. The game requests
a browser but does not upload or submit the issue itself, and reports only that a
browser was requested. URL text may remain in browser history.

The editor also supports one explicitly saved local draft. Saving snapshots the
bounded narrative and both captured metadata observations before handing storage
to the worker. Repeated saves update the same report; a different report cannot
silently replace the saved slot. Resume saved requires confirmation before replacing
in-memory edits. The first manual opening after restart restores an available saved
draft without capturing new metadata. Recovery retains report/capture identities
and observation times, disables metadata sharing and restores no review approval.
Saving unreviewed text is local persistence, not export or sharing consent.

Draft and reviewed-export publications share the reporting lease and 5 MiB quota,
validate schema, lengths and hashes, and expire after seven days. Invalid latest
publications are not replaced with guessed older data. Storage failure leaves the
live editor usable. Multiple saved drafts, oldest-report eviction and atomic
incident linkage remain unimplemented. Explicit deletion writes a validated
tombstone before allowing a replacement; it cannot resurrect an older revision.
This is a bounded recovery slice of Spec B, not its complete artifact store. New
draft-specific verification is recorded in the
[draft evidence](evidence/reporting-draft-proof.md).

Game-free checks and limitations are documented for
[metadata](../FTKModFramework/Tests/ReportingMetadata/README.md),
[session persistence](../FTKModFramework/Tests/ReportingSession/README.md),
[local drafts](../FTKModFramework/Tests/ReportingDraft/README.md) and the
[worker facade](../FTKModFramework/Tests/ReportingRuntime/README.md).
The standalone [shipped Mono check](../FTKModFramework/Tests/ReportingSession/LegacyMono/README.md)
passed storage and ownership checks on macOS without launching Unity. Integrated
live observations belong in the [restart proof record](evidence/reporting-restart-proof.md);
the earlier menu-only trials do not qualify the new editor or lifecycle wiring.
Windows ownership, complete native input/lifecycle coverage and frame/allocation
budgets remain separate gates. No release or epic/spec closure follows from a build
or these game-free checks.
