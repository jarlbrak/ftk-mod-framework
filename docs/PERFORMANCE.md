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

The [subsystem replacement investigation](PERFORMANCE-SUBSYSTEMS.md) compares
GPU water, shared cloud positioning, and persistent hex overlays. Its temporary
work-removal measurements are exploratory evidence, not shipped improvements.

The subsequent [native water experiment](PERFORMANCE-NATIVE-WATER.md) demonstrates
a large reduction in water CPU work and a 31.5% FPS gain in one measured view.
Other views and background runs were mixed. The isolated developer tool is
separate from the normal framework and is not enabled by default.

## Whole-framework result

The latest [whole-framework in-game benchmark](PERFORMANCE-OVERALL.md) did not
establish a repeatable overall gain. Its planned comparison was slower with the
current build, while a follow-up was faster. All runs and host/focus limitations
are retained; isolated gains below must not be presented as whole-game gains.

## Water mesh optimization

The framework reduces repeated calculations in the native `WaterDistort` and
`LakeDistort` render callbacks. It preserves vertex positions, cursor advancement,
callback frequency, triangle overwrite order, and the existing mesh uploads. It
does not lower graphics settings or change the animation rate.

`Performance/OptimizeWaterMeshes` defaults to `true` in the framework configuration.
Set it to `false` and restart the game to prevent both water patches from being
installed. This is also the compatibility opt-out for another mod that changes
these callbacks. Changing the setting during play does not toggle the patches.

Noise reuse requires previously initialized results with bit-identical actual
Perlin arguments. Changed coordinates cannot reuse a result merely because their
vertices originally shared a position. Nonfinite arguments and unsupported cache
inputs call the original noise function. The cache retains at most 128 source
arrays and 65,536 vertices, with an additional 4 MiB estimated-retention budget;
these limits exclude temporary cache-construction allocations. Scene unload clears
the cache, and framework shutdown clears it and disables both optimization helpers.

For well-conditioned triangles, the second and third corner normals reuse the
first corner's normal. This changes floating-point rounding, so normals are not
bit-exact. A finite-value, scale, area, and edge-consistency guard rejects poorly
conditioned or unsupported inputs and executes the original corner expressions.
The differential tests use a maximum component-error acceptance ceiling of `1e-4`;
that test ceiling is not a mathematical error guarantee for every possible mesh.

The transpiler preflights the expressions and local-variable relationships before
rewriting them. Unsupported instruction shapes, branch or exception boundaries
inside matched expressions, and external uses of skipped temporary locals leave
the incoming method unchanged. It runs at Harmony's `Priority.Last` to inspect
earlier transforms. Another transform running afterward can still conflict;
compatibility with arbitrary third-party patches is not established.

The measured candidate passed 126,380 game-free assertions, including emitted-IL
execution, cache bounds, lifecycle callbacks, numerical comparisons, and rejection
of altered instruction patterns. These tests use a modern CLR with engine stubs;
they do not establish native Mono behavior by themselves.

Native validation of candidate SHA-256
`9f029d8e80e2d49a21af5f01c2cca48704dd013c76379ef4cf587b14e98a5499`
compared 25 callbacks of each water type in an isolated single-player overworld.
Across 41,875 vertex comparisons there were zero bit mismatches. Across 41,875
final normal comparisons, the maximum component difference was
`2.9802322387695313e-8` for water and `1.1920928955078125e-7` for lakes, with no
nonfinite or zero-vector classification changes. This establishes numerical
agreement for the observed scenario, not combat, co-op, other-platform, or
long-session coverage. It does not by itself establish an FPS improvement.

A native Save and Exit / Resume cycle cleared cache retention from 1,675 vertices
and 54,112 estimated bytes to zero, then rebuilt it to the same bounded state.
A second 41,875-vertex comparison after resume again had zero bit mismatches;
normal error stayed at or below `1.1920928955078125e-7` with no classification
changes. A separate restart with `OptimizeWaterMeshes=false` reported disabled
helpers, zero retained cache entries, and no Harmony patches on either callback.
The owned game process was stopped and the opt-out test configuration restored.

## Measured water result

On September 24, 2026, eight 900-frame captures alternated the two water patches
fully removed and installed in one isolated game process at a fixed town-hex
camera position. Every capture remained focused. The sequence was
off/on/on/off/off/on/on/off, with 120 warmup frames and no managed callback timers
during sampling. Harmony inventory confirmed zero water patches in each off arm
and exactly one production transpiler per callback in each on arm.

