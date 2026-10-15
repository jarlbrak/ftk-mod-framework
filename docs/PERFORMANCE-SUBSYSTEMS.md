# Replacing expensive presentation subsystems

Follow-up: the [native water batch](PERFORMANCE-NATIVE-WATER.md) now provides a
working, numerically validated alternative to a GPU rewrite. The initial
screening results and proposed GPU decision gate below remain historical
exploration evidence, not the current implementation plan.

The next performance investigation stays within the existing Unity player. The
objective is to remove recurring work while preserving appearance, animation,
gameplay, saves, and multiplayer behavior. Rewriting arithmetic in another
language is useful only if it removes a measured bottleneck; it does not remove
mesh uploads, draw submission, or rendering by itself.

## September 24 exploratory measurements

Sixteen captures of 1,800 frames each compared the current framework, with its
water optimization enabled, against temporary removal of one presentation
subsystem at a time. Each block used control, candidate, candidate, control.
All 28,800 sampled frames were focused, with no focus transitions. Managed
callback timers and native sampler recording were disabled.

The isolated macOS Apple M5 fixture used Unity 2017.2.2p2 under Rosetta, 1280 by
720, quality level 4, VSync off, no configured frame cap, and the same stationary
town camera. Another game instance and a VM were active on the host. Their load
was not controlled. No trial is excluded.

The table reports the average of two trial means and the average of two trial
p95 values in each condition. Lower milliseconds are better. It does not report
a pooled p95 or statistical confidence interval.

| Work temporarily removed | Control mean | Candidate mean | Control p95 | Candidate p95 |
| --- | ---: | ---: | ---: | ---: |
| Water callbacks, first block | 9.945 ms | 10.872 ms | 13.142 ms | 15.429 ms |
| Cloud positioning | 11.125 ms | 13.420 ms | 15.259 ms | 20.700 ms |
| Hex overlay updates/submissions | 10.485 ms | 8.742 ms | 12.677 ms | 10.329 ms |
| Water callbacks, repeated block | 12.618 ms | 11.242 ms | 15.187 ms | 16.412 ms |

These are diagnostic interventions, not optimizations ready to ship:

- Water retained its last rendered mesh but stopped deformation, normal
  calculation, uploads, and animation-clock advancement.
- Clouds stopped their parent positioning callbacks. Gameplay cloud reveal
  logic was not replaced.
- Hex overlays skipped `HexInfo.Update`, including visibility timers and draw
  submissions. `HexInfo.OnWillRenderObject` and its realm audio bookkeeping
  remained untouched.

Harmony dispatch overhead remained. A correct replacement would retain useful
work and might change CPU/GPU overlap, so these experiments are not exact upper
bounds and their savings cannot be added together.

The first water block had paired mean savings of +0.337 and -2.190 ms. The repeat
had +1.033 and +1.719 ms, but worse p95 in both pairs. Overlay savings were
+0.133 and +3.354 ms, while control frame times changed substantially. Cloud
removal was slower in both pairs. This does not establish that removing cloud
work is intrinsically slower, nor that an overlay rewrite saves 3 ms.

The evidence supports further investigation, not a reliable FPS or stutter claim.
All trial summaries, settings, binary hashes, collection counts, and skipped
callback counts are in the [numeric record](evidence/performance-subsystem-ceilings-2026-09-24.json).
Temporary hooks were removed, production water patches remained enabled, and
the isolated game was stopped after capture. No production runtime change was
made for this investigation.

## Candidate replacements

These designs follow inspection of the installed managed assemblies. Feasibility
is distinct from demonstrated performance.

| Candidate | Work it could eliminate | Required invariant | Main unresolved risk |
| --- | --- | --- | --- |
| GPU water deformation and normals | CPU vertex/triangle loops and recurring vertex/normal uploads | Existing animation timing, final triangle normal assignment, material appearance, and camera behavior | Noise/normal parity, shader pass coverage, culling bounds, CPU mesh consumers, and GPU cost |
| Shared cloud positioning scheduler | Repeated common offset calculation and unchanged transform writes | Identical cloud positions and existing gameplay reveal behavior | Actual frame benefit may be small; camera and lifecycle invalidation must be complete |
| Persistent regional hex overlay renderer | Repeated per-object scans and draw submissions | Identical visible overlays and unchanged realm audio bookkeeping | Correct invalidation and culling; prior per-object combined-mesh prototype regressed |

Water is the strongest structural hypothesis, because it can eliminate an entire
CPU-to-renderer update path. The existing optimization still uploads vertices and
normals. The native noise implementation is recoverable, but matching it in a
shader is not yet verified. A material AssetBundle loading route exists in the
framework; a compatible water shader and its platform build pipeline still need
validation.

Cloud positioning is narrower: `BigCloud.LateUpdate` calculates a shared
camera-dependent offset and writes a transform. `MiniCloud.UpdateCloud` handles
gameplay transitions and is not a per-frame Unity `Update` callback. A scheduler
must preserve those transitions rather than disabling cloud objects.

A regional overlay renderer must maintain persistent geometry with explicit
invalidation. The earlier prototype still scanned tiles from individual
`HexInfo` callbacks. Its lower submission count did not improve frame times;
that design is not a basis for a performance claim.

## Next experiment and decision gate

First repeat water measurements on a quieter host in two water-heavy views,
including camera movement. Split removal of computation from removal of uploads
while preserving callback clock advancement. Randomize repeated paired blocks
and retain unchanged-control repetitions to expose drift. Measure mean, p95,
p99, and frames over 33.3 ms separately.

Proceed to a minimal GPU water prototype only if those measurements show
repeatable headroom worth its compatibility cost. A proposed project gate is
at least 1 ms or 10% lower p95 in both views; this is a decision threshold, not
an achieved result. Inconsistent or sub-0.5 ms savings would redirect the
investigation toward rendering passes and submission costs before a rewrite.

The prototype should replace one isolated water mesh, retain the original CPU
path as fallback, and verify animation, normals, shadows, depth/reflection passes,
multiple cameras, scene unload, and save/resume. A faster average with worse
slow-frame behavior does not satisfy the user's smoothness goal. Windows,
co-op, long sessions, and third-party rendering mods remain separate gates.
