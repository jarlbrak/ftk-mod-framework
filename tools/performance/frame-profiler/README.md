# Isolated frame profiler

Developer-only BepInEx instrument for Unity 2017.2. Never distribute with the framework.
It changes no rendering settings and uses the installed engine's public `Sampler` and
`Recorder` APIs. The recorder reports native CPU sample timings, not GPU timings.

Build from the repository root:

```sh
dotnet build tools/performance/frame-profiler/FrameProfiler.csproj -c Release \
  -p:TestGameRoot="$PWD/scratch/perf-game"
```

Copy `bin/Release/net35/FtkFrameProfiler.dll` to the isolated copy's BepInEx plugins.
Launch that copy with `FTK_MODEL_TEST=1`, `FTK_FRAME_PROFILE=1`, and
`FTK_MODEL_TEST_ROOT` set to its exact absolute root. The root must be a nonsymlink
immediate child of a directory named `scratch`, and the runtime-model-test isolation
plugin must already be active. See [the benchmark guide](../README.md).

Atomically replace `frame-profile-command.json` in that game root with one of:

```json
{"id":"inventory-1","action":"inventory"}
```

```json
{"id":"capture-1","action":"capture","frames":600,"seconds":60,"samplers":[]}
```

Use names returned in the inventory's `samplers` array to select up to 128 unique
samplers. An empty array records only frame intervals. Unavailable samplers reject
the capture. IDs accept 1 to 64 ASCII letters, digits, hyphens, or underscores and
must be new on each request. The output reservation prevents replay after restart.

Results appear in `frame-profile-ID.json`; captures additionally write CSV alongside
it. Inventory includes scene camera components, enabled state, render paths and target
textures, display settings, and hashes of loaded assemblies. Hashing and inventory
happen before capture, followed by 120 warmup frames. Capture stops at either bound:
1 to 10,000 frames or 1 to 300 seconds. Defaults are 600 frames and 60 seconds.
Commands are not polled during capture.

Capture stores unscaled frame intervals, recorder nanoseconds, and sample block counts
in preallocated arrays at `Update`, then restores each recorder's prior enabled state
before writing results. Recorder aggregation and frame alignment are engine-defined;
nested markers overlap and must not be summed. Zero counters do not establish that
no work happened. Confirm nonzero recorder support on the actual player build before
using counters as evidence. Frame times include recorder instrumentation overhead;
repeat comparisons with empty sampler arrays and with the diagnostic plugin absent.

This tool does not establish a FPS improvement itself. Use matched isolated saves,
scenarios, display settings, hardware, mod sets, repeated runs, and preserved build
hashes. CSV samples exclude setup and output writes, but other running diagnostic
plugins can still affect the measurement. Handled failures restore owned instrumentation, release sample buffers, and mark the
reserved result `failed` where writable. A cleanup failure blocks further captures
until restart. An empty or `warming` result after a crash is incomplete evidence.

## Managed callback fallback

Release players may return no native sampler names. Inventory also reports enabled,
active `MonoBehaviour` type counts and exact `managedCallbacks` names, plus public
primitive, enum, color, and vector fields on camera components. Properties and object
references are not inspected.

A capture can select up to 256 callbacks from that inventory:

```json
{"id":"managed-1","action":"capture","frames":600,"seconds":60,"samplers":[],"managedMethods":["ExampleComponent.Update"]}
```

Temporary Harmony prefix/postfix timers record inclusive `Stopwatch` ticks and call
counts in preallocated buffers, removing only this tool's patches afterward. The CSV
contains per-frame counts and ticks; JSON includes aggregate milliseconds and counts.
Timers use value-type Harmony state with no per-call boxing or row allocation by this
tool. Patching and dictionary lookup overhead still apply. Patched callbacks remain
patched through the warmup but do not accumulate timings until capture starts.

Managed intervals follow this plugin's `Update` boundaries, so exact frame alignment
varies with callback order. Nested callbacks overlap. Exceptional calls may be omitted
because the postfix may not execute. Use this mode to locate candidates, then disable
managed instrumentation for matched frame-time comparison. These CPU durations do not
measure asynchronous GPU work or establish causality for an entire frame stall.


Inventory filters camera and behavior objects to valid, loaded scenes and reports each
camera's scene name. Behavior counts additionally require enabled components on active
GameObjects. Disabled camera components can still appear in the camera inventory.
Ambiguous overloaded callback names are omitted from the selectable method list.

Capture metadata records GC collection counts for generations 0, 1, and 2 plus
`GC.GetTotalMemory(false)` at the end of warmup and immediately after the final sample,
before cleanup and output allocation. No collection is forced. Heap delta estimates
the change in managed heap size; it may include uncollected garbage and does not
measure total allocated bytes. Some Mono runtimes report matching
counts for multiple generations; do not sum those counts as independent collections.
Managed and native sample buffers are released after successful or failed captures.

Inventory records `Application.isFocused` and `Application.runInBackground`. Captures
also include an `is_focused` CSV column (1 or 0), focused/unfocused frame counts,
`focusChangedCount`, and focus/background settings at measurement boundaries. Focus
is sampled once per Update, so transitions between samples may be missed. These are
read-only observations; this tool never changes background execution or focus settings.
Compare runs with matching focus state and account for unrelated host CPU activity.
