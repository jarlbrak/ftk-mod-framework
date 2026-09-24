# Reporting metadata checks

Run `dotnet run --project FTKModFramework/Tests/ReportingMetadata/ReportingMetadata.csproj -c Release`.
The game-free suite links the production collector and report factory. It checks automatic
collection at report creation, detached previous/current observations, collector failure
recovery, optional metadata exclusion, logs off, selection versus unknown load outcomes,
initialization/reload authority, sensitive-field exclusion and the final UTF-8 byte cap.

The source adapter copies existing references on the Unity thread. It never discovers mods,
reads saves, starts networking or probes arbitrary configuration. The first implementation
omits free-form display names/descriptions and restricts machine fields to identifier syntax,
with conservative exclusions for credential patterns, addresses and long opaque strings.
Excluded fields are null; inventory records disclose field exclusion. This is not a general
log sanitizer. Logs, session role and adventure identifiers have no producer in this slice.
The context flag is `session_or_transition`, because the native started flag precedes settled
play. An absent or unsupported plugin metadata source is unavailable, never an empty success.

The factory captures once and preserves immutable strings. It has no browser, export, review,
narrative editor or draft persistence. `IncludeMetadata` defaults on and can be disabled;
logs remain unavailable. The native ReportingPanel now adds bounded description editing, metadata preview,
exact-text review and explicit clipboard copy. Plugin and ReportingRuntime wire startup
checkpoints and shutdown observation, while ReportingMenu defers restart offers until title
readiness. These integration paths are outside this linked-source metadata suite. The UI
retains an in-memory draft on Back/reopen and supports one explicitly saved local draft
through the separate [draft service](../ReportingDraft/README.md). Optional log collection,
browser handoff and submission remain absent. See the [integrated evidence record](../../../docs/evidence/reporting-restart-proof.md)
for actual live observations rather than inferring them from these factory checks.
No live-game, frame-time, allocation, shutdown-callback or platform claim follows from this test.
