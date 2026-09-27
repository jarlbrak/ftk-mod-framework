# Native water batching experiment

A developer-only native batch now replaces the water deformation and normal
calculation inside the existing Unity player. It calls Unity's own Perlin function
and preserves the original animation clocks, lake wrapping, mesh lookup, and
vertex/normal uploads. The normal framework remains unchanged.

The [isolated tool and build instructions](../tools/performance/native-water/README.md)
contain the implementation. It is OFF by default, restricted to inspected macOS
x86_64 game/framework binaries, and requires three runtime numerical gates before
activation. This is an experimental contribution, not a supported release feature.

## Measured improvement and limits

On September 24, 2026, the scratch implementation completed eight alternating
900-frame captures at a fixed town view, with all 7,200 frames focused. The control
already included the framework's existing water optimization. Resolution, quality,
animation frequency and uploads were unchanged. Managed timing hooks were absent
from these captures.

| Pooled metric, first view | Current framework | Native batch | Change |
| --- | ---: | ---: | ---: |
| Mean frame time | 7.968 ms | 6.060 ms | 23.9% lower |
| FPS from mean frame time | 125.5 | 165.0 | 31.5% higher |
| p95 frame time | 10.150 ms | 7.654 ms | 24.6% lower |
| p99 frame time | 12.862 ms | 9.560 ms | 25.7% lower |
| Frames above 33.3 ms, 3,600 frames per arm | 5 | 3 | Too few events for a reliable hitch claim |

All four adjacent pairs improved mean and p95. These values combine every frame
in the corresponding arm, rather than averaging per-trial percentiles.

A second, closer and slightly panned view did **not** establish an improvement.
Its pooled result was 117.5 FPS for control and 111.6 FPS for native, with p95
worsening from 9.625 to 13.500 ms. One native trial contained 40 frames above
33.3 ms; it remains in the results. Other FTK tests and host workloads were active
or changing during the session. Their contribution to individual stalls was not
measured, so the slower run cannot simply be discarded as external noise.

Separate instrumented captures in that second view established substantial CPU
work reduction: complete water callbacks averaged 2.031 ms per frame under the
current framework and 0.316 ms under the native batch, about 6.4 times faster or
84.4% less callback time. Each 600-frame capture contained 3,600 water callbacks
and 600 lake callbacks. This includes pinning, interop and mesh uploads, as well
as identical timing overhead. Instrumented captures are not used for FPS claims.

The packaged tool has its own binary identity and measurements. Its foreground
pair before save/resume improved from 121.7 to 170.0 FPS, with p95 falling from
9.150 to 7.356 ms. Other packaged trials ran in the background or changed focus;
not all improved p95. One foreground pair is insufficient to establish a broad
smoothness claim. All trials and pairing eligibility are retained in the
[numeric evidence](evidence/performance-native-water-2026-09-24.json).

After save/resume, eight packaged-tool captures all ran unfocused. Pooled FPS
improved from 95.2 to 104.8, but p95 worsened from 15.314 to 16.924 ms. Every
pair in that background series improved its mean and worsened its p95. These
results remain a material limit on any claim that the tool consistently reduces
lag or slow frames.

The result is a demonstrated local frame-time gain and a large reduction in water
CPU work. It does not establish that long hitches are fixed, that every view is
faster, or that the result generalizes to combat, other worlds, Windows, Linux or
co-op. The two views cover one early-game town area.

## Correctness and lifecycle evidence

The first native implementation failed strict numerical comparison. Testing
identified wider intermediate arithmetic in this Mono runtime. The accepted mode
uses matching intermediate precision rather than fast-math approximations.

The packaged tool passed these checks in the running game:

- 36,000 normal components matched bit-for-bit in the selected arithmetic mode,
  covering 2,400 cases with degenerate, tiny, huge and nonfinite inputs, repeated
  indices and untouched normal slots.
- 10,081 native-bound Perlin values matched ordinary Unity calls bit-for-bit.
- 7,680 vertex/normal components matched across 256 complete synthetic water
  batches. Invalid inputs were rejected without modifying arrays.
- Actual water/lake output matched the original calculations for 41,875 vertices
  and normal vectors before save/exit, and again for 41,875 after resume.
- Activation before self-testing was refused. Save/exit/resume completed with the
  native tool enabled, resumed callbacks advanced, and no native fallback occurred.

The native implementation pins arrays only for each synchronous call and retains
no pointers or mesh objects. Managed fallback covers rejected inputs, not native
machine faults. Isolation and build-specific gates remain necessary.

Game-free verification passed 163 native-kernel safety assertions, covering
rejection before writes, untouched normals, winding and degenerate signed zero.
The native tool and parent lookup benchmark built without warnings. Rebuilding
the final tool reproduced both measured binary hashes. Relative documentation
links, evidence JSON and `git diff --check` passed. Final cleanup removed the
experiment's patches, verified the original framework owners, and stopped the
owned isolated game.

## Next delivery gates

The experiment supports pursuing a native accelerator, with further verification
before integrating it into normal framework distribution:

1. Repeat foreground measurements on a host without competing game tests and
   include movement, additional scenes and long-session hitch analysis.
2. Validate each additional platform's compiler, Mono ABI and numeric behavior.
3. Exercise co-op, other mods, renderer lifecycle and failure recovery independently.
4. Review native packaging, opt-in configuration and compatibility policy before
   making it a supported runtime dependency.

GPU water remains a possible future design. The native batch provides a smaller,
working way to remove managed arithmetic and per-vertex interop overhead without
a new shader toolchain or engine migration.
