# Native water experiment

Developer-only instrumentation for an isolated original For The King macOS x86_64 player.
This is not a framework feature, supported mod release, or performance guarantee. It is OFF
on every process launch and has no framework project or packaging dependency.

See the [measured results and limitations](../../../docs/PERFORMANCE-NATIVE-WATER.md)
for the numerical checks, frame captures and mixed slow-frame results.

The native batch computes water deformation and normals in one interop call. The existing
cursor advancement, lake cursor wrap, mesh lookup, vertex upload and normal upload remain in
place. A rejected native input falls through to the existing managed arithmetic. Mesh assets,
quality settings, update frequency and animation clocks are not replaced.

Only macOS x86_64 Unity 2017.2 Mono is supported. Interop also requires the 12-byte Vector3 layout with component offsets 0, 4 and 8. An Apple Silicon host runs the game's x86_64
process through translation. Other platforms/runtimes fail closed before native loading.
The companion is loaded by its exact path beside this plugin, inside the explicitly isolated
root, rather than by a process-global library search. It remains loaded until process exit.

## Build and isolated deployment

Prerequisites: macOS, Xcode command-line tools, .NET SDK, an isolated game copy directly under
`scratch`, BepInEx and the configured runtime model-test isolation helper. Read
[the isolation workflow](../../../.agents/skills/ingame-smoke/SKILL.md) before deployment.
Never use the Steam installation, production saves or a running process for deployment.

From the repository root:

```bash
bash tools/performance/native-water/build.sh "$TEST_GAME_ROOT"
```

The script builds net35 managed interop against the isolated game's assemblies and an x86_64
C++ dylib with strict floating-point flags. It prints source/binary hashes and does not deploy.
Do not remove the flags, enable fast math, or assume another compiler produces equivalent math.

With the isolated game stopped, copy only `FtkNativeWater.dll` and
`libftk_water_native.dylib` from `bin/Release/net35/` into
`$TEST_GAME_ROOT/BepInEx/plugins/native-water/`. Remove other experimental water plugins from
that test copy. Keep the two files together; restart after changing either binary. Do not add
compiled libraries, game assemblies or capture files to git.

Launch through the configured isolated route with:

```text
FTK_MODEL_TEST=1
FTK_MODEL_TEST_ROOT=<exact isolated game root>
FTK_NATIVE_WATER=1
```

The active `com.ftkmf.runtime-model-test` helper is mandatory. No normal framework dependency
is declared. Only the inspected `com.ftkmf.framework` water patch shape and binary hash are allowed; other water patch owners
cause activation to refuse. The inspected game assembly is SHA-256
`94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`;
the allowed patched framework binary is
`7e16f411cf704dddb38d24b2547354a02ab2b6b869ac8f4f74a14d54ccad929e`
or the texture-memory build
`aac00e1e03a1fb53ba9bfbd8b4017c2f82bfecfe03d4531a6f1906d2993a2223`.
The latter changes framework-owned PNG uploads, transaction-local PNG sharing, and splash texture lifetime; its
decompiled water transpiler is unchanged. The unknown-portrait allocation build
`b307c20a76bec2d0cacabbd9f17c167e3f722f142e39ac9ee6bdbe15801c75c8`
is also allowed after confirming the same unchanged water transpiler.
A different hash requires a fresh method-body review before changing these developer pins.
Boundary, schema and call checks reject unsupported shapes; they do not prove equivalence against arbitrary modified IL.
This is an experimental matcher, not a guarantee of compatibility with later third-party patches.

## Command protocol and gates

Atomically replace `$TEST_GAME_ROOT/native-water-command.json` with, for example:

```json
{"id":"numeric-001","action":"selftest"}
```

Results appear as `native-water-<id>.json` in that root. Use a fresh ID each time, containing
1-80 ASCII letters, digits, underscores or hyphens. Existing result files are never overwritten.
A command left over from an earlier process is ignored. Supported actions:

| Action | Behavior |
| --- | --- |
| `selftest` | Disable the experiment; test native normal arithmetic against running Unity operations and compare native-bound Perlin against running Unity Perlin; test complete water batches. Does not enable patches. |
| `on` | Enable only after all three numeric gates pass in this process. |
| `off` | Remove only this experiment's patches, restoring prior managed callbacks. |
| `inventory` | Return enabled state, process-lifetime completion/fallback counters and evidence hashes. |

Require `selfTestsPassed: true`, no `error`, an exact normal mode, zero normal bit mismatches,
no rejected valid normal cases, successful invalid-topology rejection, and 10,081 Perlin
comparisons with zero bit mismatches. Require the end-to-end `water.ready` gate as well:
7,680 vertex/normal component checks across 256 synthetic meshes, plus invalid-input rejection
without writes. All three numeric stages must pass before `on` can patch callbacks. Normal tests use 2,400 synthetic cases per arithmetic
mode, including degenerate/nonfinite inputs, repeated indices and unused normal slots. These
are runtime numeric checks, not a proof of every live mesh, camera or lifecycle.

The native Perlin target is resolved through the running Mono internal-call lookup only after
checking the reflected static signature and InternalCall flag. This is an unsupported engine
interop boundary and is deliberately restricted to an isolated developer process. Native faults
such as invalid machine-code targets or memory corruption cannot be caught or recovered by the
managed fallback. The fallback contract applies to ordinary rejected-input return values.

## Verification workflow

1. Build; retain printed source/binary hashes. A successful build is game-free evidence only.
2. Launch the isolated game, issue `selftest`, and inspect its complete result. Do not time tests.
3. Load a native saved test scene, issue `inventory` while OFF, then `on`. Confirm that completed
   callback counters increase and that fallback/error counters do not conceal failed activation.
4. Independently compare actual callback output to native Unity calculations at the same updated
   cursor before making correctness claims. Numeric self-tests alone do not validate the IL hooks.
5. Collect matched `off/on/on/off` frame captures with the same scene, camera, resolution, graphics,
   focus state and instrumentation. Use [frame-profiler](../frame-profiler/README.md); do not
   execute self-tests, build code or inspect full scene state during timed captures.
6. Repeat with a second view and camera movement. Record mean, median, tail frame times and GC,
   plus hashes and all controls. Restore `off` and verify that only this experiment's owner vanished.
7. Stop the isolated game before changing or removing binaries.

Always distinguish this packaged tool's measurements from earlier scratch prototypes. No FPS
claim is implied by this source drop. Save/load, co-op, other platforms and broad gameplay remain
separate validation gates. Existing framework face-normal reuse can differ slightly from the
original normal arithmetic; this experiment's selected native mode is checked against running
Unity's original three-corner calculations, not a cached framework approximation.

## Game-free native boundary checks

```bash
bash tools/performance/native-water/tests/test-kernel.sh
```

This compiles the actual kernel source into a temporary host-architecture executable and runs
bounded safety checks: invalid indices/counts, null pointers and identical input/output buffer
rejection before writes; untouched unreferenced normals; triangle winding; degenerate negative
zero; and full-water rejection without writes when Unity Perlin is unbound. It needs a C++11
compiler but no game assemblies or Unity process. The test-only `FTK_WATER_KERNEL_TEST` macro
bypasses the production architecture guard; the regular build never defines it. Artifacts live
in a temporary directory and are removed on exit.

These tests do not validate Mono interop, production x86_64 rounding, the internal-call binding,
Harmony hooks, live mesh ownership, or performance. The isolated runtime gates remain mandatory.
