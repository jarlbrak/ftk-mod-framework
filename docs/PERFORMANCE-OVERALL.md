# Whole-framework in-game benchmark

The September 25 dedicated-window repeat measured **7.52% higher average FPS**
and **3.50% less CPU time per rendered frame** in the stationary Oarton scenario.
Both adjacent launch pairs improved average FPS. RAM was effectively unchanged,
p95 frame time was unchanged, and p99 worsened. This is a modest local throughput
result, not a demonstrated improvement in hitching or whole-game memory use.
Earlier contradictory results remain below; isolated fixture gains must not be
added together or presented as whole-game gains.

## September 25 dedicated-window results

| Metric | Baseline | Current |
| --- | ---: | ---: |
| Pooled FPS | 110.44 | 118.74 |
| Individual capture FPS range | 109.26 to 111.12 | 110.72 to 124.52 |
| P95 frame time | 10.158 ms | 10.162 ms |
| P99 frame time | 11.12 ms | 13.86 ms |
| Frames longer than 33.33 ms | 0.068% | 0.126% |
| Process CPU time per rendered frame | 17.92 ms | 17.29 ms |
| Process CPU use, one logical CPU = 100% | 197.89% | 205.32% |
| Median physical footprint at capture boundaries | 4.395 GiB | 4.396 GiB |

The two adjacent launch-pair FPS improvements were **8.05% and 6.99%**.
Total process CPU utilization increased while rendering more frames; the measured
CPU saving is per frame, not lower total utilization at an equal frame rate.
P99 worsened by 24.6%, with more frames above 33.33 ms. No reduction in lag or
hitching is established, and the memory difference is negligible.

The same baseline/current binaries, instrumentation, save, camera and display
settings described below were used. Production water was enabled in current;
experimental native water and unknown-portrait changes remained off. The user
provided a dedicated foreground window. Only the benchmark game instance was
running at the checked setup, with no competing build observed; Time Machine
and ordinary host services remained active. Recorded one-minute load samples
ranged 6.04 to 8.42 for baseline and 5.56 to 8.86 for current. This is quieter
than the earlier slow runs, not complete host isolation.

Four accepted fresh launches followed baseline/current/current/baseline, each
with 60 seconds of post-camera settling, 120 profiler warmup frames and two
30-second captures. All eight captures were focused throughout; none was
excluded. One additional closing-baseline setup failed camera alignment and
was restarted before sampling. A temporary idle-sleep prevention assertion was
used for the final accepted baseline process only and ended on process exit.
Setup screenshots matched; screenshots and inventory were outside sampling.

There are only four independent launches; captures within a launch are correlated.
This does not establish statistical significance or generalize to combat,
co-op, other machines, mod-heavy content or long sessions. Diagnostic overhead
was matched but not separately quantified. Yesterday's conflicting evidence
still limits any broad claim that the framework is consistently faster.

[Dedicated-window numeric evidence](evidence/overall-performance-dedicated-2026-09-25/summary.json)
and [local reconstruction script](evidence/overall-performance-dedicated-2026-09-25/archive.py)
retain every timed capture. Independent review reproduced the pooled metrics,
launch-pair gains, hashes and focus/settings checks from the eight raw CSVs.
The script requires ignored local captures. The owned game was stopped, the
current framework restored, and the original isolated configuration restored.

## September 24 results

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

## September 25 repeat attempt

A quieter-host repeat was attempted with the same binaries, save, camera and
settings, using 60 seconds of settling before two 30-second captures per launch.
The first baseline launch produced two fully focused captures at 103.21 and
93.48 FPS. Both current-build captures and both same-process retries were
unfocused and are retained as excluded. There is no valid current arm, so this
attempt supplies no new improvement percentage.

During the retry, another isolated FTK instance was running and a concurrent
TypeScript compiler snapshot used approximately four logical CPUs. Host
one-minute load rose above 22. The attempt stopped before completing ABBA;
unrelated processes were left running. The owned game was stopped and its
original isolated configuration restored. A dedicated window without concurrent
game testing or heavy builds remains necessary.

[Repeat attempt evidence](evidence/overall-performance-repeat-2026-09-25.json)
records all six captures and their focus exclusions. These interference
observations do not establish the cause of every September 24 slowdown.
