# Reporting worker facade checks

Run the linked-source harness in both modes:

```
dotnet run --project FTKModFramework/Tests/ReportingRuntime/ReportingRuntime.csproj -c Release
dotnet run --project FTKModFramework/Tests/ReportingRuntime/ReportingRuntime.csproj -c Release -- busy
```

Only the source adapter, BepInEx paths and hot-reload observations are stubbed. The actual
runtime facade, record store, lease, metadata formatter and report object run against private
throwaway directories. Checks cover early startup without game reads, prior report provenance,
review without acknowledgement, stable epoch refresh, deferred main-thread dismissal callback,
worker shutdown ordering and manual reporting when another owner holds the root.
Draft checks cover durable worker saves, detached recovery copies with sharing reset, busy-root
save failure and simultaneous save/dismiss completions when one UI callback throws.

This does not qualify native Unity lifecycle callbacks, collector performance or native UI.
Startup coverage begins when the background owner acquires the lease. Shutdown waits at most
two seconds for queued work; a blocked write may leave an unexpected-exit offer next launch.
No actual game, installation report store or non-test process is accessed.
