# Scrolling and rejected foliage fixtures

Original measurement code, not game decompilation or a supported content plugin.
This freezes the resource probe used for these measurements, including its older
portrait and texture commands. Only `scroll-fixture` and `foliage-benchmark` are used
in this pass. Build with `dotnet build -c Release -p:TestGameRoot=<isolated root>`.

Follow the repository's authorized isolated game workflow. Deploy only the resulting
FtkResourcePrototype.dll with the owned player stopped. Require the active runtime
model-test helper, its exact root/save isolation, and `FTK_RESOURCE_PROBE=1`.
Never deploy to the normal installation or use production saves.

Send atomic `resource-command.json` files under that isolated root, with fresh IDs:

```json
{"id":"scrolling-001","action":"scroll-fixture"}
```

The result is `resource-scrolling-001.json`. The scrolling fixture creates owned
materials through the actual legacy resource helper. It removes the unused native
character component before activating the fixture, disables automatic scrolling,
and invokes the real patched LateUpdate through a bound delegate. The first three
cases must preserve owned material identity as well as matching phase and offsets.
A foreign-material case deliberately tests native fallback. It should produce one
compatibility warning; no other relevant exceptions are expected. Fixture objects
are destroyed in finally. Stop the owned player afterward.

After reaching a settled isolated overworld, `foliage-benchmark` runs eight timed
batches against the actual active foliage controller. Temporary Harmony patches
replace repeated keyword writes with state queries. Patching and warmup are outside
the timer. It checks output snapshots and repair of externally toggled keywords,
and restores native patches/fields/keyword state in finally. This hypothesis was
rejected for lack of a consistent speedup. Heap endpoint deltas are not allocated
bytes. The benchmark establishes no overall FPS or process CPU improvement.
