# Enemy visual scale regressions

Run from the repository root:

```sh
dotnet run --project tools/ai-model-pipeline/scale-tests/EnemyVisualScaleTests.csproj -c Release
```

This links the production `EnemyVisualScale` component against minimal Unity value and transform stubs. It checks neutral scale on native 0.35 and 0.9 bodies, nonuniform native axes, explicit factors, repeated application, independent bodies, a model of serialized baseline copying, and the bundled procedural boss's legacy absolute scale. It does not validate Unity component attachment, native serialization, avatar lifetimes, or the Harmony spawn hook. Those require an in-game gate with the newly built framework.

`Content.SetEnemyVisual(enemy, tint, scale)` treats `scale` as a multiplier of the spawned body's original local scale. A mesh-only registration supplies factor 1 and leaves native body size intact. The component retains that original baseline so repeated application does not compound scale. `widthBoost` multiplies the original x/z axes in addition to `scale`.

Compatibility: older framework builds replaced the native local scale with the requested value even though the public API documented 1 as unchanged. External content tuned to that old behavior may need to retune its factor (for a uniform native 0.9 body, old absolute 1 corresponds to factor 1/0.9). Nonuniform native bodies retain their native proportions. The in-assembly procedural RealmBoss branch explicitly opts into its old absolute dimensions with `legacyAbsoluteScale`; the stock baseline, authored model, and public mesh APIs do not opt in. This flag is internal and does not add a public API mode.
