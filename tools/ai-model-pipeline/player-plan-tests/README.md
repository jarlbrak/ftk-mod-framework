# Conditional player outfit plan tests

```sh
dotnet run --project tools/ai-model-pipeline/player-plan-tests/PlayerMeshPlanTests.csproj -c Release
```

These tests compile the production PlayerMeshPlan and public descriptor classes. They exercise absent/present conditional paths, independent apparel variants, exact native mesh identity, duplicate transforms and renderers, missing required body/hair, immutable array snapshots, one combined transaction dispatch, transaction failure propagation and the Applied-plus-retained lease fast path before native identity resolution.

Unity objects, the existing ExplicitEnemyMeshSwap transaction and EnemyMeshResources lease are stand-ins. This suite verifies the plan's decisions and calls, not strict GLB decoding, Unity native path enumeration, real resource ownership/cleanup or game hooks. Those still require the installed-game build and dedicated live fixtures. In particular, transaction failure propagation is not new evidence that the existing rollback implementation succeeds under a native Unity exception.
