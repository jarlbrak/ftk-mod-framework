# Whole-framework in-game benchmark

The September 24, 2026 comparison **does not establish a repeatable overall
performance improvement**. The planned whole-build comparison was substantially
slower with the current build; an additional current-build launch was faster than
the baseline. Both outcomes are retained. Earlier isolated water, texture and
lookup gains must not be added together or presented as whole-game gains.

## Results

Five fresh game launches produced ten accepted 30-second captures. Four other
captures lost foreground focus and are retained but excluded. The planned order
was baseline, current, current, baseline, with two captures per launch. A further
current launch checked the unexpected slowdown. It was an unplanned follow-up,
not part of the balanced comparison.

| Metric | Baseline, four captures | Current, planned four captures | Current, follow-up two captures |
| --- | ---: | ---: | ---: |
| Pooled FPS | 95.55 | 48.52 | 109.66 |
| Individual capture FPS range | 85.27 to 106.04 | 34.02 to 57.71 | 108.66 to 110.66 |
| P95 frame time | 14.38 ms | 56.49 ms | 10.67 ms |
| P99 frame time | 23.02 ms | 156.34 ms | 13.21 ms |
| Frames longer than 33.33 ms | 0.51% | 9.77% | 0.17% |
| Process CPU time per rendered frame | 18.89 ms | 24.23 ms | 18.54 ms |
| Process CPU use, one logical CPU = 100% | 180.51% | 117.50% | 203.29% |
| Median physical footprint at capture boundaries | 4.348 GiB | 4.292 GiB | 4.415 GiB |

The planned comparison observed **49.2% lower FPS**. The follow-up alone observed
**14.8% higher FPS** against the same pooled baseline. Combining every accepted
current capture yields 68.90 FPS, **27.9% lower**, p95 31.43 ms, CPU/frame 21.21 ms,
and median footprint 4.411 GiB. That combined result is descriptive: it includes
an unplanned extra current launch and has unequal observation counts.

The lower CPU percentage during the slow runs is not an efficiency win: fewer
frames were rendered and CPU time per frame increased. Memory endpoints also
changed direction between launches. Neither a reliable total CPU reduction nor
a reliable baseline-game RAM reduction is established here.

## Method and scope

Baseline is the game plus framework snapshot `5561401f`, framework DLL SHA-256
`69c00dd78bb10cc97a166bb54c18035cf3c2e130f0dac0f1cbf6159bc7f628a2`.
All 236 retained framework/probe source files were checked against that commit.
Current is `12b2be3e`, DLL SHA-256
`883065872d32b7ce717e37fd8a5eccaa9aa6d54c526eab2fbbff4eccea151c31`.
This is not vanilla versus modded, nor a single optimization toggled off/on. The
current build also contains reporting, UI and gameplay changes unrelated to this
performance work.

The isolated save, three-hero party, stationary Oarton camera, empty content
package and setup screenshots were matched. Hardware was Apple M5, macOS 26.6.2,
x86_64 Unity 2017.2.2p2/Mono, 1280 by 720 windowed, quality level 4, VSync off and
uncapped FPS. Production water optimization was enabled in the current build;
experimental native water and the experimental unknown-portrait setting were off.
The same profiler and inactive diagnostic binaries were present in both builds.

Each capture follows twenty seconds of settling after camera setup and the
profiler's 120 warmup frames. No managed callback timers, sampler counters or
forced collections were enabled. Inventory and screenshots were outside timed
sampling. Fully focused captures with zero focus changes are included regardless
of performance. Focus recovery extended baseline process age before its accepted
captures, so memory endpoints are not a controlled lifetime or leak experiment.

FPS is total sampled frames divided by their summed frame intervals. Percentiles
use pooled raw samples, index floor((n-1)*p). Two captures from one launch are
correlated; there are four independent launches in the planned comparison.
CPU includes all process threads. Physical footprint is the operating system's
accounted process footprint, not RSS or a sum of Unity, managed and driver memory.
It is measured at boundaries, not as a peak.

Competing host workloads were uncontrolled, with varying load recorded at capture
boundaries; no unrelated processes were stopped. Host contention is a plausible
contributor, not a demonstrated explanation for the entire slowdown. Framework
changes, startup state and instrumentation interactions also remain possible.
This session cannot support a statistical-significance claim, a causal regression
percentage, or a positive headline performance guarantee.

The workload does not exercise mod-heavy textures, repeated portrait creation,
combat, co-op or long-session behavior. Their separate fixture gains remain scoped
to those fixtures. A stable-host, matched-age repeat and investigation of the slow
current-build runs are required before claiming overall improvement.

## Evidence and cleanup

[All trial summaries, hashes and aggregate results](evidence/overall-performance-2026-09-24/summary.json)
include every adverse focused result and every focus exclusion. The
[local reconstruction script](evidence/overall-performance-2026-09-24/archive.py)
requires the retained ignored scratch captures; the public summary alone does not
reproduce raw frame percentiles. An independent review recomputed the statistics
from all fourteen raw CSVs and checked their hashes and inclusion rules.

The owned game was stopped, its current framework binary retained, and its original
isolated configuration restored. The normal installation and production saves
were not modified.
