# Durable local reporting draft checks

Run `dotnet run --project FTKModFramework/Tests/ReportingDraft/ReportingDraft.csproj -c Release`.
The real filesystem suite covers report/capture identity, previous/current provenance,
recovery with metadata sharing off, detached cache copies, same-report updates, different-report
slot rejection, UTF-8 narrative limits, shared quota failure, normal restart, expiry, corrupt
latest state and refusal to write after the session lease is released.

The backend requires the existing session-store lease and its serialized owner. It has one
saved draft slot and no autosave. An unexpired different report is never overwritten. Explicit
save updates the same report identity; there is no deletion/replacement UI in this slice.
Save does not link or acknowledge an unexpected-exit incident, so that offer may return.

The encoded description is capped at 8,256 UTF-8 bytes, allowing bounded field framing
around the UI's 8,192-byte total narrative limit. Each current/previous metadata string is independently
capped at 261,120 UTF-8 bytes. The combined binary draft body is capped at 538,688 bytes, plus a
32-byte integrity hash. It counts with session records, temporary files and unknown files
against the shared 5 MiB root quota. Temporary publication is validated before an immutable
revision becomes visible. Seven-day expiry removes the recovery offer and attempts to remove
owned valid records; invalid content remains untouched, and a failed cleanup preserves the
latest authority rather than reviving an older draft. No power-loss durability claim is made.

The report object restores IDs, timestamps and detached metadata without collecting again.
Every recovered/cached copy starts with metadata sharing off and logs unavailable. Review is
owned by the UI and is not persisted. Worker-facade tests in ../ReportingRuntime additionally
cover save callbacks on the caller thread, busy-root failure and isolated simultaneous
save/dismiss completions. These game-free tests do not qualify native UI or Windows behavior.

Reviewed artifact checks also exercise exact UTF-8 files, text-only exclusion, included diagnostics,
idempotent revision retry, conflicting bytes, shared quota, corruption and validated expiry.
Report text is capped at 64 KiB and diagnostics at 256 KiB. A binary schema-1 manifest records
report/capture/revision identities, UTC publication, lengths and SHA-256 hashes and publishes
last. Files remain flat in the leased root; at most 16 export manifests are retained. Existing
matching revisions are reused only after payload validation. Interrupted unpublished files
remain quota-counted; invalid or unknown files never authorize deletion.
