# Isolated singular GLB and tint lifetime fixture

This opt-in content route exercises `Content.SetEnemyVisual` and
`Content.SetEnemyBodyMeshFromGlb` on a fresh `Content.AddEnemy` clone of `fishA01`.
It never calls `Content.SetEnemyBodyMeshesFromGlb` for the legacy row. Existing
profiles still use the plural route and retain their original model bytes.

The new stable key is `ftkmf_modeltest_reefstrider_legacy`. It uses the original
Reefstrider GLB and PNG, with tint `[0.85, 0.95, 1, 1]` and the source profile's
health floor and uniform scale. `bindingKind: "legacy-singular"` selects the
branch. Its single `renderers` entry is expected observation metadata for the
capture runner; **the singular public API receives no renderer path**. A capture
must independently prove which live renderer received the mesh. The registration
report names the actual requested public APIs and leaves spawn validation pending.

The fixture forbids material-slot options, emission options, resource-prefab
replacement and portrait-marker overrides. Native emission remains enabled. This
is a distinct tinted lifetime fixture, so the prior emission-disabled Reefstrider
appearance verdict does not transfer to it. No native geometry or texture is copied.

Prepare a separate catalog without modifying the input or any models:

```sh
python3 tools/ai-model-pipeline/runtime-test-content/prepare_legacy_profile.py \
  --catalog scratch/mirewarden-game/model-test-profiles.json \
  --models scratch/mirewarden-game/BepInEx/plugins/FTKModFramework_content/models \
  --output scratch/runtime-profile-406-reefstrider-legacy
```

The command refuses an existing output, checks the two original asset hashes,
appends exactly one row and verifies all preceding rows remain equal. The number
406 describes the reviewed 405-row input, not a hard-coded assumption. No asset
copy is required because the fixture uses the already installed original files.

Build content to a separate output, keeping deployed and default binaries intact:

```sh
dotnet build tools/ai-model-pipeline/runtime-test-content/RuntimeModelTestContent.csproj \
  -c Release -p:TestGameRoot="$PWD/scratch/mirewarden-game" \
  -p:OutputPath="$PWD/scratch/legacy-content-build/" \
  -p:BaseIntermediateOutputPath="$PWD/scratch/legacy-content-obj/"
dotnet run --project tools/ai-model-pipeline/legacy-content-tests/LegacyContentTests.csproj \
  -c Release -p:TestGameRoot="$PWD/scratch/mirewarden-game"
```

Before deployment, review and freeze the source, DLL, input/output catalog and
original asset hashes. The root game controller must separately verify process
absence and deploy the approved Core ownership fix, reviewed passive observer,
content DLL and catalog with an explicit receipt. This document does not authorize
an automatic restart or replace that receipt.

Required live evidence remains: registration and singular load logs, actual target
mesh/material/texture and tint, native combat owner, native HUD portrait clone
inheritance, and final native owner destruction with resource disposal. Preserve
native camera/render-texture caches as separate lifetimes. A registration PASS,
synthetic clone exercise or plural-route test does not prove the singular route's
native lifetime. Ordinary attacks/death and resulting loot must be recorded
separately where applicable. No live acceptance is claimed by preparing this fixture.
