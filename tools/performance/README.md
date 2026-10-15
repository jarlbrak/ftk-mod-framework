# Isolated lookup benchmark

`FtkLookupBenchmark.dll` is an opt-in diagnostic plugin for the original game's
Mono runtime. It is not a shipped framework component or a gameplay performance
probe. It blocks the main thread during each trial and deliberately forces garbage
collection before timing. Do not interpret trial duration as gameplay frame time.

Build against an existing isolated game copy with the runtime model-test harness:

```bash
dotnet build tools/performance/LookupBenchmark.csproj -c Release \
  -p:TestGameRoot="$PWD/scratch/perf-game"
```

Copy only `bin/Release/net35/FtkLookupBenchmark.dll` from this directory to the
copy's `BepInEx/plugins/` directory. The build references the game's public managed
assemblies without copying or publicizing them. See the
[runtime-test setup](../ai-model-pipeline/runtime-test/README.md) for save isolation
and preparation of the game copy. No original installation is used by default.

Launch that copy with all three environment variables:

- `FTK_MODEL_TEST=1`
- `FTK_MODEL_TEST_ROOT` equal to its exact absolute game root
- `FTK_PERF_BENCHMARK=1`

The root must be a direct child of a directory named `scratch` and must not be a
symlink. The `com.ftkmf.runtime-model-test` plugin must be loaded and enabled.
The benchmark does not change saves, gameplay state, networking, or render settings.

Write `perf-lookup-command.json` in the isolated game root:

```json
{"id":"baseline-01","passes":100}
```

Commands are polled on the main thread at most twice per second and limited to
2048 bytes. IDs accept 1 through 64 ASCII letters, digits, underscores, or hyphens.
Passes must be an integer from 1 through 1000. Use a new ID for every trial.
`perf-lookup-baseline-01.json` is reserved before executing; an existing result
prevents replay even after restarting the game. A `running` result indicates an
unfinished trial, `failed` includes an error, and `complete` contains measurements.

Each trial enumerates every native `FTK_itembase.ID` name and checks its patched
`GetEnum` result against case-insensitive `Enum.Parse` before timing. It then warms
all names, forces garbage collection outside timing, and measures repeated lookups
with a checked aggregate checksum. Results include name count, passes, call count,
first-lookup milliseconds, warmup milliseconds, timed milliseconds, generation-zero
collection delta, and
SHA256 plus MVID identity for the framework, probe, game, and benchmark assemblies.
The first-lookup value measures this trial's first invocation before parity checks,
not guaranteed cold initialization: normal game startup or earlier trials may have
already initialized the lookup. SHA256 reads the loaded assembly's backing file outside timing; do not replace DLLs
while the game is running. An absent assembly is explicitly reported as not loaded.

For a comparison, keep machine, game copy, plugins, scene, probe settings, and pass
count identical. Restart after replacing the framework DLL and collect multiple
trials in baseline/candidate/baseline order. Summarize the median and spread, retain
all completed trials, and report native-name parity separately from elapsed time.
This benchmark exercises canonical native item names only. It does not establish
behavior for mixed-case, numeric, comma-separated, invalid, or custom identifiers,
and does not establish a frame-rate improvement. Use focused correctness tests and
separate gameplay captures for those claims.
