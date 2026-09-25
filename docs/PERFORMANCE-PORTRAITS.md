# Unknown encounter portrait allocations

The experimental `Performance/AvoidUnusedEncounterPortraitTextures` setting defaults
to `false` and requires a restart. When enabled, it skips an unused texture allocation
for an enemy ID already resolved to `None`. It does not reclaim known-enemy textures.

The inspected native method allocates a readable, mipmapped ARGB32 texture and sets
its wrap mode, then replaces it with the shared unknown-enemy image. The patch skips
that complete temporary allocation, assignment, and wrap operation. It uses the
already-resolved ID and preserves the native unknown image, level visibility, and
portrait background updates. Known enemies and IDs later hidden by Deimos keep the
native path. Shared textures are never modified or destroyed.

The transpiler checks the full allocation fragment, field/getter/constructor operands,
local types, ID initialization, branch boundary, and absence of incoming labels or
exception regions. Unsupported instruction shapes retain native behavior and log a
warning. This is compatibility with the inspected original game, not a guarantee for
arbitrary third-party transpilers. Invalid UI dimensions can no longer throw from
this unused allocation; intermediate texture-setter notifications are also skipped.

## Live results

In the settled same-process comparison, fifty unknown-portrait initializations
retained **50 textures and 23.49 MiB** of additional Unity allocations in the native
control. The candidate created **zero** new portrait textures; its allocator delta
was -0.05 MiB, consistent with no retained texture growth and unrelated engine noise.
The earlier separate baseline measured 23.40 MiB of growth for the same fifty calls.
These are avoided allocations in this fixture, not a fixed reduction in game RAM.

Both the shared unknown image and the tested known-enemy image had identical GPU
readback hashes in the candidate and native control. Twenty-one known-enemy calls
still retained twenty-one textures in the candidate. A known-to-player transition
kept both native UI texture aliases in agreement. The full mixed sequence retained
21 candidate textures versus 72 in the native control; the 51 avoided textures
correspond exactly to its 51 unknown calls.

The fifty-call initialization batch took 8.35 ms with the candidate and 96.89 ms in
the same-process native control. These are exploratory single-batch wall times,
not statistical CPU benchmarks: ordering, warmup, scheduling and engine activity
were uncontrolled, and known-enemy timings also varied despite their unchanged path.
No process CPU reduction or FPS improvement is inferred from them.

## Measurement boundaries

The isolated fixture constructs a widget and invokes the real native initializer,
which creates its normal portrait child. It does not drive an actual encounter menu.
Candidate texture observations count newly loaded 328 by 280 ARGB32 textures; the
original baseline also instruments the constructor directly. GPU readback occurs
after initialization timing. Recorded Unity allocator values include unrelated engine
activity and are not process RSS or total graphics memory.

The same-process control removes only this transpiler after destroying the candidate
widget. Both arms use the same settled scene. All constructed widgets are destroyed;
remaining native known-enemy allocations are released when the owned player exits.
The original isolated configuration is restored afterward.

## Rejected approaches

Two cleanup implementations were measured and discarded. The first reclaimed or
reused owned textures after scanning loaded references on every initialization;
50 unknown portraits took 1,704 ms versus 42.6 ms in the original baseline. The second
batched cleanup outside initialization, but each global reference census still added
24 to 40 ms of synchronous work. Neither implementation is included in the framework.
The final patch performs no resource census, per-frame cleanup, ownership tracking,
texture reuse, or texture destruction.

## Coverage

Ninety-one game-free checks exercise the IL preflight's acceptance and rejection
boundaries. They do not execute the rewritten native method. Live fixture results
are archived separately, including premature scene-setup captures and both rejected
implementations. No general CPU-utilization, FPS, long-session stability, end-to-end
menu, Deimos, combat, co-op, or other-platform improvement is claimed. Broader UI
coverage remains required before enabling this experimental setting by default.

[Numeric evidence](evidence/unknown-portrait-allocation-2026-09-24/summary.json)
and [owned fixture source](evidence/unknown-portrait-allocation-2026-09-24/owned-fixture-source/README.md).
