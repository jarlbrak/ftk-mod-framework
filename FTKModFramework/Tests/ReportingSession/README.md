# Reporting session persistence checks

Run `dotnet run --project FTKModFramework/Tests/ReportingSession/ReportingSession.csproj -c Release`.
The suite creates only disposable private test directories and child processes it owns.
It tests real process lease contention, forced termination, orderly shutdown observation,
previous/current provenance, pending incident priority, durable link/dismiss, expiry,
corruption, staging quota and per-record limits. The source also builds with the framework's
net35 target. A separate [shipped Mono check](LegacyMono/README.md) qualifies the
standalone macOS storage/lease path. Windows locking and native callbacks remain open.

The internal store accepts an explicit dedicated root and already sanitized primitive metadata.
It does not collect game state or create a report draft. Persist a draft before calling
`TryLinkPendingReport`; a non-null `ReportId` is durable caller-provided linkage, not proof
of an issue, submission or saved draft existence. The linked incident retires on the next
launch; its saved draft owns the prior evidence so a later distinct crash can be offered. `Dispose` releases ownership and does not
record normal shutdown. Only the appropriate application quit callback should call
`TryRecordShutdown`. Every mutation reports failure; failure suppresses `Pending` for the
current launch and disables further writes. Manual reporting can continue independently.

Immutable hashed state revisions publish current and pending records together. A truncated or
invalid latest revision disables tracking; it never falls back to an older dirty session and
invent a crash. Staging space counts against quota. Record content is binary rather than JSON;
metadata is a detached UTF-8 string. Records have no power-loss durability guarantee. Corrupt
storage is left untouched for explicit recovery. Cleanup requires strict generated names
and valid schema/hash/records; unrecognized or invalid files remain quota-counted. The store rejects linked roots and files.
Net35 eagerly enumerates directory names before its 128-file bound; only a dedicated owned
reporting root is supported. No game process or live store is used by these checks.
