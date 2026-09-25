# CPU and memory utilization

The resource pass adds CPU-time and memory observations to the isolated frame
profiler and reduces retained texture copies in framework-owned model and UI
loaders. It does not lower the game's quality settings by default.

## Changes

Explicit renderer assignments now decode each distinct resolved PNG path once
per allocation transaction. All materials in that transaction may reference the
same immutable texture; materials themselves remain private. The texture enters
the existing resource lease once. Independent transactions remain independent,
and source/clone retention continues through the existing lease.

Explicit and legacy model PNG loaders, marketplace preview images, and the splash
logo discard their CPU pixel copies after upload. The splash also destroys its
owned logo when dismissed. Vanilla textures, package item icons, imported meshes,
and asset-bundle textures are unaffected. See the
[model texture contract](MODEL-RENDERER-API.md).

## Measured model memory

Live constructed fixtures exercised the actual loaders using an original
1024 by 1024 RGBA PNG and triangle GLB. They used twenty textures or twenty
renderer parts, with GPU readback and final-owner cleanup checks.

| Fixture | Readable CPU-copy control | Uploaded without CPU copy | Reduction in settled Unity allocations |
| --- | ---: | ---: | ---: |
| Twenty independent explicit assignments | 80.05 MiB | 0.10 MiB | 79.95 MiB |
| Twenty independent legacy assignments, including mipmaps | 106.68 MiB | 0.06 MiB | 106.62 MiB |

These are differences from each case's initial Unity allocator snapshot after
settling. They are not total texture memory: this counter does not account for all
graphics storage, and process footprint is affected by native pools and compression.
The GPU copy remains usable. Temporary readback allocations are excluded from the
settled snapshot. Small residual differences include unrelated engine activity.

In the same twenty-part single-transaction fixture, PNG sharing reduced distinct
texture objects from **20 to 1**, a **95% reduction**. With CPU copies deliberately
retained, settled Unity allocation growth fell from 80.09 to 4.13 MiB, isolating the
sharing effect. With the final non-readable uploads, the redundant GPU textures are
also absent, but an exact driver-memory saving is not established by Unity's
allocator counter. Twenty independent calls still produced twenty textures.

Every tested readable/non-readable image produced the same GPU readback SHA-256.
A cloned twenty-part object retained its shared texture after source destruction;
all forty pinned source/clone texture references became Unity-null after final
owner destruction. Independent explicit and inactive legacy fixtures also released
all recorded textures. This covers constructed binding and ownership, not complete
avatar appearance, scrolling shaders, every mip level, combat, co-op, or other
platforms. UI preview and splash visual/lifetime checks remain separate live gates.

## Native unknown portrait follow-up

An opt-in [unknown portrait allocation skip](PERFORMANCE-PORTRAITS.md) avoids the
native texture that is immediately discarded for a pre-resolved unknown enemy.
This targets baseline game allocations. Known-enemy texture retention remains native;
no global cleanup scans or quality changes are introduced.

## CPU observations

The [native water experiment](PERFORMANCE-NATIVE-WATER.md) was compared on/off with
30 and 60 FPS targets. CPU time includes every process thread. A value of 100%
means one fully occupied logical CPU; it is not a percentage of the whole machine.
The added profiler performs boundary reads, not per-frame process polling.

The first eight-trial 60 FPS-target series used **31.87 to 27.46 ms of process CPU
per rendered frame**, a **13.9% reduction**. It did not sustain the target:
33.18 versus 46.32 FPS. Consequently total CPU utilization increased from 105.8%
to 127.2% while more frames were rendered. That is improved work per frame, not a
reduction in total utilization at the same throughput.

Other series are preserved in the evidence, including stalls and mismatched
throughput. The later four-trial 60 FPS-target series used 17.65 versus 16.52 ms
CPU/frame, with 59.70 versus 56.16 FPS; its lower total CPU must not be attributed
entirely to efficiency. A requested cap alone does not prove equal throughput.
The final texture-memory framework build repeated the comparison at 32.85 versus
29.39 ms CPU/frame, a 10.5% reduction, while rendering 35.39 versus 45.64 FPS.
Across all five series, pooled CPU/frame fell 6.4% to 13.9%. This remains a measured
work-per-frame result under changing host conditions.
No general stutter improvement or stable whole-machine CPU percentage is claimed.
Other game instances and host workloads continued throughout these measurements.

## Baseline memory and a quality tradeoff

An initial package-only overworld process footprint was about 4.2 GiB. A macOS
footprint snapshot attributed about 1.5 GiB to graphics categories, whereas the
managed heap was approximately 208 MiB. Much of the process footprint was
compressed or swapped. RSS alone therefore understated memory pressure.
Framework model-texture savings require models that use these loaders; the blank
package-only baseline cannot demonstrate those savings.

A separate temporary `masterTextureLimit=1` experiment retained the same quality
preset and other settings. Its two reduced-resolution observations were 4372.67
and 4378.31 MiB physical footprint; full-resolution observations were 4552.00 and
4596.36 MiB. This roughly 180 to 220 MiB difference is a **visual-quality tradeoff**,
with changing process memory and uncontrolled host load. It is not included in the
lossless gains, does not prove all textures are affected, and is not enabled in the
framework. The original setting was restored after capture.

## Evidence and verification

[All trial summaries, identities, texture results and clone observations](evidence/resource-utilization-2026-09-24/summary.json)
include negative and mixed results. The adjacent `archive.py` regenerates this
record from retained local captures. The
[frozen owned fixture source](evidence/resource-utilization-2026-09-24/owned-fixture-source/README.md)
describes the control and its boundaries. Game assemblies, decompiled game source,
assets, saves, screenshots and logs are not part of the public record.

Release framework and tooling builds passed. The resource-ledger suite passed
320 checks; the unchanged native water kernel passed 163 boundary checks. Final
native water numerical self-tests passed against the newly allowlisted framework
build after verifying its decompiled water transpiler was unchanged. Builds and
these checks do not substitute for the remaining live coverage named above.
