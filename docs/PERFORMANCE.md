# Performance measurement

`FTKPerfProbe` is a separate, optional BepInEx diagnostics plugin. It is not a
framework dependency. Build it with
`dotnet build FTKPerfProbe/FTKPerfProbe.csproj -c Release`, using `FtkManagedDir`
to select the installed game's managed assemblies when necessary. Deploy the
result to an authorized test installation's `BepInEx/plugins` directory.

## Capturing frame times

F10 starts or stops a capture. The default duration is 30 seconds, configured in
`com.ftkperf.probe.cfg`. F9 toggles the overlay; leave it off during comparisons.
F11 produces a scene census outside the measured interval.

Capture stores up to 120,000 frame records in a preallocated buffer. It discards
two setup frames, then samples until the requested duration, a manual stop, or
the capacity limit. It does not overwrite earlier samples. CSV formatting and
file writes happen after sampling stops; that export can itself cause a stall
outside the capture. Buffer allocation changes retained managed memory, and two
discarded frames do not establish that the heap or GC has stabilized.

Each capture has a unique timestamp/UUID filename and a `-summary.txt` companion.
The summary records the scenario, machine, scene, graphics settings, attached
probe counts, loaded assembly identities/MVIDs, and whole-capture mean,
nearest-rank p50/p95/p99, maximum frame time, GC frames and collection counts.
Higher frame-time percentiles mean slower frames. They are not FPS percentiles.

The CSV keeps the existing column schema. `alloc_est_bytes` is positive managed
heap growth, not allocated bytes per frame. Collection frames use `-1` because
allocation cannot be inferred from the net heap change. Bucket timings are
inclusive and may overlap; do not add them as exclusive CPU costs. The overworld
bucket now measures both `Update` and `LateUpdate` where present, so its values
are not comparable to captures made with the older probe.

Developer tooling can invoke these APIs on the Unity main thread:

```csharp
FTKPerfProbe.Plugin.StartCapture("overworld-stationary", 30);
// After CaptureActive becomes false, inspect LastCapturePath and LastCaptureError.
```

`StartCapture` returns false when unavailable, already capturing, or unable to
start. `LastCapturePath` is populated only after a successful export. This API
does not create a network endpoint or enable the agent bridge.

## Comparing a change

1. Keep the exact probe binary and coverage settings identical between builds.
   Record framework and game binary hashes alongside the summaries.
2. Match hardware, graphics settings, resolution, frame cap, save, camera, party,
   mod set and scenario. Warm up the scenario before collecting multiple runs.
3. Keep overlays, screenshot/video capture, scene census, build jobs and other
   diagnostic work outside the measured interval. Record unavoidable host load.
4. Alternate baseline and candidate runs when practical. Report the full run
   distribution, not only the fastest result. Separately check probe overhead.
5. Distinguish method benchmarks, load-time measurements and gameplay frame
   captures. A faster lookup does not imply a corresponding FPS increase.

Use the [scale-budget gate](SCALE-BUDGET.md) for content-loading regressions.
It does not measure gameplay frame rate. Live tests must use the configured
isolated installation and preserve production saves and plugins.

## Locating frame-time costs

The developer-only [isolated frame profiler](../tools/performance/frame-profiler/README.md)
records bounded frame captures, focus state, collection counts and estimated heap
size. It can use available Unity CPU counters or temporarily time selected managed
callbacks when a release player exposes no native counters. It never changes
graphics settings or ships with the framework.

Managed timings are inclusive and add instrumentation overhead. Use them to choose
an optimization target, then remove managed timers for FPS comparisons. Match focus
and background execution as well as graphics settings, and record competing host
load. A lower callback time alone does not establish smoother gameplay.

## Item lookup optimization and measured result

The framework resolves custom IDs before checking a bounded dictionary of exact
native item enum names. Values are derived using the original case-insensitive
parser during framework startup. Numeric strings, different casing, whitespace,
combinations and unknown names fall through to the original method. Custom
registrations are never cached, so registration changes remain visible. If cache
construction fails, the original parser remains available.

Internal one-table and two-table synthetic lookups also avoid temporary `Type[]`
arguments. The public params-array entry point remains compatible with existing
mod binaries. A game-free allocation test measured zero bytes for 400,000 such
internal calls versus 14,400,000 bytes through params-array expansion. This byte
count describes the test CLR, not Unity's allocator.

On September 24, 2026, an isolated macOS game on an Apple M5 ran the
[lookup benchmark](../tools/performance/README.md) over all 676 declared item
names, 50 times per trial (33,800 lookups). Every name matched the native parser
before each trial, and every timed checksum matched afterward.

| Build/order | Five-trial median | Observed range |
| --- | ---: | ---: |
| Baseline before | 1,794.088 ms | 1,727.713 to 1,919.423 ms |
| Final candidate | 3.201 ms | 3.160 to 3.361 ms |
| Baseline repeated afterward | 1,735.474 ms | 1,724.670 to 1,972.725 ms |

That is about 542 to 560 times faster for this warmed canonical-name workload.
These measurements use baseline commit `5561401f` and the candidate rebased onto
that commit. All trials, binary hashes and methodology are in the
[numeric evidence record](evidence/performance-lookup-2026-09-24.json).
The same probe and benchmark binaries ran in every group. No uninstrumented
comparison was made. Earlier-base exploration is retained separately, including
an 8,309.583 ms baseline outlier; it is not final-binary acceptance evidence.

An exploratory lazy cache paid 53.57 ms on its first benchmark lookup. The final
implementation constructs the cache before installing patches, moving that cost
into startup. Its first benchmark invocation took 3.509 ms, which can include JIT
and instrumentation startup. This does not establish an overall startup-time
improvement or eliminate the cache's construction cost.

These are method-throughput results, not an FPS claim. Exploratory stationary
overworld captures recorded no ID lookups, so they do not demonstrate a frame-time
benefit from this change. Gameplay, load-time and allocation rates depend on how
often a scenario resolves IDs. Co-op, other operating systems, long-session save
behavior and compatibility with third-party patches of `GetEnum` remain untested.
The native-name fast path skips the original body, which can affect another mod's
transpiler or later side-effecting Harmony prefix.

The final current-base binary also passed the native-input harness's 60 checks,
created a single-player adventure through native UI controls, and displayed its
three heroes in the overworld. Four development self-test PASS lines were
observed; the two deliberate AddPassive rejection errors were expected. Release
builds, 115 game-free lookup checks and 28 probe tests passed. This smoke does not
establish combat, save/reload or co-op coverage. The disposable test game was
stopped after verification; production plugins and saves were not modified.