| Metric, median of four trials per mode | Original water code | Optimized water code |
| --- | ---: | ---: |
| FPS, reciprocal of mean frame time | 106.49 | 115.50 |
| Mean frame time | 9.39 ms | 8.66 ms |
| 95th-percentile frame time | 11.07 ms | 10.09 ms |
| 99th-percentile frame time | 13.41 ms | 12.16 ms |

This is an observed 8.5% FPS increase and 8.9% p95 reduction in this scenario.
All four adjacent pairs improved FPS; three improved p95. Other framework patches
remain identical in both arms, so this isolates the water change rather than
comparing entire framework versions. The original-water arm has no optimization
helper-call overhead. No statistical-significance claim is made from four pairs.

A preceding crossover that only disabled the helpers measured a 6.5% FPS increase
and 6.9% p95 reduction, but retained fallback-call overhead. Its p99 result worsened.
Across both experiments, large-hitch counts and p99 were not consistently improved.
These measurements therefore do not establish elimination of large hitches or
broadly smoother combat, menus or networking.

Separate baseline-DLL runs used the same camera and settings. The last three-run
baseline batch measured median 115.42 FPS and p95 11.06 ms; the preceding candidate
batch measured 122.52 FPS and p95 9.14 ms. Earlier restart comparisons were strongly
confounded by another game, a VM and compiler processes. Their much larger gap
cannot be attributed to this patch. The evidence record retains every anchored
run and excluded setup/focus run instead of combining them into one gain estimate.

See [all trials, binary identities and numerical evidence](evidence/performance-water-2026-09-24.json).
These results describe one Apple M5 running the macOS game through its shipped
Mono runtime, with 1280x720 windowed rendering, quality level 4 and VSync disabled.
Both comparison arms used the same graphics settings and diagnostic plugins.

### Current-master integration repeat

After merging master `d7be6630`, the water source was unchanged. The rebuilt
framework SHA-256 was
`7e16f411cf704dddb38d24b2547354a02ab2b6b869ac8f4f74a14d54ccad929e`.
Release build and the water, lookup and probe suites passed again. Native validation
again compared 41,875 vertices with zero bit mismatches and maximum normal
component error `1.1920928955078125e-7`.

Another eight-run original-water/optimized-water crossover on this merged binary
measured median trial FPS of 116.97 / 119.75 (+2.4%) and p95 of 9.47 / 9.37 ms
(1.1% lower). One optimized trial contained eight frames over 33 ms. Across all
frames, pooled mean time improved only from 8.5444 to 8.5241 ms, about 0.24%.
This smaller repeat reinforces the limits: typical-run gains vary with conditions,
and large hitches are not solved. These trials are recorded separately rather than
pooled with the earlier binary's results.

The new upstream reporting UI surfaced a native `AkInitializer.OnApplicationFocus`
null-reference exception during startup. Its prompt was dismissed without sending
a report, then native setup resumed. No water-patch error was observed. This is a
separate startup observation, not a passing audio-initialization claim.

## Repeating the overworld comparison

Use the same isolated save, quality preset, resolution, zoom, rotation and tilt for
both builds. Hero formation offsets can change on reload, so following a portrait
alone does not establish an identical camera position. From the town view, use
NextLocation and then PrevLocation after each movement settles to center the town
hex itself. Check camera position before sampling. In the measured fixture this
used native Tab and Shift+Tab input, followed by the maximum zoom distance of 70.

Collect repeated alternating baseline and candidate batches with managed callback
timers disabled. Keep prototype patches off. Record all trials, including those
excluded because focus changes or the setup differs. Compare only matching focus
conditions. Capture preparation, screenshots and numerical validation belong
outside the measured interval. Variable host load remains a limitation even when
these controls match.

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

The lookup candidate, before the water changes, passed the native-input harness's 60 checks,
created a single-player adventure through native UI controls, and displayed its
three heroes in the overworld. Four development self-test PASS lines were
observed; the two deliberate AddPassive rejection errors were expected. Release
builds, 115 game-free lookup checks and 28 probe tests passed. This smoke does not
establish combat, save/reload or co-op coverage. The disposable test game was
stopped after verification; production plugins and saves were not modified.

## Resource utilization

See [CPU and memory utilization](PERFORMANCE-RESOURCES.md) for measured model
texture-copy savings, transaction-local PNG sharing, CPU-per-frame comparisons,
and the separate reduced-texture-resolution experiment. Process footprint, RSS,
Unity allocator counters and managed heap values overlap and must not be summed.
