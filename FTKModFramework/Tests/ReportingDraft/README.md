# Durable local reporting draft checks

Run `dotnet run --project FTKModFramework/Tests/ReportingDraft/ReportingDraft.csproj -c Release`.
The filesystem suite covers collection creation, same-ID updates, targeted deletion, restart,
per-draft expiry, corrupt authority rejection, quotas, released leases, detached copies,
legacy imports and preservation of report IDs, capture IDs, metadata, captured logs and choice.

The existing session-store lease and serialized worker own the flat draft files. Schema 3
publishes one verified collection generation with at most ten drafts, ordered by newest save
time and report ID for ties. Saving an eleventh identity preserves all existing drafts; updating
one identity remains possible. Each description accepts at most 4,000 UTF-16 characters and
16,000 UTF-8 bytes. Current and previous logs each accept at most 128 KiB UTF-8. Metadata keeps
its existing per-source limit. The total collection body is capped at 1.5 MiB, so large captures
may fill storage before the ten-draft count limit.

Saves reserve space for another collection publication within the shared 5 MiB root quota.
Deletion uses only its actual smaller publication size. Tests exhaust save headroom and verify
that deletion still succeeds without removing unrelated files. External files can exhaust the
shared quota independently; failed mutations preserve durable state. Invalid files remain
untouched and quota-counted. Empty collection generations prevent deleted drafts from reviving.
No power-loss durability claim is made.

Valid schema-1 and schema-2 drafts import with diagnostics enabled. Legacy descriptions up to
the previous 8,256-byte limit survive import and later collection publications; editing them
requires the new description limit. Schema 3 preserves an explicit diagnostics opt-out. Copies
retain that choice, exact captured logs and provenance without collecting another session's
logs. Drafts expire after seven days; expired entries are never offered, even if unrelated files
block physical cleanup. Saving a draft does not acknowledge an unexpected-exit incident.

Worker-facade tests in ../ReportingRuntime cover main-thread callbacks, multiple drafts,
busy-root failure, failed-delete restoration and simultaneous save/dismiss completions.
These game-free tests do not qualify native UI or Windows behavior.

Reviewed legacy export checks still exercise exact UTF-8 files, text-only exclusion, included
diagnostics, idempotent revision retry, conflicting bytes, shared quota, corruption and validated
expiry. Export records remain independent of the saved-draft collection.
